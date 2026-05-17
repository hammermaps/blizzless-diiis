using System.Linq;
using DiIiS_NA.GameServer.GSSystem.ActorSystem.Implementations;
using DiIiS_NA.GameServer.MessageSystem;
using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("world", "World commands", Account.UserLevels.Tester, inGameOnly: true)]
public class WorldCommand : CommandGroup
{
    [Command("info", "Current World Info", inGameOnly: true)]
    public string Info(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient?.Player is not {} player)
            return T("You are not in game");
        
        if (player.World == null)
            return T("You are not in world");
        
        var world = player.World;
        return T("[{0}] - {1}", world.SNO.ToString(), world.SNO) + "\n" +
               T("{0} players", world.Players.Count) + "\n" +
               T("{0} of {1} monsters alive", world.Monsters.Count(s=>!s.Dead), world.Monsters.Count) + "\n" +
               T("~ {0:F1} avg. monsters level", world.Monsters.Average(s=>s.Attributes[GameAttributes.Level])) + "\n" +
               T("~ {0:F1} avg. monsters HP", world.Monsters.Average(s=>s.Attributes[GameAttributes.Hitpoints_Max])) + "\n" +
               T("{0} portal(s)", world.Portals.Count) + "\n" +
               T("{0} door(s)", world.GetAllDoors().Length) + "\n" +
               T("{0} door(s)", world.Actors.Count(s=>s.Value is Door)) + "\n" +
               T(world.Game.ActiveNephalemPortal ? "Nephalem portal is active" : "Nephalem portal is inactive") + "\n" +
               T("{0} nephalem progress", world.Game.ActiveNephalemProgress);
    }
}
