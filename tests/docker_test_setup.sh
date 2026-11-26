#!/usr/bin/env bash
# Fallback launcher for the Docker test stack when the Compose plugin is not
# installed. Mirrors docker-compose.test.yml.
#
#   ./tests/docker_test_setup.sh up      # db + server
#   ./tests/docker_test_setup.sh tests   # run unit + regression suites in a container
#   ./tests/docker_test_setup.sh logs    # follow server logs
#   ./tests/docker_test_setup.sh down    # remove containers/network (db volume kept)
set -euo pipefail

cd "$(dirname "$0")/.."

NET=diiis-test-net
DB=diiis-test-db
SRV=diiis-test-server
PG_USER=postgres
PG_PASS="${POSTGRES_PASSWORD:-postgres}"
APP_USER="${DIIIS_DB_USER:-diiis}"
APP_PASS="${DIIIS_DB_PASSWORD:-diiis_test}"
D3_DIR="${D3_DATA_DIR:-$PWD/d3data}"

cleanup_network() {
    docker network inspect "$NET" >/dev/null 2>&1 || docker network create "$NET" >/dev/null
}

wait_for_db() {
    echo "waiting for PostgreSQL..."
    for _ in $(seq 1 60); do
        if docker exec "$DB" pg_isready -U "$PG_USER" -d postgres >/dev/null 2>&1; then
            echo "database ready"
            return 0
        fi
        sleep 2
    done
    echo "database did not become ready" >&2
    docker logs "$DB" | tail -40 >&2 || true
    return 1
}

cmd_up() {
    cleanup_network
    docker rm -f "$DB" "$SRV" >/dev/null 2>&1 || true
    docker volume create diiis-test-db-data >/dev/null

    docker run -d --name "$DB" --network "$NET" \
        -e POSTGRES_USER="$PG_USER" -e POSTGRES_PASSWORD="$PG_PASS" -e POSTGRES_DB=postgres \
        -e APP_DB_USER="$APP_USER" -e APP_DB_PASSWORD="$APP_PASS" -e TZ=UTC \
        -v "$PWD/db/initdb/dump.sql:/docker-entrypoint-initdb.d/01-dump.sql:ro" \
        -v "$PWD/db/initdb/02-worlds.sh:/docker-entrypoint-initdb.d/02-worlds.sh:ro" \
        -v "$PWD/db/initdb/03-app-user.sh:/docker-entrypoint-initdb.d/03-app-user.sh:ro" \
        -v "$PWD/src/DiIiS-NA/worlds.backup:/opt/worlds.backup:ro" \
        -v diiis-test-db-data:/var/lib/postgresql/data \
        -p 127.0.0.1:55432:5432 \
        postgres:17 >/dev/null
    wait_for_db

    mkdir -p "$D3_DIR"
    docker build -t diiis-test-server . >/dev/null

    docker run -d --name "$SRV" --network "$NET" --restart no \
        -e DIIIS_DB_TYPE=postgres -e DIIIS_DB_SERVER=db -e DIIIS_DB_PORT=5432 \
        -e DIIIS_DB_USER="$APP_USER" -e DIIIS_DB_PASSWORD="$APP_PASS" -e TZ=UTC \
        -v "$PWD/configs/test/config.ini:/app/config.ini:ro" \
        -v "$D3_DIR:/d3data:ro" \
        -p 83:83 -p 1119:1119 -p 1345:1345 -p 2001:2001 -p 9800:9800 -p 9100:9100 \
        diiis-test-server >/dev/null

    echo "server started: docker logs -f $SRV"
    echo "game data: $D3_DIR"
}

cmd_tests() {
    docker run --rm --network "$NET" \
        -e TMPDIR=/tmp -e HOME=/tmp -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
        -u "$(id -u):$(id -g)" \
        -v "$PWD:/src" -w /src \
        mcr.microsoft.com/dotnet/sdk:10.0 \
        bash /src/tests/run_all_tests.sh
}

case "${1:-up}" in
    up) cmd_up ;;
    tests) cmd_tests ;;
    logs) docker logs -f "$SRV" ;;
    down)
        docker rm -f "$SRV" "$DB" >/dev/null 2>&1 || true
        docker network rm "$NET" >/dev/null 2>&1 || true
        echo "stopped (volume diiis-test-db-data kept)"
        ;;
    *) echo "usage: $0 {up|tests|logs|down}" >&2; exit 2 ;;
esac
