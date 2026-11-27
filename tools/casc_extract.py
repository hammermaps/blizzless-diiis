#!/usr/bin/env python3
"""Unpack the Diablo III client data the emulator needs, straight from an original install.

Reads the game's CASC storage through libcasc.so (tools/build_casclib.sh) and writes
either the asset list or the files themselves into a directory of your choice. The
server reads CASC in place; this tool is for setups that need the data on disk (a
container without the game, an audit of the install, or copying data to another machine).

Examples:
    ./tools/casc_extract.py --list
    ./tools/casc_extract.py --out d3data --toc
    ./tools/casc_extract.py --out d3data --pattern Actor --limit 20
    ./tools/casc_extract.py --out d3data --all
"""
import argparse
import ctypes
import os
import re
import struct
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LIB_CANDIDATES = [
    REPO / "src" / "DiIiS-NA" / "libs" / "libcasc.so",
    REPO / ".build" / "casclib" / "libcasc.so",
]
TOC_GROUPS = 70
TOC_HEADER = TOC_GROUPS * 3 * 4 + 4
TARGET_BUILD = "2.8.0.99920"
FIND_DATA_SIZE = 1104       # sizeof(CASC_FIND_DATA) on x64 Linux; 1024 would corrupt the heap
FIND_DATA_FILE_SIZE = 1064  # offsetof(CASC_FIND_DATA, FileSize) for MAX_PATH = 1024
MAX_PATH = 1024             # CascLib MAX_PATH outside Windows


def fail(message):
    print("error: " + message, file=sys.stderr)
    sys.exit(1)


def load_library():
    for candidate in LIB_CANDIDATES:
        if candidate.exists():
            return ctypes.CDLL(str(candidate))
    fail("libcasc.so not found; run ./tools/build_casclib.sh first")


def bind(lib):
    lib.CascOpenStorage.argtypes = [ctypes.c_char_p, ctypes.c_uint32, ctypes.POINTER(ctypes.c_void_p)]
    lib.CascOpenStorage.restype = ctypes.c_bool
    lib.CascOpenFile.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_uint32, ctypes.c_uint32,
                                 ctypes.POINTER(ctypes.c_void_p)]
    lib.CascOpenFile.restype = ctypes.c_bool
    lib.CascGetFileSize64.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulonglong)]
    lib.CascReadFile.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_uint32,
                                 ctypes.POINTER(ctypes.c_uint32)]
    lib.CascReadFile.restype = ctypes.c_bool
    lib.CascCloseFile.argtypes = [ctypes.c_void_p]
    lib.CascCloseStorage.argtypes = [ctypes.c_void_p]
    lib.CascFindFirstFile.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_void_p, ctypes.c_char_p]
    lib.CascFindFirstFile.restype = ctypes.c_void_p
    lib.CascFindNextFile.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
    lib.CascFindNextFile.restype = ctypes.c_bool
    lib.CascFindClose.argtypes = [ctypes.c_void_p]
    lib.GetCascError.restype = ctypes.c_uint32


def find_game(explicit):
    if explicit:
        path = Path(explicit).expanduser()
        if not (path / ".build.info").exists():
            fail(f"{path} does not look like a Diablo III install (.build.info missing)")
        return path

    candidates = []
    if os.environ.get("D3_DATA_DIR"):
        candidates.append(Path(os.environ["D3_DATA_DIR"]))
    candidates += [REPO / "Diablo III", REPO / "d3data", Path.cwd() / "Diablo III"]
    prefix = os.environ.get("WINEPREFIX", str(Path.home() / ".wine"))
    candidates.append(Path(prefix) / "drive_c" / "Program Files (x86)" / "Diablo III")

    for candidate in candidates:
        if (candidate / ".build.info").exists():
            return candidate
    fail("no Diablo III install found; pass --game <folder>")


def build_version(game_dir):
    text = (game_dir / ".build.info").read_text(errors="replace")
    match = re.search(r"\|(\d+\.\d+\.\d+\.\d+)\|", text)
    return match.group(1) if match else "unknown"


def parse_toc(blob):
    """Mirror of CascStorage.ReadCoreToc: (group, sno id, name) per TOC entry."""
    entries = []
    for group in range(TOC_GROUPS):
        count = struct.unpack_from("<i", blob, group * 4)[0]
        first = TOC_HEADER + struct.unpack_from("<i", blob, (TOC_GROUPS + group) * 4)[0]
        names = first + count * 12
        for i in range(count):
            entry = first + i * 12
            sno_id = struct.unpack_from("<i", blob, entry)[0]
            name_start = names + struct.unpack_from("<i", blob, entry + 8)[0]
            name_end = blob.find(b"\0", name_start)
            if name_end < 0:
                continue
            entries.append((group, sno_id, blob[name_start:name_end].decode("utf-8", "replace")))
    return entries


class Storage:
    def __init__(self, lib, game_dir):
        self.lib = lib
        self.handle = ctypes.c_void_p()
        if not lib.CascOpenStorage(str(game_dir).encode(), 0xFFFFFFFF, ctypes.byref(self.handle)):
            fail(f"CascLib could not open {game_dir} (error {lib.GetCascError():#x})")

    def close(self):
        if self.handle:
            self.lib.CascCloseStorage(self.handle)
            self.handle = None

    def list_files(self, mask="Base\\*"):
        """Every file under mask as (name, size); mirrors CascStorage.IndexFiles."""
        find_data = ctypes.create_string_buffer(FIND_DATA_SIZE)
        find = self.lib.CascFindFirstFile(self.handle, mask.encode(), find_data, None)
        if not find:
            fail(f"CascLib found no files matching {mask} (error {self.lib.GetCascError():#x})")
        files = []
        try:
            while True:
                raw = find_data.raw
                end = raw.find(b"\0", 0, MAX_PATH)
                name = raw[:MAX_PATH if end < 0 else end].decode("ascii", "replace")
                files.append((name, struct.unpack_from("<q", raw, FIND_DATA_FILE_SIZE)[0]))
                if not self.lib.CascFindNextFile(find, find_data):
                    break
        finally:
            self.lib.CascFindClose(find)
        return files

    def open_file(self, name):
        handle = ctypes.c_void_p()
        ok = self.lib.CascOpenFile(self.handle, name.encode(), 0, 0, ctypes.byref(handle))
        return handle if ok else None

    def size(self, handle):
        value = ctypes.c_ulonglong()
        self.lib.CascGetFileSize64(handle, ctypes.byref(value))
        return int(value.value)

    def read(self, name):
        handle = self.open_file(name)
        if not handle:
            fail(f"CascLib could not open {name} (error {self.lib.GetCascError():#x})")
        try:
            size = self.size(handle)
            buffer = ctypes.create_string_buffer(size)
            read = ctypes.c_uint32()
            if not self.lib.CascReadFile(handle, buffer, size, ctypes.byref(read)) or read.value != size:
                fail(f"CascLib could not read {name} (error {self.lib.GetCascError():#x})")
            return buffer.raw
        finally:
            self.lib.CascCloseFile(handle)

    def extract(self, name, target):
        handle = self.open_file(name)
        if not handle:
            return False
        try:
            total = self.size(handle)
            target.parent.mkdir(parents=True, exist_ok=True)
            with open(target, "wb") as out:
                written = 0
                chunk = ctypes.create_string_buffer(1 << 20)
                while written < total:
                    want = min(len(chunk), total - written)
                    got = ctypes.c_uint32()
                    if not self.lib.CascReadFile(handle, chunk, want, ctypes.byref(got)) or not got.value:
                        raise IOError(f"read failed at {written} of {total}")
                    out.write(chunk.raw[:got.value])
                    written += got.value
            return True
        finally:
            self.lib.CascCloseFile(handle)


def main():
    parser = argparse.ArgumentParser(description="Extract Diablo III data for the DiIiS emulator")
    parser.add_argument("--game", help="Diablo III install folder (default: auto-detect)")
    parser.add_argument("--out", help="output folder for extracted files")
    parser.add_argument("--list", action="store_true", help="summarise the install and exit")
    parser.add_argument("--toc", action="store_true", help="write the parsed SNO table as CSV")
    parser.add_argument("--pattern", help="only files whose path matches this regex")
    parser.add_argument("--limit", type=int, default=0, help="stop after N files")
    parser.add_argument("--all", action="store_true", help="extract every file")
    parser.add_argument("--dry-run", action="store_true", help="print what would be written")
    args = parser.parse_args()

    game_dir = find_game(args.game)
    version = build_version(game_dir)
    print(f"Game data:  {game_dir}")
    print(f"Version:    {version}" + ("" if version == TARGET_BUILD else "  (expected " + TARGET_BUILD + ")"))

    lib = load_library()
    bind(lib)
    storage = Storage(lib, game_dir)
    try:
        files = storage.list_files()
        print(f"Files:      {len(files)} under Base\\, {sum(size for _, size in files) / 1e9:.2f} GB")

        if args.list:
            extensions = {}
            for name, _ in files:
                ext = os.path.splitext(name)[1].lower()
                extensions[ext] = extensions.get(ext, 0) + 1
            for ext, count in sorted(extensions.items(), key=lambda item: -item[1])[:15]:
                print(f"  {ext or '(none)':>10}: {count:>6}")
            return

        out_dir = Path(args.out).expanduser() if args.out else None
        if args.toc:
            if not out_dir:
                fail("--toc needs --out")
            out_dir.mkdir(parents=True, exist_ok=True)
            toc = parse_toc(storage.read("Base\\CoreTOC.dat"))
            manifest = out_dir / "CoreTOC.csv"
            manifest.write_text("group,sno_id,name\n" + "\n".join(f"{g},{i},{n}" for g, i, n in toc) + "\n")
            print(f"SNO table:  {manifest} ({len(toc)} assets)")

        selected = files
        if args.pattern:
            regex = re.compile(args.pattern, re.IGNORECASE)
            selected = [item for item in selected if regex.search(item[0])]
        if args.limit:
            selected = selected[:args.limit]

        if not (args.all or args.pattern or args.limit):
            print("Nothing extracted; pass --all, --pattern or --limit (see --help).")
            return
        if not args.out:
            fail("--out is required when extracting")
        out_dir = Path(args.out).expanduser()
        out_dir.mkdir(parents=True, exist_ok=True)

        if args.dry_run:
            for name, size in selected:
                print(f"  would extract {name} ({size} bytes)")
            return

        done = 0
        total = 0
        failed = 0
        for name, _ in selected:
            target = out_dir / name.replace("\\", "/")
            try:
                if storage.extract(name, target):
                    done += 1
                    total += target.stat().st_size
                    if done % 500 == 0:
                        print(f"  {done}/{len(selected)} files, {total / 1e9:.2f} GB")
                else:
                    failed += 1
            except OSError as error:
                failed += 1
                print(f"  skip {name}: {error}", file=sys.stderr)
        print(f"Extracted:  {done}/{len(selected)} files, {total / 1e9:.2f} GB into {out_dir}"
              + (f", {failed} failed" if failed else ""))
    finally:
        storage.close()


if __name__ == "__main__":
    main()
