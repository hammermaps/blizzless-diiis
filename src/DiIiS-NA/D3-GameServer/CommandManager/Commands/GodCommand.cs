using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

/// <summary>
/// Alias for <c>!invulnerable</c>; the toggle itself lives in
/// <see cref="InvulnerabilityToggle"/> so both commands stay in sync.
/// </summary>
[CommandGroup("god", "Alias for !invulnerable: makes you invulnerable", Account.UserLevels.Tester, inGameOnly: true)]
public class GodCommand : CommandGroup
{
    [DefaultCommand(Account.UserLevels.Tester, true)]
    public string God(string[] @params, BattleClient invokerClient)
    {
        return InvulnerableCommand.Describe(InvulnerabilityToggle.Toggle(invokerClient.InGameClient.Player));
    }
}
