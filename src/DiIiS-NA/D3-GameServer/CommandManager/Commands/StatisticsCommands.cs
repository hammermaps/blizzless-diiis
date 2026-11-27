using System.Linq;
using System.Text;
using DiIiS_NA.GameServer.GSSystem.PlayerSystem;
using DiIiS_NA.LoginServer.AccountsSystem;
using DiIiS_NA.LoginServer.Battle;

namespace DiIiS_NA.GameServer.CommandManager;

[CommandGroup("stats", "Character statistics persisted in the account database.", inGameOnly: true)]
public class StatisticsCommands : CommandGroup
{
    [DefaultCommand(minUserLevel: Account.UserLevels.User, inGameOnly: true)]
    public string Show(string[] @params, BattleClient invokerClient)
    {
        if (invokerClient?.InGameClient?.Player is not { } player || player.Toon?.DBToon is not { } dbToon)
            return T("You are not in game.");

        // Flush first so the numbers read back include the current session.
        player.Statistics.Flush();

        var stats = ToonStatisticsTracker.Load(dbToon.Id);
        if (stats == null)
            return T("No statistics recorded for {0} yet.", dbToon.Name);

        var builder = new StringBuilder();
        builder.AppendLine(T("Statistics for {0} (toon {1})", dbToon.Name, dbToon.Id));
        builder.AppendLine(T("Gold collected: {0:N0}", stats.GoldCollected));
        builder.AppendLine(T("Playtime: {0} ({1} sessions)", FormatDuration(stats.PlayedSeconds), stats.Sessions));
        builder.AppendLine(T("Kills: {0:N0} (normal {1:N0}, elite {2:N0})", stats.KillsTotal, stats.KillsNormal, stats.KillsElite));
        builder.AppendLine(T("  champion {0:N0}, rare {1:N0}, boss {2:N0}, unique {3:N0}",
            stats.KillsChampion, stats.KillsRare, stats.KillsBoss, stats.KillsUnique));
        builder.AppendLine(T("Deaths: {0:N0}, chests: {1:N0}, events: {2:N0}",
            stats.Deaths, stats.ChestsOpened, stats.EventsCompleted));
        builder.AppendLine(T("Lore books collected: {0:N0} (stored ids: {1})",
            stats.LoreCollected, dbToon.Lore == null ? 0 : dbToon.Lore.Length / 4));
        builder.AppendLine(T("First played: {0:yyyy-MM-dd HH:mm}  Last played: {1:yyyy-MM-dd HH:mm}",
            stats.FirstPlayed, stats.LastPlayed));

        var monsters = ToonStatisticsTracker.LoadMonsterKills(dbToon.Id, 5);
        if (monsters.Any())
        {
            builder.AppendLine(T("Top monsters:"));
            foreach (var monster in monsters)
                builder.AppendLine(T("  {0} ({1}): {2:N0}", monster.MonsterName, monster.MonsterSno, monster.Kills));
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatDuration(long seconds)
    {
        var time = System.TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalHours}h {time.Minutes}m {time.Seconds}s";
    }
}
