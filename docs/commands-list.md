# Server Commands List

## Account  Commands

| Command  Group | Command        | Example                             | Description                                    |
| -------------- | -------------- | ----------------------------------- | ---------------------------------------------- |
| Account Group  | `show`         | `!account show test@`               | Shows information about given account          |
|                | `add`          | `!account add test@ 123456 test`    | Allows you to add a new user account           |
|                | `setpassword`  | `!account setpassword test@ 123654` | Allows you to set a new password for account   |
|                | `setbtag`      | `!account setbtag test@ NonTest`    | Allows you to change battle tag for account    |
|                | `setuserlevel` | `!account setuserlevel admin`       | Allows you to set a new user level for account |
| Mute Command   | `mute`         | `!mute test@`                       | Disable chat functions for user                |

## Game  Commands

| Command  Group          | Command     | Example                  | Description                                                    |
| ----------------------- | ----------- | ------------------------ | ---------------------------------------------------------------|
| Spawn command           | `spawn`     | `!spawn 6632`            | Spawn a mob by ID                                              |
| Level up command        | `levelup`   | `!levelup 2`             | Levels you character                                           |
| Unlock Artisans command | `unlockart` | `!unlockart`             | Unlock all artisans for you in Campaign                        |
| Platinum command        | `platinum`  | `!platinum 100`          | Platinum for you                                               |
| Gold command            | `gold`      | `!gold 100`              | Gold for you?                                                  |
| Materials command       | `materials` | `!materials 500`         | Adds that many of every crafting material (default 100)        |
| Blood shards command    | `shards`    | `!shards 500`            | Adds blood shards to gamble with Kadala (default 100)          |
| Loot pool command       | `lootpool`  | `!lootpool Boots`        | Lists the legendaries an item type can roll for your class     |
| Item command            | `item`      | `!item P61_Unique_Boots_01 2 ancient` | Spawns an item by its internal name (up to 100), with its random affixes for your class; `ancient` makes level 70 legendaries ancient. items-identification-doc.md lists some names, though many are old versions |
|                         |             | `!item Unique_Gem_023_x1 rank 99` | `rank` sets a legendary gem's rank (Unique_Gem_001_x1 to Unique_Gem_023_x1; 023 is Legacy of Dreams) |
| Teleport command        | `tp`        | `!tp 71150`              | Teleport character to World by ID                              |
| SpeedHack command       | `speed`     | `!speed 2`               | Increase you speed character                                   |
| Lookup Command          | `lookup`    | `!lookup item axe`       | Display all founded in game objects with entered text in Name  |
|                         |             | `!lookup world Tristram` |                                                                |
|                         |             | `!lookup actor zombie`   |                                                                |
|                         |             | `!lookup power Punch`    |                                                                |

# All Commands
Available commands from `!commands`: 

### !account
Provides account management commands.

### !mute
Disables chat messages for the account for some defined time span.

### !tag
Switch private Tag for connect

### !powerful
Makes your character with absurd amount of damage. Useful for testing.

### !resourceful
Makes your character with full resource. Useful for testing.

### !info
Get current game information.

### !followers
Manage your followers.

### !difficulty
Changes difficulty of the game

### !heal
Heals yourself

### !invulnerable
Makes you invulnerable

### !spawn
Spawns a mob.

Usage: spawn [actorSNO] [amount]

### !levelup
Levels your character.

Optionally specify the number of levels: !levelup [count]

### !unlockart
Unlock all artisans: !unlockart

Optionally set the Blacksmith, Jeweler and Mystic to a level too, up to 12, skipping their training: !unlockart [level]

### !platinum
Platinum for your character.

Optionally specify the number of levels: !platinum [count]

### !stashup
Upgrade Stash.

### !gold
Gold for your character.

Optionally specify the number of gold: !gold [count]

### !materials
Adds every crafting material in the currency tab: Reusable Parts, Arcane Dust, Veiled Crystal, Death's Breath,
Forgotten Souls, the act bounty materials and the Infernal Machine organs.

Optionally specify the number of each (default 100): !materials [count]

### !shards
Blood shards for your character, to gamble with Kadala.

Optionally specify the number of shards (default 100): !shards [count]

### !lootpool
Lists the legendaries an item type can roll for your class and level: what drops, Kadala and Kanai's Cube choose
from. The server log has the full list. From the server console, name the class (and optionally the level, 70 if not
given).

Usage: !lootpool <item type> [class] [level], for example !lootpool Boots, !lootpool ChestArmor or !lootpool Boots Wizard 70

### !achiplatinum
Platinum for your character.

Optionally specify the number of levels: !platinum [count]

### !eff
Platinum for your character.

Optionally specify the number of levels: !eff [count]

### !item
Spawns an item (with a name or type), rolled for your class.

Usage: item [type <type>|<name>] [amount] [ancient] [rank <rank>]

### !drop
Drops an epic item for your class.

Optionally specify the number of items: !drop [1-20]

### !tp
Transfers your character to another world.

### !conversation
Starts a conversation.

 Usage: conversation snoConversation

### !speed
Modify speed walk of you character.

Usage: !speed <value>

Reset: !speed

### !commands
Lists available commands for your user-level.

### !help
usage: help <command>

Type 'commands' to get a list of available commands.

### !quest
Retrieves information about quest states and manipulates quest progress.

 Usage: quest [triggers | trigger eventType eventValue | advance snoQuest]

### !lookup
Searches in sno databases.

Usage: lookup [actor|conv|power|scene|la|sp|weather] <pattern>

# Item List

You can use the official website to search items: https://eu.diablo3.blizzard.com/en-us/item/

```c
// Sample: Firebird's Breast
// Url: https://eu.diablo3.blizzard.com/en-us/item/firebirds-breast-Unique_Chest_Set_06_x1
// Name: Unique_Chest_Set_06_x1
```

You can also access the elements created for testing during game development :)
