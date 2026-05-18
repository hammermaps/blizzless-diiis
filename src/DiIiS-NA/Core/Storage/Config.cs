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

		private static readonly Config _instance = new Config();
		public static Config Instance { get { return _instance; } }
		private Config() : base("Storage") { }
	}
}
