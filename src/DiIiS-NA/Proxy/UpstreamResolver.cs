using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DiIiS_NA.Proxy
{
	/// <summary>
	/// Minimal DNS client for the upstream lookup. The hosts file points the Battle.net names at
	/// this machine (that is how the client is redirected), so <see cref="Dns"/> would answer with
	/// 127.0.0.1. A, AAAA and CNAME answers are read straight from the configured resolver instead.
	/// </summary>
	internal sealed class UpstreamResolver
	{
		private readonly string _dnsServer;

		public UpstreamResolver(string dnsServer)
		{
			_dnsServer = string.IsNullOrWhiteSpace(dnsServer) ? "1.1.1.1" : dnsServer.Trim();
		}

		public async Task<IPAddress> ResolveAsync(string host)
		{
			if (IPAddress.TryParse(host, out var literal))
				return literal;

			var a = await QueryAsync(host, 1).ConfigureAwait(false); // A
			if (a != null)
				return a;

			var aaaa = await QueryAsync(host, 28).ConfigureAwait(false); // AAAA
			if (aaaa != null)
				return aaaa;

			throw new InvalidOperationException($"Upstream '{host}' could not be resolved through {_dnsServer}.");
		}

		private async Task<IPAddress> QueryAsync(string host, ushort type)
		{
			var query = BuildQuery(host, type);

			using var udp = new UdpClient();
			var server = IPAddress.Parse(_dnsServer);
			await udp.SendAsync(query, query.Length, new IPEndPoint(server, 53)).ConfigureAwait(false);

			using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
			var receive = await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
			return ParseAnswer(receive.Buffer);
		}

		private static byte[] BuildQuery(string host, ushort type)
		{
			var buffer = new List<byte>();
			var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
			buffer.Add((byte)(id >> 8));
			buffer.Add((byte)id);
			buffer.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

			foreach (var label in host.Split('.'))
			{
				buffer.Add((byte)label.Length);
				buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(label));
			}
			buffer.Add(0x00);
			buffer.Add((byte)(type >> 8));
			buffer.Add((byte)type);
			buffer.AddRange(new byte[] { 0x00, 0x01 }); // class IN
			return buffer.ToArray();
		}

		/// <summary>
		/// Reads the first address record of the requested type. The answer section can contain a
		/// CNAME chain followed by the address, so the walk skips names instead of assuming one.
		/// </summary>
		private static IPAddress ParseAnswer(byte[] data)
		{
			if (data.Length < 12) return null;

			var questionCount = (data[4] << 8) | data[5];
			var answerCount = (data[6] << 8) | data[7];
			var offset = 12;

			for (var i = 0; i < questionCount; i++)
			{
				while (offset < data.Length && data[offset] != 0)
				{
					if ((data[offset] & 0xC0) == 0xC0) { offset += 2; goto questionDone; }
					offset += data[offset] + 1;
				}
				offset++;
			questionDone:
				offset += 4;
			}

			for (var i = 0; i < answerCount && offset + 10 <= data.Length; i++)
			{
				if ((data[offset] & 0xC0) == 0xC0) offset += 2;
				else
				{
					while (offset < data.Length && data[offset] != 0) offset += data[offset] + 1;
					offset++;
				}

				if (offset + 10 > data.Length) return null;

				var recordType = (data[offset] << 8) | data[offset + 1];
				var length = (data[offset + 8] << 8) | data[offset + 9];
				offset += 10;
				if (offset + length > data.Length) return null;

				if (recordType == 1 && length == 4)
					return new IPAddress(new[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] });
				if (recordType == 28 && length == 16)
				{
					var bytes = new byte[16];
					Array.Copy(data, offset, bytes, 0, 16);
					return new IPAddress(bytes);
				}

				offset += length;
			}

			return null;
		}
	}
}
