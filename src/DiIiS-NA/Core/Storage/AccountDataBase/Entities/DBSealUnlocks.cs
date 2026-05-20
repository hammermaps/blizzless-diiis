using FluentNHibernate.Data;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Entities
{
	/// <summary>
	/// Persists Altar of Rites Seal and Potion-Power unlock state per game-account per season.
	/// SealMask is a 26-bit bitmask (bit 0 = Seal 1 … bit 25 = Seal 26).
	/// PotionPowerMask is a 3-bit bitmask (bit 0 = Power 1 … bit 2 = Power 3).
	/// </summary>
	public class DBSealUnlocks : Entity
	{
		public new virtual ulong Id { get; protected set; }
		public virtual DBGameAccount DBGameAccount { get; set; }
		public virtual int Season { get; set; }
		public virtual long SealMask { get; set; }
		public virtual int PotionPowerMask { get; set; }
	}
}
