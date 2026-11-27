# Agent Guide – DiIiS (Blizzless D3 Emulator)

## Project Overview

DiIiS is an open-source, non-commercial local server emulator for **Diablo III: Reaper of Souls** (client version **2.8.0.99920**). It is written in **C# / .NET 10** (all projects target `net10.0`) and provides a fully self-hosted game server including authentication, lobby, dungeon generation, item generation, AI, quests, and multiplayer (LAN) support.

Game data comes from the installed 2.8.0 game through CascLib, or from the bundled 2.7.4 MPQ archives under `src/DiIiS-NA/DataBase/MPQ`. The bundled `libs/CascLib.dll` is a **Windows** binary: on Linux the CASC path cannot load it and the server falls back to the 2.7.4 MPQs. Set `Storage.GameDataPath` in `config.ini` to the game folder; auto-detection only works on Windows.

> **Legal:** This project is for educational/research purposes only. No proprietary Blizzard assets are included. See [disclaimer.md](disclaimer.md).

---

## Repository Layout

```
blizzless-diiis/
├── src/
│   ├── Blizzless-D3.sln          # Solution file
│   ├── DiIiS-NA/                 # Main server project
│   │   ├── Blizzless.csproj
│   │   ├── Program.cs
│   │   ├── BGS-Server/           # Battle.net Gateway Server (auth/lobby)
│   │   ├── REST/                 # REST API endpoints
│   │   ├── Core/                 # Shared core (config, logging, helpers, storage, MPQ, schedulers)
│   │   ├── DataBase/             # PostgreSQL data access layer
│   │   └── D3-GameServer/        # Game server logic
│   │       ├── GSSystem/
│   │       │   ├── AISystem/     # Monster / boss AI
│   │       │   ├── ActorSystem/  # Actors (NPCs, monsters, players)
│   │       │   ├── GameSystem/   # Game session management
│   │       │   ├── GeneratorsSystem/ # Dungeon / world generation (DRLG)
│   │       │   ├── ItemsSystem/  # Item & affix generation
│   │       │   ├── MapSystem/    # Map / scene system
│   │       │   ├── ObjectsSystem/# Game objects
│   │       │   ├── PlayerSystem/ # Player state & inventory
│   │       │   ├── PowerSystem/  # Skills & powers
│   │       │   ├── QuestSystem/  # Quest scripts (all 5 acts + Adventure Mode)
│   │       │   ├── SkillsSystem/ # Skill trees
│   │       │   └── TickerSystem/ # Game-loop tick management
│   │       ├── CommandManager/   # In-game console commands
│   │       ├── ClientSystem/     # Client connection handling
│   │       ├── MessageSystem/    # Packet/message processing
│   │       └── AchievementSystem/
│   └── DiIiSNet/                 # Shared networking library
├── configs/
│   └── config.mods.json          # Default world configuration (rates, multipliers, etc.)
├── db/                           # Database migration/seed scripts
├── docs/
│   ├── commands-list.md          # In-game command reference
│   ├── game-world-settings.md    # config.mods.json documentation
│   ├── items-identification-doc.md
│   └── report-form.md
├── Dockerfile
├── docker-compose.yml
└── README.md
```

---

## Build & Run

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 |
| PostgreSQL | 17 (test stack) / 9.5.25+ (minimum) |

### Build

```shell
dotnet publish ./src/DiIiS-NA/Blizzless.csproj --configuration Release --output ./publish
```

### Database (manual)

1. Install PostgreSQL and create databases: `diiis` and `worlds`.
2. Edit `src/DiIiS-NA/database.Account.config` and `database.Worlds.config` with your credentials.
3. Restore `src/DiIiS-NA/worlds.backup` to the `worlds` database.

**Or use MariaDB/MySQL:**

1. Import `db/initdb/dump.mysql.sql` and `db/initdb/dump.worlds.mysql.sql`.
2. Copy `database.Account.mysql.config` / `database.Worlds.mysql.config` to the publish directory and set credentials.
3. Set `DatabaseType = mysql` under `[Storage]` in `config.ini`.

### Database (Docker – recommended)

**PostgreSQL (default):**
```shell
docker-compose up
```

**MariaDB/MySQL:**
```shell
docker-compose -f docker-compose.mysql.yml up
```

### Start the Server

```shell
cd ./publish
./Blizzless          # or dotnet Blizzless.dll
```

Once started, create an account via the console:

```
!account add username@ Password BattleTag
!account add username@ Password BattleTag owner   # with owner role
```

---

## Configuration

The server configuration lives in `config.mods.json` (generated from `config.ini` on first run). Key sections:

| Section | Notable Keys |
|---------|-------------|
| `Rate` | `Experience`, `Money`, `Drop`, `ChangeDrop` |
| `Health` | `PotionRestorePercentage`, `PotionCooldown`, `ResurrectionCharges` |
| `Monster` / `Boss` | `HealthMultiplier`, `DamageMultiplier` |
| `Quest` | `AutoSave`, `UnlockAllWaypoints` |
| `NephalemRift` | `ProgressMultiplier`, `AutoFinish`, `OrbsChance` |

Full reference: [docs/game-world-settings.md](docs/game-world-settings.md)

---

## Key In-Game Commands

| Command | Example | Description |
|---------|---------|-------------|
| `!account add` | `!account add user@ pw Tag` | Create account |
| `!levelup` | `!levelup 10` | Level up character |
| `!item` | `!item Unique_Chest_Set_06_x1` | Spawn item by name |
| `!spawn` | `!spawn 6632` | Spawn monster by SNO ID |
| `!tp` | `!tp 71150` | Teleport to world |
| `!gold` / `!platinum` | `!gold 1000000` | Add currency |
| `!difficulty` | | Change game difficulty |
| `!lookup` | `!lookup actor zombie` | Search SNO databases |

Full reference: [docs/commands-list.md](docs/commands-list.md)

---

## Active Development Roadmap

The current focus areas (see README for full details):

1. **Rift / Greater Rift System** – GR closure, exit portal, resource consumption, death penalty, mob removal.
2. **Items & Crafting** – Kanai's Cube, Enchant NPC, Ramaladni's Gift, gem/affix/set/legendary validation.
3. **Progression & Gameplay** – Waypoint crashes, normal rift portals, difficulty persistence, weapon display.
4. **Bounties & Rewards** – Full bounty system validation.

---

## Development Guidelines

- **Active branch:** `test-stable` – current 2.8.0 work lands here (`community` is the legacy upstream branch; CI builds `test-stable` and `community`).
- **Testing branch:** `test-stable` or `community` for QA/playtest.
- **Language & runtime:** C# (latest supported by the SDK) / .NET 10 (`net10.0`). Do not change the target framework without explicit discussion.
- **Database:** PostgreSQL (default) or MariaDB/MySQL (opt-in via `DatabaseType = mysql` in `config.ini`). The persistence layer uses NHibernate/FluentNHibernate (`Core/Storage/`). Do not introduce additional ORM frameworks or alternative data-access libraries.
- **No proprietary assets:** Never commit Blizzard game data, MPQ files, or client binaries. The game folder is `.gitignore`d (`/Diablo III/`, `/d3data/`).
- **No donate/store features:** The donation store is intentionally removed and must not be re-added.
- **Secrets:** Never commit credentials. Connection strings go in `database.*.config` files (already `.gitignore`'d from production).

## Linux: reading an original install

The bundled `src/DiIiS-NA/libs/CascLib.dll` is a Windows binary and cannot be loaded on Linux, so the
server would fall back to the bundled 2.7.4 MPQ archives. Build the Linux library once:

```shell
./tools/build_casclib.sh          # cmake + tools/casclib-read-only.patch -> src/DiIiS-NA/libs/libcasc.so
```

Then point `Storage.GameDataPath` in `config.ini` at the install (auto-detection only works on Windows):

```ini
GameDataPath = /path/to/Diablo III
```

The Docker image builds the same library, and `docker-compose.test.yml` mounts the install read-only at
`/d3data`. Two helper tools use it:

- `tools/casc_extract.py --list` lists the install (files, sizes); `--pattern` / `--all` extract into a folder.
- `tools/patch_client.sh --apply|--undo|--launch` prepares an **unmodified** client (hosts redirect plus the
  server's CA in the Wine store) so the original game can be used; no game file is changed.

The Linux `CASC_FIND_DATA` layout differs from Windows (`MAX_PATH` is 1024 and `FileSize` sits at offset
1064 instead of 304); `CascStorage` selects the offsets per platform. Keep them in sync when updating CascLib.

### Submitting Issues

Follow the report form at [docs/report-form.md](docs/report-form.md) before opening a GitHub issue.

---

## Client Setup (for testing)

1. Use Diablo III client **2.8.0.99920**.
2. The server creates its own login certificate and local CA on first start (there is no bundled `bnetserver.p12` any more). `play.bat` (Windows) or `tools/play.sh` (Linux/Wine) installs/trusts the CA for you.
3. Redirect `us.actual.battle.net` and `eu.actual.battle.net` to `127.0.0.1` via the hosts file, **or** patch the executable directly with your server IP. `play.bat` / `tools/play.sh` do this and undo it when the game closes.
4. Launch with `-launch` argument: `"Diablo III64.exe" -launch`.
