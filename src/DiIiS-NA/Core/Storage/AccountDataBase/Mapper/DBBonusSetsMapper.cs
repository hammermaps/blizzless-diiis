using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBBonusSetsMapper : ClassMap<DBBonusSets>
	{
		public DBBonusSetsMapper()
		{
			Table("collection_editions");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			Map(e => e.SetId);
			References(e => e.DBAccount);
			Map(e => e.Claimed).Not.Nullable().Default("false");
			References(e => e.ClaimedToon);
			Map(e => e.ClaimedHardcore).Not.Nullable().Default("false");
		}
	}
}
