using DiIiS_NA.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DiIiS_NA.LoginServer.Base
{
	/// <summary>
	/// Certificate for the Battle.net TLS listener, generated per machine instead of shipping a
	/// shared one whose private key is public. The signing CA's private key only exists in memory
	/// while the server certificate is issued, so nothing can mint further certificates from it.
	/// </summary>
	public static class LocalCertificate
	{
		private static readonly Logger Logger = LogManager.CreateLogger("Certificate");

		private const string PfxPassword = "123";

		private static readonly string[] DefaultHostNames =
		{
			"us.actual.battle.net", "eu.actual.battle.net", "kr.actual.battle.net", "cn.actual.battle.net", "localhost"
		};

		public static string Folder => Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3Offline");
		public static string PfxPath => Path.Combine(Folder, "bnetserver.p12");
		public static string CaCertPath => Path.Combine(Folder, "D3Offline-LocalCA.cer");

		public static X509Certificate2 Load() => X509CertificateLoader.LoadPkcs12FromFile(PfxPath, PfxPassword);

		/// <summary>
		/// Ensures a server certificate exists that is valid for the default Battle.net names, localhost,
		/// the loopback address and every extra name in [Battle-Server] CertificateNames. A patched client
		/// that points at an arbitrary address only accepts the certificate when that address is in the SAN
		/// list, so an existing certificate is regenerated as soon as the list changes.
		/// </summary>
		public static void EnsureExists(string extraNames = "")
		{
			var names = BuildNames(extraNames);
			Directory.CreateDirectory(Folder);

			if (File.Exists(PfxPath))
			{
				try
				{
					using var existing = Load();
					if (CoversAllNames(existing, names))
					{
						Logger.Info("Using local certificate: {0}", PfxPath);
						return;
					}
					Logger.Warn("Local certificate does not cover all configured names ({0}); regenerating it.",
						string.Join(", ", names));
				}
				catch (CryptographicException e)
				{
					Logger.WarnException(e, "Local certificate could not be read; regenerating it.");
				}
			}

			var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
			var notAfter = notBefore.AddYears(5);

			using var caKey = RSA.Create(2048);
			var caRequest = new CertificateRequest($"CN=D3Offline Local CA ({Environment.MachineName})", caKey,
				HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
			caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
				X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
			caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, false));
			using var ca = caRequest.CreateSelfSigned(notBefore, notAfter);

			using var serverKey = RSA.Create(2048);
			var serverRequest = new CertificateRequest($"CN={DefaultHostNames[0]}", serverKey,
				HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			var san = new SubjectAlternativeNameBuilder();
			foreach (var name in names)
			{
				if (IPAddress.TryParse(name, out var address))
					san.AddIpAddress(address);
				else
					san.AddDnsName(name);
			}
			serverRequest.CertificateExtensions.Add(san.Build());
			serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
			serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
				X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
			serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
				new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
			serverRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, true, false));

			var serial = RandomNumberGenerator.GetBytes(16);
			serial[0] &= 0x7F;
			using var issued = serverRequest.Create(ca, notBefore, notAfter.AddDays(-1), serial);
			using var server = issued.CopyWithPrivateKey(serverKey);

			var caPublic = ca.Export(X509ContentType.Cert);
			var bundle = new X509Certificate2Collection { server, X509CertificateLoader.LoadCertificate(caPublic) };
			File.WriteAllBytes(PfxPath, bundle.Export(X509ContentType.Pkcs12, PfxPassword));
			File.WriteAllBytes(CaCertPath, caPublic);

			Logger.Success("Generated a new local certificate in {0}", Folder);
			Logger.Info("Certificate names: {0}", string.Join(", ", names));
			Logger.Warn("Trust {0} (Current User > Trusted Root Certification Authorities) before connecting the game.", CaCertPath);
		}

		/// <summary>Default names, the loopback address and the configured extras, without duplicates.</summary>
		internal static List<string> BuildNames(string extraNames)
		{
			var names = new List<string>(DefaultHostNames) { IPAddress.Loopback.ToString() };
			if (!string.IsNullOrWhiteSpace(extraNames))
			{
				foreach (var raw in extraNames.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var name = raw.Trim();
					if (name.Length > 0 && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
						names.Add(name);
				}
			}
			return names;
		}

		private static bool CoversAllNames(X509Certificate2 certificate, IEnumerable<string> names)
		{
			var extension = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
			if (extension == null)
				return false;

			var dns = extension.EnumerateDnsNames().ToList();
			var addresses = extension.EnumerateIPAddresses().ToList();
			foreach (var name in names)
			{
				if (IPAddress.TryParse(name, out var address))
				{
					if (!addresses.Contains(address))
						return false;
				}
				else if (!dns.Contains(name, StringComparer.OrdinalIgnoreCase))
				{
					return false;
				}
			}
			return true;
		}
	}
}
