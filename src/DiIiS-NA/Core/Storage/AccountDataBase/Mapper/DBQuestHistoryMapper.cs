using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBQuestHistoryMapper : ClassMap<DBQuestHistory>
	{
		public DBQuestHistoryMapper()
		{
			Table("quests");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBToon).Nullable();
			Map(e => e.QuestId);
			Map(e => e.isCompleted).Not.Nullable().Default("false");
			Map(e => e.QuestStep);
		}
	}
}
