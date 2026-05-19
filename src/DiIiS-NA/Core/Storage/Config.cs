using System;
using System.Text;

namespace DiIiS_NA.Core.Storage
{
	public sealed class Config : Core.Config.Config
	{
		public string Root { get { return this.GetString("Root", "DataBase"); } set { this.Set("Root", value); } }
		public string MPQRoot { get { return this.GetString("MPQRoot", "DataBase/MPQ"); } set { this.Set("MPQRoot", value); } }
		public bool EnableTasks { get { return this.GetBoolean("EnableTasks", true); } set { this.Set("EnableTasks", value); } }
		public bool LazyLoading { get { return this.GetBoolean("LazyLoading", false); } set { this.Set("LazyLoading", value); } }

		/// <summary>
		/// Database backend to use. Supported values: <c>postgres</c> (default), <c>mysql</c>.
		/// Determines which <c>database.*.config</c> file is loaded by the session providers.
		/// </summary>
		public string DatabaseType { get { return this.GetString("DatabaseType", "postgres"); } set { this.Set("DatabaseType", value); } }

		/// <summary>
		/// Database server hostname or IP address.
		/// When non-empty this value overrides the <c>Server</c> entry in the NHibernate XML config file.
		/// </summary>
		public string DatabaseServer { get { return this.GetString("DatabaseServer", ""); } set { this.Set("DatabaseServer", value); } }

		/// <summary>
		/// Database server port (e.g. 5432 for PostgreSQL, 3306 for MySQL/MariaDB).
		/// When non-zero this value overrides the <c>Port</c> entry in the NHibernate XML config file.
		/// </summary>
		public int DatabasePort { get { return this.GetInt("DatabasePort", 0); } set { this.Set("DatabasePort", value); } }

		/// <summary>
		/// Database user name.
		/// When non-empty this value overrides the <c>User ID</c> entry in the NHibernate XML config file.
		/// </summary>
		public string DatabaseUser { get { return this.GetString("DatabaseUser", ""); } set { this.Set("DatabaseUser", value); } }

		/// <summary>
		/// Database password.
		/// When non-empty this value overrides the <c>Password</c> entry in the NHibernate XML config file.
		/// </summary>
		public string DatabasePassword { get { return this.GetString("DatabasePassword", ""); } set { this.Set("DatabasePassword", value); } }

		/// <summary>
		/// Applies any connection overrides configured in this section (Server, Port, User, Password)
		/// on top of the connection string loaded from the NHibernate XML config file.
		/// Keys that are not overridden remain unchanged.
		/// </summary>
		/// <param name="connectionString">Original connection string from the XML config file.</param>
		/// <returns>Connection string with config.ini overrides applied.</returns>
		public string ApplyConnectionStringOverrides(string connectionString)
		{
			var parts = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var segment in connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var trimmed = segment.Trim();
				if (string.IsNullOrEmpty(trimmed)) continue;
				var eq = trimmed.IndexOf('=');
				if (eq < 0) { parts[trimmed] = ""; continue; }
				parts[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
			}

			if (!string.IsNullOrEmpty(DatabaseServer))
				parts["Server"] = DatabaseServer;
			if (DatabasePort > 0)
				parts["Port"] = DatabasePort.ToString();
			if (!string.IsNullOrEmpty(DatabaseUser))
				parts["User ID"] = DatabaseUser;
			if (!string.IsNullOrEmpty(DatabasePassword))
				parts["Password"] = DatabasePassword;

			var sb = new StringBuilder();
			foreach (var kvp in parts)
			{
				sb.Append(kvp.Key);
				sb.Append('=');
				sb.Append(kvp.Value);
				sb.Append(';');
			}
			return sb.ToString();
		}

		private static readonly Config _instance = new Config();
		public static Config Instance { get { return _instance; } }
		private Config() : base("Storage") { }
	}
}
