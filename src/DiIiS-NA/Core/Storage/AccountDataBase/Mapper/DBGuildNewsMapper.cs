using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBGuildNewsMapper : ClassMap<DBGuildNews>
	{
		public DBGuildNewsMapper()
		{
			Table("guild_news");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBGuild);
			References(e => e.DBGameAccount);
			Map(e => e.Type);
			Map(e => e.Time).CustomType<UInt64UserType>();
			Map(e => e.Data);
		}
	}
}
