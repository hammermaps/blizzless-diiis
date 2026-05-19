# Changelog

All notable changes to DiIiS (Blizzless D3 Emulator) are documented here.  
Changes are listed from newest to oldest and grouped by feature area.

---

## [Unreleased]

---

## Recent Changes (Community Branch)

### PR #25 – Docker / Linux Setup (MariaDB)
- Added Linux/Docker deployment with MariaDB and automatic SQL-dump import.
- Database port bound to `127.0.0.1` for security by default.
- Converted grant scripts to shell scripts using `$MARIADB_USER`.
- Updated `docker-compose.mysql.yml` with clarified NAT comments and `PublicIP` placeholder.

### PR #24 – Server Crash Hardening
- Guarded all unprotected LINQ queries against null/empty collections.
- Fixed integer division-by-zero in damage-pulse calculations (guard `< 1`).
- Added null-dereference guards throughout the game server loop.
- Applied code-review improvements for thread safety.

### PR #23 – Companion / Hireling Item Equipping
- Fixed `Encode` `NotImplementedException` in hireling equipment handling.
- Expanded visual equipment broadcast to all 8 hireling item slots.
- Aligned weapon slot comments with codebase terminology.
- Split and clarified leash-helper type guards for hirelings vs. monsters.

### PR #22 – Monster & Boss AI Improvements
- Introduced `MonsterBrain` AI tuning options via `GameServerConfig`:
  - `AttackDelay`, `RetargetDelay`, `SearchRange`, `LeashRange`, `RangedSpacing`, `BossEnrageTargeting`.
- Added leash-reset helper: monsters return to spawn and reset state when player escapes leash range.
- Guard applied to prevent bosses from incorrectly triggering leash behaviour.
- AI state fully reset on leash return (target, aggro flags).
- Added power-score tie tolerance for consistent skill selection.
- Documented all AI tuning constants inline.

### PR #21 – Dungeon Generator (DRLG) Improvements
- Improved dungeon variety through randomised exit selection.
- Made exit selection deterministic (reproducible per seed).
- Polished side-path exit handling and continuing-path exit logic.
- Cleaned up event tile room chance naming and constants.

### PR #20 – xUnit Test Project & CI Pipeline
- Added `src/DiIiS.Tests/` with **108 unit tests** (xUnit + coverlet, targeting `net8.0`).
- Tests cover dungeon generator, item generation, affix formulas, Season 27 patch logic, and more.
- Updated GitHub Actions CI workflow to build and run the test suite.
- Fixed CI: removed `--no-build` flag from `dotnet test` step.
- Fixed `ReinitSeed` test to capture expected/actual values before `Assert.Equal`.

### PR #19 – Rift / Greater Rift Feature Completion
- Added `RiftOnly` monster pool for Nephalem/Greater Rifts.
- Implemented **Empowered Rift** (gold cost scales by GR level using bit-shift formula).
- Rift Guardian now drops correct loot and **Death's Breath**.
- Normal rifts: added close portal after Guardian kill.
- Greater Rifts: world cleanup via `Task.Delay` after completion (TickTimer not ticked in GR).
- Fixed `int` overflow in empowered rift gold cost calculation.
- Extracted all magic numbers to named constants (code-review follow-up).
- Documented `PlayerIndex == 0` guard for single-player rift reward path.

### PR #18 – Agent Docs & Nephalem Rift Comparison Update
- Updated `docs/nephalem-rift-comparison.md` to reflect all implemented fixes.
- Improved readability and thread-safety in rift code paths.

### PR #17 – MariaDB/MySQL Config Cleanup
- Fixed misleading `.gitignore` comment in MySQL config template files.

### PR #16 – Nephalem Rift: Wiki vs. Implementation Gap Analysis
- Added `docs/nephalem-rift-comparison.md` documenting differences between the wiki reference and the C# implementation.
- Implemented 3 HIGH-priority fixes identified in the comparison.

### PR #15 – Wave 1: Patch 2.7.4 Foundation (Season 27)
- Adventure Mode flagged as unlocked for all accounts.
- Difficulty T7–T16 clamped to T6 for characters below level 70.
- Echoing Nightmare XP reduced to 17% of normal.
- Uber boss realms auto-close 60 s after all bosses die.
- Angelic Crucible item-use hook scaffolded (`Season27Patch.TryUseAngelicCrucible`).

### PR #14 – Items: Off-Hand Legendaries & Bug Fixes
- Added **42 previously bugged legendary items** with zero/placeholder stats corrected.
- Added missing off-hand legendary items (e.g. `Unique_Orb_004_x1` → Mirrorball).
- Cleaned up `IsBuggedItem` comments to reflect placeholder/zero stats.

### PR #13 – Adventure Mode: Bounty System (Phase 1)
- Implemented Adventure Mode step 1: bounty reagent pickup and reward scaling.
- Added `docs/adventure-mode-implementation-plan.md`.

### PR #12 – Bug Fix Descriptions in Advanced Docs
- Updated bug descriptions in `docs/Advanced/` to be more accurate.

### PR #11 – Wave 3 / Agent Docs: Class-Specific Sanctified Powers
- Replaced placeholder Sanctified Power bonuses with **21 class-specific powers** (3 per class × 7 classes):
  - **Barbarian:** Whirlwind damage boost, Leap shockwave, Earthquake radius.
  - **Crusader:** Condemn instant explode, Blessed Hammer damage vs. stunned, Holy Cause healing.
  - **Demon Hunter:** Strafe rate, Multishot extra arrows, Vengeance rockets.
  - **Monk:** Wave of Light, Sweeping Wind, Seven-Sided Strike mobility.
  - **Necromancer:** Bone Spear, Army of the Dead CDR, Skeletal Mages.
  - **Witch Doctor:** Piranhas pull, Firebats damage, Haunt spread.
  - **Wizard:** Meteor damage, Arcane Torrent missiles, Black Hole drain.
- Fixed Skeleton King quest bugs (door portal reveal inversion).
- Added null guards and world-load pattern fixes (code review).

### PR #9 – Statistics System Extension
- Extended server code with player and game statistics tracking.

### PR #10 – Build Error Fixes
- Resolved compilation errors introduced during community branch merges.

### PR #8 – Wave 2: Angelic Crucible Perfect Ancient Re-Roll
- Added `AffixGenerator.MaxRollAffixAttributes`: re-evaluates all affix formulas and writes `maxValue` to every attribute.
- `TryUseAngelicCrucible` now forces all affix values to their maximum roll on sanctification.
- Weapon and armour base stats (`Damage_Weapon_Min`, `Damage_Weapon_Delta`, `Armor_Item`) also forced to maximum.
- Persistence unchanged: `PersistItem` serialises the updated attributes map.

### PR #7 – Community Commits Check & MariaDB Support (Core)
- Added MariaDB/MySQL backend support:
  - `MySqlDialect.cs`, `MySqlConnectorDriver.cs` using `MySqlConnector 2.3.7`.
  - All NHibernate mappers use `GeneratedBy.Identity()`.
  - Schema scripts: `db/initdb/dump.mysql.sql` and `db/initdb/dump.worlds.mysql.sql`.
  - `DatabaseType = mysql` opt-in in `config.ini` under `[Storage]`.
  - New Docker Compose file: `docker-compose.mysql.yml`.

### Adventure Mode – Phases 2 & 3 (community branch)
- **Phase 2:** Currency sync (gold/blood shards), rift scaling safeguards, reward constants.
- **Phase 3:** Challenge Rift activation, seasonal journey season field, schema names.

---

## Older / Upstream Changes

### Network & Client Compatibility
- Updated network code for client **2.7.4.84161**.
- Fixed `Skill_Override` `GameAttribute` encoding.
- Fixed Wizard's Ray of Frost rune VFX.
- Fixed resurrection time attribute encoding and corpse resurrection.

### Actor / World System
- Extracted `ActorSno` IDs into a dedicated enum (replaces magic integer constants).
- Extracted `WorldSno` IDs into a dedicated enum.
- Increased default stash size.
- Implemented temporary workaround to unlock stash tabs.

### Auth & Account
- Fixed account creation flow.
- Improved auth error messages.

### Miscellaneous
- Friends subsystem added.
- Portal to New Tristram from the Weeping Hollow patched.
- Item sell sound added.
- Golem despawn on self-destruct fixed.
- Knockback and coin-pickup animations corrected.
- Large number of compiler warnings removed.
- Docker: PostgreSQL container for easier local setup.
