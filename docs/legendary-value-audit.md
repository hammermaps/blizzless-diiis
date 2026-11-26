# Legendary value audit (wiki / item database cross-check)

Target: PC **2.8.0.99920**. Checked 2026-11-26 against the Diablo Wiki (Fandom),
D3Planner item data and the Blizzard 2.7.7 patch notes; the installed client data
remains authoritative where it differs.

| Item | Code behavior | Reference | Result |
|---|---|---|---|
| Etched Sigil | rolled multiplier only for Energy Twister; free twister every second | Item text: ET only, 200–250% (2.6.10 reverted the all-spender bonus) | matches |
| The Twisted Sword | 125–150% per active cast, damage cap 3, Raging Storm, up to 5 merged casts | 2.7.7 notes: cap reduced from 5 to 3; merge rule unchanged | matches |
| Valthek's Rebuke | straight path + rolled ET multiplier | Item: straight path, 300–400% increased ET damage (2.6.10) | matches |
| Ranslor's Folly | rolled ET multiplier + 30-yard pull, 2 s pulse, 6 s per-target ICD | Item: 225–300% + 30-yard pull. Wiki states 5 s immunity; installed SF26 = 6 | installed value kept (6 s) |
| Mantle of Channeling | rolled outgoing bonus, fixed 25% reduction | Item: 20–25% damage, 25% reduction; nine listed channels | **fixed**: activation was Wizard-only, now all nine |
| Convention of Elements | rolled independent bonus, 4 s, class element cycle | Item: 150–200% for 4 s; alphabetical class rotation | matches |
| Stone of Jordan | static elemental bonuses equalized to the highest | Item: all elemental damage bonuses equal to the highest | matches |
| Squirt's Necklace | +10% damage / +5% damage taken per stack, max 10; shield hits keep stacks | Item and wiki: 10 stacks, life damage resets, absorption does not | matches |
| Aquila Cuirass | 50% reduction above the rolled resource threshold | Item: 50% above 90–95% resource | matches |
| Stone Gauntlets | +50% armor per stack (max 5), −15% move, −20% attack speed | Wiki/D3Planner: same per-stack values | matches |
| Ice Climbers | freeze/immobilize immunity, suppresses Stone Gauntlets penalties | Wiki: cannot be frozen or immobilized | matches |
| Ashnagarr's Blood Bracer | rolled shield amplification | Item: shields 75–100% stronger | matches |
| Hergbrash's Binding | rolled cost reduction for the three Wizard channels | Item: 50–65% channel cost reduction | matches |
| Leoric's Crown | socketed normal-gem effect × (1 + roll) | Item: gem effect increased by 75–100% | matches |
| Legacy of Dreams | 3.75% × (rank+1) damage and 2% reduction per legendary, doubled for ancients from rank 25, no set bonuses | Wiki: rank 99 + 13 legendaries = 4875% / 26%; ancients = 9750% / 52% | matches |
| Urshi upgrades | 3 base, +1 no deaths, +1 empowered rift; chance 100% − 10% per rank above GR, min 1%; no rank ceiling | Wiki/Blizzard: same rules, but every gem has a maximum rank (50/99/100/150 depending on the gem) | **fixed**: the ceiling is now read from `ItemTable.JewelMaxRank` (2.8.0 offset 1268); `!item ... rank` clamps to it |

## 2.8.0 installed-data check

The client data in this repository's records is authoritative where it disagrees
with the wiki. Verified against the installed 2.8.0 fields:

| Mechanic | Installed field | Code |
|---|---|---|
| Legendary gem ceiling | `ItemTable.JewelMaxRank`, offset 1268 | `Item.CanIncreaseLegendaryGemRank`, `ItemCommand` clamp |
| Legendary gem rank scaling | `Attribute1[0]`, unlock rank `JewelSecondaryEffectUnlockRank` | `Item.UpdateLegendaryGemAttributes` |
| Twisted Sword | saved roll `Item_Power_Passive[484709] = 1.34`; damage cap 3 | `TwistedSwordBuff` |
| Etched Sigil | saved roll raw value (e.g. 2.43 → ×3.43) | `EtchedSigilBuff` |
| Valthek / Ranslor | 4.0 / 3.0; Ranslor SF2 = 6 | `EnergyTwister`, `HitPayload` |
| Aquila / Squirt's | SF0 0.25 poll, SF1 0.5 DR / SF0 0.1, SF1 0.05, max 10, reset 2 | `AquilaBuff`, `SquirtsBuff` |
| Stone Gauntlets | SF0/1/2/3/5 = 1 s, 0.15, 0.5, 0.20, 5 | `StoneGauntletsBuff` |
| Mantle of Channeling | SF0 0.25 damage reduction; item lists nine channels | `MantleOfChannelingBuff` |
| LoD | base/per-rank 0.0375, secondary 1, unlock 25, max 99 | `Item.UpdateLegendaryGemAttributes`, `Player.UpdateLegacyOfDreams` |

No installed 2.8.0 value contradicted the implemented constants. The only code
change from this pass is the gem rank ceiling, which the client data defines but
the server previously ignored.

## Open items

- Ranslor's per-target immunity is 6 s from the installed formula while community
  wikis still state 5 s; a live capture should settle it.
- Valthek's damage bonus for the stationary Wicked Wind rune is not separated in
  the current implementation (multiplier applies to every Energy Twister hit).
- Convention's initial phase and Mantle's per-class channel cadences still need
  live comparison.
