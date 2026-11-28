using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Extensions;
using DiIiS_NA.Core.Helpers.Math;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.MPQ;
using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.Core.Types.Math;
using DiIiS_NA.GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.Core.Types.TagMap;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Actions;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Implementations;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Implementations.Hirelings;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Movement;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem;
using DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations;
using DiIiS_NA.GameServer.GSSystem.TickerSystem;
using DiIiS_NA.GameServer.MessageSystem;

namespace DiIiS_NA.GameServer.GSSystem.AISystem.Brains
{
    /// <summary>
    /// AI controller for every combat-capable monster (normal, champion,
    /// rare and boss). Drives aggro acquisition, target selection, power
    /// usage and pathfinding.
    ///
    /// <para>Decision flow executed every <see cref="Think(int)"/>:</para>
    /// <list type="number">
    ///   <item><description><see cref="ShouldSkipThink"/> — special-case
    ///     opt-outs (Siegebreaker, garden pillars, hidden/dead bodies,
    ///     paused games).</description></item>
    ///   <item><description><see cref="IsCrowdControlled"/> — bail on stun,
    ///     freeze, blind, web, knockback or the summoning intro buff.</description></item>
    ///   <item><description><see cref="HandleFearEffect(int)"/> — flee for
    ///     the duration of any <c>Feared</c> flag.</description></item>
    ///   <item><description>Throttle: only run the expensive combat block
    ///     every <c>60 * GameServerConfig.MonsterThinkTick</c> ticks.</description></item>
    ///   <item><description><see cref="EvaluateTargetsAndAct(int)"/> — retarget
    ///     on a 2-second cadence and attack on a 1-second cadence.</description></item>
    /// </list>
    ///
    /// <para>See <c>docs/Battle.md</c> §3.2 for a full walkthrough and the
    /// balance knobs in <see cref="DEFAULT_SEARCH_RANGE"/>,
    /// <see cref="POWER_DELAY_SECONDS"/>, etc.</para>
    /// </summary>
    public class MonsterBrain : Brain
    {
        /// <summary>Named logger; hides the base <c>Brain.Logger</c>.</summary>
        private new readonly Logger Logger;

        /// <summary>
        /// All powers this monster can use, keyed by power SNO, each with
        /// its own individual <see cref="Cooldown"/> tracker. Populated from
        /// the monster's MPQ <c>SkillDeclarations</c> in the constructor.
        /// A synthetic melee entry (<see cref="MELEE_ATTACK_SNO"/>) is added
        /// if no melee is declared, so every monster can always swing.
        /// </summary>
        public Dictionary<int, Cooldown> PresetPowers { get; private set; }

        /// <summary>
        /// Per-power use range declared in the monster MPQ record
        /// (<c>MonsterSkillDeclarations[i].UseRangeMin/UseRangeMax</c>).
        /// Only powers with <c>UseRangeMax &gt; 0</c> are stored; the rest fall back
        /// to the power's own <c>AttackRadius</c>.
        /// </summary>
        private readonly Dictionary<int, (float Min, float Max)> _powerUseRanges = new();

        /// <summary>Global throttle between attack attempts (1 second).</summary>
        private TickTimer _powerDelay;

        /// <summary>Throttle between target re-selection passes (2 seconds).</summary>
        private TickTimer _targetUpdateDelay;

        /// <summary>Last tick seen by <see cref="Think(int)"/>, used by the stuck detection.</summary>
        private int _lastTick;

        /// <summary>Target currently being chased, to measure whether the chase makes progress.</summary>
        private Actor _chaseTarget;

        /// <summary>Smallest distance reached while chasing <see cref="_chaseTarget"/>.</summary>
        private float _chaseBestDistance = float.MaxValue;

        /// <summary>Tick at which <see cref="_chaseBestDistance"/> last improved.</summary>
        private int _chaseBestTick;

        /// <summary>Target that could not be reached; ignored until <see cref="_stuckUntilTick"/>.</summary>
        private Actor _stuckTarget;

        /// <summary>Tick until which <see cref="_stuckTarget"/> stays ignored.</summary>
        private int _stuckUntilTick;

        /// <summary>Throttle for the pack aggro broadcast (2 seconds).</summary>
        private int _lastPackCallTick;

        /// <summary>Next tick at which an idle monster may wander a few steps.</summary>
        private int _nextPatrolTick;

        /// <summary>Position and tick of the last movement watchdog sample.</summary>
        private Vector3D _watchPosition;
        private int _watchTick;


        /// <summary>
        /// Per-power cooldown tracker. <see cref="CooldownTimer"/> being
        /// <c>null</c> means "off-cooldown right now".
        /// </summary>
        public struct Cooldown
        {
            /// <summary>Active timer; <c>null</c> when the power is ready.</summary>
            public TickTimer CooldownTimer;

            /// <summary>Base cooldown duration in seconds (pre-CDR).</summary>
            public float CooldownTime;
        }

        // ─────────────────────────── AI tuning constants ──────────────────
        // These are the primary balance knobs for monster AI responsiveness.
        // See docs/Battle.md §5.6 for guidance on tuning them.

        /// <summary>Fallback aggro / target search radius in world units.</summary>
        private const float DEFAULT_SEARCH_RANGE = 50f;

        /// <summary>Power SNO of the basic monster melee attack.</summary>
        private const int MELEE_ATTACK_SNO = 30592;

        /// <summary>Power SNO of the Firewall special (shares a cooldown bucket).</summary>
        private const int FIREWALL_SNO = 223284;

        /// <summary>Reserved — unused, retained for future firewall handling.</summary>
        private const int FIREWALL_ACTOR_SNO = -1; // ActorSno._a1dun_leor_firewall2 - use actual value

        /// <summary>Melee attack range padding added on top of the body cylinder.</summary>
        private const float BASE_MELEE_RANGE = 10f;

        /// <summary>Absolute upper bound for monster attack range.</summary>
        private const float MAX_ATTACK_RANGE = 35f;

        /// <summary>Minimum distance to flee when feared.</summary>
        private const float FEARED_RETREAT_MIN = 3f;

        /// <summary>Maximum distance to flee when feared.</summary>
        private const float FEARED_RETREAT_MAX = 8f;

        /// <summary>Fallback seconds between attack attempts.</summary>
        private const float POWER_DELAY_SECONDS = 1.0f;

        /// <summary>Fallback seconds between target re-selection passes.</summary>
        private const float TARGET_UPDATE_DELAY_SECONDS = 2.0f;

        /// <summary>Fallback distance at which non-boss monsters give up pursuit.</summary>
        private const float DEFAULT_LEASH_RANGE = 120f;

        /// <summary>Fallback preferred spacing for ranged monsters.</summary>
        private const float DEFAULT_RANGED_PREFERRED_DISTANCE = 14f;

        /// <summary>Score delta treated as a tie when randomly picking among best powers.</summary>
        private const float POWER_SCORE_TIE_TOLERANCE = 0.01f;

        /// <summary>Cooldown applied to boss summon skills after each cast.</summary>
        private const float SUMMONING_COOLDOWN_BOSS = 15f;

        /// <summary>Cooldown applied to non-boss summon skills after each cast.</summary>
        private const float SUMMONING_COOLDOWN_NORMAL = 7f;

        /// <summary>Cooldown applied to "special" powers (firewall, 96925).</summary>
        private const float SPECIAL_POWER_COOLDOWN = 10f;

        // ──────────────────────── Per-brain state ────────────────────────

        /// <summary>Set once after logging a "no usable powers" warning, to avoid log spam.</summary>
        private bool _warnedNoPowers;

        /// <summary>Cached current target chosen by <see cref="UpdateTarget"/>.</summary>
        private Actor _target;

        /// <summary>Count of powers declared in MPQ (for the "no usable powers" warning).</summary>
        private int _mpqPowerCount;

        /// <summary>Sticky flag: true while we are actively running from a fear effect.</summary>
        private bool _feared;

        /// <summary>Last boss health phase that was applied by the lightweight boss profile.</summary>
        private int _bossHealthPhase = 100;

        /// <summary>
        /// Last actor that attacked this monster. Used as priority target so
        /// the monster retaliates against whoever is currently hurting it.
        /// Written by <c>AttackPayload.Apply</c> when a player hits a monster.
        /// </summary>
        public Actor AttackedBy;

        /// <summary>
        /// Expiration timer on <see cref="AttackedBy"/>. Reserved for future
        /// use — currently the retaliation persists until the target dies.
        /// </summary>
        public TickTimer TimeoutAttacked;

        /// <summary>
        /// Scripted priority target — takes precedence over both
        /// <see cref="AttackedBy"/> and nearest-enemy selection.
        /// Set from boss phase scripts to lock a boss onto a specific player.
        /// </summary>
        public Actor PriorityTarget;

        /// <summary>
        /// Creates a new monster brain, loading the body's declared skills
        /// from the monster MPQ file and building the initial
        /// <see cref="PresetPowers"/> dictionary.
        /// </summary>
        /// <param name="body">The monster actor this brain will drive.</param>
        public MonsterBrain(Actor body)
            : base(body)
        {
            Logger = LogManager.CreateLogger(GetType().Name);
            PresetPowers = new Dictionary<int, Cooldown>();

            // Actors without a monster SNO (pillars, dummies, visuals) have
            // nothing to do — leave PresetPowers empty.
            if (body.ActorData.MonsterSNO <= 0)
            {
                Logger.Warn($"$[red]${GetType().Name}$[/]$ - $[red]$Monster $[white bold underline]$\"{body.SNO}\"$[/]$$ has no monster SNO$[/]$");
                return;
            }

            // Load the monster's MPQ record to enumerate its declared skills.
            var monsterData = (DiIiS_NA.Core.MPQ.FileFormats.Monster)MPQStorage.Data.Assets[SNOGroup.Monster][body.ActorData.MonsterSNO].Data;
            _mpqPowerCount = monsterData.SkillDeclarations.Count(e => e.SNOPower != -1);

            // Walk every declared skill in parallel with its MonsterSkillDeclarations
            // entry (which carries the Timer / cooldown). Only skills that we
            // have an actual C# implementation for end up in PresetPowers.
            for (int i = 0; i < monsterData.SkillDeclarations.Length; i++)
            {
                if (monsterData.SkillDeclarations[i].SNOPower == -1) continue;
                if (PowerLoader.HasImplementationForPowerSNO(monsterData.SkillDeclarations[i].SNOPower))
                {
                    // MPQ stores cooldown as tenths of seconds.
                    var cooldownTime = monsterData.MonsterSkillDeclarations[i].Timer / 10f;
                    PresetPowers.Add(monsterData.SkillDeclarations[i].SNOPower, new Cooldown { CooldownTimer = null, CooldownTime = cooldownTime });

                    // The same entry carries the distance band in which the monster is
                    // supposed to use the skill. Without it every skill fires at the
                    // MAX_ATTACK_RANGE fallback, which is why casters opened up from
                    // across the room.
                    var useRangeMax = monsterData.MonsterSkillDeclarations[i].UseRangeMax;
                    if (useRangeMax > 0f)
                    {
                        var useRangeMin = Math.Max(0f, monsterData.MonsterSkillDeclarations[i].UseRangeMin);
                        _powerUseRanges[monsterData.SkillDeclarations[i].SNOPower] = (useRangeMin, useRangeMax);
                    }
                }
            }

            // Guarantee every monster has *some* basic attack — if no melee
            // was declared, inject the canonical melee SNO with zero cooldown.
            if (!monsterData.SkillDeclarations.Any(s => s.SNOPower == MELEE_ATTACK_SNO))
                PresetPowers.Add(MELEE_ATTACK_SNO, new Cooldown { CooldownTimer = null, CooldownTime = 0f });
        }

        /// <summary>
        /// Main AI tick. Runs the decision flow documented on the class.
        /// </summary>
        /// <param name="tickCounter">Current game tick.</param>
        public override void Think(int tickCounter)
        {
            _lastTick = tickCounter;

            // Step 1: hard opt-outs (special actors, hidden, dead, paused).
            if (ShouldSkipThink())
                return;

            // Step 2: CC gate — stunned/frozen/blind/webbed/knocked/summoning.
            if (IsCrowdControlled())
                return;

            // Step 3: fear handling — run away for the duration of the fear.
            if (HandleFearEffect(tickCounter))
                return;

            _feared = false;

            // Step 4: throttle. GameServerConfig.MonsterThinkTick is in
            // seconds; 1.0 → think once per second, 0.5 → twice per second.
            // This is the primary lever for monster AI responsiveness.
            // Stagger by the actor id: with a plain "tick % interval" every monster in the world
            // computed in the same tick, which produced the 50-100 ms spikes in the update log.
            if ((tickCounter + Body.GlobalID) % GetThinkIntervalTicks() != 0)
                return;

            // Step 5: a movement action that cannot make progress must not block the brain.
            // MoveToPointWithPathfindAction only ends on its own after reaching the heading, so a
            // monster whose route is blocked kept the action forever and never evaluated targets
            // again - the rest of a pack stood still after the first kill.
            AbortStalledMovement(tickCounter);

            // Step 6: main combat logic only runs if we are not already mid-action.
            if (CurrentAction == null)
            {
                EvaluateTargetsAndAct(tickCounter);
            }
        }

        /// <summary>
        /// Hard opt-outs that must skip every <see cref="Think(int)"/>
        /// pass. Covers scripted uber bosses, non-thinking decorative
        /// actors (pillars, portals), NPCs, hidden or dead bodies, paused
        /// games and the global <c>Disabled</c> flag.
        /// </summary>
        /// <returns><c>true</c> if this brain should not think this tick.</returns>
        private bool ShouldSkipThink()
        {
            // Hard-coded special actors whose AI is externally scripted
            // (boss phase state machines, etc.) and must not run the default
            // monster brain logic.
            switch (Body.SNO)
            {
                case ActorSno._uber_siegebreakerdemon:
                case ActorSno._a4dun_garden_corruption_monster:
                case ActorSno._a4dun_garden_hellportal_pillar:
                case ActorSno._belialvoiceover:
                    return true;
            }

            // Skip NPCs (shopkeepers / quest givers), hidden / invisible
            // actors, dead bodies, paused games, and anything the game has
            // explicitly flagged Disabled.
            return Body is NPC || Body.Hidden || !Body.Visible || Body.Dead ||
                   Body.World.Game.Paused || Body.Attributes[GameAttributes.Disabled];
        }

        /// <summary>
        /// Returns true if the monster is under any form of crowd control
        /// that should cancel its current action and prevent it from acting
        /// this tick. Also cancels <see cref="Brain.CurrentAction"/> when CC
        /// is detected so interrupted attacks do not resume when the CC ends.
        /// </summary>
        private bool IsCrowdControlled()
        {
            bool isCrowdControlled = Body.Attributes[GameAttributes.Frozen] ||
                                     Body.Attributes[GameAttributes.Stunned] ||
                                     Body.Attributes[GameAttributes.Blind] ||
                                     Body.Attributes[GameAttributes.Webbed] ||
                                     Body.Disable ||
                                     // KnockbackBuff and SummonedBuff are
                                     // implemented as regular buffs — the
                                     // summoning buff is what holds newly
                                     // summoned mobs still during their intro.
                                     Body.World.BuffManager.GetFirstBuff<KnockbackBuff>(Body) != null ||
                                     Body.World.BuffManager.GetFirstBuff<SummonedBuff>(Body) != null;

            if (isCrowdControlled)
            {
                CancelCurrentAction();
                _powerDelay = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Runs the monster in a random direction while the <c>Feared</c>
        /// attribute is set. Re-rolls the flee target on the edge (i.e. when
        /// the fear first starts or when the previous flee finishes), but
        /// does not interrupt an already-running flee.
        /// </summary>
        /// <returns><c>true</c> if fear is active and this tick should stop processing.</returns>
        private bool HandleFearEffect(int tickCounter)
        {
            if (!Body.IsFeared)
                return false;

            // Start a new flee only on rising edge (we weren't feared last
            // tick) or if the previous flee action completed / was cancelled.
            if (!_feared || CurrentAction == null)
            {
                if (!_feared)
                    Logger.Trace("{0} began fleeing (feared)", Body.SNO);
                CancelCurrentAction();
                _feared = true;
                CurrentAction = new MoveToPointWithPathfindAction(
                    Body,
                    // Random point between FEARED_RETREAT_MIN and
                    // FEARED_RETREAT_MAX tiles away, in any direction.
                    PowerContext.RandomDirection(Body.Position, FEARED_RETREAT_MIN, FEARED_RETREAT_MAX)
                );
            }

            return true;
        }

        /// <summary>
        /// Throttled retarget + attack step. Called from <see cref="Think(int)"/>
        /// at most once per <see cref="POWER_DELAY_SECONDS"/> after the
        /// overall throttle has already decided we can think this tick.
        /// </summary>
        private void EvaluateTargetsAndAct(int tickCounter)
        {
            // Lazy-init the timers the first time this monster actually gets
            // to act (zero-arg ctor would run during monster load and waste
            // allocations for mobs the player never gets near).
            if (ShouldReturnToLeash())
            {
                BeginLeashReturn();
                return;
            }

            UpdateBossProfile();

            _powerDelay ??= new SecondsTickTimer(Body.World.Game, GetAttackDelaySeconds());
            _targetUpdateDelay ??= new SecondsTickTimer(Body.World.Game, GetRetargetDelaySeconds());

            // Refresh the target every TARGET_UPDATE_DELAY_SECONDS. Cheaper
            // than rescanning every tick and also gives the player a chance
            // to kite — no instant re-aggro.
            if (_targetUpdateDelay.TimedOut)
            {
                _targetUpdateDelay = new SecondsTickTimer(Body.World.Game, GetRetargetDelaySeconds());
                UpdateTarget();
            }

            // Gate attacks on POWER_DELAY_SECONDS. This is the "rhythm" of
            // monster attacks — everyone attacks at ~1 Hz unless a specific
            // power has its own per-skill cooldown.
            if (_powerDelay.TimedOut)
            {
                _powerDelay = new SecondsTickTimer(Body.World.Game, GetAttackDelaySeconds());

                if (_target != null && !_target.Dead)
                {
                    ExecuteAttackOnTarget(tickCounter);
                }
                else if (Body.Position != Body.CheckPointPosition)
                {
                    // No target in range → drift back to spawn so packs
                    // don't slowly migrate across the level.
                    CurrentAction = new MoveToPointWithPathfindAction(Body, Body.CheckPointPosition);
                }
                else if (tickCounter >= _nextPatrolTick)
                {
                    // Idle and already at the checkpoint: take a few steps around it so a
                    // resting pack does not look like a row of statues. Guarded by the leash
                    // range, so the wander can never walk the monster out of its area.
                    _nextPatrolTick = tickCounter + 300 + FastRandom.Instance.Next(300);
                    var patrolPoint = PickPatrolPoint();
                    if (patrolPoint != null)
                        CurrentAction = new MoveToPointWithPathfindAction(Body, patrolPoint);
                }
            }
        }

        /// <summary>
        /// Selects the next target in priority order:
        /// <list type="number">
        ///   <item><description>Scripted <see cref="PriorityTarget"/>.</description></item>
        ///   <item><description>Most recent <see cref="AttackedBy"/>.</description></item>
        ///   <item><description>Nearest valid combat target (see
        ///     <see cref="IsValidCombatTarget(Actor)"/>).</description></item>
        /// </list>
        /// </summary>
        private void UpdateTarget()
        {
            var prevTarget = _target;

            // Scripted priority — boss phase logic uses this to lock onto a
            // specific player regardless of positioning.
            if (PriorityTarget != null && IsValidCombatTarget(PriorityTarget))
            {
                _target = PriorityTarget;
                if (prevTarget != _target)
                    Logger.Trace("{0} UpdateTarget → priority target {1}", Body.SNO, _target.SNO);
                return;
            }

            // Retaliation priority — attack whoever hit us last. This is how
            // ranged mobs remember to keep shooting at the caster instead of
            // wandering off to the nearest player.
            if (AttackedBy != null && IsValidCombatTarget(AttackedBy))
            {
                _target = AttackedBy;
                if (prevTarget != _target)
                    Logger.Trace("{0} UpdateTarget → retaliation target {1}", Body.SNO, _target.SNO);
                return;
            }

            // Fallback: highest scored valid actor.
            _target = FindValidTargets();
            if (prevTarget != _target && _target != null)
                Logger.Trace("{0} UpdateTarget → nearest target {1}", Body.SNO, _target.SNO);
            else if (prevTarget != null && _target == null)
                Logger.Trace("{0} lost target (prev: {1})", Body.SNO, prevTarget.SNO);
        }

        /// <summary>
        /// Picks the best combat target within <see cref="DEFAULT_SEARCH_RANGE"/> in one
        /// pass. Only the winner was ever used, so the sort-into-a-new-list is replaced by
        /// a running comparison: that drops a list, two LINQ iterators and a full sort per
        /// think tick. Respects the <c>Team_Override</c> flag, which flips the monster onto
        /// the player's side (betrayal scenarios) — betrayed monsters then search for
        /// other monsters instead of players.
        /// </summary>
        private Actor FindValidTargets()
        {
            Actor best = null;
            float searchRange = GetSearchRange();

            if (Body.Attributes[GameAttributes.Team_Override] == 1)
            {
                // Team override: mind-controlled monsters attack their
                // former allies, nearest first.
                var bestDistance = float.MaxValue;
                foreach (var monster in Body.GetObjectsInRange<Monster>(searchRange))
                {
                    if (monster.Dead) continue;

                    var distance = PowerMath.Distance2D(monster.Position, Body.Position);
                    if (distance >= bestDistance) continue;

                    bestDistance = distance;
                    best = monster;
                }

                return best;
            }

            // Normal targeting: any actor in range that passes IsValidCombatTarget,
            // highest tactical score wins.
            var bestScore = float.MinValue;
            foreach (var actor in Body.GetActorsInRange(searchRange))
            {
                if (!IsValidCombatTarget(actor)) continue;

                var score = GetTargetScore(actor);
                if (score <= bestScore) continue;

                bestScore = score;
                best = actor;
            }

            return best;
        }

        /// <summary>
        /// Predicate for "is this actor a legal target for this monster to
        /// attack right now". Rejects dead / hidden / ghosted actors and
        /// non-combat helper minions; accepts players, fighting minions,
        /// hirelings and (specifically) door/barricade destructibles.
        /// </summary>
        private bool IsValidCombatTarget(Actor actor)
        {
            if (actor.Dead || actor == Body || actor.Hidden)
                return false;

            // A target we could not reach recently is skipped until the blacklist expires,
            // otherwise the monster keeps walking into the same wall forever.
            if (actor == _stuckTarget)
            {
                if (_lastTick < _stuckUntilTick)
                    return false;

                _stuckTarget = null;
            }

            // Players: must be fully loaded, not a helper actor, and not
            // ghosted (e.g. corpse walker invulnerability).
            if (actor is Player player)
            {
                return !player.Attributes[GameAttributes.Loading] &&
                       !player.Attributes[GameAttributes.Is_Helper] &&
                       player.World.BuffManager.GetFirstBuff<ActorGhostedBuff>(player) == null;
            }

            // Summoned minions: attack combat minions, ignore utility helpers.
            if (actor is Minion minion)
            {
                return !minion.Attributes[GameAttributes.Is_Helper];
            }

            // Hirelings (Templar, Scoundrel, Enchantress, Leah) are always
            // legal targets.
            if (actor is Hireling)
            {
                return true;
            }

            // Only doors and barricades among destructibles — we don't want
            // mobs to randomly smash lootable vases.
            if (actor is DesctructibleLootContainer destructible)
            {
                return destructible.SNO.IsDoorOrBarricade();
            }

            return false;
        }

        /// <summary>
        /// Picks a power, computes range, then either casts in place or
        /// pursues the target until in range.
        /// </summary>
        private void ExecuteAttackOnTarget(int tickCounter)
        {
            float targetDistance = PowerMath.Distance2D(_target.Position, Body.Position);
            int powerToUse = PickPowerToUse(targetDistance);
            if (powerToUse <= 0)
                return;

            PowerScript power = PowerLoader.CreateImplementationForPowerSNO(powerToUse);
            power.User = Body;

            float attackRange = CalculateAttackRange(power, powerToUse);
            if (ShouldRepositionForRangedPower(power, powerToUse, targetDistance))
            {
                CurrentAction = new MoveToPointWithPathfindAction(Body, GetRetreatPointFromTarget());
                return;
            }

            if (IsTargetInRange(targetDistance, attackRange))
            {
                // Ranged skills need a clear path; melee does not (the target is
                // adjacent anyway). Without this check monsters shot through walls.
                if (powerToUse != MELEE_ATTACK_SNO && !HasLineOfSight(_target))
                {
                    if (CanApproachTarget())
                        ApproachTarget(powerToUse, attackRange);
                    return;
                }

                ExecutePowerAttack(powerToUse, power);
            }
            else if (CanApproachTarget())
            {
                TrackChaseProgress(tickCounter, targetDistance);
                ApproachTarget(powerToUse, attackRange);
            }
            // Else: immobile mob out of range → no action this tick; the
            // next tick's target update may find a closer target.
        }

        /// <summary>
        /// Computes the effective attack range for a given power:
        /// body cylinder + power <c>AttackRadius</c>, with a floor for
        /// melee and a ceiling of <see cref="MAX_ATTACK_RANGE"/>.
        /// </summary>
        private float CalculateAttackRange(PowerScript power, int powerSNO)
        {
            float baseRange = Body.ActorData.Cylinder.Ax2; // body radius

            // Prefer the range the monster data declares for this skill: that is the
            // distance at which the animation and the projectile were authored to work.
            if (_powerUseRanges.TryGetValue(powerSNO, out var useRange))
                return baseRange + Math.Min(useRange.Max, MAX_ATTACK_RANGE);

            float powerRange = power.EvalTag(PowerKeys.AttackRadius);

            if (powerRange > 0f)
            {
                // Melee gets a flat 10-tile padding so monsters actually
                // commit to their swing instead of orbiting their target.
                if (powerSNO == MELEE_ATTACK_SNO)
                    return baseRange + BASE_MELEE_RANGE;

                return baseRange + Math.Min(powerRange, MAX_ATTACK_RANGE);
            }

            // Power declared no attack radius → assume long-range.
            return baseRange + MAX_ATTACK_RANGE;
        }

        /// <summary>
        /// Returns <c>true</c> if the target is within the computed attack
        /// range, accounting for the target's own body cylinder (so big
        /// targets like bosses are hit from the edge, not the centre).
        /// </summary>
        private bool IsTargetInRange(float targetDistance, float attackRange)
        {
            return targetDistance < attackRange + _target.ActorData.Cylinder.Ax2;
        }

        /// <summary>
        /// Picks a walkable point a few steps away from the spawn point for idle wandering.
        /// Returns <c>null</c> when no candidate is reachable/walkable, so the caller simply
        /// stays put instead of pathing into a wall.
        /// </summary>
        private Vector3D PickPatrolPoint()
        {
            var origin = Body.CheckPointPosition;
            if (origin == null || Body.WalkSpeed == 0)
                return null;

            var leash = GetLeashRange();
            const int attempts = 8;

            for (var i = 0; i < attempts; i++)
            {
                var angle = FastRandom.Instance.NextDouble() * Math.PI * 2;
                var radius = 4.0 + FastRandom.Instance.NextDouble() * 4.0;
                var candidate = new Vector3D(
                    origin.X + (float)(Math.Cos(angle) * radius),
                    origin.Y + (float)(Math.Sin(angle) * radius),
                    origin.Z);

                // Stay inside the leash so patrol cannot become a free pass out of the area.
                if (PowerMath.Distance2D(candidate, origin) > leash)
                    continue;

                if (Body.World.CheckLocationForFlags(candidate,
                        DiIiS_NA.Core.MPQ.FileFormats.Scene.NavCellFlags.AllowWalk, 0))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Drops a <see cref="MoveToPointWithPathfindAction"/> that has not moved the body for three
        /// seconds. The action retries a blocked route once per second and only finishes when it
        /// reaches its heading, so without this watchdog the brain never gets to act again.
        /// The action's own callback clears the action as soon as <see cref="Brain.CurrentAction"/>
        /// is null, so assigning null here is enough to end it.
        /// </summary>
        private void AbortStalledMovement(int tickCounter)
        {
            if (CurrentAction is not MoveToPointWithPathfindAction)
            {
                _watchPosition = null;
                _watchTick = tickCounter;
                return;
            }

            var position = Body.Position;

            if (_watchPosition == null)
            {
                _watchPosition = position;
                _watchTick = tickCounter;
                return;
            }

            var moved = Math.Abs(position.X - _watchPosition.X) + Math.Abs(position.Y - _watchPosition.Y);
            if (moved > 0.5f)
            {
                _watchPosition = position;
                _watchTick = tickCounter;
                return;
            }

            if (tickCounter - _watchTick <= 180)
                return;

            Logger.Debug("{0} aborts a movement action that made no progress for 3 s", Body.SNO);
            CurrentAction = null;
            _watchPosition = null;
            _watchTick = tickCounter;
        }

        /// <summary>
        /// Gives up on a chase that does not close the distance: after three seconds without
        /// getting at least one unit closer, the target is blacklisted for ten seconds and the
        /// next think pass picks another one. Also broadcasts aggro to the pack when the chase
        /// target changes, throttled to once every two seconds.
        /// </summary>
        private void TrackChaseProgress(int tickCounter, float targetDistance)
        {
            if (_chaseTarget != _target)
            {
                _chaseTarget = _target;
                _chaseBestDistance = targetDistance;
                _chaseBestTick = tickCounter;

                BroadcastAggroToPack(tickCounter);
                return;
            }

            if (targetDistance < _chaseBestDistance - 1f)
            {
                _chaseBestDistance = targetDistance;
                _chaseBestTick = tickCounter;
                return;
            }

            if (tickCounter - _chaseBestTick <= 180)
                return;

            Logger.Trace("{0} gives up on unreachable target {1}", Body.SNO, _target.SNO);
            _stuckTarget = _target;
            _stuckUntilTick = tickCounter + 600;
            _chaseTarget = null;
            _chaseBestDistance = float.MaxValue;
            _target = null;
        }

        /// <summary>
        /// Wakes the rest of the pack: nearby monsters of the same SNO adopt the same target,
        /// which is how a group reacts together instead of one member at a time.
        /// </summary>
        private void BroadcastAggroToPack(int tickCounter)
        {
            if (_target == null || tickCounter - _lastPackCallTick < 120)
                return;

            _lastPackCallTick = tickCounter;

            foreach (var monster in Body.GetObjectsInRange<Monster>(15f))
            {
                if (monster == Body || monster.Dead || monster.SNO != Body.SNO)
                    continue;

                if (monster.Brain is MonsterBrain brain && brain._target == null)
                    brain.AttackedBy = _target;
            }
        }

        /// <summary>
        /// Walks the straight line to the target and rejects it if any sampled point is not
        /// walkable, i.e. a wall sits between the two. Sampled at 3 world units with a short
        /// grace distance, because nav cells at the monster's own feet are often flagged
        /// blocked by its collision cylinder.
        /// </summary>
        private bool HasLineOfSight(Actor target)
        {
            var from = Body.Position;
            var to = target.Position;
            var distance = PowerMath.Distance2D(from, to);

            if (distance <= 8f)
                return true;

            const float step = 3f;
            var samples = (int)(distance / step);
            for (var i = 1; i < samples; i++)
            {
                var t = i / (float)samples;
                var point = new Vector3D(
                    from.X + (to.X - from.X) * t,
                    from.Y + (to.Y - from.Y) * t,
                    from.Z + (to.Z - from.Z) * t);

                if (!Body.World.CheckLocationForFlags(point,
                        DiIiS_NA.Core.MPQ.FileFormats.Scene.NavCellFlags.AllowWalk, 0))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Immobile monsters (e.g. stationary casters, siege weapons) have
        /// <c>WalkSpeed == 0</c>; they cannot pursue.
        /// </summary>
        private bool CanApproachTarget()
        {
            return Body.WalkSpeed != 0;
        }

        /// <summary>
        /// Actually fires the attack: face the target, queue a
        /// <see cref="PowerAction"/>, and apply the per-power cooldown.
        /// </summary>
        private void ExecutePowerAttack(int powerSNO, PowerScript power)
        {
            // Only rotate if we can actually turn (pillars / columns have
            // WalkSpeed 0 and face a fixed direction).
            if (Body.WalkSpeed != 0)
                Body.TranslateFacing(_target.Position, false);

            CurrentAction = new PowerAction(Body, powerSNO, _target);
            ApplyPowerCooldown(powerSNO, power);

            // Debug, not Trace: this fires for every attack of every monster and drowned the log
            // during a single pack fight.
            Logger.Debug($"{GetType().Name} {nameof(PowerAction)} on {_target.ActorType} at {_target.Position}");
        }

        /// <summary>
        /// Moves the body toward the target. Woodwraith/wasp variants use
        /// a non-pathfinding <see cref="MoveToPointAction"/> to preserve
        /// their swooping / flight behaviour — they are not bound to
        /// navmesh cells.
        /// </summary>
        private void ApproachTarget(int powerSNO, float attackRange)
        {
            if (Body.SNO.IsWoodwraithOrWasp())
            {
                // Flying mobs — straight-line move, skip pathfinding.
                CurrentAction = new MoveToPointAction(Body, _target.Position);
                Logger.Trace($"{GetType().Name} approaching target (ranged) at {_target.Position}");
            }
            else
            {
                // Ground mobs — proper navmesh pathfinding that stops at
                // the edge of attack range, carrying the intended powerSNO
                // so the action can transition straight into a cast when
                // it arrives.
                CurrentAction = new MoveToTargetWithPathfindAction(
                    Body,
                    _target,
                    attackRange + _target.ActorData.Cylinder.Ax2,
                    powerSNO
                );
                Logger.Debug($"{GetType().Name} approaching target with pathfinding");
            }
        }

        /// <summary>
        /// Applies a cooldown to the just-cast power. Respects
        /// <c>GameServerConfig.DisableMonsterPowerCooldowns</c> as a global
        /// kill switch and picks the correct duration based on power type.
        /// </summary>
        /// <param name="powerSNO">Power that was cast.</param>
        /// <param name="power">Resolved <see cref="PowerScript"/> for the power.</param>
        /// <param name="cooldownTime">Optional override; <c>0</c> means "use per-category default".</param>
        private void ApplyPowerCooldown(int powerSNO, PowerScript power, float cooldownTime = 0f)
        {
            // Global kill-switch for stress tests — never ship with this on.
            if (GameServerConfig.Instance.DisableMonsterPowerCooldowns)
                return;

            // Category-based cooldowns when the MPQ didn't set one.
            if (power is SummoningSkill)
            {
                cooldownTime = Body is Boss ? SUMMONING_COOLDOWN_BOSS : SUMMONING_COOLDOWN_NORMAL;
            }
            else if (power is MonsterAffixSkill monsterAffixSkill)
            {
                cooldownTime = monsterAffixSkill.CooldownTime;
            }
            else if (IsSpecialPowerSNO(powerSNO))
            {
                cooldownTime = SPECIAL_POWER_COOLDOWN;
            }
            else if (PresetPowers.TryGetValue(powerSNO, out var presetCooldown))
            {
                cooldownTime = presetCooldown.CooldownTime;
            }

            // Only overwrite the tracker if this power actually uses a cooldown.
            if (cooldownTime > 0f)
            {
                PresetPowers[powerSNO] = new Cooldown
                {
                    CooldownTimer = new SecondsTickTimer(Body.World.Game, cooldownTime),
                    CooldownTime = cooldownTime
                };
            }
        }

        /// <summary>
        /// Special-case powers that need a fixed global cooldown regardless
        /// of their MPQ data. Currently: <c>96925</c> and the Firewall
        /// effect (<see cref="FIREWALL_SNO"/>).
        /// </summary>
        private bool IsSpecialPowerSNO(int powerSNO)
        {
            return powerSNO == 96925 || powerSNO == FIREWALL_SNO;
        }

        /// <summary>Cancels and clears <see cref="Brain.CurrentAction"/> if one is running.</summary>
        private void CancelCurrentAction()
        {
            if (CurrentAction != null)
            {
                CurrentAction.Cancel(0);
                CurrentAction = null;
            }
        }

        /// <summary>
        /// Chooses a power to cast from <see cref="PresetPowers"/>. Filters
        /// to powers that are off-cooldown AND actually have a C# script
        /// implementation, then applies a 50/50 bias between "prefer
        /// non-melee" and "use melee" — which produces the familiar D3
        /// monster rhythm of mostly-melee-sometimes-ranged.
        /// </summary>
        /// <returns>Power SNO to cast, or <c>-1</c> if no power is available.</returns>
        protected virtual int PickPowerToUse(float targetDistance)
        {
            // One-time warning for monsters that were defined in MPQ but
            // have no implemented powers. Not repeated each tick because it
            // would spam the log for every misconfigured mob in the world.
            if (!_warnedNoPowers && PresetPowers.Count == 0)
            {
                Logger.Warn($"Monster $[red]$\"{Body.Name}\"$[/]$ has no usable powers. {_mpqPowerCount} are defined in mpq data.");
                _warnedNoPowers = true;
                return -1;
            }

            if (PresetPowers.Count <= 0)
                return -1;

            // One pass instead of three LINQ chains plus one anonymous type per power.
            // Ties within POWER_SCORE_TIE_TOLERANCE are resolved by reservoir sampling,
            // which keeps the "random among equally good powers" behaviour without
            // materialising a candidate list.
            var rng = FastRandom.Instance;
            var availableCount = 0;
            var anyAvailable = -1;
            var outOfBand = -1;
            var meleeAvailable = false;
            var best = -1;
            var bestScore = 0f;
            var tieCount = 0;

            foreach (var preset in PresetPowers)
            {
                // Cooldown must be expired and the power needs a C# implementation.
                if (preset.Value.CooldownTimer != null && !preset.Value.CooldownTimer.TimedOut)
                    continue;

                if (!PowerLoader.HasImplementationForPowerSNO(preset.Key))
                    continue;

                availableCount++;

                if (preset.Key == MELEE_ATTACK_SNO)
                    meleeAvailable = true;

                // Uniform random fallback candidate (reservoir sampling, size 1).
                if (rng.Next(availableCount) == 0)
                    anyAvailable = preset.Key;

                // Outside the declared use band the skill is not an option - this keeps casters
                // from firing point blank or from 50 yards. It is remembered as a last resort:
                // returning -1 here would make the monster stand still instead of closing in,
                // because ExecuteAttackOnTarget bails out before it ever moves.
                if (preset.Key != MELEE_ATTACK_SNO &&
                    _powerUseRanges.TryGetValue(preset.Key, out var band) &&
                    (targetDistance > band.Max || (band.Min > 0f && targetDistance < band.Min)))
                {
                    if (outOfBand < 0)
                        outOfBand = preset.Key;
                    continue;
                }

                var score = GetPowerScore(preset.Key, targetDistance);
                if (score <= 0f)
                    continue;

                if (best < 0)
                {
                    // First scored power sets the bar; starting from 0 would treat every
                    // near-zero score as a tie and could return a worse power.
                    best = preset.Key;
                    bestScore = score;
                    tieCount = 1;
                }
                else if (score > bestScore + POWER_SCORE_TIE_TOLERANCE)
                {
                    best = preset.Key;
                    bestScore = score;
                    tieCount = 1;
                }
                else if (Math.Abs(score - bestScore) < POWER_SCORE_TIE_TOLERANCE)
                {
                    tieCount++;
                    if (rng.Next(tieCount) == 0)
                        best = preset.Key;
                }
            }

            if (best > 0)
                return best;

            if (meleeAvailable)
                return MELEE_ATTACK_SNO;

            if (anyAvailable > 0)
                return anyAvailable;

            // Only out-of-band skills left: return one anyway so the caller computes a range and
            // walks towards the target instead of freezing in place.
            return outOfBand;
        }

        private int GetThinkIntervalTicks()
        {
            return Math.Max(1, (int)(60f * Math.Max(0.1f, GameServerConfig.Instance.MonsterThinkTick)));
        }

        private float GetAttackDelaySeconds()
        {
            float delay = Math.Max(0.1f, GameServerConfig.Instance.MonsterAttackDelaySeconds);
            if (Body is Boss && GetHealthPercent(Body) <= GameServerConfig.Instance.BossEnrageHealthPercent)
                delay *= Math.Max(0.1f, GameServerConfig.Instance.BossEnrageAttackDelayMultiplier);

            return delay;
        }

        private float GetRetargetDelaySeconds()
        {
            return Math.Max(0.25f, GameServerConfig.Instance.MonsterRetargetDelaySeconds);
        }

        private float GetSearchRange()
        {
            var configured = GameServerConfig.Instance.MonsterSearchRange;
            return configured > 0f ? configured : DEFAULT_SEARCH_RANGE;
        }

        private float GetLeashRange()
        {
            var configured = GameServerConfig.Instance.MonsterLeashRange;
            return configured > 0f ? configured : DEFAULT_LEASH_RANGE;
        }

        private float GetRangedPreferredDistance()
        {
            var configured = GameServerConfig.Instance.MonsterRangedPreferredDistance;
            return configured > 0f ? configured : DEFAULT_RANGED_PREFERRED_DISTANCE;
        }

        private bool ShouldReturnToLeash()
        {
            return Body is Monster and not Boss &&
                   Body.CheckPointPosition != null &&
                   PowerMath.Distance2D(Body.Position, Body.CheckPointPosition) > GetLeashRange();
        }

        private void BeginLeashReturn()
        {
            if (Body is not (Monster and not Boss))
                return;

            _target = null;
            AttackedBy = null;
            PriorityTarget = null;
            _powerDelay = null;
            _targetUpdateDelay = null;
            CurrentAction = new MoveToPointWithPathfindAction(Body, Body.CheckPointPosition);
        }

        private void UpdateBossProfile()
        {
            if (Body is not Boss)
                return;

            var healthPercent = GetHealthPercent(Body);
            var phase = healthPercent <= 25f ? 25 : healthPercent <= 50f ? 50 : healthPercent <= 75f ? 75 : 100;
            if (phase >= _bossHealthPhase)
                return;

            _bossHealthPhase = phase;
            _powerDelay = null;
            _targetUpdateDelay = null;

            var weakTarget = FindWeakestPlayerTarget();
            if (weakTarget != null)
                PriorityTarget = weakTarget;

            Logger.Trace("{0} boss AI phase changed to {1}% health", Body.SNO, phase);
        }

        private Actor FindWeakestPlayerTarget()
        {
            return Body.GetActorsInRange(GetSearchRange())
                .Where(IsValidCombatTarget)
                .OfType<Player>()
                .OrderBy(GetHealthPercent)
                .FirstOrDefault();
        }

        private float GetTargetScore(Actor actor)
        {
            var distance = PowerMath.Distance2D(actor.Position, Body.Position);
            var score = Math.Max(0f, GetSearchRange() - distance);

            if (actor == AttackedBy)
                score += 100f;

            if (actor is Player player)
            {
                score += 60f;
                var healthPercent = GetHealthPercent(player);
                score += Math.Max(0f, 100f - healthPercent) * 0.75f;

                if (Body is Boss && healthPercent <= GameServerConfig.Instance.BossWeakTargetHealthPercent)
                    score += 50f;
            }
            else if (actor is Hireling)
            {
                score += 30f;
            }
            else if (actor is Minion)
            {
                score += 20f;
            }
            else if (actor is DesctructibleLootContainer)
            {
                score *= 0.25f;
            }

            return score;
        }

        private float GetPowerScore(int powerSNO, float targetDistance)
        {
            if (powerSNO == MELEE_ATTACK_SNO)
            {
                var meleeReach = Body.ActorData.Cylinder.Ax2 + BASE_MELEE_RANGE + (_target?.ActorData.Cylinder.Ax2 ?? 0f);
                return targetDistance <= meleeReach ? 70f : 15f;
            }

            var power = PowerLoader.CreateImplementationForPowerSNO(powerSNO);
            power.User = Body;
            var attackRange = CalculateAttackRange(power, powerSNO) + (_target?.ActorData.Cylinder.Ax2 ?? 0f);
            var score = 40f;

            if (targetDistance <= attackRange)
                score += 60f;
            else if (targetDistance <= attackRange + 10f)
                score += 20f;

            if (power is SummoningSkill)
                score += Body is Boss ? 35f : 15f;

            if (Body is Boss)
                score += 20f;

            if (targetDistance < GetRangedPreferredDistance())
                score += 10f;

            return score;
        }

        private bool ShouldRepositionForRangedPower(PowerScript power, int powerSNO, float targetDistance)
        {
            return Body is not Boss &&
                   Body.WalkSpeed > 0f &&
                   powerSNO != MELEE_ATTACK_SNO &&
                   CalculateAttackRange(power, powerSNO) > BASE_MELEE_RANGE &&
                   targetDistance < GetRangedPreferredDistance();
        }

        private Vector3D GetRetreatPointFromTarget()
        {
            var dx = Body.Position.X - _target.Position.X;
            var dy = Body.Position.Y - _target.Position.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length < 0.01f)
                return RandomPossibleDirection(Body.Position, FEARED_RETREAT_MIN, FEARED_RETREAT_MAX, Body.World);

            var retreatDistance = Math.Min(FEARED_RETREAT_MAX, GetRangedPreferredDistance());
            return new Vector3D(
                Body.Position.X + (float)(dx / length) * retreatDistance,
                Body.Position.Y + (float)(dy / length) * retreatDistance,
                Body.Position.Z
            );
        }

        private static float GetHealthPercent(Actor actor)
        {
            var max = actor.Attributes[GameAttributes.Hitpoints_Max_Total];
            if (max <= 0f)
                max = actor.Attributes[GameAttributes.Hitpoints_Max];

            return max <= 0f ? 100f : (actor.Attributes[GameAttributes.Hitpoints_Cur] / max) * 100f;
        }

        /// <summary>
        /// Adds a new power at runtime (e.g. when an elite affix grants an
        /// extra ability mid-fight). Assigns a reasonable placeholder
        /// cooldown — 5 s if the monster already has melee, or a random
        /// 1–2 s if not (so we don't generate a free-cast loop).
        /// </summary>
        public void AddPresetPower(int powerSNO)
        {
            if (PresetPowers.ContainsKey(powerSNO))
            {
                Logger.Debug($"Monster $[red]$\"{Body.Name}\"$[/]$ already has power {powerSNO}.");
                return;
            }

            float cooldownTime = PresetPowers.ContainsKey(MELEE_ATTACK_SNO) ? 5f : 1f + (float)FastRandom.Instance.NextDouble();
            PresetPowers.Add(powerSNO, new Cooldown { CooldownTimer = null, CooldownTime = cooldownTime });
        }

        /// <summary>
        /// Removes a power from this monster's repertoire. No-op if the
        /// power was not present.
        /// </summary>
        public void RemovePresetPower(int powerSNO)
        {
            if (PresetPowers.ContainsKey(powerSNO))
            {
                PresetPowers.Remove(powerSNO);
            }
        }

        /// <summary>
        /// Utility: picks a random navmesh-walkable point in an annulus
        /// around <paramref name="position"/>. Used by several skill
        /// implementations that need to place effects near an actor.
        /// Retries up to 100 times before giving up and returning the last
        /// point (which may be on an unwalkable cell — callers should
        /// re-validate if that matters).
        /// </summary>
        public static Core.Types.Math.Vector3D RandomPossibleDirection(Core.Types.Math.Vector3D position, float minRadius, float maxRadius, MapSystem.World world)
        {
            float angle = (float)(FastRandom.Instance.NextDouble() * Math.PI * 2);
            float radius = minRadius + (float)FastRandom.Instance.NextDouble() * (maxRadius - minRadius);
            Core.Types.Math.Vector3D point = null;
            int attemptCount = 0;

            // Reject-sample until we find a walkable point or time out.
            while (attemptCount < 100)
            {
                point = new Core.Types.Math.Vector3D(
                    position.X + (float)Math.Cos(angle) * radius,
                    position.Y + (float)Math.Sin(angle) * radius,
                    position.Z
                );

                if (world.CheckLocationForFlag(point, DiIiS_NA.Core.MPQ.FileFormats.Scene.NavCellFlags.AllowWalk))
                    break;

                attemptCount++;
            }

            return point;
        }

        /// <summary>
        /// Bypasses normal targeting/delay logic and immediately casts the
        /// given skill on the given target. Used by boss phase scripts and
        /// other "scripted move" situations where the monster must react
        /// right now instead of waiting for its next think tick.
        /// </summary>
        public void FastAttack(Actor target, int skillSNO)
        {
            PowerScript power = PowerLoader.CreateImplementationForPowerSNO(skillSNO);
            power.User = Body;

            if (Body.WalkSpeed != 0)
                Body.TranslateFacing(target.Position, false);

            CurrentAction = new PowerAction(Body, skillSNO, target);
            ApplyPowerCooldown(skillSNO, power);

            //Logger.Trace($"{GetType().Name} {nameof(FastAttack)} on {target.ActorType}");
        }
    }
}
