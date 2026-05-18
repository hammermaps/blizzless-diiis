using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.Core.Types.TagMap;
using DiIiS_NA.GameServer.GSSystem.MapSystem;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.GSSystem.TickerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.ACD;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.World;
using System.Threading.Tasks;

namespace DiIiS_NA.GameServer.GSSystem.ActorSystem.Implementations
{
    [HandledSNO(ActorSno._p2_weeklychallenge_obelisk /* x1_OpenWorld_LootRunObelisk_B.acr */)]
    public sealed class ChallengeObelisk : Gizmo
    {
        public ChallengeObelisk(World world, ActorSno sno, TagMap tags)
            : base(world, sno, tags)
        {
            Attributes[GameAttributes.TeamID] = 2;
            Attributes[GameAttributes.MinimapActive] = true;
            Attributes.BroadcastChangedIfRevealed();
        }

        public override void OnTargeted(Player player, TargetMessage message)
        {
            bool activated = false;
            World.Game.IsChallengeRift = true;
            World.Game.NephalemGreater = true;
            World.Game.CurrentGreaterRiftLevel = 1;

            foreach (var plr in World.Game.Players.Values)
            {
                plr.Attributes[GameAttributes.In_Tiered_Challenge_Rift] = 1f;
                plr.Attributes[GameAttributes.Eligible_For_Weekly_Challenge_Reward] = 0f;
                plr.Attributes.BroadcastChangedIfRevealed();
            }

            PlayAnimation(5, (AnimationSno)AnimationSet.TagMapAnimDefault[AnimationSetKeys.Opening]);
            Attributes[GameAttributes.Team_Override] = (activated ? -1 : 2);
            Attributes[GameAttributes.Untargetable] = !activated;
            Attributes[GameAttributes.NPC_Is_Operatable] = activated;
            Attributes[GameAttributes.Operatable] = activated;
            Attributes[GameAttributes.Operatable_Story_Gizmo] = activated;
            Attributes[GameAttributes.Disabled] = !activated;
            Attributes[GameAttributes.Immunity] = !activated;
            Attributes.BroadcastChangedIfRevealed();
            CollFlags = 0;

            TickTimer timeout = new SecondsTickTimer(World.Game, 3.5f);
            var boom = Task<bool>.Factory.StartNew(() => WaitToSpawn(timeout));
            boom.ContinueWith(delegate
            {
                var actor = World.GetActorBySNO(ActorSno._x1_openworld_challenge_rifts_portal);
                if (actor != null)
                {
                    actor.SetVisible(true);
                    actor.Reveal(player);
                }

                World.BroadcastIfRevealed(plr => new ACDCollFlagsMessage()
                {
                    ActorID = DynamicID(plr),
                    CollFlags = 0
                }, this);
            });

        }

        public override bool Reveal(Player player)
        {
            if (!base.Reveal(player))
                return false;
            if (!Attributes[GameAttributes.Operatable])
            {
                var actor = World.GetActorBySNO(ActorSno._x1_openworld_challenge_rifts_portal);
                if (actor != null)
                {
                    actor.SetVisible(false);
                    actor.Unreveal(player);
                }
            }
            else
            {
                PlayAnimation(5, (AnimationSno)AnimationSet.TagMapAnimDefault[AnimationSetKeys.Opening]);
            }

            return true;
        }

        private bool WaitToSpawn(TickTimer timer)
        {
            while (timer.TimedOut != true)
            {

            }
            return true;
        }

    }
}
