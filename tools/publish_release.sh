#!/usr/bin/env bash
# Publishes the Release build without clobbering the live database configs.
#
# Why: src/DiIiS-NA/database.{Account,Worlds}.config are tracked in the repo and
# `dotnet publish` copies them into the output directory. Publishing straight into
# the directory the server runs from therefore replaced the working credentials
# with the template ones, and the next start died with
#   Npgsql.PostgresException: 28P01: password authentication failed
# This script publishes to a staging directory, syncs binaries only, and keeps
# whatever database.*.config already sits in the target (or restores them from
# --config-from if given).
#
# Usage: tools/publish_release.sh [target] [--config-from <dir>]
#   target        defaults to ~/.hermes/cache/scratch/serverrel
#   --config-from source directory holding the working database.*.config

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="${HOME}/.hermes/cache/scratch/serverrel"
CONFIG_FROM=""

while [ $# -gt 0 ]; do
	case "$1" in
		--config-from) CONFIG_FROM="$2"; shift 2 ;;
		*) TARGET="$1"; shift ;;
	esac
done

STAGING="$(mktemp -d "${TMPDIR:-/tmp}/blizzless_publish.XXXXXX")"
trap 'rm -rf "$STAGING"' EXIT

echo "==> publish (Release) -> $STAGING"
dotnet publish "$REPO_ROOT/src/DiIiS-NA/Blizzless.csproj" -c Release --nologo -v q -o "$STAGING"

# Binaries and data, but never the tracked config templates.
echo "==> sync binaries -> $TARGET"
mkdir -p "$TARGET"
rsync -a --delete \
	--exclude 'database.*.config' \
	--exclude 'config.ini' \
	--exclude 'server.log' \
	"$STAGING/" "$TARGET/"

if [ -n "$CONFIG_FROM" ]; then
	echo "==> restore database configs from $CONFIG_FROM"
	cp "$CONFIG_FROM"/database.Account.config "$CONFIG_FROM"/database.Worlds.config "$TARGET/"
fi

for f in database.Account.config database.Worlds.config; do
	if [ ! -f "$TARGET/$f" ]; then
		echo "FEHLER: $TARGET/$f fehlt - Server kann ohne Datenbankverbindung nicht starten." >&2
		exit 1
	fi
	# The templates use a placeholder user; refuse to start on those silently.
	if grep -q 'User ID=postgres' "$TARGET/$f"; then
		echo "WARNUNG: $TARGET/$f nutzt User 'postgres' (Template?). Mit --config-from korrigieren." >&2
	fi
done

echo "==> fertig: $TARGET"
