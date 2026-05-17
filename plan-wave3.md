# Wave 3 – Class-Specific Sanctified Powers (21 Powers)

> **Agent instruction:** Before doing anything else, read `agent.md` in the repository root.
> It describes the project layout, build commands, branch rules, and coding conventions.
> All code must be placed on the `community` branch (or a feature branch targeting it).
> Also read `plan-wave1.md` and `plan-wave2.md` before starting.

---

## Goal

Replace the three generic placeholder stat bonuses in `Season27Patch.ApplyEquippedSanctifiedBonus`
with **21 meaningful class-specific Sanctified Powers** (3 per class × 7 classes) that mirror
the Season 27 patch notes.

---

## Background

The current `ApplyEquippedSanctifiedBonus` selects one of three generic bonuses based on
`GetSanctifiedPower(item) % 3`.  The power index is stored as
`Item_LegendaryItem_Level_Override - SanctifiedMarkerBase` and is in the range 0–20 with
offsets per class:

| Class | Power indices |
|-------|--------------|
| Barbarian | 0, 1, 2 |
| Crusader | 3, 4, 5 |
| DemonHunter | 6, 7, 8 |
| Monk | 9, 10, 11 |
| Necromancer | 12, 13, 14 |
| WitchDoctor | 15, 16, 17 |
| Wizard | 18, 19, 20 |

Key source files:

| File | Why |
|------|-----|
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Season27Patch.cs` | `ApplyEquippedSanctifiedBonus` – replace placeholder logic |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PlayerSystem/Player.cs` | `SetAttributesByItems` – already calls `ApplyEquippedSanctifiedBonus` |
| `src/DiIiS-NA/D3-GameServer/MessageSystem/GameAttribute.List.cs` | Full list of `GameAttributes` you can set |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Implementations/HeroSkills/Barbarian.cs` etc. | Reference for how buff auras are applied per class |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/HitPayload.cs` | Reference for in-combat proc patterns |

---

## Sanctified Powers per class (Season 27 reference)

Implement at least a **reasonable approximation** of each power.  Exact tuning can be adjusted
later; correct mechanical behaviour matters more than exact numbers.

### Barbarian (0, 1, 2)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 0 | Whirlwind pulls enemies in and deals 500% increased damage | Increase `Power_Damage_Percent_Bonus` for SNO 80028 (Whirlwind); add pull/knockback flag if possible |
| 1 | Leap creates a shockwave that deals 300% weapon damage | Increase `Power_Damage_Percent_Bonus` for SNO 93202 (Leap) |
| 2 | Earthquake's radius is doubled and deals 200% more damage | Increase `Power_Damage_Percent_Bonus` for SNO 69221 (Earthquake) |

### Crusader (3, 4, 5)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 3 | Condemn instantly explodes; +400% weapon damage | Increase `Power_Damage_Percent_Bonus` for Condemn SNO |
| 4 | Blessed Hammer deals 500% more damage vs. Stunned enemies | Increase `Power_Crit_Percent_Bonus` for Blessed Hammer SNO |
| 5 | Holy Cause heals for 5% max HP per kill | Add `Hitpoints_On_Kill` bonus |

### DemonHunter (6, 7, 8)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 6 | Strafe fires twice as fast | Increase `Attacks_Per_Second_Percent` for Strafe SNO |
| 7 | Multishot fires 50% extra arrows | Increase `Power_Damage_Percent_Bonus` for Multishot SNO (75301) |
| 8 | Vengeance fires additional rockets dealing 100% weapon damage | Add `Damage_Weapon_Percent_Bonus` |

### Monk (9, 10, 11)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 9 | Wave of Light deals 500% more damage | Increase `Power_Damage_Percent_Bonus` for Wave of Light SNO (97328) |
| 10 | Sweeping Wind's damage is tripled | Increase `Power_Damage_Percent_Bonus` for Sweeping Wind SNO |
| 11 | Seven-Sided Strike can be activated while moving | Add `Movement_Scalar_Uncapped_Bonus` 0.3f + cooldown reduction for SSS |

### Necromancer (12, 13, 14)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 12 | Bone Spear deals 400% more damage | Increase `Power_Damage_Percent_Bonus` for Bone Spear SNO |
| 13 | Army of the Dead cooldown reduced by 50% | Add `Power_Cooldown_Reduction` for Army of the Dead SNO |
| 14 | Skeletal Mages have no active skill limit | Increase `Hitpoints_Max_Percent_Bonus` and `Power_Damage_Percent_Bonus` placeholder |

### WitchDoctor (15, 16, 17)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 15 | Piranhas pulls all enemies and deals 500% weapon damage | Increase `Power_Damage_Percent_Bonus` for Piranhas SNO |
| 16 | Firebats deals 400% more damage | Increase `Power_Damage_Percent_Bonus` for Firebats SNO |
| 17 | Haunt deals 500% more damage and spreads on kill | Add `Power_Damage_Percent_Bonus` + `Hitpoints_On_Kill` |

### Wizard (18, 19, 20)
| Index | Power | Implementation hint |
|-------|-------|---------------------|
| 18 | Meteor deals 500% more damage | Increase `Power_Damage_Percent_Bonus` for Meteor SNO (30744) |
| 19 | Arcane Torrent fires 3× as many missiles | Increase `Power_Damage_Percent_Bonus` for Arcane Torrent SNO |
| 20 | Black Hole consumes all Arcane Power and deals damage per point | Add resource-drain and `Damage_Weapon_Percent_Bonus` |

---

## Detailed tasks

### Task 3-A  Expand `ApplyEquippedSanctifiedBonus`

Replace the current `switch (GetSanctifiedPower(item) % 3)` block with a
`switch (GetSanctifiedPower(item))` using all 21 cases.

For each case, set the relevant `GameAttributes` values on the `player` object following the
patterns already used in `Player.SetAttributesByItems` (see lines 900–1080 of `Player.cs`).

Example structure:

```csharp
switch (GetSanctifiedPower(item))
{
    // Barbarian
    case 0: player.Attributes[GameAttributes.Power_Damage_Percent_Bonus, 80028] += 5.0f; break;
    case 1: player.Attributes[GameAttributes.Power_Damage_Percent_Bonus, 93202] += 3.0f; break;
    case 2: player.Attributes[GameAttributes.Power_Damage_Percent_Bonus, 69221] += 2.0f; break;
    // Crusader
    case 3: ...
    ...
    default: player.Attributes[GameAttributes.Movement_Scalar_Uncapped_Bonus] += 0.15f; break;
}
```

### Task 3-B  Add power-active check helpers

Add a public helper `GetEquippedSanctifiedPower(Player player)` to `Season27Patch.cs` that
returns the power index (0–20) of the first equipped sanctified item, or `-1` if none.

This can be used by HitPayload/DeathPayload in Wave 4 for proc-based powers.

### Task 3-C  (Optional) Wire procs into HitPayload / DeathPayload

For powers that are proc-based (e.g. "on kill", "pulls enemies") rather than passive stat
bonuses, add the corresponding logic in:

- `GSSystem/PowerSystem/Payloads/HitPayload.cs` – use
  `Season27Patch.GetEquippedSanctifiedPower(user)` for damage procs.
- `GSSystem/PowerSystem/Payloads/DeathPayload.cs` – for on-kill effects.

This task is optional for Wave 3 and can be deferred if complex; basic stat bonuses are
sufficient for a first pass.

---

## Acceptance criteria

1. A Barbarian who equips a sanctified item with power index 0 sees an increased Whirlwind
   damage stat in `Player.SetAttributesByItems`.
2. Only the class that the item's power belongs to benefits.
   (The power index is set at sanctification time in `GetRandomSanctifiedPower` using
   `classOffset + FastRandom.Instance.Next(0, 3)` – the class restriction is therefore
   already enforced at sanctification, not equip time.)
3. Build succeeds with no new errors.
4. No regression: non-sanctified items do not receive any bonus.

---

## Files to modify

| File | Change |
|------|--------|
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Season27Patch.cs` | Replace placeholder bonus switch with 21-case switch; add `GetEquippedSanctifiedPower` |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/HitPayload.cs` | (Optional) proc-based power effects |
| `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/DeathPayload.cs` | (Optional) on-kill effects |

---

## Next wave

Continue with **plan-wave4.md**.
