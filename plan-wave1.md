# Wave 1 – Patch 2.7.4 Foundation (COMPLETED)

> **Agent instruction:** Before doing anything else, read `agent.md` in the repository root.
> It describes the project layout, build commands, branch rules, and coding conventions.
> All code must be placed on the `community` branch (or a feature branch targeting it).

---

## Status: ✅ Done

Wave 1 implemented the five highest-priority, smallest-effort changes from the 2.7.4 changelog.

### What was done

| # | Change | Files touched |
|---|--------|--------------|
| 1 | Adventure Mode flagged as unlocked for all accounts | `BGS-Server/AccountsSystem/GameAccount.cs` |
| 2 | T7–T16 clamped to T6 for characters below level 70 | `BGS-Server/GamesSystem/GameDescriptor.cs`, `GSSystem/GameSystem/GSBackend.cs` |
| 3 | Echoing Nightmare XP reduced to 17 % of normal | `GSSystem/PlayerSystem/Player.cs` (XP grant path), `GSSystem/ItemsSystem/Season27Patch.cs` |
| 4 | Uber boss realm auto-closes 60 s after all bosses die | `GSSystem/PowerSystem/Payloads/DeathPayload.cs` |
| 5 | Angelic Crucible item-use hook scaffolded | `GSSystem/ItemsSystem/Season27Patch.cs`, `GSSystem/PlayerSystem/Inventory.cs`, `GSSystem/MapSystem/World.cs`, `GSSystem/PlayerSystem/Player.cs` |

### Key file: `Season27Patch.cs`

`src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Season27Patch.cs`

This static helper class is the central hub for all Season 27 logic.  
It is called from `Inventory.OnInventoryRequestUseMessage`, `Player.SetAttributesByItems`,  
`World.SpawnRandomLegOrSetEquip`, and `DeathPayload`.

### Known gaps left for later waves

- `TryUseAngelicCrucible` marks the item as primal-ancient **but does not re-roll affixes to max values** – Wave 2.
- `ApplyEquippedSanctifiedBonus` applies generic placeholder stat bonuses instead of
  class-specific Sanctified Powers – Wave 3.
- No Altar of Rites (Season 28 / patch 2.7.5) – Wave 4 (optional).

---

## Build verification

```shell
dotnet build ./src/Blizzless-D3.sln
```

Expected: `Build succeeded.`  Zero new errors (there are pre-existing warnings that are not your responsibility).

---

## Next wave

Continue with **plan-wave2.md**.
