using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("invulnerable", "Makes you invulnerable", Account.UserLevels.Tester, inGameOnly: true)]
public class InvulnerableCommand : CommandGroup
{
    [DefaultCommand(Account.UserLevels.Tester, true)]
    public string Invulnerable(string[] @params, BattleClient invokerClient)
    {
        return Describe(InvulnerabilityToggle.Toggle(invokerClient.InGameClient.Player));
    }

    internal static string Describe(InvulnerabilityResult result)
    {
        return result switch
        {
            InvulnerabilityResult.DevModeActive =>
                "You have !debug enabled, which overrides invulnerability. Disable it to use this command.",
            InvulnerabilityResult.Disabled => T("You are no longer invulnerable."),
            _ => T("You are now invulnerable.")
        };
    }
}
