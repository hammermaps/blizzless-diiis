using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBMonsterKillMapper : ClassMap<DBMonsterKill>
	{
		public DBMonsterKillMapper()
		{
			Table("toon_monster_kills");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBToon).Column("toon_id").Not.Nullable();
			Map(e => e.MonsterSno).Column("monster_sno").Not.Nullable();
			Map(e => e.MonsterName).Column("monster_name").Not.Nullable().Default("''");
			Map(e => e.Kills).Not.Nullable().Default("0");
			Map(e => e.Updated);
		}
	}
}
