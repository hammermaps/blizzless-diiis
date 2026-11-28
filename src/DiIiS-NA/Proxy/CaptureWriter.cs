using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiIiS_NA.Proxy
{
	/// <summary>
	/// Writes everything the proxy sees. Every connection produces two raw dumps (client to server
	/// and server to client) that can be replayed with any protobuf/HTTP tool, plus one readable log
	/// with timestamps, sizes and a hex/ascii preview per chunk.
	/// </summary>
	internal sealed class CaptureWriter : IDisposable
	{
		private readonly object _sync = new();
		private readonly string _directory;
		private readonly StreamWriter _log;
		private readonly int _previewBytes;
		private int _connectionCounter;

		public CaptureWriter(string directory, int previewBytes)
		{
			_directory = Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? "captures" : directory);
			Directory.CreateDirectory(_directory);
			_previewBytes = Math.Max(64, previewBytes);

			var path = Path.Combine(_directory, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.log");
			_log = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
			{
				AutoFlush = true
			};
			LogPath = path;
		}

		public string LogPath { get; }

		public void Note(string text)
		{
			lock (_sync)
				_log.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {text}");
		}

		/// <summary>Opens the raw dumps of one connection.</summary>
		public ConnectionCapture OpenConnection(string endpoint, string remote)
		{
			var number = System.Threading.Interlocked.Increment(ref _connectionCounter);
			var name = $"conn{number:000}-{endpoint}";
			Note($"== {name} opened by {remote}");

			return new ConnectionCapture(
				name,
				Path.Combine(_directory, name + "-c2s.bin"),
				Path.Combine(_directory, name + "-s2c.bin"),
				this);
		}

		internal void Write(ConnectionCapture connection, bool fromClient, byte[] data, int length, int totalForDirection)
		{
			connection.Dump(fromClient, data, length);

			var direction = fromClient ? "C->S" : "S->C";
			var preview = Math.Min(length, _previewBytes);
			var head = Encoding.ASCII.GetString(data, 0, preview)
				.Replace("\r", "\\r").Replace("\n", "\\n");
			var hex = BitConverter.ToString(data, 0, Math.Min(preview, 128)).Replace("-", " ");

			lock (_sync)
			{
				_log.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {connection.Name} {direction} {length} bytes (total {totalForDirection})");
				_log.WriteLine($"    ascii: {head}");
				_log.WriteLine($"    hex  : {hex}{(length > 128 ? " ..." : "")}");
			}

			InspectForGameServerAddress(data, length);
		}

		/// <summary>
		/// Looks for a Battle.net or IP:port address in the captured bytes; that is the game server
		/// address the client is told to use, and the one that has to be redirected to capture play.
		/// </summary>
		private void InspectForGameServerAddress(byte[] data, int length)
		{
			var text = Encoding.ASCII.GetString(data, 0, length);
			var match = System.Text.RegularExpressions.Regex.Match(
				text,
				@"((?:[a-z0-9][a-z0-9\-]*\.)+(?:battle\.net|blizzard\.com)|(?:\d{1,3}\.){3}\d{1,3})[^\x20-\x7e]{0,3}?(\d{2,5})",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);

			if (!match.Success) return;

			var address = $"{match.Groups[1].Value}:{match.Groups[2].Value}";
			if (_seenAddresses.Add(address))
				Note($"    !! possible game server address in stream: {address} " +
				     "(redirect that name to [Proxy] ListenIP:GameServerPort and add it to CertificateNames)");
		}

		private readonly HashSet<string> _seenAddresses = new();

		public void Dispose()
		{
			lock (_sync)
			{
				_log.Flush();
				_log.Dispose();
			}
		}
	}

	internal sealed class ConnectionCapture
	{
		private readonly FileStream _clientToServer;
		private readonly FileStream _serverToClient;
		private readonly CaptureWriter _owner;
		private int _clientBytes;
		private int _serverBytes;

		public ConnectionCapture(string name, string clientToServerPath, string serverToClientPath, CaptureWriter owner)
		{
			Name = name;
			_owner = owner;
			_clientToServer = new FileStream(clientToServerPath, FileMode.Create, FileAccess.Write, FileShare.Read);
			_serverToClient = new FileStream(serverToClientPath, FileMode.Create, FileAccess.Write, FileShare.Read);
		}

		public string Name { get; }

		internal void Dump(bool fromClient, byte[] data, int length)
		{
			if (fromClient) _clientToServer.Write(data, 0, length);
			else _serverToClient.Write(data, 0, length);
		}

		public void Record(bool fromClient, byte[] data, int length)
		{
			var total = fromClient
				? (_clientBytes += length)
				: (_serverBytes += length);

			_owner.Write(this, fromClient, data, length, total);
		}

		public void Flush()
		{
			_clientToServer.Flush();
			_serverToClient.Flush();
		}

		public void Finish()
		{
			Flush();
			_clientToServer.Dispose();
			_serverToClient.Dispose();
			_owner.Note($"== {Name} closed ({_clientBytes} bytes up, {_serverBytes} bytes down)");
		}
	}
}
