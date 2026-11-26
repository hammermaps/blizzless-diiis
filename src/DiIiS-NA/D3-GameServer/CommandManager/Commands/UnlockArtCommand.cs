using System;
using DiIiS_NA.GameServer.MessageSystem.Message.Definitions.Hireling;
using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("unlockart", "Unlock all artisans: !unlockart\nOptionally set their level too, up to 12: !unlockart [level]", Account.UserLevels.Tester, inGameOnly: true)]
public class UnlockArtCommand : CommandGroup
{
    [DefaultCommand(inGameOnly: true)]
    public string UnlockArt(string[] @params, BattleClient invokerClient)
    {
        var player = invokerClient.InGameClient.Player;
        int? level = null;
        if (@params != null && @params.Length > 0)
        {
            if (!int.TryParse(@params[0], out var requested) || requested < 1)
                return "Invalid artisan level.";
            level = requested;
        }

        player.BlacksmithUnlocked = true;
        player.JewelerUnlocked = true;
        player.MysticUnlocked = true;
        player.GrantAchievement(74987243307766); // Blacksmith
        player.GrantAchievement(74987243307780); // Jeweler
        player.GrantAchievement(74987247205955); // Mystic
        player.GrantCriteria(74987252674266); // Kanai's Cube found
        player.KanaiUnlocked = true;

        player.HirelingTemplarUnlocked = true;
        player.InGameClient.SendMessage(new HirelingNewUnlocked() { NewClass = 1 });
        player.GrantAchievement(74987243307073);
        player.HirelingScoundrelUnlocked = true;
        player.InGameClient.SendMessage(new HirelingNewUnlocked() { NewClass = 2 });
        player.GrantAchievement(74987243307147);
        player.HirelingEnchantressUnlocked = true;
        player.InGameClient.SendMessage(new HirelingNewUnlocked() { NewClass = 3 });
        player.GrantAchievement(74987243307145);

        if (level.HasValue)
            player.SetArtisanLevel(level.Value);
        player.LoadCrafterData();
        player.UpdateArtisanWorkshops();
        player.Toon.GameAccount.NotifyUpdate();
        return level.HasValue
            ? T("All artisans unlocked at level {0}", Math.Min(level.Value, 12))
            : T("All artisans Unlocked");
    }
}