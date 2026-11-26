# Docker-Testaufbau

Ein ein-Befehl-Teststack für den gemergten 2.8.0-Stand: PostgreSQL mit Schema und
Weltdaten, der Server aus `./Dockerfile` und ein Test-Runner-Container für alle
automatisierten Suiten.

## Voraussetzungen

- Docker 24+ (Compose-Plugin optional, siehe Fallback unten)
- Diablo-III-2.8.0-Installation für den Server (CascLib liest die Daten read-only)
- optional: `.env` mit eigenen DB-Zugangsdaten (`cp .env.example .env`)

Ohne gemountete Installation startet der Server mit den gebündelten 2.7.4-MPQ-Archiven
(Log: "No installed Diablo III found ... using the bundled MPQ archives"). Für einen
2.8.0-Client die Installation über `D3_DATA_DIR` bereitstellen. Die Testsuiten
laufen ohne Spieldaten.

## Schnellstart

```bash
# Spieledaten bereitstellen (Ordner mit Diablo III64.exe, Data/, ...
# oder D3_DATA_DIR auf eine vorhandene Installation zeigen lassen)
D3_DATA_DIR=/pfad/zu/Diablo\ III docker compose -f docker-compose.test.yml up --build
```

Alternativ die Installation nach `./d3data` kopieren/symlinken und ohne
`D3_DATA_DIR` starten.

| Dienst | Container | Ports (host) |
|--------|-----------|--------------|
| db | `diiis-test-db` | 127.0.0.1:55432 → 5432 |
| server | `diiis-test-server` | 83, 1119, 1345, 2001, 9800, 9100 |
| tests (Profil) | `diiis-test-runner` | – |

Host-Port 55432 statt 5432, damit eine lokal installierte PostgreSQL nicht kollidiert.

## Tests ausführen

```bash
docker compose -f docker-compose.test.yml --profile tests run --rm tests
```

Der Runner baut die Solution und führt aus:

1. `DiIiS.Tests` (xUnit)
2. `tests/CombatRegression` (Kampf-/Meter-Verhalten)
3. `tests/CubeRegression` (Kanai's Cube)

`TMPDIR=/tmp` ist im Container gesetzt, weil der CombatMeter-Pipe-Pfad sonst das
Unix-Socket-Limit (108 Zeichen) überschreitet.

## Ohne Compose-Plugin

`docker compose` fehlt z. B. bei reinem `docker.io`-Paket. Dann:

```bash
./tests/docker_test_setup.sh up      # DB + Server bauen/starten
./tests/docker_test_setup.sh logs    # Server-Logs folgen
./tests/docker_test_setup.sh tests   # Suiten im SDK-Container
./tests/docker_test_setup.sh down    # stoppen (DB-Volume bleibt)
```

## Aufbau im Detail

- **db**: `postgres:17`; `db/initdb/dump.sql` legt das Account-Schema in `diiis` an,
  `db/initdb/02-worlds.sh` erzeugt `worlds` und spielt
  `src/DiIiS-NA/worlds.backup` ein. Healthcheck über `pg_isready`.
- **server**: Build aus `./Dockerfile` (SDK 10 → self-contained, runtime-deps 10).
  DB-Zugang ausschließlich über `DIIIS_DB_*`-Variablen; `configs/test/config.ini`
  bindet alle Dienste an `0.0.0.0` und setzt `GameDataPath=/d3data`.
- **tests**: `mcr.microsoft.com/dotnet/sdk:10.0`, Quellbaum read-write gemountet,
  NuGet-Cache als Volume. Läuft als aufrufender Benutzer (`UID`/`GID`).

## Troubleshooting

- **Server-Log: GameDataPath/„game data not found“** → `D3_DATA_DIR` prüfen; der
  Pfad muss auf die Installation zeigen (Ordner mit `Data/` und `Diablo III64.exe`).
- **Port belegt** → Mapping in `docker-compose.test.yml` anpassen (nur Host-Seite).
- **DB neu aufsetzen** → `docker compose -f docker-compose.test.yml down -v`
  (löscht `db-test-data`), beim Fallback-Skript `docker volume rm diiis-test-db-data`.
- **CombatRegression scheitert mit „invalid length for domain sockets“** →
  `TMPDIR` auf einen kurzen Pfad setzen (im Container bereits `/tmp`).
- **Andere DB-Zugangsdaten** → `.env` (`POSTGRES_USER`, `POSTGRES_PASSWORD`) oder
  beim Fallback-Skript die gleichnamigen Umgebungsvariablen exportieren.
