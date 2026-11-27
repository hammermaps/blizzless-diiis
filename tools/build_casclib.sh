#!/usr/bin/env bash
# Builds libcasc.so (x64, Release) from external/CascLib and copies it to src/DiIiS-NA/libs,
# where the server loads it next to the Windows CascLib.dll. Linux counterpart of
# tools/build-casclib.ps1; the same patch keeps the game folder readable for the server.
#
# Usage: ./tools/build_casclib.sh [--clean]
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
CASC_DIR="$REPO_DIR/external/CascLib"
BUILD_DIR="$REPO_DIR/.build/casclib"
PATCH_FILE="$REPO_DIR/tools/casclib-read-only.patch"
TARGET="$REPO_DIR/src/DiIiS-NA/libs/libcasc.so"

if [[ ! -f "$CASC_DIR/CMakeLists.txt" ]]; then
    echo "CascLib source missing; initialising the submodule..."
    git -C "$REPO_DIR" submodule update --init external/CascLib
fi

# The patch opens .build.info and the local CASC files read-only, so a write-protected
# game install works, and makes the build-number parser compile with a non-Windows compiler.
if git -C "$CASC_DIR" apply --check "$PATCH_FILE" 2>/dev/null; then
    git -C "$CASC_DIR" apply "$PATCH_FILE"
    echo "Applied $(basename "$PATCH_FILE")."
elif git -C "$CASC_DIR" apply --reverse --check "$PATCH_FILE" 2>/dev/null; then
    echo "Patch already applied."
else
    echo "Could not apply $PATCH_FILE; check external/CascLib for local changes." >&2
    exit 1
fi

[[ "${1:-}" == "--clean" ]] && rm -rf "$BUILD_DIR"

cmake -S "$CASC_DIR" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE=Release
cmake --build "$BUILD_DIR" -j"$(nproc)"

install -m 0755 "$BUILD_DIR/libcasc.so" "$TARGET"
echo "libcasc.so copied to src/DiIiS-NA/libs."

# Leave the submodule clean so `git status` stays readable; the next run re-applies the patch.
git -C "$CASC_DIR" apply --reverse "$PATCH_FILE"
