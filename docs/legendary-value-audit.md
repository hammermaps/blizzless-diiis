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
| Urshi upgrades | 3 base, +1 no deaths, +1 empowered rift; chance 100% − 10% per rank above GR, min 1% | Wiki/Blizzard: same rules | matches |

## Open items

- Ranslor's per-target immunity is 6 s from the installed formula while community
  wikis still state 5 s; a live capture should settle it.
- Valthek's damage bonus for the stationary Wicked Wind rune is not separated in
  the current implementation (multiplier applies to every Energy Twister hit).
- Convention's initial phase and Mantle's per-class channel cadences still need
  live comparison.
