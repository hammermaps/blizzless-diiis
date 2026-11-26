using DiIiS_NA.Core.Logging;
using DiIiS_NA.D3_GameServer.Core.Types.SNO;
using DiIiS_NA.GameServer.Core.Types.TagMap;
using DiIiS_NA.GameServer.GSSystem.ActorSystem;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Interactions;
using DiIiS_NA.GameServer.GSSystem.ItemsSystem;
using DiIiS_NA.GameServer.GSSystem.MapSystem;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Misc;

namespace DiIiS_NA.D3_GameServer.GSSystem.ActorSystem.Implementations.Artisans
{
	/// <summary>
	/// Altar of Rites NPC – Season 28 (Patch 2.7.5).
	/// Allows players to unlock Seals and Potion Powers by sacrificing items/gold.
	/// NOTE: The placeholder SNO <see cref="ActorSno._p75_altar_of_rites_npc"/> must be
	///       updated to the real actor SNO once a compatible client build is available.
	/// </summary>
	[HandledSNO(ActorSno._p75_altar_of_rites_npc)]
	public class AltarOfRites : InteractiveNPC
	{
		private static readonly Logger Logger = LogManager.CreateLogger(nameof(AltarOfRites));

		/// <summary>Spawn coordinates for the Altar of Rites NPC in New Tristram.</summary>
		public const float SpawnX = 80f;
		public const float SpawnY = 92f;
		public const float SpawnZ = 0.1f;

		public AltarOfRites(World world, ActorSno sno, TagMap tags)
			: base(world, sno, tags)
		{
			// Without at least one interaction OnTargeted returns early and OnCraft is never called.
			Interactions.Add(new CraftInteraction());
		}

		/// <summary>
		/// Opens the Altar of Rites UI panel on interaction.
		/// The ANNDataMessage with <c>OpenArtisanWindowMessage</c> reuses the artisan
		/// window opcode because a dedicated Altar-UI opcode does not exist in 2.7.4.
		/// </summary>
		public override void OnCraft(Player player)
		{
			Logger.Trace("AltarOfRites opened by {0}.", player?.Toon?.Name);
			// Send the artisan-window message so the client renders a functional panel.
			player.InGameClient.SendMessage(
				new ANNDataMessage(Opcodes.OpenArtisanWindowMessage) { ActorID = DynamicID(player) });
			player.CurrentArtisan = ArtisanType.AltarOfRites;
		}

		/// <summary>
		/// Shortcut overload: unlock seal by 0-based index coming from client command/packet.
		/// </summary>
		public void TryUnlockSeal(Player player, int sealIndex)
		{
			if (Season28Patch.TryUnlockSeal(player, sealIndex))
			{
				Logger.Info("Player {0} unlocked Altar Seal {1}.", player.Toon.Name, sealIndex + 1);
			}
		}

		/// <summary>
		/// Shortcut overload: unlock potion power by 0-based index.
		/// </summary>
		public void TryUnlockPotionPower(Player player, int powerIndex)
		{
			if (Season28Patch.TryUnlockPotionPower(player, powerIndex))
			{
				Logger.Info("Player {0} unlocked Altar Potion Power {1}.", player.Toon.Name, powerIndex + 1);
			}
		}

		public override bool Reveal(Player player)
		{
			// The Altar is only available in Season mode.
			if (!World.Game.IsSeasoned)
				return false;

			return base.Reveal(player);
		}
	}
}
