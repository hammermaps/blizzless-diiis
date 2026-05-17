using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("difficulty", "Changes difficulty of the game", Account.UserLevels.GM, inGameOnly: true)]
public class DifficultyCommand : CommandGroup
{
    [Command("max", "Sets difficulty to max", Account.UserLevels.GM, inGameOnly: true)]
    public string Max(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient is null)
            return T("You must execute this command in-game.");
        if (invokerClient.InGameClient.Player.World.Game.Difficulty == 19)
            return T("You can't increase difficulty any more.");
        invokerClient.InGameClient.Player.World.Game.SetDifficulty(19);
        return T("Difficulty set to max - {0}", invokerClient.InGameClient.Player.World.Game.Difficulty);
    }
    
    [Command("min", "Sets difficulty to min", Account.UserLevels.GM, inGameOnly: true)]
    public string Min(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient is null)
            return T("You must execute this command in-game.");
        if (invokerClient.InGameClient.Player.World.Game.Difficulty == 0)
            return T("You can't decrease difficulty any more.");
        invokerClient.InGameClient.Player.World.Game.SetDifficulty(0);
        return T("Difficulty set to min - {0}", invokerClient.InGameClient.Player.World.Game.Difficulty);
    }
    
    [Command("up", "Increases difficulty of the game", Account.UserLevels.GM, inGameOnly: true)]
    public string Up(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient is null)
            return T("You must execute this command in-game.");
        if (invokerClient.InGameClient.Player.World.Game.Difficulty == 19)
            return T("You can't increase difficulty any more.");
        invokerClient.InGameClient.Player.World.Game.RaiseDifficulty(invokerClient.InGameClient, null);
        return T("Difficulty increased - set to {0}", invokerClient.InGameClient.Player.World.Game.Difficulty);
    }

    [Command("down", "Decreases difficulty of the game", Account.UserLevels.GM, inGameOnly: true)]
    public string Down(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient is null)
            return T("You must execute this command in-game.");
        if (invokerClient.InGameClient.Player.World.Game.Difficulty == 0)
            return T("Difficulty is already at minimum");
        invokerClient.InGameClient.Player.World.Game.LowDifficulty(invokerClient.InGameClient, null);
        return T("Difficulty decreased - set to {0}", invokerClient.InGameClient.Player.World.Game.Difficulty);
    }

    [Command("set", "Sets difficulty of the game", Account.UserLevels.GM, inGameOnly: true)]
    public string Set(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient is null)
            return T("You must execute this command in-game.");
        if (!int.TryParse(@params[0], out var difficulty) || difficulty is < 0 or > 19)
            return T("Invalid difficulty. Must be between 0 and 19.");
        invokerClient.InGameClient.Player.World.Game.SetDifficulty(difficulty);
        return T("Difficulty set to {0}", invokerClient.InGameClient.Player.World.Game.Difficulty);
    }

    [DefaultCommand(inGameOnly: true)]
    public string Default(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient is null)
            return T("You must execute this command in-game.");
        return T("Current difficulty is {0}", invokerClient.InGameClient.Player.World.Game.Difficulty) + "\n" +
               T("Difficulties range from 0-19.") + "\n\n" +
               T("Use !difficulty set <value> - to set difficulty to a specific value.") + "\n" +
               T("Use !difficulty up - to increase difficulty by 1.") + "\n" +
               T("Use !difficulty down - to decrease difficulty by 1.") + "\n" +
               T("Use !difficulty max - to set difficulty to max (19).") + "\n" +
               T("Use !difficulty min - to set difficulty to min (0).");
    }
}
