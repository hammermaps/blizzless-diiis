using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using FluentNHibernate.Mapping;

namespace DiIiS_NA.Core.Storage.AccountDataBase.Mapper
{
	public class DBAccountMapper : ClassMap<DBAccount>
	{
		public DBAccountMapper()
		{
			Table("accounts");
			Id(e => e.Id).CustomType<UInt64UserTypeNullable>().GeneratedBy.Identity().UnsavedValue(null);
			Map(e => e.Email);
			Map(e => e.DiscordTag);
			Map(e => e.DiscordId).CustomType<UInt64UserType>().Default("0");
			Map(e => e.Banned).Not.Nullable().Default("false");
			Map(e => e.Salt)/*.CustomSqlType("VarBinary(32)")*/.Length(32);
			Map(e => e.PasswordVerifier)/*.CustomSqlType("VarBinary")*/.Length(128);
			Map(e => e.SaltedTicket);
			Map(e => e.BattleTagName);
			Map(e => e.HashCode);
			Map(e => e.ReferralCode);
			References(e => e.InviteeAccount).Nullable();
			Map(e => e.Money).CustomType<UInt64UserType>().Default("0");
			Map(e => e.UserLevel);
			Map(e => e.LastOnline).CustomType<UInt64UserType>();
			Map(e => e.HasRename).Not.Nullable().Default("false");
			Map(e => e.RenameCooldown).CustomType<UInt64UserType>().Default("0");
			//HasMany(e => e.DBGameAccounts).Cascade.All();//Cascade all means if this Account gets deleted/saved/update ALL GameAccounts do the same :)
			//HasManyToMany(e => e.Friends).ParentKeyColumn("AccountAId").ChildKeyColumn("AccountBId").Cascade.SaveUpdate();
		}
	}
}
