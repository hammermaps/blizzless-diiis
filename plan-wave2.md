# Wave 2 – Angelic Crucible: Perfect Ancient Stat Re-Roll

> **Agent instruction:** Before doing anything else, read `agent.md` in the repository root.
> It describes the project layout, build commands, branch rules, and coding conventions.
> All code must be placed on the `community` branch (or a feature branch targeting it).
> Also read `plan-wave1.md` to understand what was already implemented.

---

## Goal

When a player uses an **Angelic Crucible** on a Legendary/Set item the item must receive
**maximum possible values on every existing affix** (identical to Primal Ancient behaviour
in the real game). The current `Season27Patch.TryUseAngelicCrucible` sets the quality flag
but leaves all affix values unchanged.

---

## Background: how affix values are stored

1. Each `Item` holds a list `AffixList` of `Affix` objects.
2. Each `Affix` was generated in `AffixGenerator.AddAffix` / `AffixGenerator.GenerateAffixes`
   using `FormulaScript.Evaluate(effect.Formula.ToArray(), item.RandomGenerator, out result, out minValue, out maxValue)`.
3. The `result` was a random value in `[minValue, maxValue]`.  The formula result was written
   directly into `item.Attributes[attributeId]`.
4. To achieve a **perfect roll**, every attribute value for an affix must be set to `maxValue`.
5. Weapon and armour *base* stats (e.g. `Damage_Weapon_Min`, `Armor_Item`) are generated
   separately in the `Item` constructor – those too must be forced to maximum.

Key source files to read before editing:

| File | Why |
|------|-----|
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/AffixGenerator.cs` | `AddAffix`, `GenerateAffixes` – contains the `FormulaScript.Evaluate` loops |
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Item.cs` | `Item` constructor – weapon/armour base stat generation |
| `src/DiIiS-NA/Core/Helpers/Math/RandomHelper.cs` | `ItemRandomHelper` – used as the RNG inside formulas |
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Season27Patch.cs` | Entry point (`TryUseAngelicCrucible`) |

---

## Detailed tasks

### Task 2-A  Add `MaxRollAffixAttributes` to `AffixGenerator.cs`

Add a **new public static method** `MaxRollAffixAttributes(Item item)` that iterates the
item's `AffixList` and, for every attribute specifier in every affix's definition, re-evaluates
the formula with a deterministic max seed and **writes `maxValue`** (not `result`) back into
`item.Attributes[attributeId, attributeKey]`.

Pseudocode:

```csharp
public static void MaxRollAffixAttributes(Item item)
{
    foreach (var affix in item.AffixList)
    {
        foreach (var effect in affix.Definition.AttributeSpecifier)
        {
            if (FormulaScript.Evaluate(effect.Formula.ToArray(), item.RandomGenerator,
                                       out _, out _, out float maxValue))
            {
                item.Attributes[(GameAttributeF)GameAttribute.Attributes[effect.AttributeId],
                                 effect.SNOParam] = maxValue;
            }
        }
    }
}
```

Notes:
- Use the existing `item.RandomGenerator` (do **not** replace it with a new seed; the client uses
  the seed to reproduce roll positions – only attribute values need to be clamped to max).
- If `minValue == maxValue` the value is already fixed and no change is needed.
- Only touch attribute specifiers with a non-null formula array.

### Task 2-B  Force-max weapon / armour base stats

Inside `Season27Patch.TryUseAngelicCrucible`, after calling `AffixGenerator.MaxRollAffixAttributes`,
also force base-stat maxes for weapons and armour.  Look at the existing code in `Item.cs` where
`Damage_Weapon_Min`, `Damage_Weapon_Delta`, and `Armor_Item` are calculated and replicate that
clamping to max.

### Task 2-C  Call from `TryUseAngelicCrucible`

In `Season27Patch.TryUseAngelicCrucible` (already in `Season27Patch.cs`), add the two calls
right after setting `Ancient_Rank` and before calling `BroadcastChangedIfRevealed`:

```csharp
AffixGenerator.MaxRollAffixAttributes(target);
// Force base weapon/armour max stats here (Task 2-B)
```

### Task 2-D  Persist the updated attributes

`PersistItem` is already called at the end of `TryUseAngelicCrucible`.  No additional persistence
logic is needed; just verify that `ItemGenerator.SaveToDB` serialises the full attributes map
(it calls `item.Attributes.Serialize()` – that is already correct).

---

## Acceptance criteria

1. After using an Angelic Crucible, **all numeric affixes on the item read their maximum
   possible roll** (verify with `!item` command or by inspecting item attributes in debug logs).
2. Build succeeds with no new errors:
   ```shell
   dotnet build ./src/Blizzless-D3.sln
   ```
3. An item that was already primal-ancient receives a second sanctification without
   duplicating or corrupting affixes.
4. Items with `minValue == maxValue` (fixed rolls) are not broken.

---

## Files to modify

| File | Change |
|------|--------|
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/AffixGenerator.cs` | Add `MaxRollAffixAttributes` |
| `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/Season27Patch.cs` | Call `MaxRollAffixAttributes` + base-stat max |

Do **not** change `ItemGenerator.cs`, `Item.cs`, or `Inventory.cs` unless strictly necessary.

---

## Next wave

Continue with **plan-wave3.md**.
