using DiIiS_NA.GameServer.MessageSystem;

namespace DiIiS_NA.GameServer.GSSystem.PowerSystem.Implementations.General
{
	/// <summary>
	/// Season 28 Altar of Rites Potion Power temporary buff.
	/// Applied when the player drinks a health potion and the corresponding
	/// Potion Power bit is set in <c>Player.AltarPotionPowerMask</c>.
	/// Reverses its attribute changes on expiry so bonuses never stack.
	/// </summary>
	[ImplementsPowerBuff(10)] // slot 10 to avoid collision with other DrinkHealthPotion buffs
	public class Season28PotionPowerBuff : PowerBuff
	{
		private readonly int _potionPowerMask;
		private readonly float _durationSeconds;

		// Bonus amounts applied per active Power bit
		private const float Power1CritDmgBonus = 0.25f;
		private const float Power2ThornsBonus  = 0.30f;
		private const float Power3HpBonus      = 0.20f;

		public Season28PotionPowerBuff(int potionPowerMask)
		{
			_potionPowerMask = potionPowerMask;
			// Power 3 (HP burst) lasts 2 s; all others last 5 s.
			_durationSeconds = (potionPowerMask & 4) != 0 ? 2f : 5f;
		}

		public override void Init()
		{
			base.Init();
			Timeout = WaitSeconds(_durationSeconds);
		}

		public override bool Apply()
		{
			if (!base.Apply()) return false;

			if ((_potionPowerMask & 1) != 0) // Power 1: +25% crit damage
				Target.Attributes[GameAttributes.Crit_Damage_Percent] += Power1CritDmgBonus;

			if ((_potionPowerMask & 2) != 0) // Power 2: +30% thorns
				Target.Attributes[GameAttributes.Thorns_Percent_All] += Power2ThornsBonus;

			if ((_potionPowerMask & 4) != 0) // Power 3: +20% max HP (survivability burst)
				Target.Attributes[GameAttributes.Hitpoints_Max_Percent_Bonus] += Power3HpBonus;

			Target.Attributes.BroadcastChangedIfRevealed();
			return true;
		}

		public override void Remove()
		{
			base.Remove();

			if ((_potionPowerMask & 1) != 0)
				Target.Attributes[GameAttributes.Crit_Damage_Percent] -= Power1CritDmgBonus;

			if ((_potionPowerMask & 2) != 0)
				Target.Attributes[GameAttributes.Thorns_Percent_All] -= Power2ThornsBonus;

			if ((_potionPowerMask & 4) != 0)
				Target.Attributes[GameAttributes.Hitpoints_Max_Percent_Bonus] -= Power3HpBonus;

			Target.Attributes.BroadcastChangedIfRevealed();
		}

		/// <summary>
		/// Do not stack with itself; extend the existing buff's timer instead
		/// (TimedBuff.Stack behaviour).
		/// </summary>
		public override bool Stack(Buff buff) => base.Stack(buff);
	}
}
