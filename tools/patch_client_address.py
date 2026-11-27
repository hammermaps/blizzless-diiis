#!/usr/bin/env python3
"""Point an original Diablo III client at an arbitrary server address.

The client resolves its Battle.net hosts through four region strings in .rdata
(us/eu/kr/cn.actual.battle.net) plus a test host, selected by a small jump table at
0x1406ea2ab..0x1406ea2db (function returns the region's hostname; regions above 0x12
fall back to the US name, see docs/client-patching.md). Rewriting those strings there is
therefore enough to redirect every login attempt, without touching game logic.

The replacement is NUL padded to the original length, so the file size and every code
offset stay unchanged. The certificate of the emulator must list the new address in its
SAN ([Battle-Server] CertificateNames); otherwise TLS verification rejects the server.

Usage:
    ./tools/patch_client_address.py --address 192.168.1.10
    ./tools/patch_client_address.py --address 127.0.0.1 --battle-net
    ./tools/patch_client_address.py --status
    ./tools/patch_client_address.py --restore
"""
import argparse
import hashlib
import json
import re
import shutil
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
EXE_NAME = "Diablo III64.exe"

# Login hosts: the region switch table plus the test entry the client can be told to use.
LOGIN_HOSTS = [
    "us.actual.battle.net",
    "eu.actual.battle.net",
    "kr.actual.battle.net",
    "cn.actual.battle.net",
    "test.actual.battle.net",
]
# Optional: telemetry endpoints and the RPC endpoint some services use.
BATTLE_NET_HOSTS = [
    "telemetry-in.battle.net",
    "telemetry-in-dev.battle.net",
    "tdk-instance.battle.net",
    "v1.rpc.battle.net",
]


def fail(message):
    print("error: " + message, file=sys.stderr)
    sys.exit(1)


def find_exe(explicit):
    if explicit:
        game = Path(explicit).expanduser()
    else:
        game = None
        for candidate in (Path.cwd() / "Diablo III", REPO / "Diablo III", REPO / "d3data"):
            if (candidate / "x64" / EXE_NAME).exists():
                game = candidate
                break
        if game is None:
            fail("no install found; pass --game <Diablo III folder>")

    assert game is not None
    exe = game / "x64" / EXE_NAME if (game / "x64" / EXE_NAME).exists() else game / EXE_NAME
    if not exe.exists():
        fail(f"{EXE_NAME} not found under {game}")
    return exe


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def manifest_path(exe):
    return exe.with_name(exe.name + ".patch.json")


def backup_path(exe):
    return exe.with_name(exe.name + ".orig")


def read_manifest(exe):
    path = manifest_path(exe)
    if not path.exists():
        return None
    try:
        return json.loads(path.read_text())
    except (OSError, json.JSONDecodeError):
        return None


def validate_address(address):
    if not re.fullmatch(r"[A-Za-z0-9.-]{1,27}", address):
        fail("address must be a hostname or IPv4 address using letters, digits, '.' and '-' (max 27 chars)")
    return address


def plan_replacements(data, hosts, address):
    """Return [(host, old_offset, new_bytes)] or fail when the address does not fit."""
    replacements = []
    too_long = []
    for host in hosts:
        needle = host.encode("ascii")
        new = address.encode("ascii")
        if len(new) > len(needle):
            too_long.append((host, len(needle), len(new)))
            continue
        start = 0
        while True:
            offset = data.find(needle, start)
            if offset < 0:
                break
            padded = new + b"\0" * (len(needle) - len(new))
            replacements.append((host, offset, padded))
            start = offset + 1
    if too_long:
        details = ", ".join(f"{h} ({have} chars, need {need})" for h, have, need in too_long)
        fail("address does not fit into the client's string slots: " + details)
    return replacements


def status(exe, hosts):
    manifest = read_manifest(exe)
    print(f"client:    {exe}")
    print(f"sha256:    {sha256(exe)}")
    if manifest:
        print(f"patched:   {manifest.get('address')} at {manifest.get('patched_at')}")
        print(f"backup:    {backup_path(exe)} ({manifest.get('original_sha256', '')[:16]}...)")
    else:
        print("patched:   no (no patch manifest next to the executable)")
    data = exe.read_bytes()
    for host in hosts:
        print(f"  {host:<28} {'present' if host.encode() in data else 'patched'}")


def patch(exe, hosts, address):
    data = bytearray(exe.read_bytes())
    replacements = plan_replacements(data, hosts, address)
    if not replacements:
        fail("none of the known host strings were found; is this the right executable?")
    if not backup_path(exe).exists():
        shutil.copy2(exe, backup_path(exe))
    original_hash = sha256(backup_path(exe))

    for host, offset, padded in replacements:
        data[offset:offset + len(padded)] = padded

    exe.write_bytes(bytes(data))

    # Verify: same size, no original string left, exactly len(replacements) new slots.
    after = exe.read_bytes()
    if len(after) != len(data):
        fail("file size changed while writing; restore with --restore")
    leftovers = [host for host in hosts if host.encode() in after]
    if leftovers:
        fail("replacement incomplete, still present: " + ", ".join(leftovers))

    manifest = {
        "address": address,
        "hosts": hosts,
        "replacements": len(replacements),
        "original_sha256": original_hash,
        "patched_sha256": sha256(exe),
        "patched_at": __import__("datetime").datetime.now().isoformat(timespec="seconds"),
    }
    manifest_path(exe).write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"patched:   {len(replacements)} string slots -> {address} (file size unchanged)")
    for host, offset, _ in replacements:
        print(f"  {host:<28} @ file offset {offset:#x}")
    print(f"backup:    {backup_path(exe)}")
    print("note:      the server certificate must list the new address ([Battle-Server] CertificateNames)")


def restore(exe):
    backup = backup_path(exe)
    if not backup.exists():
        fail(f"no backup at {backup}")
    expected = (read_manifest(exe) or {}).get("original_sha256")
    shutil.copy2(backup, exe)
    restored = sha256(exe)
    if expected and restored != expected:
        fail(f"restored file hash {restored[:16]}... does not match the recorded original {expected[:16]}...")
    print(f"restored:  {exe}")
    print(f"sha256:    {restored}")


def main():
    parser = argparse.ArgumentParser(description="Redirect an original Diablo III client to another server address")
    parser.add_argument("--address", help="hostname or IPv4 address the client should connect to")
    parser.add_argument("--game", help="Diablo III folder (default: auto-detect)")
    parser.add_argument("--battle-net", action="store_true",
                        help="also rewrite telemetry/RPC hosts (no login effect, keeps traffic local)")
    parser.add_argument("--status", action="store_true", help="show patch state and detectable hosts")
    parser.add_argument("--restore", action="store_true", help="copy the backup back")
    args = parser.parse_args()

    exe = find_exe(args.game)
    hosts = LOGIN_HOSTS + BATTLE_NET_HOSTS if args.battle_net else LOGIN_HOSTS

    if args.status:
        status(exe, hosts)
        return
    if args.restore:
        restore(exe)
        return
    if not args.address:
        parser.error("--address is required unless --status/--restore is used")
    patch(exe, hosts, validate_address(args.address))


if __name__ == "__main__":
    main()
