# Game World Settings

The parameters of the world can be easily altered using the configuration file located within `config.ini`.

## Configuration

The parameters specified in the `config.ini` file will be saved to the server folder, overwriting the default settings. For example, all values below use their default settings.

```ini
[Game-Server]
; rates
RateExp = 1
RateMoney = 1
RateDrop = 1
RateChangeDrop = 1
RateMonsterHP = 1
RateMonsterDMG = 1
; items
ChanceHighQualityUnidentified = 30
ChanceNormalUnidentified = 5
; bosses
BossHealthMultiplier = 6
BossDamageMultiplier = 3
; monster / boss AI
MonsterThinkTick = 1
MonsterAttackDelaySeconds = 1
MonsterRetargetDelaySeconds = 2
MonsterSearchRange = 50
MonsterLeashRange = 120
MonsterRangedPreferredDistance = 14
BossEnrageHealthPercent = 25
BossEnrageAttackDelayMultiplier = 0.75
BossWeakTargetHealthPercent = 40
; nephalem
NephalemRiftProgressMultiplier = 1
NephalemRiftAutoFinish = false
NephalemRiftAutoFinishThreshold = 2
NephalemRiftOrbsChance = 0
; health
HealthPotionRestorePercentage = 60
HealthPotionCooldown = 30
ResurrectionCharges = 3
; waypoints
UnlockAllWaypoints = false
; player attribute modifier
StrengthMultiplier = 1
StrengthParagonMultiplier = 1
DexterityMultiplier = 1
DexterityParagonMultiplier = 1
IntelligenceMultiplier = 1
IntelligenceParagonMultiplier = 1
VitalityMultiplier = 1
VitalityParagonMultiplier = 1
; quests
AutoSaveQuests = false
; minimap
ForceMinimapVisibility = false
```

## Description

```json
{
  "Rate": {
    "Experience": 1.0, // Experience Rate
    "Money": 1.0, // money rate
    "Drop": 1.0, // drop rate
    "ChangeDrop": 1.0 // change drop rate
  },
  "Health": {
    "PotionRestorePercentage": 60.0, // how many in percent will a potion restore
    "PotionCooldown": 30.0, // how many seconds for a full potion recharge
    "ResurrectionCharges": 3 // how many times can you revive at corpse
  },
  "Monster": {
    "HealthMultiplier": 1.0, // monster health multiplier
    "DamageMultiplier": 1.0 // monster damage multiplier
  },
  "Boss": {
    "HealthMultiplier": 6.0, // boss health multiplier
    "DamageMultiplier": 3.0 // boss damage multiplier
  },
  "Quest": {
    "AutoSave": false, // auto save at every quest
    "UnlockAllWaypoints": false // unlocks all waypoints in-game
  },
  "Player": {
    "Multipliers": { // multipliers for the player (e.g. a paragon might need twice these values for fairer gameplay)
      "Strength": {
        "Normal": 1.0,
        "Paragon": 1.0
      },
      "Dexterity": {
        "Normal": 1.0,
        "Paragon": 1.0
      },
      "Intelligence": {
        "Normal": 1.0,
        "Paragon": 1.0
      },
      "Vitality": {
        "Normal": 1.0,
        "Paragon": 1.0
      }
    }
  },
  "Items": {
    "UnidentifiedDropChances": { // chances in % of a dropped item to be unidentified
      "HighQuality": 100.0, // legendary and set items; live: 100
      "NormalQuality": 0.0 // magic and rare items; live: 0
    }
  },
  "Minimap": {
    "ForceVisibility": false // forces in-game minimap to be always visible
  },
  "NephalemRift": { // improves overall nephalem rift experience
    "ProgressMultiplier": 1.0,
    "AutoFinish": false,
    "AutoFinishThreshold": 2,
    "OrbsChance": 2.0 // chances of spawning an orb
  }
}
```

### config.ini keys (legacy / AI tuning)

These flat `config.ini` keys are read by `GameServerConfig`; the JSON above is `config.mods.json` (`GameModsConfig`), generated from `config.ini` on first start.

| Key              | Description               |
| ---------------- | ------------------------- |
| `RateExp`        | Experience multiplier     |
| `RateMoney`      | Currency multiplier       |
| `RateDrop`       | Drop quantity multiplier  |
| `RateChangeDrop` | Drop quality multiplier   |
| `RateMonsterHP`  | Monsters HP multiplier    |
| `RateMonsterDMG` | Monster damage multiplier |
| `ChanceHighQualityUnidentified` | Percentage that a unique, legendary, set or special item created is unidentified |
| `ChanceNormalUnidentified` | Percentage that normal item created is unidentified |
| `ResurrectionCharges` | Amount of times user can resurrect at corpse |
| `BossHealthMultiplier` | Boss Health Multiplier |
| `BossDamageMultiplier` | Boss Damage Multiplier |
| `MonsterThinkTick` | Seconds between monster AI think passes. Lower values are more responsive but cost more CPU. |
| `MonsterAttackDelaySeconds` | Seconds between monster attack attempts. |
| `MonsterRetargetDelaySeconds` | Seconds between monster tactical target rescans. |
| `MonsterSearchRange` | Monster aggro / target search range in world units. |
| `MonsterLeashRange` | Non-boss distance from spawn before dropping target and returning home. |
| `MonsterRangedPreferredDistance` | Preferred spacing that ranged monsters try to keep from their target. |
| `BossEnrageHealthPercent` | Boss HP percent at which enrage attack timing applies. |
| `BossEnrageAttackDelayMultiplier` | Boss attack-delay multiplier below the enrage HP threshold. |
| `BossWeakTargetHealthPercent` | Player HP threshold that bosses prioritize as weak targets. |
| `HealthPotionRestorePercentage` | How much (from 1-100) a health potion will heal. |
| `HealthPotionCooldown` | How much (in seconds) to use a health potion again. |
| `UnlockAllWaypoints` | Unlocks all waypoints in campaign |
| `StrengthMultiplier` | Player's strength multiplier |
| `StrengthParagonMultiplier` | Player's strength multiplier **for paragons** |
| `DexterityMultiplier` | Player's dexterity multiplier |
| `DexterityParagonMultiplier` | Player's dexterity multiplier **for paragons** |
| `IntelligenceMultiplier` | Player's intelligence multiplier |
| `IntelligenceParagonMultiplier` | Player's intelligence multiplier **for paragons** |
| `VitalityMultiplier` | Player's vitality multiplier |
| `VitalityParagonMultiplier` | Player's vitality multiplier **for paragons** |
| `AutoSaveQuests` *in tests* | Force Save Quests/Step, even if Act's quest setup marked as Saveable = FALSE. Doesn't apply to OpenWorld games. |
| `NephalemRiftProgressMultiplier` | Nephalem Rift Progress Modifier |
| `NephalemRiftAutoFinish` | Nephalem Auto-Finish when there's still `NephalemRiftAutoFinishThreshold` monsters or less are alive on the rift |
| `NephalemRiftAutoFinishThreshold` | Nephalem Rift Progress Modifier |
| `NephalemRiftOrbsChance` | Nephalem Rifts chance of spawning a orb. |
| `ForceMinimapVisibility` | Forces the minimap visibility |
