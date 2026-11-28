using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.LoginServer.Base;

namespace DiIiS_NA.Proxy
{
	/// <summary>
	/// Traffic proxy between the retail client and the real Battle.net service.
	///
	/// The client is pointed at this machine through the hosts file and trusts the local CA (exactly
	/// the mechanism <c>tools/patch_client.sh</c> already installs for the emulator), so the proxy
	/// can terminate TLS with <see cref="LocalCertificate"/> and read every byte. Upstream is reached
	/// through an explicit DNS lookup, because the hosts file points the same names here.
	///
	/// Nothing is rewritten or answered: every client connection becomes exactly one upstream
	/// connection whose payload is forwarded byte for byte, in both directions. Battle.net therefore
	/// sees the original client's requests unchanged (same bytes, same order, same HTTP headers and
	/// WebSocket frames), and the client sees the original answers. To capture the game server connection, redirect the game server name the session
	/// response contains to <see cref="ProxyConfig.GameServerPort"/> and add it to CertificateNames;
	/// the capture log prints the address as soon as it appears in the stream.
	/// </summary>
	public sealed class ProxyServer
	{
		private static readonly Logger Logger = LogManager.CreateLogger("Proxy");

		private readonly ProxyConfig _config;
		private X509Certificate2 _certificate;
		private readonly UpstreamResolver _resolver;
		private readonly CaptureWriter _capture;

		public ProxyServer(ProxyConfig config)
		{
			_config = config;
			_resolver = new UpstreamResolver(config.DnsServer);
			_capture = new CaptureWriter(config.CaptureDirectory, config.CapturePreviewBytes);
		}

		public async Task RunAsync(CancellationToken cancellation)
		{
			LocalCertificate.EnsureExists(_config.CertificateNames);
			_certificate = LocalCertificate.Load();

			Logger.Warn("Proxy mode: everything the client sends is recorded in clear text at {0}", _capture.LogPath);
			Logger.Info("Upstream Battle.net: {0}:{1} (HTTP {2}) via DNS {3}",
				_config.UpstreamBnetHost, _config.UpstreamBnetPort, _config.UpstreamHttpsPort, _config.DnsServer);

			var listeners = new[]
			{
				ListenAsync("https", _config.HttpsPort, _config.UpstreamBnetHost, _config.UpstreamHttpsPort, cancellation),
				ListenAsync("bnet", _config.BnetPort, _config.UpstreamBnetHost, _config.UpstreamBnetPort, cancellation)
			};

			Task gameServerListener;
			if (string.IsNullOrWhiteSpace(_config.UpstreamGameServerHost))
			{
				Logger.Warn("No [Proxy] UpstreamGameServerHost configured yet: only Battle.net traffic is captured. " +
				            "The capture log names the game server address as soon as the client receives it.");
				gameServerListener = Task.CompletedTask;
			}
			else
			{
				gameServerListener = ListenAsync("gs", _config.GameServerPort,
					_config.UpstreamGameServerHost, _config.UpstreamGameServerPort, cancellation);
			}

			await Task.WhenAll(listeners.Concat(new[] { gameServerListener })).ConfigureAwait(false);

			_capture.Dispose();
		}

		private async Task ListenAsync(string endpoint, int port, string upstreamHost, int upstreamPort, CancellationToken cancellation)
		{
			var listener = new TcpListener(IPAddress.Parse(_config.ListenIP), port);
			try
			{
				listener.Start();
			}
			catch (SocketException e)
			{
				// Ports below 1024 need privileges. Losing one listener must not stop the others.
				Logger.Error("Proxy listener {0} on port {1} could not start ({2}). Run the proxy with the capability to bind " +
				             "privileged ports (sudo, CAP_NET_BIND_SERVICE, or an iptables redirect).", endpoint, port, e.Message);
				return;
			}
			Logger.Success("{0} proxy listening on {1}:{2} -> {3}:{4}", endpoint, _config.ListenIP, port, upstreamHost, upstreamPort);

			try
			{
				while (!cancellation.IsCancellationRequested)
				{
					var client = await listener.AcceptTcpClientAsync(cancellation).ConfigureAwait(false);
					_ = Task.Run(() => HandleAsync(endpoint, client, upstreamHost, upstreamPort, cancellation), cancellation);
				}
			}
			catch (OperationCanceledException)
			{
				// shutting down
			}
			finally
			{
				listener.Stop();
			}
		}

		private async Task HandleAsync(string endpoint, TcpClient client, string upstreamHost, int upstreamPort, CancellationToken cancellation)
		{
			var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
			var connection = _capture.OpenConnection(endpoint, remote);

			TcpClient upstream = null;
			try
			{
				client.NoDelay = true;
				var clientStream = await SniffAsync(client, connection, cancellation).ConfigureAwait(false);
				if (clientStream == null)
				{
					connection.Finish();
					client.Dispose();
					return;
				}

				var address = await _resolver.ResolveAsync(upstreamHost).ConfigureAwait(false);
				_capture.Note($"{connection.Name} upstream {upstreamHost} resolved to {address}");

				upstream = new TcpClient { NoDelay = true };
				await upstream.ConnectAsync(address, upstreamPort, cancellation).ConfigureAwait(false);
				Stream upstreamStream = upstream.GetStream();

				if (clientStream.IsTls)
				{
					var ssl = new SslStream(upstreamStream, false, (_, _, _, _) => true);
					await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
					{
						TargetHost = upstreamHost,
						EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
					}, cancellation).ConfigureAwait(false);
					upstreamStream = ssl;
				}

				using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
				await Task.WhenAll(
					PumpAsync(clientStream.Stream, upstreamStream, connection, true, linked),
					PumpAsync(upstreamStream, clientStream.Stream, connection, false, linked)
				).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				_capture.Note($"{connection.Name} aborted: {e.GetType().Name}: {e.Message}");
			}
			finally
			{
				connection.Finish();
				client.Dispose();
				upstream?.Dispose();
			}
		}

		/// <summary>
		/// Decides from the first byte whether the connection speaks TLS (0x16 = handshake record) or
		/// plain HTTP, exactly like the emulator's own sniffer has to.
		/// </summary>
		private async Task<ClientSide> SniffAsync(TcpClient client, ConnectionCapture connection, CancellationToken cancellation)
		{
			var stream = client.GetStream();
			var first = new byte[1];
			var read = await stream.ReadAsync(first, cancellation).ConfigureAwait(false);
			if (read == 0)
				return null;

			var prefixed = new PrefixedStream(stream, first);

			if (first[0] != 0x16)
			{
				_capture.Note($"{connection.Name} plain HTTP");
				return new ClientSide(prefixed, false);
			}

			var ssl = new SslStream(prefixed, false);
			await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
			{
				ServerCertificate = _certificate,
				EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
				ClientCertificateRequired = false
			}, cancellation).ConfigureAwait(false);

			_capture.Note($"{connection.Name} TLS ({ssl.SslProtocol}), SNI {ssl.TargetHostName ?? "?"}, cipher {ssl.NegotiatedCipherSuite}");
			return new ClientSide(ssl, true);
		}

		private async Task PumpAsync(Stream from, Stream to, ConnectionCapture connection, bool fromClient, CancellationTokenSource linked)
		{
			var buffer = new byte[16 * 1024];
			try
			{
				while (true)
				{
					var read = await from.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
					if (read <= 0)
						break;

					connection.Record(fromClient, buffer, read);
					await to.WriteAsync(buffer.AsMemory(0, read), linked.Token).ConfigureAwait(false);
					await to.FlushAsync(linked.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				// the other direction ended
			}
			catch (IOException e)
			{
				_capture.Note($"{connection.Name} {(fromClient ? "client" : "upstream")} closed: {e.Message}");
			}
			finally
			{
				linked.Cancel();
			}
		}

		private sealed record ClientSide(Stream Stream, bool IsTls);
	}

	/// <summary>Replays the byte that was read to decide the protocol, then behaves like the socket stream.</summary>
	internal sealed class PrefixedStream : Stream
	{
		private readonly Stream _inner;
		private byte[] _prefix;

		public PrefixedStream(Stream inner, byte[] prefix)
		{
			_inner = inner;
			_prefix = prefix;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_prefix != null && _prefix.Length > 0)
			{
				var take = Math.Min(count, _prefix.Length);
				Array.Copy(_prefix, 0, buffer, offset, take);
				_prefix = take == _prefix.Length ? null : _prefix[take..];
				return take;
			}
			return _inner.Read(buffer, offset, count);
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			if (_prefix != null && _prefix.Length > 0)
			{
				var take = Math.Min(buffer.Length, _prefix.Length);
				_prefix.AsMemory(0, take).CopyTo(buffer);
				_prefix = take == _prefix.Length ? null : _prefix[take..];
				return take;
			}
			return await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			=> ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

		public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
			=> _inner.WriteAsync(buffer, cancellationToken);
		public override void Flush() => _inner.Flush();
		public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if (disposing) _inner.Dispose();
			base.Dispose(disposing);
		}
	}
}
