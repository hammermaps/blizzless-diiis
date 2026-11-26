# Legacy of Dreams display

The gem tooltip and the combat meter describe different quantities. At rank 99 the gem's first line is
**375% per legendary item**. Its rank-25 secondary line says that the bonus doubles for ancient items.
The first line does not change to 750% or to the character's equipment total. With thirteen ancients and
no active set bonus, the equipment total is **+9750% damage (98.5×)** and **52% damage reduction (0.48× incoming)**.
A set item can count; an active set bonus disables LoD.

## Evidence and cause

- [Blizzard's 2.6.6 release notes](https://news.blizzard.com/en-gb/article/23093788/patch-2-6-6-is-now-live)
  specify the base 3.75% damage and 2% reduction per legendary, no active set bonuses, rank-25 ancient doubling,
  and maximum rank 99 (read from `ItemTable.JewelMaxRank`; Urshi refuses upgrades at the cap).
- Installed PC client **2.8.0.99920**, `Base\GameBalance\Items_Legendary.gam`, item `Unique_Gem_023_x1`:
  within the 1408-byte record, the base attribute at offset 624 is `Item_Power_Passive[483319] = 0.0375`;
  the per-rank attribute at 1248 is the same power and value; the secondary attribute at 1272 is
  `Item_Power_Passive[483320] = 1`; the unlock rank at 1296 is 25. All three formula scripts are constant
  pushes followed by return. The secondary-unlock field is also 25 for the other ordinary legendary gems.
- The installed `enUS\StringList\ItemPassivePowerDescriptions.stl` formats the primary value as
  `VALUE1 * 100` and gives the ancient doubling its own secondary description.
- Before this fix, the actual outgoing packet for the user's rank-99 gem contained `Jewel_Rank = 99`
  but only `Item_Power_Passive[483319] = 0.0375`, with no secondary power. The server's damage path
  independently used the rank, so combat could receive the bonus while the tooltip remained at 3.75%.

The item path now applies the data-defined per-rank increment and unlocked secondary attribute on creation,
load, socket reconstruction, command rank changes, and Urshi upgrades. Values are recomputed from the base
definition so repeated loads or refreshes cannot compound them. This also corrects the shared rank-attribute
handling for other ordinary legendary gems; it does not implement their missing combat mechanics.

The meter reports LoD's rank, equipment counts, ancient unlock state, active-set restriction, and the actual
outgoing/incoming multipliers stored by the player calculation. Direct hits, pet hits, and incoming mitigation
name the LoD factor. This change preserves the existing LoD damage calculation.

## Validation and limits

`CombatRegression --assets` runs `LegendaryGemChecks` covering the parsed unlock offset, ranks 0/24/25/99, repeated refreshes and rank decreases,
mixed gear including primals, the thirteen-ancient maximum, a lone set piece versus an active two-piece bonus,
and removing the gem. `CombatMeterChecks` checks the serialized display and its inactive state.

To repeat the tooltip checks against the installed item data extracted with `tools/protocol/casc_read.py`, set
`D3_LOD_GAMEBALANCE` to the extracted `Items_Legendary.gam` file and run `CombatRegression --gem-tooltips`.
These optional checks initialize the item catalogue and need the installed client and a console (the existing
debug game-data loader clears the console). The default combat checks do not require these assets.
No Blizzard assets are included in the regression fixture.

Verified on 2026-10-28: all 69 combat checks including the optional LoD checks passed;
both tooltip checks passed against the extracted installed item record and failed against the previous
server DLL. The WPF fixture rendered the rank-99 values and passed hotkey registration/hide/show checks.
The updated local server opened all four listeners and the ordinary-user meter reconnected.

These checks establish server arithmetic and display data, not a full retail combat comparison. Urshi's
upgrade success/failure rules, rank caps, broken equipment, and Ring of Royal Grandeur interactions remain
outside this display fix. In-game tooltip rendering still requires a client reconnect and visual confirmation.
