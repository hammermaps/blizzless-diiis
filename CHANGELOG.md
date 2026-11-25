# Changelog

This is a fork of [Blizzless DiIiS](https://github.com/blizzless/blizzless-diiis), which targets the 2.7.4.84161 client. It runs the current Battle.net client (2.8.0.99920) and reads game data from your own D3 install. All credit for the server itself goes to the DiIiS devs.

This is a work in progress. It's playable, but there's a lot left to do. Versions stay at 0.x until it's in a state I'd call done. New changes go under the newest version. Once that version is tagged, it gets a date and a new version starts above it.

## 0.1.0 (in progress)

The first version: getting DiIiS onto the 2.8.0 client, plus fixes for what I ran into while playing.

### Before you update

- Only the 2.8.0 client works. A 2.7.4 client can't connect anymore.
- Paragon bonuses saved by the old .NET 7 build don't carry over.
- bnetserver.p12 is gone. The cert gets generated on first start.

### Runtime

- Moved from .NET 7 to .NET 10. .NET 7 is out of support.
- DotNetty 0.7.6. The TLS handshake in 0.7.5 hangs on .NET 8+.
- NHibernate 5.5.4. Versions before 5.5.2 reject every entity on .NET 9+. 5.5.4 also fixes a SQL injection advisory.
- Npgsql 7.0.10, for a security advisory.
- Paragon bonuses are saved as plain bytes now. They used to go through BinaryFormatter, which .NET 8 disables.

### Login / offline

- The server makes its own login certificate on first start, signed by a local CA. The old bundled bnetserver.p12 was the same for everyone, and its private key is public. The CA key only ever exists in memory.
- play.bat trusts the CA once and points the Battle.net login hosts at 127.0.0.1 while the game is running.
- The telemetry and crash report hosts are redirected too, so nothing from an offline session gets sent to Blizzard.
- The redirect is removed when the game closes. `play.bat -Undo` removes the CA as well.
- The login server logs the TLS handshake, the websocket upgrade and the first data a client sends. That helps when a new client version won't connect.

### Game data

- The server reads the CASC storage of your installed game through CascLib (MIT). It finds the install by itself, or you can set `Storage.GameDataPath` in config.ini. Setting it to `mpq` still uses the 2.7.4 MPQs.
- Parsers are updated for the 2.8.0 layouts: item records, paragon bonuses, GameBalance tables and cube recipes.
- An offset that points outside a file is rejected. An asset that fails to parse isn't retried on every access.
- The game folder only needs to be readable. CascLib asked for write access when it checked .build.info, so a read-only install fell back to the 2.7.4 MPQs. tools/build-casclib.ps1 now applies a small patch that opens those files read-only.

### Protocol

- 2.8.0 sends game requests through GameUtilities v2. Those get translated to the old v1 requests.
- Updated message layouts:
  - hero state (new field)
  - game-synced flags
  - saved quest updates
  - saved conversations
  - loadouts (4 legendary power slots)
  - loadout items
  - trickle
- Attributes:
  - Narrow ints are packed the way the client reads them.
  - Two attributes changed type.
  - Eight attributes that were being shifted twice are sent as-is.
- Unknown messages get logged and the rest of their packet is dropped. Before, their data was read as garbage messages.
- tools/protocol has the scripts I used to compare the client's message layouts, attribute table and data formats against DiIiS. They should make the next patch easier.

### Performance

- Client messages were cancelling the running game update. The client sends messages nonstop while you move and fight, so most updates got skipped. Messages wait for the update now.
- Updates run every 50 ms instead of 100 ms, still at 60 ticks/s. That halves the delay the loop adds.
- The Windows timer resolution is set to 1 ms, and Nagle is off on game connections.
- Logging was a big chunk of the lag:
  - The console table redrew every row ever logged. It keeps the last 100 now.
  - The log file is written in batches.
  - Packet dumps only get built if something records them.
  - One attribute warning was logged thousands of times, each with a slow source lookup. It's logged once now.
- Monsters froze and the game lagged when you hit lots of them (Energy Twister on a pack, for example). On a third of the hits that didn't kill, a monster flinched, and the server stopped it for 0.3 seconds by tying up a worker thread in a busy loop. Your own actions are handled on those same threads. A second hit during that time saved the stopped speed and put it back afterwards, so monsters could stay stuck in place. The pause now runs on the game's own timers, and a second hit only extends it.
- Monsters no longer send a hit effect every time the server checks what's near you. Every monster within 200 yards played a hit effect (a sound and a flash) five times a second, whether or not anything hit it. In a rift that came to around 500 messages a second, more than all the real damage numbers.
- Teleport moves you right away. DiIiS waited 0.3 seconds first, on every cast. The power's only delay in the game data (0.4 seconds) is for the effects after you land. Wormhole's follow-up teleports still wait 0.3 seconds, as on live. Reversal's jump back keeps DiIiS's 0.3 seconds, since I don't know live's timing for it.
- Arcane Torrent ticks at live's rate: 3 times per attack (2 with Death Blossom, 1.5 with Arcane Mines), so faster attack speed means more, smaller hits. DiIiS ticked every 0.3 seconds whatever your attack speed. Damage per second is the same.
- The server knows where walking monsters are. It used to update a monster's position only at the end of each walk step, and skipped the update if the monster was flinching just then. Your client still walked the whole step. In a fight, monsters ended up a step or two (20 to 40 yards) away from where you saw them. So Arcane Torrent aimed at a monster landed somewhere else, sometimes off screen, and tornadoes and other area hits damaged monsters that weren't near them on your screen. The server now follows each step as it happens, the same way your client draws it.
- Energy Twister's damage follows the tornado. It used to stay where the tornado spawned.
- Tornadoes keep moving. Once the server tracked them, each new point they headed for was closer than the last, so they stood still for about 4 of their 6 seconds, mostly once they reached a pack. They go back to DiIiS's path (out to where you cast, then wandering around the spot you cast from), and pick their next point when they reach one instead of every half second.
- Every 30 seconds the server logs how long your client's data waited before it was handled (at Debug level, next to the update timings). Long waits there mean the server delayed your actions.

### Fixes from play testing

- Stuck on the loading screen. Item levels were read from the wrong offset, so vendors had nothing to stock and world gen crashed.
- Crash going back to the local map.
- The first waypoint click on the map did nothing.
- Projectiles hit only after they'd already passed the target.
- Ray of Frost aimed into the ground.
- Loot and projectiles ended up inside the ground in outdoor areas.
- No arc or sound on item drops.
- No animation or sound when identifying.
- "Select A Play Mode" popped up at every login. Account settings, options and hero settings are saved now.
- Tutorials showed with tutorials off, and seen ones came back.
- The Ruins of Corvus waypoint sometimes stopped working until you restarted the game. DiIiS's saved map tiles for Corvus (and Path to Corvus) include a scene that 2.8.0 doesn't have. When the random layout picked it, the map failed to build, and the half-built world stayed around for the rest of the session. Tiles whose scene isn't in your game data are skipped now. Two other maps had one such tile each (three Act II caves, Arreat Crater level 1), but they never ended up using it.
- Ruins of Sescheron and the Eternal Woods had no monsters. DiIiS has no spawn list for either, since they were added after DiIiS's data. Sescheron now spawns Ice Clan Warriors, Impalers and Shamans, Glacial Monstrosities and Colossi, Frost Maggots and Ice Porcupines. The Eternal Woods spawn Frozen Corpses, Shuffling Corpses, Decaying Remains, Glacial Monstrosities and Colossi and Ice Porcupines, plus its uniques Kashyyk, Lap'yin, Deadeye and Quillflurry. The Icy Pit inside the woods still has none.
- Exceptions and freezes:
  - entering some level areas
  - killing a bounty unique in some areas
  - breaking a container that was already destroyed
  - creating an item with no item definition

### Crafting

- Crafting multiple items made one no matter the count. Now it makes as many as you asked for and pulls materials from the currency tab, backpack and stash. It stops when something runs out or your bag is full.
- Crafting a single item works too. It had no handler before.
- Artisan workshops show your own artisan level instead of always looking like level 1. Training plays the right animation from level 10 up.
- Training an artisan to level 11 no longer needs an extra Death's Breath.

### Kadala

- Gambling works now. Nothing handled the request before.
- Blood shards you pick up count right away. Before, she only counted what you had when the world loaded.
- The phylactery is back in her stock.

### Kanai's Cube

Before this, only the cow portal worked. Everything else either kept your stuff or gave you something for free.

- Recipes come from the game's TransmuteRecipes table.
- These recipes work:
  - Reforge Legendary
  - Upgrade Rare
  - Convert Set Item
  - converting crafting materials
  - the cow level portal
- Rare helms, chests and belts can be upgraded. Their rares use a different item type than the legendaries.
- A failed transmute shows the real client error ("Missing core ingredients." etc.) and resets the cube. Before, it animated and did nothing.

### Loot

- Smart Loot: 85% of drops are for your class, with your main stat. Before, drops ignored class.
- Legendaries come from the game's own drop table (weights and class flags). DiIiS picked them by name, which caused two problems:
  - Firebird's, Raekor's, Marauder's, Inna's, Akkhan's, the Necro sets and items like Nilfur's Boast never dropped.
  - Crafted sets and old item versions did drop.

  The pool now matches maxroll's drop data except for one item, where the installed game is a patch newer.
- Ancestors' Grace is hardcore only, like on live. Season theme items don't drop, since there are no seasons.
- Six legendaries that DiIiS had flagged as bugged (no reason given) are back, because live drops them. I haven't tested these yet.
- Legendary affixes:
  - Fixed affixes are actually applied (Nilfur's has its Int now).
  - Primary and secondary counts are right.
  - Skill bonuses are for your class.
  - Tiers match the item level.
  - CDR and area damage roll again.
- Drops, Kadala and the cube all pull from the same pool.
- Legendary stats roll in live's ranges. They were a tier low before (350-415 Intelligence on boots instead of 416-500).
- Ancient items: 10% of level 70 legendaries and set items are ancient, with live's higher ranges (550-650 Intelligence on boots). Primal ancients aren't in yet.
- Cosmetic rewards with no stats (Conqueror's Shako and the rest of that season set, Helm of the Hierarch) don't drop or come from gambling anymore.
- Level 70 magic and rare items use level 70 bases only. Before, they could get level 60 to 65 bases with less armor or damage.
- Magic and rare items are always identified, and legendary and set items always drop unidentified, like on live. Before, 5% of magic and rare items came unidentified, and only 30% of legendaries did. An existing `config.mods.json` keeps its old values, so set `UnidentifiedDropChances` to 100 and 0 there.

### Legendary powers

- Wire the remaining equipped Wizard powers: Aquila Cuirass, Stone Gauntlets/Ice Climbers, Hergbrash's Binding, Ashnagarr's Blood Bracer, Stone of Jordan, Convention of Elements, Squirt's Necklace, and Leoric's Crown's helmet-gem multiplier. Add supported buff icons and meter labels; correct channel flat-cost splitting and Ward capacity display/absorption. The [implementation registry](docs/legendary-powers.md) tracks all fourteen audited item powers and Legacy of Dreams, including incomplete/live-unverified behavior.
- Add the current equipped Etched Sigil's free Energy Twisters and damage bonus. Casts follow channel ticks and the published attack-speed breakpoints, keep the cursor aim, and suppress secondary on-hit procs. The three channels use client formulas for timing and resource cost per tick. Twister damage radii come from game data, and Raging Storm merges one matching twister from the same player. Exact live parity still needs verification; see [implementation, evidence, and remaining work](docs/etched-sigil.md).
- Add the current equipped Twisted Sword's rolled Energy Twister damage bonus with the 2.7.7 three-stack cap, automatic Raging Storm, and up to five merged casts. Merged tornadoes retain the selected rune and count their contributing casts toward the sword bonus. Wicked Wind's merge damage follows published live measurements. See [evidence, regression checks, and remaining parity work](docs/twisted-sword.md).
- Add Mantle of Channeling for the three Wizard channels, with the item's damage roll, 25% damage reduction and no startup delay. Correct Twisted Sword/Galvanizing Ward icon slots, Flame Ward's element and reduction, Arcane Torrent's damage ramp, Deflection's stacking shields, Wormhole's single follow-up, and the outdated Astral Presence/Audacity values. See the [selected-rune audit, evidence and remaining Wizard work](docs/wizard-status.md); these changes do not establish complete live parity.
- Add current Valthek's Rebuke and Ranslor's Folly handlers for equipped and cubed powers: independent Twister damage bonuses, straight travel with the Wicked Wind exception, and periodic lesser-enemy pulls with per-target cooldowns. The combat meter names both multipliers. See [client evidence, regression checks and remaining timing/collision work](docs/twister-cube.md).

### Legendary gems

- Legacy of Dreams works. While you have no set bonus, each legendary or set item you wear adds 3.75% damage per rank + 1 (375% at rank 99) and 2% damage reduction. From rank 25, ancient items count double. The items add up: 13 ancients at rank 99 give 9750% more damage and 52% less damage taken. It boosts your pets' damage too. The other legendary gems still do nothing.
- Gem ranks are saved. A socketed gem used to go back to rank 1 after a relog or when you took it out, and an Urshi upgrade on a gem in your bags was lost on relog.
- Upgrading a gem socketed in equipped jewelry with Urshi works. It used to fail.
- Greater rift guardians can drop Legacy of Dreams. They only picked from the first 22 gems before.
- Taking the gems out of an equipped item removes their bonuses right away.

### GM commands

- `!materials [count]` adds every crafting material.
- `!shards [count]` adds blood shards.
- `!lootpool <type> [class] [level]` lists which legendaries an item type can roll for a class.
- `!unlockart [level]` now takes a level.
- `!item` rolls an item's random affixes for your class. Before, they rolled for no class: any main stat, and never your class's skill bonuses.
- `!item <name> [amount] ancient` spawns ancient legendaries. Without `ancient`, they aren't.
- `!item <gem> rank <rank>` spawns a legendary gem at that rank, for example `!item Unique_Gem_023_x1 rank 99` for Legacy of Dreams.
