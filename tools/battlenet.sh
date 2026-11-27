#!/usr/bin/env bash
# Starts the official Battle.net client via Proton (Blizzard's servers, no local redirect).
# Usage: ./tools/battlenet.sh
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BATTLENET_EXE="$REPO_DIR/Battle.net/Battle.net.exe"

# Proton 11 fixes libcef CrRendererMain crashes that occur in vanilla Wine 11.
STEAM_ROOT="${STEAM_ROOT:-$HOME/.steam/debian-installation}"
PROTON_DIR="$STEAM_ROOT/steamapps/common/Proton 11.0"
WINE="${WINE:-$PROTON_DIR/files/bin/wine}"
WINESERVER="$PROTON_DIR/files/bin/wineserver"
PROTON_LIB="$PROTON_DIR/files/lib"

# Use the existing system Wine prefix so Battle.net's installed data is available.
# (The Proton prefix at ~/.wine-battlenet-proton works too but lacks Battle.net
# installation data; use it by passing WINEPREFIX=~/.wine-battlenet-proton.)
WINEPREFIX="${WINEPREFIX:-$HOME/.wine}"

if [[ ! -f "$BATTLENET_EXE" ]]; then
    echo "Battle.net.exe not found at $BATTLENET_EXE"
    exit 1
fi

if [[ ! -f "$WINE" ]]; then
    echo "Proton Wine binary not found at $WINE"
    echo "Set STEAM_ROOT or WINE to the correct path."
    exit 1
fi

# Ensure Proton's libvkd3d DLLs are present in the prefix — Proton's wined3d.dll
# requires them and the system Wine prefix does not include them.
for bits in i386 x86_64; do
    dest_dir="$WINEPREFIX/drive_c/windows/$([ "$bits" = i386 ] && echo syswow64 || echo system32)"
    for dll in libvkd3d-1.dll libvkd3d-shader-1.dll libvkd3d-utils-1.dll; do
        src="$PROTON_DIR/files/lib/vkd3d/$bits-windows/$dll"
        [[ -f "$src" && ! -f "$dest_dir/$dll" ]] && cp "$src" "$dest_dir/$dll"
    done
done

# Init the prefix on first run from Proton's default_pfx template.
# This ensures all Proton DLLs (libvkd3d, dxvk, etc.) are present without
# running the full proton script which requires Steam's runtime environment.
if [[ ! -d "$WINEPREFIX" ]]; then
    DEFAULT_PFX="$PROTON_DIR/files/share/default_pfx"
    if [[ ! -d "$DEFAULT_PFX" ]]; then
        echo "Proton default_pfx not found at $DEFAULT_PFX"
        exit 1
    fi
    echo "Initialising Proton Wine prefix at $WINEPREFIX ..."
    cp -a "$DEFAULT_PFX" "$WINEPREFIX"
    # dosdevices symlinks are absent in default_pfx
    ln -sf "../drive_c" "$WINEPREFIX/dosdevices/c:"
    ln -sf "/" "$WINEPREFIX/dosdevices/z:"
    # default_pfx uses relative symlinks valid only at Proton's install path;
    # rewrite them to absolute paths so the prefix works anywhere.
    find "$WINEPREFIX" -type l ! -exec test -e {} \; -print 2>/dev/null | while read -r link; do
        rel=$(readlink "$link" | sed 's|^\(\.\./\)*||')
        abs="$PROTON_DIR/files/$rel"
        [[ -e "$abs" ]] && ln -sf "$abs" "$link"
    done
fi

# --no-sandbox: CEF's renderer sandbox is not supported in Wine and causes
#               a hard int3 crash in libcef's CrRendererMain thread.
# WINEDEBUG=-all: suppresses harmless Wine debug noise.
# __NV_PRIME_RENDER_OFFLOAD / __GLX_VENDOR_LIBRARY_NAME: Optimus — NVIDIA GPU.
WINEPREFIX="$WINEPREFIX" \
WINESERVER="$WINESERVER" \
LD_LIBRARY_PATH="$PROTON_LIB:${LD_LIBRARY_PATH:-}" \
WINEDEBUG=-all \
__NV_PRIME_RENDER_OFFLOAD=1 \
__GLX_VENDOR_LIBRARY_NAME=nvidia \
"$WINE" "$BATTLENET_EXE" --no-sandbox
