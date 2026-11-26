using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBGuildMemberMapper : ClassMap<DBGuildMember>
	{
		public DBGuildMemberMapper()
		{
			Table("guild_members");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBGuild);
			References(e => e.DBGameAccount);
			Map(e => e.Note).Length(50);
			Map(e => e.Rank);
		}
	}
}
