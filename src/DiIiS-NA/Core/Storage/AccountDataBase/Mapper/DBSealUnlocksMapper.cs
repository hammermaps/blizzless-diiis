using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBSealUnlocksMapper : ClassMap<DBSealUnlocks>
	{
		public DBSealUnlocksMapper()
		{
			Table("altar_seal_unlocks");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBGameAccount).Not.Nullable();
			Map(e => e.Season).Not.Nullable().Default("28");
			Map(e => e.SealMask).Not.Nullable().Default("0");
			Map(e => e.PotionPowerMask).Not.Nullable().Default("0");
		}
	}
}
