using NHibernate.Driver;

namespace DiIiS_NA.Core.Storage
{
	/// <summary>
	/// NHibernate ADO.NET driver for the MIT-licensed <c>MySqlConnector</c> NuGet package.
	/// Uses <c>@</c>-prefixed named parameters, matching the MySqlConnector convention.
	/// </summary>
	public class MySqlConnectorDriver : ReflectionBasedDriver
	{
		public MySqlConnectorDriver()
			: base(
				"MySqlConnector",
				"MySqlConnector.MySqlConnection",
				"MySqlConnector.MySqlCommand")
		{
		}

		public override bool UseNamedPrefixInSql => true;
		public override bool UseNamedPrefixInParameter => true;
		public override string NamedPrefix => "@";
	}
}
