 

![](pictures/logo.png)

# DiIiS Project

DiIiS is a fully-functional open-source local server for [Diablo III: Reaper of Souls](https://eu.diablo3.blizzard.com).

## This fork

This fork runs DiIiS with the current Battle.net client, 2.8.0.99920, on the `2.8.0-client` branch, and reads game data from your own Diablo III install. It's a work in progress. The [changelog](CHANGELOG.md) lists what has changed so far. For the 2.7.4 client, use [upstream DiIiS](https://github.com/blizzless/blizzless-diiis).

# Disclaimer

[Disclaimer and Project Philosophy](disclaimer.md).

# Development

Developers, please contribute using the branch: **_community_**, always create your branches from it and also follow the Roadmap below.

# Enthusiasts

To test the server, use the **_test-stable_** or **_community_** branch

## Features

### Core Systems
- Account creation, authorization and lobby
- Chat, Friends and Clan systems
- LAN / multiplayer support
- REST API for server management

### Gameplay
- All 7 classes including Necromancer, with basic mechanics for almost all active abilities
- Set items, Legendary items and affix generation (including 42 previously bugged off-hand items fixed)
- All main story quests for all 5 Acts fully scripted
- Adventure Mode with Bounties (Phases 1–3), Nephalem Rifts and Greater Rifts
- Challenge Nephalem Rifts (basis implemented)
- Season 27 support: Angelic Crucible (perfect ancient stat re-roll), Sanctified Powers for all 7 classes (21 powers)
- Difficulty system (Normal through Torment XVI; levels below 70 capped at Torment VI)
- Echoing Nightmare (XP tuned to 17% of normal)
- Uber boss realms (auto-close 60 s after all bosses die)

### Rifts & Greater Rifts
- RiftOnly monster pool
- Empowered Rift support
- Rift Guardian loot and Death's Breath drops
- Normal rift close portal
- Greater Rift world cleanup on completion

### World & AI
- DRLG dungeon generator with improved variety and deterministic exit selection
- Monster Brain AI tuning (attack delay, retarget delay, search/leash range, ranged spacing, boss enrage)
- AI implemented for 80%+ of minions and 40%+ of monsters
- Personal AI for approximately half of all bosses
- Leash/reset AI for monsters and bosses

### Infrastructure
- PostgreSQL (default) and MariaDB/MySQL database backends
- Docker support for both PostgreSQL and MariaDB
- 108 unit tests (xUnit) with CI pipeline
- Server hardened against crashes (null-dereference, division-by-zero, unchecked LINQ)

## Restrictions

- Donate Store implementation is removed.

# Installation

## Supported Clients

Each version of the client includes changes to structures, opcodes and attributes.

The currently supported version of the client: **2.8.0.99920**, the current Battle.net version. The 2.7.4.84161 client works with upstream DiIiS, not with this branch.

## Server Deploying
### Prepare Database

The server supports **PostgreSQL** (default) and **MariaDB/MySQL** as database backends.

#### PostgreSQL (default)

##### Manual
1. Install [PostgreSQL 14+](https://www.postgresql.org/download/) (the Docker setup uses 17) and remember the password you give the `postgres` user.
2. Create two empty databases, `diiis` and `worlds`:
   ```shell
   createdb -U postgres diiis
   createdb -U postgres worlds
   ```
3. Restore `worlds.backup` into `worlds`; the server creates the tables in `diiis` itself on first start:
   ```shell
   pg_restore -U postgres --clean --if-exists -d worlds src/DiIiS-NA/worlds.backup
   ```
4. If your `postgres` password isn't `postgres`, change it in `database.Account.config` and `database.Worlds.config`: in `src/DiIiS-NA` before you compile, or in the publish folder after.

##### Using Docker
1. [Install docker](https://docs.docker.com/get-docker/)
2. Run `docker-compose up` from the repository root.

For a self-contained test stack (database + server + automated suites) see
[docs/docker-test-setup.md](docs/docker-test-setup.md): `docker compose -f docker-compose.test.yml up --build`.

#### MariaDB / MySQL (alternative)

##### Manual
1. Install [MariaDB 10.11+](https://mariadb.org/download/) or [MySQL 5.7+](https://dev.mysql.com/downloads/).
2. Import the account schema: `mysql -u root -p < db/initdb/dump.mysql.sql`
3. Import the worlds schema: `mysql -u root -p < db/initdb/dump.worlds.mysql.sql`
4. Copy the MySQL config templates to your publish/output directory:
   ```shell
   cp src/DiIiS-NA/database.Account.mysql.config <publish-dir>/
   cp src/DiIiS-NA/database.Worlds.mysql.config  <publish-dir>/
   ```
5. Edit both config files and set your real host/credentials.
6. Add the following line to `config.ini` under the `[Storage]` section:
   ```ini
   DatabaseType = mysql
   ```

##### Using Docker
1. [Install docker](https://docs.docker.com/get-docker/)
2. Run `docker-compose -f docker-compose.mysql.yml up` from the repository root.

### Compile and run
1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
2. Go to the repo directory and compile the project using this command:
```shell
dotnet publish ./src/DiIiS-NA/Blizzless.csproj --configuration Release --output ./publish
```
3. Copy the [config.ini](configs/config.ini) file to the publish folder (it overwrites the default settings) and set `BindIP`/`PublicIP` to your network addresses. [Skip this stage for local game]
   - A `config.mods.json` is generated from `config.ini` on first start; edit it to change world settings.
4. Go to the publish folder, launch the Blizzless executable and wait until the server starts - it creates a hierarchy.
   - The server finds your Diablo III install by itself. If it's somewhere unusual, set `GameDataPath` under `[Storage]` in `config.ini`.
   - On first start it creates its own login certificate in `%LOCALAPPDATA%\D3Offline`.
   - The database has to be running first. If the server stops with `Failed to connect to 127.0.0.1:5432`, start it with `docker compose up -d db`, or start PostgreSQL if you installed it yourself.
5. Create user account(s) using console: `!account add Login Password Tag`

#### Example:

> !account add username@ YourPassword YourBattleTag

Creates an account with Login `username@`, password `YourPassword` and BattleTag `YourBattleTag`

> !account add username@ YourPassword YourBattleTag owner

Creates an account with Login `username@`, password `YourPassword` and BattleTag `YourBattleTag` with rank `owner`

## Prepare Client

Do this for each client connecting to the server.

1. Install Diablo III through the Battle.net app and let it update to the [supported client](#supported-clients).

2. Start the server (see above) and leave it running.

3. Run `play.bat` from the repo folder and click Yes on the Windows prompts: one to edit the hosts file each time, and one to trust the server's certificate the first time. It points the Battle.net login hosts at 127.0.0.1, sends Blizzard's telemetry and crash-report hosts nowhere, and starts the game with `-launch`.

   **Linux (Wine):** run `tools/play.sh` instead. It requires `sudo` for the hosts edit and passes `--no-sandbox` to work around a [CEF sandbox crash in Wine](#linux-wine). `tools/play.sh --undo` removes the redirect and the certificate.

4. Login to the game using your credentials.

5. When you close the game, play.bat removes the redirect, so the Battle.net app works normally again. `play.bat -Undo` also removes the certificate.

6. [Skip this stage for local game] After that, when creating a game (in client), indicate the creation of a public game. Other players, when connecting, must also indicate a public game, and at the start they will connect to you.

7. You're in the game world!

To do it by hand instead of with play.bat:
- Trust `%LOCALAPPDATA%\D3Offline\D3Offline-LocalCA.cer` (made by the server on first start) under Current User > Trusted Root Certification Authorities.
- Add these lines to `%WinDir%\System32\drivers\etc\hosts`. The official Battle.net app can't connect while they're there.  
  `127.0.0.1 us.actual.battle.net`  
  `127.0.0.1 eu.actual.battle.net`
- Launch `"Diablo III64.exe" -launch` from the game's `x64` folder.

### Linux / Wine

Battle.net embeds Chromium (CEF) for its UI. Wine does not support CEF's renderer sandbox, so the launcher crashes immediately with an `int3` trap in `libcef` unless `--no-sandbox` is passed. `tools/play.sh` handles this automatically.

**Requirements:**
- Wine 10+ with 32-bit support (`wine32:i386`)
- DXVK installed in the Wine prefix (for D3D9/DXGI)
- On Optimus laptops: `libnvidia-egl-gbm1` and `libnvidia-egl-xlib1` (both i386 and amd64)

**Known issues:**
- `libEGL: failed to create dri2 screen` for the NVIDIA GPU — harmless on X11 Optimus; the Intel integrated GPU handles Battle.net's Chromium UI fine.
- `WSALookupServiceBegin failed` — Wine limitation, does not affect connectivity.

# Server Configuration

## Global configuration

Using the configuration file you can easily override the [global world parameters](docs/game-world-settings.md).

## Command system

The command system allows you to get control of the game world if you have rights. A list of commands is available [here](docs/commands-list.md).

# Issues

Check the [report form](docs/report-form.md) before submitting issue, this will help people save time!

# System requirements

|            | **Entry-level**              | **Mid-range**                | **High-end**                 |
| ---------- | ---------------------------- | ---------------------------- | ---------------------------- |
| **CPU**    | Intel Core i5 or AMD Ryzen 5 | Intel Core i7 or AMD Ryzen 7 | Intel Core i9 or AMD Ryzen 9 |
| **Memory** | 4 GB RAM                     | 16 GB RAM                    | 64 GB RAM                    |
| **Disk**   | 500 MB                       | 1 GB                         | 1 GB                         |

# Screenshots

You can see more screenshots [here](SCREENSHOTS.md)

![](pictures/ingame-screen-1.png)

