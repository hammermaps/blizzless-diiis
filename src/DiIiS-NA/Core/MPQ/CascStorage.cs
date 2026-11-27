using DiIiS_NA.Core.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DiIiS_NA.Core.MPQ
{
	/// <summary>
	/// Maps the CascLib DLL name used by the P/Invoke declarations to the platform's library. Windows loads
	/// the bundled CascLib.dll; Unix loads libcasc.so built from external/CascLib (see tools/build_casclib.sh).
	/// </summary>
	internal static class CascLibrary
	{
		[System.Runtime.CompilerServices.ModuleInitializer]
		internal static void Register()
		{
			if (OperatingSystem.IsWindows())
				return;

			try
			{
				NativeLibrary.SetDllImportResolver(typeof(CascStorage).Assembly, Resolve);
			}
			catch (InvalidOperationException)
			{
				// Another initializer registered a resolver first; the default probing still applies.
			}
		}

		private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
		{
			if (name != "CascLib.dll")
				return IntPtr.Zero;

			foreach (var candidate in new[] { "libcasc.so", "libCascLib.so" })
			{
				if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
					return handle;
			}
			return IntPtr.Zero;
		}
	}

	/// <summary>
	/// Read-only access to an installed Diablo III's CASC storage through CascLib, so the server uses exactly
	/// the game data the player's client has instead of a bundled copy from another patch.
	/// </summary>
	public sealed class CascStorage : IDisposable
	{
		private static readonly Logger Logger = LogManager.CreateLogger("CascStorage");

		private const string Library = "CascLib.dll";
		private const uint LocaleAll = 0xFFFFFFFF;
		// CASC_FIND_DATA sizes differ per platform: CascLib's MAX_PATH is 260 on Windows and 1024 elsewhere,
		// which moves FileSize (offsetof) and grows the whole struct (1104 bytes on Linux x64).
		private static readonly int MaxPath = OperatingSystem.IsWindows() ? 260 : 1024;
		private static readonly int FindDataSize = OperatingSystem.IsWindows() ? 1024 : 1104;
		private static readonly int FindDataFileSize = OperatingSystem.IsWindows() ? 304 : 1064;
		private const int TocGroups = 70;

		[DllImport(Library, CharSet = CharSet.Ansi)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascOpenStorage(string szParams, uint dwLocaleMask, out IntPtr phStorage);

		[DllImport(Library)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascCloseStorage(IntPtr hStorage);

		[DllImport(Library, CharSet = CharSet.Ansi)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascOpenFile(IntPtr hStorage, string szFileName, uint dwLocaleFlags, uint dwOpenFlags, out IntPtr phFile);

		[DllImport(Library)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascGetFileSize64(IntPtr hFile, out ulong pFileSize);

		[DllImport(Library)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascReadFile(IntPtr hFile, byte[] lpBuffer, uint dwToRead, out uint pdwRead);

		[DllImport(Library)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascCloseFile(IntPtr hFile);

		[DllImport(Library, CharSet = CharSet.Ansi)]
		private static extern IntPtr CascFindFirstFile(IntPtr hStorage, string szMask, byte[] pFindData, string szListFile);

		[DllImport(Library)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascFindNextFile(IntPtr hFind, byte[] pFindData);

		[DllImport(Library)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool CascFindClose(IntPtr hFind);

		[DllImport(Library)]
		private static extern uint GetCascError();

		private readonly IntPtr _storage;
		private readonly object _lock = new();
		private readonly Dictionary<string, long> _files = new(StringComparer.OrdinalIgnoreCase);

		public string GameDirectory { get; }
		public int FileCount => _files.Count;

		private CascStorage(string gameDirectory, IntPtr storage)
		{
			GameDirectory = gameDirectory;
			_storage = storage;
			IndexFiles(@"Base\*");
		}

		public static CascStorage Open(string gameDirectory)
		{
			if (!CascOpenStorage(gameDirectory, LocaleAll, out var storage))
				throw new IOException($"CascLib could not open the game data in {gameDirectory} (error {GetCascError()}).");
			return new CascStorage(gameDirectory, storage);
		}

		/// <summary>
		/// The configured Diablo III folder, or the installed one from the Windows uninstall entries.
		/// </summary>
		public static string FindGameDirectory(string configured)
		{
			if (!string.IsNullOrWhiteSpace(configured))
				return Directory.Exists(configured) ? configured : null;
			if (!OperatingSystem.IsWindows())
				return null;

			foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
			foreach (var path in new[] { @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" })
			{
				using var uninstall = root.OpenSubKey(path);
				if (uninstall == null) continue;
				foreach (var name in uninstall.GetSubKeyNames())
				{
					using var app = uninstall.OpenSubKey(name);
					if (app?.GetValue("DisplayName") as string == "Diablo III" &&
					    app.GetValue("InstallLocation") is string dir && Directory.Exists(dir))
						return dir;
				}
			}
			return null;
		}

		public IAssetFile GetFile(string name) =>
			_files.TryGetValue(name, out var size) ? new CascAssetFile(this, name, size) : null;

		public byte[] ReadAllBytes(string name)
		{
			lock (_lock)
			{
				if (!CascOpenFile(_storage, name, 0, 0, out var file))
					throw new FileNotFoundException($"CascLib could not open {name} (error {GetCascError()}).");
				try
				{
					CascGetFileSize64(file, out var size);
					var buffer = new byte[size];
					if (!CascReadFile(file, buffer, (uint)size, out var read) || read != size)
						throw new IOException($"CascLib could not read {name} (error {GetCascError()}).");
					return buffer;
				}
				finally
				{
					CascCloseFile(file);
				}
			}
		}

		/// <summary>
		/// Every asset listed in Base\CoreTOC.dat, as (SNO group, SNO id, name).
		/// </summary>
		public List<(int Group, int SnoId, string Name)> ReadCoreToc()
		{
			const int headerSize = TocGroups * 3 * 4 + 4;
			var toc = ReadAllBytes(@"Base\CoreTOC.dat");
			var entries = new List<(int, int, string)>();
			for (int g = 0; g < TocGroups; g++)
			{
				int count = BitConverter.ToInt32(toc, g * 4);
				int first = headerSize + BitConverter.ToInt32(toc, (TocGroups + g) * 4);
				int names = first + count * 12; // each group's names follow its entries
				for (int i = 0; i < count; i++)
				{
					int entry = first + i * 12;
					int nameStart = names + BitConverter.ToInt32(toc, entry + 8);
					int nameEnd = Array.IndexOf(toc, (byte)0, nameStart);
					entries.Add((BitConverter.ToInt32(toc, entry), BitConverter.ToInt32(toc, entry + 4),
						Encoding.ASCII.GetString(toc, nameStart, nameEnd - nameStart)));
				}
			}
			return entries;
		}

		private void IndexFiles(string mask)
		{
			var findData = new byte[FindDataSize];
			var find = CascFindFirstFile(_storage, mask, findData, null);
			if (find == IntPtr.Zero)
			{
				Logger.Error("CascLib found no files matching {0} (error {1}).", mask, GetCascError());
				return;
			}
			try
			{
				do
				{
					int length = Array.IndexOf(findData, (byte)0, 0, MaxPath);
					var name = Encoding.ASCII.GetString(findData, 0, length < 0 ? MaxPath : length);
					_files[name] = BitConverter.ToInt64(findData, FindDataFileSize);
				} while (CascFindNextFile(find, findData));
			}
			finally
			{
				CascFindClose(find);
			}
		}

		public void Dispose() => CascCloseStorage(_storage);
	}

	internal sealed class CascAssetFile : IAssetFile
	{
		private readonly CascStorage _storage;

		public CascAssetFile(CascStorage storage, string name, long size)
		{
			_storage = storage;
			Name = name;
			Size = size;
		}

		public string Name { get; }
		public long Size { get; }
		public Stream Open() => new MemoryStream(_storage.ReadAllBytes(Name), writable: false);
	}
}
