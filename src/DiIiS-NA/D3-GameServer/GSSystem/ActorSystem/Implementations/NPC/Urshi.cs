using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.Core.Types.TagMap;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Text;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.World;

namespace DiIiS_NA.GameServer.GSSystem.ActorSystem.Implementations
{
	[HandledSNO(ActorSno._p1_lr_tieredrift_nephalem)]
	class Urshi : InteractiveNPC
	{
		public Urshi(MapSystem.World world, ActorSno sno, TagMap tags)
			: base(world, sno, tags)
		{
			Field7 = 1;
			Attributes[GameAttributes.MinimapActive] = true;
		}

		public override void OnTargeted(Player player, TargetMessage message)
		{
			base.OnTargeted(player, message);
			player.InGameClient.SendMessage(new DisplayGameTextMessage(Opcodes.DisplayGameTextMessage)
			{
				Message = "Messages:TieredRift_JewelUpgrade"
			});
		}
	}
}
