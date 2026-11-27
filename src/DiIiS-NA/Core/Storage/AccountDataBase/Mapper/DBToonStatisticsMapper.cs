using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBToonStatisticsMapper : ClassMap<DBToonStatistics>
	{
		public DBToonStatisticsMapper()
		{
			Table("toon_statistics");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBToon).Column("toon_id").Unique().Not.Nullable();
			Map(e => e.GoldCollected).Column("gold_collected").Not.Nullable().Default("0");
			Map(e => e.PlayedSeconds).Column("played_seconds").Not.Nullable().Default("0");
			Map(e => e.Sessions).Not.Nullable().Default("0");
			Map(e => e.KillsTotal).Column("kills_total").Not.Nullable().Default("0");
			Map(e => e.KillsNormal).Column("kills_normal").Not.Nullable().Default("0");
			Map(e => e.KillsElite).Column("kills_elite").Not.Nullable().Default("0");
			Map(e => e.KillsChampion).Column("kills_champion").Not.Nullable().Default("0");
			Map(e => e.KillsRare).Column("kills_rare").Not.Nullable().Default("0");
			Map(e => e.KillsBoss).Column("kills_boss").Not.Nullable().Default("0");
			Map(e => e.KillsUnique).Column("kills_unique").Not.Nullable().Default("0");
			Map(e => e.Deaths).Not.Nullable().Default("0");
			Map(e => e.ChestsOpened).Column("chests_opened").Not.Nullable().Default("0");
			Map(e => e.EventsCompleted).Column("events_completed").Not.Nullable().Default("0");
			Map(e => e.FirstPlayed).Column("first_played");
			Map(e => e.LastPlayed).Column("last_played");
			Map(e => e.Updated);
		}
	}
}
