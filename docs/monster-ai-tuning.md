# Monster AI tuning

All knobs live in the `[Game-Server]` section of `config.ini` and are read through
`GameServerConfig` at runtime, so tuning a fight never needs a rebuild — edit the file, restart
the server, fight again. Missing keys fall back to the default in the table below, so you only
have to write the ones you actually change.

## Combat rhythm

| Key | Default | Effect |
| --- | --- | --- |
| `MonsterThinkTick` | `1` | Seconds between AI decisions. `0.5` = twice per second (twitchier, more CPU), `2` = once per two seconds (cheap, sluggish). Think phases are staggered per actor id, so a low value does not bunch all monsters into the same tick. |
| `MonsterAttackDelaySeconds` | `1` | Minimum time between two attack attempts of one monster, independent of per-power cooldowns. |
| `MonsterRetargetDelaySeconds` | `2` | How often the target is re-chosen. Raising it gives the player more room to kite. |
| `DisableMonsterPowerCooldowns` | `false` | `true` lets monsters ignore their per-skill cooldown timers — useful to reproduce a power that never fires. |

## Reach

| Key | Default | Effect |
| --- | --- | --- |
| `MonsterSearchRange` | `50` | Aggro / target search radius in world units. |
| `MonsterLeashRange` | `120` | Distance from the spawn point at which a monster gives up and walks back. Also caps the idle patrol wander. |
| `MonsterRangedPreferredDistance` | `14` | Distance a ranged monster tries to keep (backs off below it). |
| `SweepMonstersTiles` | `40` | Tile radius used when the spawn generator looks for walkable ground for a pack. |

Per-skill ranges are **not** configured here: they come from the monster record itself
(`MonsterSkillDeclarations[i].UseRangeMin/UseRangeMax`). A power is only chosen while the target
is inside its declared band, and `CalculateAttackRange` uses the declared maximum instead of the
former blanket `MAX_ATTACK_RANGE` (35 units).

## Boss behaviour

| Key | Default | Effect |
| --- | --- | --- |
| `BossEnrageHealthPercent` | `25` | Health percentage below which a boss counts as enraged. |
| `BossEnrageAttackDelayMultiplier` | `0.75` | Attack delay multiplier while enraged (`0.75` = 25 % faster). |
| `BossWeakTargetHealthPercent` | `40` | Health percentage below which a boss prefers a weakened target. |
| `BossHealthMultiplier` / `BossDamageMultiplier` | `3` | Global boss stat scaling. |

## Counter-testing a change

1. Edit the key in `config.ini`, restart the server (the values are cached per process).
2. Fight the same pack again; compare `Updates in the last 30s: … average … ms, slowest … ms,
   … over the 50 ms budget` in the log. Anything above zero over-budget lines means the change
   costs more than the 50 ms tick budget allows.
3. For behaviour regressions (`monsters do not shoot`, `they walk into walls`) raise
   `MonsterThinkTick` to `0.5` first: that separates "AI is too slow" from "AI decided wrong".
   The relevant trace lines are emitted at `Trace` level by `MonsterBrain` (`gives up on
   unreachable target`, `UpdateTarget → …`).
