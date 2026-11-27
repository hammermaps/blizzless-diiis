#!/usr/bin/env bash
# Linux launcher: points Diablo III at the local D3Offline server via Wine.
# Equivalent to tools/play.ps1 for Windows.
# Usage: ./tools/play.sh [--undo]
set -euo pipefail

MARKER='# D3Offline'
HOSTS_FILE='/etc/hosts'
HOST_NAMES=(
    'us.actual.battle.net'
    'eu.actual.battle.net'
    'telemetry-in.battle.net'
    'iir.blizzard.com'
)

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BATTLENET_EXE="$REPO_DIR/Battle.net/Battle.net.exe"

# Proton 11 fixes libcef CrRendererMain crashes that occur in vanilla Wine 11.
STEAM_ROOT="${STEAM_ROOT:-$HOME/.steam/debian-installation}"
PROTON_DIR="$STEAM_ROOT/steamapps/common/Proton 11.0"
WINE="${WINE:-$PROTON_DIR/files/bin/wine}"
WINESERVER="${WINESERVER:-$PROTON_DIR/files/bin/wineserver}"
PROTON_LIB="$PROTON_DIR/files/lib"

# Use the existing system Wine prefix so Battle.net's installed data is available.
WINEPREFIX="${WINEPREFIX:-$HOME/.wine}"

# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------

set_redirect() {
    local enable="$1"
    # Remove any existing D3Offline entries
    sudo sed -i "/$MARKER/d" "$HOSTS_FILE"
    if [[ "$enable" == "1" ]]; then
        for host in "${HOST_NAMES[@]}"; do
            echo "127.0.0.1 $host $MARKER" | sudo tee -a "$HOSTS_FILE" > /dev/null
        done
        # Flush DNS cache (systemd-resolved)
        sudo systemd-resolve --flush-caches 2>/dev/null || true
        sudo resolvectl flush-caches 2>/dev/null || true
        echo "Redirect enabled: D3 login servers → 127.0.0.1"
    else
        echo "Redirect removed."
    fi
}

trust_cert() {
    local cert_path
    # Wine path → Linux path
    cert_path="$WINEPREFIX/drive_c/users/$(id -un)/AppData/Local/D3Offline/D3Offline-LocalCA.cer"
    if [[ ! -f "$cert_path" ]]; then
        echo "No certificate at $cert_path."
        echo "Start the server once so it can generate one, then re-run this script."
        exit 1
    fi

    # Trust in Wine's own certificate store (for HTTPS inside Wine)
    local wine_env
    wine_env="WINEPREFIX=$WINEPREFIX WINESERVER=$WINESERVER LD_LIBRARY_PATH=$PROTON_LIB:${LD_LIBRARY_PATH:-} WINEDEBUG=-all"
    if ! env $wine_env "$WINE" certutil -verifystore Root D3Offline 2>/dev/null | grep -q 'D3Offline'; then
        echo "Trusting D3Offline certificate in Wine store (one time)..."
        env $wine_env "$WINE" certutil -addstore Root "Z:${cert_path//\//\\\\}" || true
    fi
}

# ------------------------------------------------------------------
# Main
# ------------------------------------------------------------------

if [[ "${1:-}" == "--undo" ]]; then
    set_redirect 0
    WINEPREFIX="$WINEPREFIX" WINESERVER="$WINESERVER" \
    LD_LIBRARY_PATH="$PROTON_LIB:${LD_LIBRARY_PATH:-}" WINEDEBUG=-all \
    "$WINE" certutil -delstore Root D3Offline 2>/dev/null || true
    echo "Redirect and certificate removed."
    exit 0
fi

if [[ ! -f "$BATTLENET_EXE" ]]; then
    echo "Battle.net.exe not found at $BATTLENET_EXE"
    exit 1
fi

# Ensure Proton's libvkd3d DLLs are present in the prefix.
for bits in i386 x86_64; do
    dest_dir="$WINEPREFIX/drive_c/windows/$([ "$bits" = i386 ] && echo syswow64 || echo system32)"
    for dll in libvkd3d-1.dll libvkd3d-shader-1.dll libvkd3d-utils-1.dll; do
        src="$PROTON_DIR/files/lib/vkd3d/$bits-windows/$dll"
        [[ -f "$src" && ! -f "$dest_dir/$dll" ]] && cp "$src" "$dest_dir/$dll"
    done
done

# Init the prefix on first run from Proton's default_pfx template.
if [[ ! -d "$WINEPREFIX" ]]; then
    DEFAULT_PFX="$PROTON_DIR/files/share/default_pfx"
    echo "Initialising Proton Wine prefix at $WINEPREFIX ..."
    cp -a "$DEFAULT_PFX" "$WINEPREFIX"
    ln -sf "../drive_c" "$WINEPREFIX/dosdevices/c:"
    ln -sf "/" "$WINEPREFIX/dosdevices/z:"
    find "$WINEPREFIX" -type l ! -exec test -e {} \; -print 2>/dev/null | while read -r link; do
        rel=$(readlink "$link" | sed 's|^\(\.\./\)*||')
        abs="$PROTON_DIR/files/$rel"
        [[ -e "$abs" ]] && ln -sf "$abs" "$link"
    done
fi

# Check server is running on port 1119
if ! ss -tlnp 2>/dev/null | grep -q ':1119' && ! netstat -tlnp 2>/dev/null | grep -q ':1119'; then
    echo "The D3Offline server is not running (nothing on port 1119)."
    echo "Start it first, then re-run this script."
    exit 1
fi

trust_cert
set_redirect 1

cleanup() {
    set_redirect 0
}
trap cleanup EXIT

echo "Battle.net is pointed at the local server. Close it to switch back."
echo ""

# Launch Battle.net via Proton Wine.
# --no-sandbox: CEF renderer sandbox is unsupported in Wine (int3 in libcef CrRendererMain).
# Proton 11: fixes WoW64 Mojo-IPC incompatibility that vanilla Wine 11 cannot handle.
WINEPREFIX="$WINEPREFIX" \
WINESERVER="$WINESERVER" \
LD_LIBRARY_PATH="$PROTON_LIB:${LD_LIBRARY_PATH:-}" \
WINEDEBUG=-all \
__NV_PRIME_RENDER_OFFLOAD=1 \
__GLX_VENDOR_LIBRARY_NAME=nvidia \
"$WINE" "$BATTLENET_EXE" --no-sandbox

echo ""
echo "Battle.net closed. Diablo III uses Blizzard's servers again."
