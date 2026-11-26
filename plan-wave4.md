# Wave 4 – Altar of Rites (Season 28 / Patch 2.7.5) – Optional

> **Agent instruction:** Before doing anything else, read `agent.md` in the repository root.
> It describes the project layout, build commands, branch rules, and coding conventions.
> All code must be placed on the `community` branch (or a feature branch targeting it).
> Also read `plan-wave1.md`, `plan-wave2.md`, and `plan-wave3.md` before starting.

---

## ⚠️ Client compatibility warning

This feature requires a **Diablo III client version newer than 2.7.4.84161**.
The DiIiS emulator currently targets **exactly 2.7.4.84161**.

> **Do not begin this wave until the client-side packet format and UI SNO IDs for the
> Altar of Rites have been identified and confirmed to work with the existing message layer.**

This plan is provided for documentation and future planning purposes only.

---

## Goal

Implement the **Altar of Rites** — a new in-town NPC introduced in Season 28 (patch 2.7.5).
Players sacrifice items/materials to unlock 26 permanent account-wide **Seals** (passive buffs)
and 3 powerful **Potion Powers**.

---

## Scope overview

| # | Component | Effort |
|---|-----------|--------|
| 1 | NPC spawn + interaction UI wiring | Large |
| 2 | Database table: unlocked Seals per account per season | Medium |
| 3 | 26 Seal passive attribute effects on the Player | Large |
| 4 | Seal unlock cost validation in Inventory | Medium |
| 5 | 3 Potion Power buff triggers | Medium |

---

## Task 4-A  Identify client SNO IDs

Before writing any code, look up the Altar of Rites NPC and UI in the game's SNO data:

- Find the `ActorSno` for the Altar NPC (it is likely in `ActorSystem/Actor.cs` or in the SNO
  type list at `Core/Types/SNO/ActorSno.cs`).
- Find the `WorldSno` for each town where the altar should appear
  (`Core/Types/SNO/WorldSno.cs`).
- Find the interaction `QuestSno` or conversation SNO that drives the Altar UI.

### How to search

```shell
grep -ri "altar" src/DiIiS-NA/D3-GameServer/Core/Types/SNO/
grep -ri "altar" src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/
```

---

## Task 4-B  Spawn Altar NPC in town worlds

In the appropriate `QuestSystem` Act scripts (e.g. `ActI.cs`, `AdventureMode.cs`), spawn the
Altar NPC at the town waypoint position.  Reference how other town NPCs are spawned:

```csharp
// Example pattern from existing NPC spawn code
World.SpawnMonster(ActorSno.AltarOfRites_NPC, position);
```

---

## Task 4-C  Database: seal unlock table

Add a new NHibernate-mapped entity (follow the pattern in
`src/DiIiS-NA/Core/Storage/AccountDataBase/`) for storing which Seals each account has
unlocked in a given season.

```csharp
// Suggested table: DBSealUnlocks
// Columns: Id (PK), GameAccountId (FK), Season (int), SealMask (long/bitmask for 26 seals)
```

Add the corresponding FluentNHibernate mapping and database migration script under `db/`.

---

## Task 4-D  Seal effects on Player

Create a new static class `Season28Patch.cs` (analogous to `Season27Patch.cs`) under
`GSSystem/ItemsSystem/`.  Implement `ApplySealBonuses(Player player, long sealMask)` that
reads the player's unlocked Seal bitmask and sets the corresponding `GameAttributes`.

Reference Seal effects from the Season 28 patch notes:
- Seal 1: +15% damage
- Seal 2: +10% movement speed
- Seal 3: Immunity to crowd control effects
- Seal 4: Death's Breath drops doubled
- Seal 5: Nephalem Glory activates every 60 s
- … (continue for all 26 Seals)

Call `Season28Patch.ApplySealBonuses(this, dbAccount.SealMask)` from `Player.SetAttributesByItems`.

---

## Task 4-E  Seal unlock cost validation

When the player interacts with the Altar NPC, validate that:
- The player has the required sacrificial items in inventory.
- The required gold/materials are present.
- The requested Seal hasn't already been unlocked.

Deduct the cost and set the corresponding bit in `DBSealUnlocks.SealMask`.

---

## Task 4-F  Potion Power buff triggers

The 3 Potion Powers activate when the player uses a health potion.  Find the potion-use
handler (likely in `Inventory.cs` or a dedicated item script) and add a call to
`Season28Patch.TriggerPotionPower(player, potionPower)`.

---

## Acceptance criteria

> These criteria only apply once client compatibility has been confirmed.

1. The Altar of Rites NPC spawns in Act I / Adventure Mode town.
2. Interacting with the Altar opens the correct UI panel.
3. Unlocking Seals persists across sessions (stored in the DB).
4. The correct passive attribute bonuses are applied per unlocked Seal.
5. Potion Powers trigger on health-potion use.
6. Build succeeds with no new errors:
   ```shell
   dotnet build ./src/Blizzless-D3.sln
   ```

---

## Files to create / modify

| File | Change |
|------|--------|
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Season28Patch.cs` | New – Seal bonuses + Potion Power triggers |
| `src/DiIiS-NA/Core/Storage/AccountDataBase/DBSealUnlocks.cs` | New – NHibernate entity |
| `src/DiIiS-NA/Core/Storage/AccountDataBase/Mappings/SealUnlocksMap.cs` | New – Fluent mapping |
| `db/<timestamp>_altar_seals.sql` | New – Migration script |
| `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/ActI.cs` (or AdventureMode) | Spawn Altar NPC |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PlayerSystem/Player.cs` | Call `Season28Patch.ApplySealBonuses` |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PlayerSystem/Inventory.cs` | Seal unlock cost check; Potion Power trigger |

---

## End of roadmap

This is the final planned wave.  After Wave 4, all items from the Diablo III 2.7.4 /
Season 27–28 changelog relevant to this emulator will be fully implemented.
