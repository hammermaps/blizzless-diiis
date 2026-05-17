using System.Linq;
using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;
using FluentNHibernate.Utils;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("account", "Provides account management commands.")]
public class AccountCommands : CommandGroup
{
    [Command("show", "Shows information about given account\nUsage: account show <email>", Account.UserLevels.GM)]
    public string Show(string[] @params, BattleClient invokerClient)
    {
        if (!@params.Any())
            return T("Invalid arguments. Type 'help account show' to get help.");

        var email = @params[0];
        var account = AccountManager.GetAccountByEmail(email);

        if (account == null)
            return T("No account with email '{0}' exists.", email);

        return T("Email: {0} User Level: {1}", account.Email, account.UserLevel);
    }

    [Command("add",
        "Allows you to add a new user account.\nUsage: account add <email> <password> <battletag> [userlevel]",
        Account.UserLevels.GM)]
    public string Add(string[] @params, BattleClient invokerClient)
    {
        if (@params.Length < 3)
            return T("Invalid arguments. Type 'help account add' to get help.");

        var email = @params[0];
        var password = @params[1];
        var battleTagName = @params[2];
        var userLevel = Account.UserLevels.User;

        if (@params.Length == 4)
        {
            var level = Account.UserLevelsExtensions.FromString(@params[3]);
            if (level == null)
                return T("Invalid user level.");
            userLevel = level.Value;
        }

        if (!email.Contains('@'))
            return T("'{0}' is not a valid email address.", email);

        if (battleTagName.Contains('#'))
            return T("BattleTag must not contain '#' or HashCode.");

        if (password.Length < 8 || password.Length > 16)
            return T("Password should be a minimum of 8 and a maximum of 16 characters.");

        if (AccountManager.GetAccountByEmail(email) != null)
            return T("An account already exists for email address {0}.", email);

        var account = AccountManager.CreateAccount(email, password, battleTagName, userLevel);
        var gameAccount = GameAccountManager.CreateGameAccount(account);
        //account.DBAccount.DBGameAccounts.Add(gameAccount.DBGameAccount);
        return
            $"Created new account {account.Email} [user-level: {account.UserLevel}] Full BattleTag: {account.BattleTag}.";
    }

    [Command("setpassword",
        "Allows you to set a new password for account\nUsage: account setpassword <email> <password>",
        Account.UserLevels.GM)]
    public string SetPassword(string[] @params, BattleClient invokerClient)
    {
        if (@params.Length < 2)
            return T("Invalid arguments. Type 'help account setpassword' to get help.");

        var email = @params[0];
        var password = @params[1];

        var account = AccountManager.GetAccountByEmail(email);

        if (account == null)
            return T("No account with email '{0}' exists.", email);

        if (password.Length < 8 || password.Length > 16)
            return T("Password should be a minimum of 8 and a maximum of 16 characters.");

        account.UpdatePassword(password);
        return T("Updated password for account {0}.", email);
    }

    [Command("setbtag", "Allows you to change battle tag for account\nUsage: account setbtag <email> <newname>",
        Account.UserLevels.GM)]
    public string SetBTag(string[] @params, BattleClient invokerClient)
    {
        if (@params.Length < 2)
            return T("Invalid arguments. Type 'help account setbtag' to get help.");

        var email = @params[0];
        var newname = @params[1];

        var account = AccountManager.GetAccountByEmail(email);

        if (account == null)
            return T("No account with email '{0}' exists.", email);

        account.UpdateBattleTag(newname);
        return T("Updated battle tag for account {0}.", email);
    }

    [Command("setuserlevel",
        "Allows you to set a new user level for account\nUsage: account setuserlevel <email> <user level>.\nAvailable user levels: owner, admin, gm, user.",
        Account.UserLevels.GM)]
    public string SetLevel(string[] @params, BattleClient invokerClient)
    {
        if (@params.Length < 2)
            return T("Invalid arguments. Type 'help account setuserlevel' to get help.");

        var email = @params[0];

        var account = AccountManager.GetAccountByEmail(email);

        if (account == null)
            return T("No account with email '{0}' exists.", email);

        var level = Account.UserLevelsExtensions.FromString(@params[1]);
        if (level == null)
            return T("Invalid user level.");
        Account.UserLevels userLevel = level.Value;

        account.UpdateUserLevel(userLevel);
        return T("Updated user level for account {0} [user-level: {1}].", email, userLevel);
    }
}