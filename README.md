 

![](pictures/logo.png)

# DiIiS Project

DiIiS is a fully-functional open-source local server for [Diablo III: Reaper of Souls](https://eu.diablo3.blizzard.com).  
Supported client version: **2.7.4.84161** — see [CHANGELOG.md](CHANGELOG.md) for recent changes.

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

The currently supported version of the client: **2.7.4.84161**

## Server Deploying
### Prepare Database

The server supports **PostgreSQL** (default) and **MariaDB/MySQL** as database backends.

#### PostgreSQL (default)

##### Manual
1. Install [PostgreSQL 14+](https://www.postgresql.org/download/).
2. Create databases: `diiis` and `worlds`.
3. Set your credentials in `database.Account.config` and `database.Worlds.config`.
4. Import the schema: `psql -U postgres -f db/initdb/dump.sql`

##### Using Docker
1. [Install docker](https://docs.docker.com/get-docker/)
2. Run `docker-compose up` from the repository root.

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
1. Install [.NET 7 SDK and runtime](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) (just runtime, not asp.net or desktop)
2. Go to the repo directory and compile the project using this command:
   ```shell
   dotnet publish ./src/DiIiS-NA/Blizzless.csproj --configuration Release --output ./publish
	```
3. [Skip this stage for local game] Copy the [config.ini](configs/config.ini) file to the publish folder (It overwrites the default settings):
	- Update the parameter entries with your IP record on the network: `BindIP` and `PublicIP`.
4. Go to the publish folder, launch Blizzless executable, wait until server start - it creates a hierarchy.
5. Create user account(s) using console: `!account add Login Password Tag`

#### Example:

> !account add username@ YourPassword YourBattleTag

Creates an account with Login `username@`, password `YourPassword` and BattleTag `YourBattleTag`

> !account add username@ YourPassword YourBattleTag owner

Creates an account with Login `username@`, password `YourPassword` and BattleTag `YourBattleTag` with rank `owner`

## Prepare Client

Do this for each client connecting to the server.

1. Get [supported client](#supported-clients) Diablo 3.

2. Install certificate [bnetserver.p12](src/DiIiS-NA/bnetserver.p12), password - `123` (the game verifies the CA root certificates).

3. Setting up redirects client to your server:

	**Method #1 - Hosts**

	  Add redirects to the `hosts` file (Windows - `%WinDir%\System32\drivers\etc\hosts`, Linux - `/etc/hosts`):  
	  `127.0.0.1 us.actual.battle.net`  
	  `127.0.0.1 eu.actual.battle.net`

	  !After the modification the official Battle.Net application will not be able to connect to the server!

	  **Method #2 - Modify main executable file**

	  ```c
	  // Find null-terminated string enum and rewrite with HexEditor to your IP server.
	  eu.actual.battle.net/
	  us.actual.battle.net/
	  cn.actual.battle.net/
	  kr.actual.battle.net/
	  ```

4. Launch client (`x64` or `x86`) with arguments `"Diablo III64.exe" -launch`

5. Login to the game using your credentials.

6. [Skip this stage for local game] After that, when creating a game (in client), indicate the creation of a public game. Other players, when connecting, must also indicate a public game, and at the start they will connect to you.

7. You're in the game world!

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

