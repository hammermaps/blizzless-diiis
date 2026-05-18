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
    /// All queries are pushed to the database level – no full-table materialisation.
    /// </summary>
    public static class ServerStatsManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger("ServerStats");

        private const int LeaderboardPageSize = 100;

        /// <summary>
        /// Returns server-wide aggregate statistics using DB-side COUNT/SUM queries.
        /// </summary>
        public static ServerStatsResponse GetServerStats()
        {
            try
            {
                int registeredAccounts = DBSessions.SessionExecute(s =>
                    s.Query<DBAccount>().Count());

                int totalToons = DBSessions.SessionExecute(s =>
                    s.Query<DBToon>().Count(t => !t.Deleted && !t.Archieved));

                // Use native SQL for ulong columns (custom NHibernate type) to avoid
                // LINQ-provider translation issues and to ensure DB-side aggregation.
                ulong totalKills = SqlSumUlong("SELECT COALESCE(SUM(totalkilled), 0) FROM game_accounts");
                ulong totalElites = SqlSumUlong("SELECT COALESCE(SUM(eliteskilled), 0) FROM game_accounts");
                ulong totalGold = SqlSumUlong("SELECT COALESCE(SUM(totalgold), 0) FROM game_accounts");

                long totalPlaytime = DBSessions.SessionExecute(s =>
                    s.Query<DBToon>()
                     .Where(t => !t.Deleted && !t.Archieved)
                     .Sum(t => (long?)t.TimePlayed) ?? 0L);

                return new ServerStatsResponse
                {
                    RegisteredAccounts = registeredAccounts,
                    TotalToons = totalToons,
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
            return BuildToonLeaderboard(
                category: "kills",
                orderingFactory: q => q.OrderByDescending(t => t.Kills),
                limit: limit,
                displayValue: t => t.Kills);
        }

        /// <summary>
        /// Returns a leaderboard ranked by total time played (DBToon.TimePlayed, in seconds).
        /// </summary>
        public static LeaderboardResponse GetPlaytimeLeaderboard(int limit = 10)
        {
            return BuildToonLeaderboard(
                category: "playtime",
                orderingFactory: q => q.OrderByDescending(t => t.TimePlayed),
                limit: limit,
                displayValue: t => t.TimePlayed);
        }

        /// <summary>
        /// Returns a leaderboard ranked by toon level descending, then experience descending.
        /// </summary>
        public static LeaderboardResponse GetLevelLeaderboard(int limit = 10)
        {
            return BuildToonLeaderboard(
                category: "level",
                orderingFactory: q => q.OrderByDescending(t => t.Level).ThenByDescending(t => t.Experience),
                limit: limit,
                displayValue: t => t.Level);
        }

        /// <summary>
        /// Returns a leaderboard ranked by elites killed (DBGameAccount.ElitesKilled, per game account).
        /// The representative toon for each account is the highest-level hero.
        /// </summary>
        public static LeaderboardResponse GetElitesLeaderboard(int limit = 10)
        {
            try
            {
                // Load only active toons; filtering and ordering happen at DB level.
                var toons = DBSessions.SessionExecute(s =>
                    s.Query<DBToon>()
                     .Where(t => !t.Deleted && !t.Archieved && t.DBGameAccount != null)
                     .OrderByDescending(t => t.Level)
                     .ThenByDescending(t => t.Experience)
                     .ToList());

                // Group in-memory by account (cheap after DB-side ordering) and pick
                // the highest-level toon as representative for each account.
                var grouped = toons
                    .GroupBy(t => t.DBGameAccount.Id)
                    .Select(g =>
                    {
                        var best = g.First(); // already ordered by level desc
                        ulong elites = g.First().DBGameAccount.ElitesKilled;
                        return (Toon: best, Value: elites);
                    })
                    .OrderByDescending(x => x.Value)
                    .Take(Math.Min(limit, LeaderboardPageSize))
                    .ToList();

                var entryList = grouped
                    .Select((x, idx) => BuildEntry(x.Toon, idx + 1, (long)Math.Min(x.Value, (ulong)long.MaxValue)))
                    .ToList();

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

        /// <summary>
        /// Queries and orders toons at the DB level; only the top-N rows are transferred.
        /// </summary>
        private static LeaderboardResponse BuildToonLeaderboard(
            string category,
            Func<IQueryable<DBToon>, IOrderedQueryable<DBToon>> orderingFactory,
            int limit,
            Func<DBToon, long> displayValue)
        {
            try
            {
                var toons = DBSessions.SessionExecute(s =>
                {
                    var q = s.Query<DBToon>().Where(t => !t.Deleted && !t.Archieved);
                    return orderingFactory(q).Take(Math.Min(limit, LeaderboardPageSize)).ToList();
                });

                var entries = toons.Select((t, idx) => BuildEntry(t, idx + 1, displayValue(t))).ToList();

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
            catch (Exception ex)
            {
                Logger.Warn($"Could not resolve battle tag for toon {toon.Id}: {ex.Message}");
            }

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

        /// <summary>
        /// Executes a native SQL scalar query that returns a numeric sum and converts it to <c>ulong</c>.
        /// </summary>
        private static ulong SqlSumUlong(string sql)
        {
            return DBSessions.SessionExecute(s =>
            {
                var raw = s.CreateSQLQuery(sql).UniqueResult();
                if (raw == null) return 0UL;
                return Convert.ToUInt64(Convert.ToDecimal(raw));
            });
        }
    }
}

