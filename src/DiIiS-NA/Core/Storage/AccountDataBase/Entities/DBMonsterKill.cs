using System;
using FluentNHibernate.Data;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Entities
{
	/// <summary>
	/// Kills of one monster type (SNO) for one character. Kept as its own table because a hero
	/// accumulates hundreds of distinct monster types; one row per (toon, monster) pair.
	/// </summary>
	public class DBMonsterKill : Entity
	{
		public new virtual ulong Id { get; protected set; }
		public virtual DBToon DBToon { get; set; }
		public virtual int MonsterSno { get; set; }
		public virtual string MonsterName { get; set; }
		public virtual int Kills { get; set; }
		public virtual DateTime Updated { get; set; }
	}
}
