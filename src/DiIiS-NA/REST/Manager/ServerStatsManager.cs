using System;
using System.Collections.Generic;
using System.Linq;
using DiIiS_NA.Core.Logging;
using DiIiS_NA.Core.Storage;
using DiIiS_NA.Core.Storage.AccountDataBase.Entities;
using DiIiS_NA.REST.Data.Api;

namespace DiIiS_NA.REST.Manager
{
    /// <summary>
    /// Aggregates server-wide statistics from the database for the REST API.
    /// </summary>
    public static class ServerStatsManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger("ServerStats");

        private const int LeaderboardPageSize = 100;

        /// <summary>
        /// Returns server-wide aggregate statistics queried from the database.
        /// </summary>
        public static ServerStatsResponse GetServerStats()
        {
            try
            {
                var accounts = DBSessions.SessionQuery<DBAccount>();
                var toons = DBSessions.SessionQuery<DBToon>()
                    .Where(t => !t.Deleted && !t.Archieved)
                    .ToList();
                var gameAccounts = DBSessions.SessionQuery<DBGameAccount>();

                long totalKills = gameAccounts.Sum(ga => (long)ga.TotalKilled);
                long totalElites = gameAccounts.Sum(ga => (long)ga.ElitesKilled);
                long totalPlaytime = toons.Sum(t => (long)t.TimePlayed);
                long totalGold = gameAccounts.Sum(ga => (long)ga.TotalGold);

                return new ServerStatsResponse
                {
                    RegisteredAccounts = accounts.Count,
                    TotalToons = toons.Count,
                    TotalKills = totalKills,
                    TotalElitesKilled = totalElites,
                    TotalPlaytimeSeconds = totalPlaytime,
                    TotalGoldCollected = totalGold
                };
            }
            catch (Exception ex)
            {
                Logger.ErrorException(ex, "GetServerStats failed");
                return new ServerStatsResponse();
            }
        }

        /// <summary>
        /// Returns a leaderboard ranked by seasonal kills (DBToon.Kills).
        /// </summary>
        public static LeaderboardResponse GetKillsLeaderboard(int limit = 10)
        {
            var entries = BuildToonLeaderboard(
                category: "kills",
                selector: t => t.Kills,
                limit: limit);
            return entries;
        }

        /// <summary>
        /// Returns a leaderboard ranked by total time played (DBToon.TimePlayed, in seconds).
        /// </summary>
        public static LeaderboardResponse GetPlaytimeLeaderboard(int limit = 10)
        {
            return BuildToonLeaderboard(
                category: "playtime",
                selector: t => t.TimePlayed,
                limit: limit);
        }

        /// <summary>
        /// Returns a leaderboard ranked by toon level, then by experience for equal levels.
        /// </summary>
        public static LeaderboardResponse GetLevelLeaderboard(int limit = 10)
        {
            return BuildToonLeaderboard(
                category: "level",
                selector: t => t.Level * 1_000_000_000L + t.Experience,
                limit: limit,
                displayValue: t => t.Level);
        }

        /// <summary>
        /// Returns a leaderboard ranked by elites killed (DBGameAccount.ElitesKilled, per game account).
        /// Because ElitesKilled is per game-account, we pick the best toon per account.
        /// </summary>
        public static LeaderboardResponse GetElitesLeaderboard(int limit = 10)
        {
            try
            {
                var toons = DBSessions.SessionQuery<DBToon>()
                    .Where(t => !t.Deleted && !t.Archieved && t.DBGameAccount != null)
                    .ToList();

                // Group by game account and pick the hero with the highest level as representative
                var grouped = toons
                    .GroupBy(t => t.DBGameAccount.Id)
                    .Select(g =>
                    {
                        var best = g.OrderByDescending(t => t.Level).ThenByDescending(t => t.Experience).First();
                        var elites = (long)g.First().DBGameAccount.ElitesKilled;
                        return (Toon: best, Value: elites);
                    })
                    .OrderByDescending(x => x.Value)
                    .Take(Math.Min(limit, LeaderboardPageSize))
                    .ToList();

                var entryList = grouped.Select((x, idx) => BuildEntry(x.Toon, idx + 1, x.Value)).ToList();

                return new LeaderboardResponse
                {
                    Category = "elites",
                    Count = entryList.Count,
                    Entries = entryList
                };
            }
            catch (Exception ex)
            {
                Logger.ErrorException(ex, "GetElitesLeaderboard failed");
                return new LeaderboardResponse { Category = "elites", Count = 0 };
            }
        }

        // ─── helpers ───────────────────────────────────────────────────────────

        private static LeaderboardResponse BuildToonLeaderboard(
            string category,
            Func<DBToon, long> selector,
            int limit,
            Func<DBToon, long> displayValue = null)
        {
            try
            {
                var toons = DBSessions.SessionQuery<DBToon>()
                    .Where(t => !t.Deleted && !t.Archieved)
                    .OrderByDescending(selector)
                    .Take(Math.Min(limit, LeaderboardPageSize))
                    .ToList();

                var display = displayValue ?? selector;
                var entries = toons.Select((t, idx) => BuildEntry(t, idx + 1, display(t))).ToList();

                return new LeaderboardResponse
                {
                    Category = category,
                    Count = entries.Count,
                    Entries = entries
                };
            }
            catch (Exception ex)
            {
                Logger.ErrorException(ex, $"BuildToonLeaderboard({category}) failed");
                return new LeaderboardResponse { Category = category, Count = 0 };
            }
        }

        private static LeaderboardEntry BuildEntry(DBToon toon, int rank, long value)
        {
            string battleTag = string.Empty;
            try
            {
                var account = toon.DBGameAccount?.DBAccount;
                if (account != null)
                    battleTag = account.BattleTagName + "#" + account.HashCode.ToString("D4");
            }
            catch { /* best effort */ }

            return new LeaderboardEntry
            {
                Rank = rank,
                BattleTag = battleTag,
                ToonName = toon.Name ?? string.Empty,
                ToonClass = toon.Class.ToString(),
                Level = toon.Level,
                Value = value
            };
        }
    }
}
