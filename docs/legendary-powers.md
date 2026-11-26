# Legendary power implementation registry

Target: Diablo III **PC 2.8.0.99920**. Updated 2026-10-29.

This is the allowlist of powers deliberately implemented or repaired and reviewed during the current work. A stored affix, an orange tooltip, extraction, or a Cube assignment does **not** by itself mean the combat effect is implemented. Powers absent from this registry are **not audited**: some have inherited handlers, but their presence and live correctness must be checked before treating them as supported.

“Implemented” below means the described server behavior exists with regression coverage. It does **not** certify exact live parity. Installed formulas establish values and client display metadata; hidden server timing, proc rules, and interactions still require PC observations. Keep limitations attached to each entry.

## Equipment and Cube powers

| Power | Power SNO / attribute | Implemented scope | Evidence, tests, remaining limits |
|---|---:|---|---|
| Etched Sigil | 484596 | Wizard's three channels: rolled Energy Twister multiplier, free casts, selected rune, item-proc suppression, channel cadence and cancellation | [Implementation record](etched-sigil.md); `EtchedSigilChecks`. Published frame breakpoints covered; full current-PC trace comparison pending. |
| The Twisted Sword | 484709 | Rolled multiplier per active cast, capped at three; granted Raging Storm alongside selected rune; up to five merged casts; stack icon | [Implementation record](twisted-sword.md); `TwistedSwordChecks`. Current formulas and merge tests; precise live merge/terrain edge cases pending. |
| Mantle of Channeling | 446640 | Immediate rolled outgoing bonus and independent 25% damage reduction during all nine listed channels: Arcane Torrent, Disintegrate, Ray of Frost, Whirlwind, Strafe, Rapid Fire, Tempest Rush, Firebats, Siphon Blood; owner-pet damage | [Wizard record](wizard-status.md); `WizardChannelingChecks`, [value audit](legendary-value-audit.md). Per-class channel cadence and resource costs remain inherited/unverified. No standalone buff icon is defined in the installed power. |
| Valthek's Rebuke | 484597 | Equipped/Cube Twister damage multiplier and straight travel; Wicked Wind remains stationary; granted and selected runes coexist | [Implementation record](twister-cube.md); `TwisterCubeChecks`. Live terrain and merge movement edges pending. |
| Ranslor's Folly | 478491 | Equipped/Cube Twister multiplier; 30-yard eligible normal-monster pull, two-second pulses, six-second per-owner/target lockout | [Implementation record](twister-cube.md); `TwisterCubeChecks`. First-pulse phase, walls, and global monster CC resistance still need live comparison. |
| Aquila Cuirass | 449064 | 50% damage reduction above the effective roll's primary-resource threshold; quarter-second polling and active icon | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Equality boundary/poll phase need live capture. |
| Stone Gauntlets | 483519 | Up to five +50% armor stacks; five-second armor timer; movement/attack penalties; Ice Climbers suppression; stack icon | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Tested with the equipped Ice Climbers. Unprotected penalty details, including the separate attack-cooldown formula, remain incompletely verified. |
| Hergbrash's Binding | 449048 | Rolled cost reduction for all three Wizard channels; percentage reductions before prorated flat reductions; Cube maximum replaces equipped duplicate | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Cost math checked; live resource traces pending. |
| Ice Climbers | 318741 | Freeze/root prevention and removal; suppresses Stone Gauntlets' movement and attack penalties; does not grant general slow/stun immunity | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Global CC/overlapping status ownership remains inherited behavior. |
| Ashnagarr's Blood Bracer | 449043 | Rolled amplification at creation for Deflection, Galvanizing Ward, Dominance, Barrier Blades, Diamond Skin; Ward capacity display and non-weapon absorption | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Deflection/Ward regression coverage; the other skills' underlying mechanics and shield ordering/recharge still need a separate audit. |
| Stone of Jordan | 484282 | Equalizes supported class elements to the highest static equipment bonus; equipment/Cube refresh and removal; Convention remains independent | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Wizard tested; exotic set/static-bonus interactions not audited. |
| Convention of Elements | 430674 | Independent rolled multiplier on the current element, four-second class-specific cycle, matching element icon; owner-pet lookup | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Wizard cycle tested; initial phase and persistence across death/world changes need live comparison. |
| Squirt's Necklace | 483552 | Ten stacks: +10% outgoing/+5% incoming per stack; one-second gain, two-second reset delay; HP damage clears stacks after shields; stack icon | [Remaining equipment record](equipped-legendaries.md); `EquippedLegendaryChecks`. Shielded/unshielded hits tested; exceptional environmental damage and phase need live comparison. |
| Leoric's Crown | `Gem_Attributes_Multiplier` (727) | Equipped helm's normal socketed gem effect multiplied by 1 + roll; excludes legendary gems and other sockets | [Remaining equipment record](equipped-legendaries.md); installed gem checks. Equipped path only: the Cube catalog currently resolves `Item_Power_Passive`, not this direct-attribute power. |

**14 item powers tracked.** Their implementation lives in the linked records and in `PowerSystem/Implementations/General`, `HeroSkills/Wizard.cs`, `HeroSkills/PassiveBuffs.cs`, `Payloads/HitPayload.cs`, and the equipment/resource hooks in `PlayerSystem`.

The [Wizard legendary backlog](wizard-legendary-backlog.md) records **35 missing
Wizard gameplay powers**, plus Slorak's cosmetic effect. It keeps shared items,
set bonuses, and seasonal powers separate and documents the static-audit scope.

## Legendary gems

| Power | Power SNO | Implemented scope | Evidence and checks |
|---|---:|---|---|
| Legacy of Dreams | 483319 / 483320 | Rank scaling, ancient doubling from rank 25, active-set exclusion, outgoing/pet multiplier, incoming reduction, tooltip and meter lifecycle | [Implementation record](legacy-of-dreams.md); `LegendaryGemChecks`, `CombatMeterChecks`. Rank 99 with thirteen ancients: 98.5× outgoing and 52% reduction. See record for live-parity limits. |

## Current Wizard loadout

Snapshot: toon 2, 2026-10-29. Twelve equipped legendary items are the entries above other than Valthek/Ranslor, which are assigned in the Cube. Blackthorne's Jousting Mail is the thirteenth equipped item: it has ordinary affixes and is only one set piece, so no active set bonus needs implementing. Legacy of Dreams is rank 99 in Convention of Elements. Leoric's Crown currently has **no socketed gem**, so its multiplier has no effect until a normal gem is inserted.

Saved rolls: Crown 93%, Aquila threshold 93%, Sigil 243%, Sword 134%, Hergbrash 59%, Mantle 21%, Ashnagarr 99%, Convention 178%. The Cube supplies current maximum rolls for Valthek/Ranslor.

## Updating this registry

For every newly implemented or changed legendary power, update its row and linked evidence record in the same change. Record the current/legacy SNO, supported classes and runes, equipment/Cube behavior, triggers and timers, stacking, proc exclusions, interactions, display slots, regression checks, and unresolved live observations. Do not silently promote a partial or inherited handler to verified parity. Add legendary gems and set bonuses separately when they are audited.
