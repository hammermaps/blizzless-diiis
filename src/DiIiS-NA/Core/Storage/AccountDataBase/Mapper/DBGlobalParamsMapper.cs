using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBGlobalParamsMapper : ClassMap<DBGlobalParams>
	{
		public DBGlobalParamsMapper()
		{
			Table("global_params");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			Map(e => e.Name);
			Map(e => e.Value).CustomType<UInt64UserType>();
		}
	}
}
