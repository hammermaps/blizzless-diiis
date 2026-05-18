using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBAchievementsMapper : ClassMap<DBAchievements>
	{
		public DBAchievementsMapper()
		{
			Table("achievements");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			References(e => e.DBGameAccount).Nullable();
			Map(e => e.AchievementId).CustomType<UInt64UserType>();
			Map(e => e.CompleteTime);
			Map(e => e.IsHardcore).Not.Nullable().Default("false");
			Map(e => e.Quantity).Default("0");
			Map(e => e.Criteria);
			//Map(e => e.Criterias).Not.Nullable().Default("");
		}
	}
}
