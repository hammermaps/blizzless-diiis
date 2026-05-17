using DiIiS_NA.LoginServer.AccountsSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiIiS_NA.GameServer.CommandManager
{
	[AttributeUsage(AttributeTargets.Class)]
	public class CommandGroupAttribute : Attribute
	{
		/// <summary>
		/// Command group's name.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// Help text for command group.
		/// </summary>
		private string _help;
		private string _cachedLocalizedHelp;
		private string _cacheLanguage;
		public string Help
		{
			get
			{
				var language = DiIiS_NA.Core.Localization.Locale.Language;
				if (_cachedLocalizedHelp != null && string.Equals(_cacheLanguage, language, StringComparison.OrdinalIgnoreCase))
					return _cachedLocalizedHelp;

				_cachedLocalizedHelp = T(_help);
				_cacheLanguage = language;
				return _cachedLocalizedHelp;
			}
		}

		/// <summary>
		/// Minimum user level required to invoke the command.
		/// </summary>
		public Account.UserLevels MinUserLevel { get; private set; }
		
		/// <summary>
		/// For InGame commands only. If true, the command will be available only in the game.
		/// </summary>
		public bool InGameOnly { get; }

		public CommandGroupAttribute(string name, string help, Account.UserLevels minUserLevel = Account.UserLevels.Admin, bool inGameOnly = false)
		{
			Name = name.ToLower();
			_help = help;
			MinUserLevel = minUserLevel;
			InGameOnly = inGameOnly;
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class CommandAttribute : Attribute
	{
		/// <summary>
		/// Command's name.
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// Help text for command.
		/// </summary>
		private string _help;
		private string _cachedLocalizedHelp;
		private string _cacheLanguage;
		public string Help
		{
			get
			{
				var language = DiIiS_NA.Core.Localization.Locale.Language;
				if (_cachedLocalizedHelp != null && string.Equals(_cacheLanguage, language, StringComparison.OrdinalIgnoreCase))
					return _cachedLocalizedHelp;

				_cachedLocalizedHelp = T(_help);
				_cacheLanguage = language;
				return _cachedLocalizedHelp;
			}
		}

		/// <summary>
		/// Minimum user level required to invoke the command.
		/// </summary>
		public Account.UserLevels MinUserLevel { get; }
		
		/// <summary>
		/// Whether the command is only for in-game command.
		/// </summary>
		public bool InGameOnly { get; }

		public CommandAttribute(string command, string help, Account.UserLevels minUserLevel = Account.UserLevels.User, bool inGameOnly = false)
		{
			Name = command.ToLower();
			_help = help;
			MinUserLevel = minUserLevel;
			InGameOnly = inGameOnly;
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class DefaultCommand : CommandAttribute
	{
		public DefaultCommand(Account.UserLevels minUserLevel = Account.UserLevels.User, bool inGameOnly = false)
			: base("", "", minUserLevel, inGameOnly)
		{
		}
	}
}
