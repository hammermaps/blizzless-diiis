using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBAccountListsMapper : ClassMap<DBAccountLists>
	{
		public DBAccountListsMapper()
		{
			Table("account_relations");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.ListOwner);
			References(e => e.ListTarget);
			Map(e => e.Type).Not.Nullable().Default("'FRIEND'");
		}
	}
}
