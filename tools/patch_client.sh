#!/usr/bin/env bash
# Prepares an ORIGINAL Diablo III install for this emulator without modifying the game files.
#
# The client finds its login servers by DNS name, so it is adapted by pointing those names at
# 127.0.0.1 and trusting the server's own certificate. Nothing inside the game folder changes,
# and --undo puts the machine back exactly as it was. The data the emulator reads comes from the
# same original install via libcasc.so (tools/build_casclib.sh).
#
# Usage:
#   ./tools/patch_client.sh --apply        # redirect + trust CA (asks for sudo once)
#   ./tools/patch_client.sh --launch       # start Diablo III directly, bypassing Battle.net
#   ./tools/patch_client.sh --undo         # remove redirect and certificate
#   ./tools/patch_client.sh --status       # show what is currently applied
set -euo pipefail

MARKER='# D3Offline'
HOSTS_FILE="${HOSTS_FILE:-/etc/hosts}"
SUDO="${SUDO-sudo}"
HOST_NAMES=(
    'us.actual.battle.net'
    'eu.actual.battle.net'
    'telemetry-in.battle.net'
    'iir.blizzard.com'
)
TARGET_BUILD='2.8.0.99920'

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DIR="${D3_DATA_DIR:-}"
CA_NAME='D3Offline-LocalCA.cer'

# Wine/Proton defaults match tools/play.sh so both scripts use the same prefix.
STEAM_ROOT="${STEAM_ROOT:-$HOME/.steam/debian-installation}"
PROTON_DIR="${PROTON_DIR:-$STEAM_ROOT/steamapps/common/Proton 11.0}"
WINE="${WINE:-$PROTON_DIR/files/bin/wine}"
WINESERVER="${WINESERVER:-$PROTON_DIR/files/bin/wineserver}"
PROTON_LIB="${PROTON_LIB:-$PROTON_DIR/files/lib}"
WINEPREFIX="${WINEPREFIX:-$HOME/.wine}"

say()  { printf '%s\n' "$*"; }
fail() { printf 'error: %s\n' "$*" >&2; exit 1; }

find_game() {
    if [[ -n "$GAME_DIR" && -f "$GAME_DIR/.build.info" ]]; then
        printf '%s' "$GAME_DIR"; return
    fi
    local candidate
    for candidate in "$REPO_DIR/Diablo III" "$REPO_DIR/d3data" "$PWD/Diablo III" \
                     "$WINEPREFIX/drive_c/Program Files (x86)/Diablo III"; do
        if [[ -f "$candidate/.build.info" ]]; then
            printf '%s' "$candidate"; return
        fi
    done
    fail "no Diablo III install found; set D3_DATA_DIR or pass --game <folder>"
}

game_version() {
    sed -n 's/.*|\([0-9][0-9.]*\)|.*/\1/p' "$1/.build.info" | tail -1
}

check_build() {
    local version
    version="$(game_version "$1")"
    [[ -n "$version" ]] || fail "could not read the build version from $1/.build.info"
    if [[ "$version" != "$TARGET_BUILD" ]]; then
        say "warning: build $version installed, the emulator targets $TARGET_BUILD"
    else
        say "build:     $version (matches the emulator)"
    fi
}

ca_path() {
    local wine_ca="$WINEPREFIX/drive_c/users/$(id -un)/AppData/Local/D3Offline/$CA_NAME"
    local native_ca="$HOME/.local/share/D3Offline/$CA_NAME"
    if [[ -f "$wine_ca" ]]; then printf '%s' "$wine_ca"; return; fi
    if [[ -f "$native_ca" ]]; then printf '%s' "$native_ca"; return; fi
    return 1
}

wine_env() {
    printf 'WINEPREFIX=%s WINESERVER=%s LD_LIBRARY_PATH=%s:%s WINEDEBUG=-all' \
        "$WINEPREFIX" "$WINESERVER" "$PROTON_LIB" "${LD_LIBRARY_PATH:-}"
}

do_hosts() {
    local enable="$1"
    ${SUDO:+$SUDO }sed -i "/$MARKER/d" "$HOSTS_FILE"
    if [[ "$enable" == "1" ]]; then
        local host
        for host in "${HOST_NAMES[@]}"; do
            printf '127.0.0.1 %s %s\n' "$host" "$MARKER" | ${SUDO:+$SUDO }tee -a "$HOSTS_FILE" > /dev/null
        done
        ${SUDO:+$SUDO }resolvectl flush-caches 2>/dev/null || true
        say "hosts:     redirected ${#HOST_NAMES[@]} Battle.net names to 127.0.0.1"
    else
        say "hosts:     redirect removed"
    fi
}

do_cert() {
    local enable="$1" ca
    if [[ "$enable" == "0" ]]; then
        [[ -x "$WINE" ]] || { say "cert:      nothing to remove (no Wine)"; return; }
        env "$(wine_env)" "$WINE" certutil -delstore Root D3Offline 2>/dev/null || true
        say "cert:      local CA removed from the Wine trust store"
        return
    fi
    if [[ ! -x "$WINE" ]]; then
        say "cert:      skipped, no Wine at $WINE (only needed for a client inside Wine)"
        return
    fi
    if ! ca="$(ca_path)"; then
        say "cert:      no $CA_NAME yet; start the server once, then run --apply again"
        return
    fi
    if env "$(wine_env)" "$WINE" certutil -verifystore Root D3Offline 2>/dev/null | grep -q D3Offline; then
        say "cert:      already trusted ($ca)"
        return
    fi
    env "$(wine_env)" "$WINE" certutil -addstore Root "Z:$(printf '%s' "$ca" | sed 's|/|\\\\|g')" > /dev/null
    say "cert:      trusted $ca"
}

status() {
    say "game:      $(find_game)"
    say "build:     $(game_version "$(find_game)")"
    if grep -q "$MARKER" "$HOSTS_FILE" 2>/dev/null; then
        say "hosts:     redirect present"
    else
        say "hosts:     redirect absent"
    fi
    if ca_path > /dev/null 2>&1; then
        say "cert:      $(ca_path)"
    else
        say "cert:      not generated yet"
    fi
    if ss -tln 2>/dev/null | grep -q ':1119'; then
        say "server:    listening on 1119"
    else
        say "server:    not running (start it before --launch)"
    fi
}

launch() {
    local game exe
    game="$(find_game)"
    exe="$game/x64/Diablo III64.exe"
    [[ -f "$exe" ]] || fail "Diablo III64.exe not found in $game/x64"
    ss -tln 2>/dev/null | grep -q ':1119' || fail "server is not running (nothing on 1119)"
    grep -q "$MARKER" "$HOSTS_FILE" || fail "run --apply first"
    say "launching: $exe -launch"
    env "$(wine_env)" __NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia \
        "$WINE" "$exe" -launch
}

# --game may appear anywhere; everything else is a command.
while [[ $# -gt 0 ]]; do
    case "$1" in
        --game) GAME_DIR="${2:-}"; shift 2 ;;
        --apply) ACTION=apply; shift ;;
        --undo) ACTION=undo; shift ;;
        --status) ACTION=status; shift ;;
        --launch) ACTION=launch; shift ;;
        -h|--help) sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) fail "unknown argument: $1" ;;
    esac
done

ACTION="${ACTION:-status}"

case "$ACTION" in
    status) status ;;
    apply)
        game="$(find_game)"
        say "game:      $game"
        check_build "$game"
        do_hosts 1
        do_cert 1
        say "ready:     start the server, then run --launch (or use tools/play.sh for Battle.net)"
        ;;
    undo)
        do_hosts 0
        do_cert 0
        ;;
    launch) launch ;;
esac
