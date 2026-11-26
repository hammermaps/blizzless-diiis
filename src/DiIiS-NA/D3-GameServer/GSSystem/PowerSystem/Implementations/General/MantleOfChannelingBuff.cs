using System;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.GameServer.GSSystem.SkillsSystem;
using DiIiS_NA.GameServer.MessageSystem;

namespace DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations
{
	// P4_ItemPassive_Unique_Ring_039: SF0 = 0.25 in the installed client.
	// Patch 2.6.10 removed the startup delay. The power defines no standalone buff icon.
	public sealed class MantleOfChannelingBuff : Buff
	{
		public const int ItemPowerSNO = 446640;
		private readonly ChanneledSkill _channel;
		private MantleOfChannelingBuff(ChanneledSkill channel) => _channel = channel;

		public float DamageBonus => !Removed && _channel.IsChannelActive && _channel.User is Player player
			? player.GetLegendaryPowerValue(ItemPowerSNO) : 0f;
		public float DamageReduction => DamageBonus > 0f ? 0.25f : 0f;

		// Blizzard lists the item's channels as Siphon Blood, Whirlwind, Rapid Fire, Strafe,
		// Tempest Rush, Firebats, Arcane Torrent, Disintegrate, and Ray of Frost.
		private static readonly int[] ChannelPowers =
		{
			Skills.Wizard.Offensive.ArcaneTorrent, Skills.Wizard.Offensive.Disintegrate,
			Skills.Wizard.Offensive.RayOfFrost,
			Skills.Barbarian.FurySpenders.Whirlwind,
			Skills.DemonHunter.HatredGenerators.Strafe, Skills.DemonHunter.HatredSpenders.RapidFire,
			Skills.Monk.SpiritSpenders.TempestRush,
			Skills.WitchDoctor.PhysicalRealm.Firebats,
			Skills.Necromancer.MainSkills.SiphonBlood
		};

		public static void Activate(ChanneledSkill channel)
		{
			if (Array.IndexOf(ChannelPowers, channel.PowerSNO) < 0) return;

			var buffs = channel.World.BuffManager;
			var existing = buffs.GetFirstBuff<MantleOfChannelingBuff>(channel.User);
			if (existing != null)
			{
				if (existing._channel == channel && existing.DamageBonus > 0f) return;
				buffs.RemoveBuff(channel.User, existing);
			}
			var buff = new MantleOfChannelingBuff(channel);
			if (buff.DamageBonus > 0f) buffs.AddBuff(channel.User, channel.User, buff);
		}

		public override bool Update() => DamageBonus <= 0f;
	}
}
