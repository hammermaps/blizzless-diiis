# Agent Guide – DiIiS (Blizzless D3 Emulator)

## Project Overview

DiIiS is an open-source, non-commercial local server emulator for **Diablo III: Reaper of Souls** (client version **2.8.0.99920**). It is written in **C# / .NET 10** (all projects target `net10.0`) and provides a fully self-hosted game server including authentication, lobby, dungeon generation, item generation, AI, quests, and multiplayer (LAN) support.

Game data comes from the installed 2.8.0 game through CascLib, or from the bundled 2.7.4 MPQ archives under `src/DiIiS-NA/DataBase/MPQ`. The bundled `libs/CascLib.dll` is a **Windows** binary; on Linux build `libs/libcasc.so` with `tools/build_casclib.sh` (see *Linux: reading an original install*), otherwise the server falls back to the 2.7.4 MPQs. Set `Storage.GameDataPath` in `config.ini` to the game folder; auto-detection only works on Windows.

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
│   ├── monster-ai-tuning.md      # config.ini combat/AI knobs and how to counter-test
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

Do not publish straight into a directory the server is running from: `database.Account.config` and
`database.Worlds.config` are tracked and get copied by `dotnet publish`, replacing the live credentials with the
template values. The next start then dies with `Npgsql.PostgresException: 28P01: password authentication failed`.
Use `tools/publish_release.sh <target> --config-from <dir with working configs>` - it publishes to staging, syncs
binaries only, and keeps the `database.*.config` already present in the target.

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
| `Game-Server` | `DisabledConversations` — comma separated conversation SNOs the NPCs must not offer (default `136980,109472`; their menu labels are missing from the client's deDE data) |

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

Combat/AI tuning knobs (`MonsterThinkTick`, `MonsterAttackDelaySeconds`, `MonsterSearchRange`,
`MonsterLeashRange`, boss enrage thresholds) are documented in
[docs/monster-ai-tuning.md](docs/monster-ai-tuning.md), including the log line to compare after a
change.

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
- **Commit language:** English only (subject + body). German stays in the chat, not in the repository history.
- **Client changes:** prefer the tools over hand edits — `tools/patch_client_address.py --address <host>` rewrites the
  Battle.net hostnames in `Diablo III64.exe` (with `.orig` backup and `--restore`), `tools/patch_client.sh` does the
  hosts redirect and CA trust. Never modify game data files; CASC access stays read-only.

## Toolchain and package rules

- `global.json` pins SDK **10.0.115** (`rollForward: latestFeature`). Do not remove it: several SDKs can be
  installed side by side and the build must not depend on whichever one happens to be first in `PATH`.
- The package set is deliberately on the .NET 10 line: `Microsoft.Data.Sqlite` for SQLite, `Npgsql` 10.x,
  `MySqlConnector` 2.6.x, `NHibernate` 5.5 + `FluentNHibernate` 3.1. Keep the build free of NuGet security
  advisories (`NU1902`/`NU1903`); a stale EF Core 7 reference once pulled a vulnerable `SQLitePCLRaw` and
  forced an end-of-life ORM into the graph for a single unused `using`.
- NHibernate/FluentNHibernate stays the only persistence layer. No EF Core, no Dapper, no second ORM.

## Where game data comes from (and where it cannot)

- Assets are read from the installed game through CASC. Everything the client still ships must be taken from
  there, never from the bundled mirror.
- `src/DiIiS-NA/DataBase/mpqdata.db` is a **2.7.4 mirror** and only overrides four groups: `Worlds`,
  `GameBalance`, `ConversationList`, `TreasureClass`. Compare it against the install before touching it:

  ```shell
  ./Blizzless --export-mpqdata GameBalance      # dry run, prints the field-by-field diff
  ./Blizzless --export-mpqdata Worlds --apply   # writes, only after a diff review and a backup
  ```

- `ConversationList` and `TreasureClass` no longer exist in the 2.8.0 client (SNO group 13 is empty, there is
  no `TreasureClass` folder), so those two tables cannot be refreshed — they stay on 2.7.4 data. Never invent
  entries for them.
- Worlds cannot be refreshed either: every 2.8.0 `.wrl` is a 200-424 byte stub without scene chunks (594 of
  666 are exactly 200 bytes), while the mirror carries the 2.7.4 composition. `--export-mpqdata Worlds` says so
  and refuses to write. The parser is version-gated (asset type 250): it reads only the dynamic-world flag,
  because the remaining stub bytes differ per file size and never decoded into trustworthy values - do not
  guess them (a fixed 12-byte tail produced denormal floats).
- NPC dialogue menus must not depend on a conversation list that the mirror does not know. A quest step names
  the conversation it waits for (`ListenConversation`) and the conversation asset names its owner
  (`SNOPrimaryNpc`); `InteractiveNPC` offers those conversations directly. Use `DisabledConversations` only
  for entries the client cannot label.

## Character statistics

- `toon_statistics` (gold, playtime, sessions, kills split by monster class, deaths, chests, events, lore) and
  `toon_monster_kills` (kills per monster SNO) are owned by `ToonStatisticsTracker`. Counters are batched in
  memory and flushed every 30 s and on logout; a failed flush puts the counters back.
- Read them back from the database, not from memory: the `!stats` command flushes and then queries.
- Schema changes go through the entity + mapper pair in `Core/Storage/AccountDataBase`; `SchemaUpdate` creates
  new tables and columns on start.

## Performance rules for the tick loop

The game loop runs at 20 Hz (`UpdateFrequency = 50 ms`) with 60 ticks per second. Nothing in a per-tick or
per-attack path may allocate without reason:

- No LINQ in per-tick/per-attack code (`Where`/`Select`/`ToList`/`OrderBy` chains allocate delegates and
  lists). Walk the collection once and keep the best candidate.
- Navigation lookups are O(scenes) today and movement samples dozens of points per step; cache per
  (scene, cell) and sample in larger increments before adding more pathfinding.
- Do not format log messages in hot paths; check the level first.
- Stagger throttled work (`(globalId + tick) % interval`) instead of `tick % interval`, so actors do not all
  compute in the same tick.

## Verification and commit rules

- Before claiming a change works: `dotnet build src/Blizzless-D3.sln -c Debug` (0 errors) and
  `./tests/run_all_tests.sh` (`== all suites passed`). The script sets `TMPDIR=/tmp` because the CombatMeter
  named pipe must fit the 108-character Unix socket limit.
- Commit messages are written in **English**, imperative subject line, and explain the observed symptom, the
  root cause and the verification. Code comments follow the same rule.
- New helper scripts and tools use **underscores**, not hyphens (`sweep_maps.sh`, `patch_client_address.py`);
  the existing hyphenated files keep their names.
- Any change that touches the mirror database, the client binaries or the world data needs a backup and a
  before/after comparison in the commit message.

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
