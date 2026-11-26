using System.Data;
using NHibernate.Dialect;

namespace DiIiS_NA.Core.Storage
{
	/// <summary>
	/// NHibernate dialect for MariaDB 10.3+ / MySQL 5.7+.
	/// Registers unsigned integer column types so that CLR <c>uint</c>/<c>ulong</c> values
	/// mapped via <see cref="UInt64UserType"/> are stored in correctly-sized columns.
	/// </summary>
	public class MySqlDialect : MySQL57Dialect
	{
		public MySqlDialect()
		{
			RegisterColumnType(DbType.UInt16, "SMALLINT UNSIGNED");
			RegisterColumnType(DbType.UInt32, "INT UNSIGNED");
			RegisterColumnType(DbType.UInt64, "BIGINT UNSIGNED");
		}
	}
}
