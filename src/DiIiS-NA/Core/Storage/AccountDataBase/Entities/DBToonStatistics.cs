using System;
using FluentNHibernate.Data;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Entities
{
	/// <summary>
	/// Aggregated lifetime statistics of a single character. Everything the game tracks per hero
	/// (gold, playtime, kills split by monster class) is persisted here so it survives sessions and
	/// can be queried from the database.
	/// </summary>
	public class DBToonStatistics : Entity
	{
		public new virtual ulong Id { get; protected set; }
		public virtual DBToon DBToon { get; set; }
		public virtual long GoldCollected { get; set; }
		public virtual long PlayedSeconds { get; set; }
		public virtual int Sessions { get; set; }
		public virtual int KillsTotal { get; set; }
		public virtual int KillsNormal { get; set; }
		public virtual int KillsElite { get; set; }
		public virtual int KillsChampion { get; set; }
		public virtual int KillsRare { get; set; }
		public virtual int KillsBoss { get; set; }
		public virtual int KillsUnique { get; set; }
		public virtual int Deaths { get; set; }
		public virtual int ChestsOpened { get; set; }
		public virtual int EventsCompleted { get; set; }
		/// <summary>Number of lore books (journals, bestiaries, ...) this hero has collected.</summary>
		public virtual int LoreCollected { get; set; }
		public virtual DateTime FirstPlayed { get; set; }
		public virtual DateTime LastPlayed { get; set; }
		public virtual DateTime Updated { get; set; }
	}
}
