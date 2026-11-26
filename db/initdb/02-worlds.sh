#!/bin/bash
# DiIiS-NA – prepare the "worlds" database for the PostgreSQL test stack.
#
# The official postgres image already created $POSTGRES_DB ("diiis") and ran
# 01-dump.sql against it. This script creates "worlds" and restores the bundled
# custom-format dump (worlds.backup), which is mounted at /opt/worlds.backup.
#
# Runs once, on first container start, as the postgres superuser.

set -euo pipefail

if [ ! -f /opt/worlds.backup ]; then
    echo "02-worlds: /opt/worlds.backup not mounted, skipping worlds restore"
    exit 0
fi

if ! psql -U "$POSTGRES_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='worlds'" | grep -q 1; then
    createdb -U "$POSTGRES_USER" worlds
fi

# dump.sql already created the worlds schema; --clean replaces it with the schema+data
# from the legacy backup. Non-fatal warnings (missing roles/ACLs) must not abort init.
pg_restore -U "$POSTGRES_USER" -d worlds --clean --if-exists --no-owner --no-privileges \
    /opt/worlds.backup || echo "02-worlds: pg_restore finished with warnings"
