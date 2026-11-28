using DiIiS_NA.Core.Config;

namespace DiIiS_NA.Proxy
{
	/// <summary>
	/// Configuration of the traffic proxy (section <c>[Proxy]</c> in config.ini).
	///
	/// The proxy sits between the retail client and the real Battle.net service, terminates TLS
	/// with the local certificate the client already trusts (see <see cref="LocalCertificate"/>),
	/// forwards everything upstream and records both directions in clear text. It replaces the
	/// normal server startup when <see cref="Enabled"/> is set.
	/// </summary>
	public sealed class ProxyConfig : Config
	{
		public ProxyConfig() : base("Proxy") { }

		public static ProxyConfig Instance { get; } = new ProxyConfig();

		public bool Enabled
		{
			get => GetBoolean(nameof(Enabled), false);
			set => Set(nameof(Enabled), value);
		}

		/// <summary>Address the listeners bind to; must be the address the hosts file points at.</summary>
		public string ListenIP
		{
			get => GetString(nameof(ListenIP), "127.0.0.1");
			set => Set(nameof(ListenIP), value);
		}

		/// <summary>HTTPS listener (Battle.net REST, agents).</summary>
		public int HttpsPort
		{
			get => GetInt(nameof(HttpsPort), 443);
			set => Set(nameof(HttpsPort), value);
		}

		/// <summary>Battle.net gateway listener: TLS/HTTP/WebSocket on the port the client uses.</summary>
		public int BnetPort
		{
			get => GetInt(nameof(BnetPort), 1119);
			set => Set(nameof(BnetPort), value);
		}

		/// <summary>
		/// Game server listener. The client learns the game server address from the Battle.net
		/// session, so point that host name at this port in the hosts file (and add it to
		/// <see cref="CertificateNames"/>) to capture the game connection as well.
		/// </summary>
		public int GameServerPort
		{
			get => GetInt(nameof(GameServerPort), 1120);
			set => Set(nameof(GameServerPort), value);
		}

		/// <summary>Host name of the real HTTPS/Battle.net endpoint, e.g. us.actual.battle.net.</summary>
		public string UpstreamBnetHost
		{
			get => GetString(nameof(UpstreamBnetHost), "us.actual.battle.net");
			set => Set(nameof(UpstreamBnetHost), value);
		}

		public int UpstreamBnetPort
		{
			get => GetInt(nameof(UpstreamBnetPort), 1119);
			set => Set(nameof(UpstreamBnetPort), value);
		}

		public int UpstreamHttpsPort
		{
			get => GetInt(nameof(UpstreamHttpsPort), 443);
			set => Set(nameof(UpstreamHttpsPort), value);
		}

		/// <summary>
		/// Upstream game server "host:port" as seen in the capture. Empty means "not captured yet";
		/// the first captured session response prints the address it contains.
		/// </summary>
		public string UpstreamGameServerHost
		{
			get => GetString(nameof(UpstreamGameServerHost), "");
			set => Set(nameof(UpstreamGameServerHost), value);
		}

		public int UpstreamGameServerPort
		{
			get => GetInt(nameof(UpstreamGameServerPort), 1119);
			set => Set(nameof(UpstreamGameServerPort), value);
		}

		/// <summary>
		/// Resolver used for the upstream lookup. The hosts file redirects the Battle.net names to
		/// this machine, so the system resolver cannot be used to find the real address.
		/// </summary>
		public string DnsServer
		{
			get => GetString(nameof(DnsServer), "1.1.1.1");
			set => Set(nameof(DnsServer), value);
		}

		/// <summary>Extra names for the local certificate (comma separated), e.g. the game server name.</summary>
		public string CertificateNames
		{
			get => GetString(nameof(CertificateNames), "");
			set => Set(nameof(CertificateNames), value);
		}

		public string CaptureDirectory
		{
			get => GetString(nameof(CaptureDirectory), "captures");
			set => Set(nameof(CaptureDirectory), value);
		}

		/// <summary>Bytes of each direction written as hex into the readable log (raw dumps are complete).</summary>
		public int CapturePreviewBytes
		{
			get => GetInt(nameof(CapturePreviewBytes), 512);
			set => Set(nameof(CapturePreviewBytes), value);
		}
	}
}
