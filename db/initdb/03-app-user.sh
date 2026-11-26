#!/bin/bash
# DiIiS-NA – create the application database user for the PostgreSQL test stack.
#
# The postgres image runs as superuser "postgres" (the SQL dumps reference that
# role). The game server connects as APP_DB_USER, so create it here and grant
# access to both databases. Runs after 01-dump.sql and 02-worlds.sh.

set -euo pipefail

APP_DB_USER="${APP_DB_USER:-diiis}"
APP_DB_PASSWORD="${APP_DB_PASSWORD:-diiis_test}"

psql -U postgres -d postgres -v ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '${APP_DB_USER}') THEN
        CREATE ROLE "${APP_DB_USER}" LOGIN PASSWORD '${APP_DB_PASSWORD}';
    END IF;
END
\$\$;
SQL

for db in diiis worlds; do
    psql -U postgres -d "$db" -v ON_ERROR_STOP=1 <<SQL
GRANT ALL PRIVILEGES ON SCHEMA public TO "${APP_DB_USER}";
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO "${APP_DB_USER}";
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO "${APP_DB_USER}";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO "${APP_DB_USER}";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "${APP_DB_USER}";
SQL
done

echo "03-app-user: role ${APP_DB_USER} ready"

# Marker for the compose healthcheck: pg_isready is already true while the init
# scripts are still running, so wait for this file instead.
touch /var/lib/postgresql/data/.diiis-init-complete
