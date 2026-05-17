using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DiIiS_NA.REST.Data.Api
{
    [DataContract]
    public class ServerStatsResponse
    {
        [DataMember(Name = "registered_accounts")]
        public int RegisteredAccounts { get; set; }

        [DataMember(Name = "total_toons")]
        public int TotalToons { get; set; }

        [DataMember(Name = "total_kills")]
        public long TotalKills { get; set; }

        [DataMember(Name = "total_elites_killed")]
        public long TotalElitesKilled { get; set; }

        [DataMember(Name = "total_playtime_seconds")]
        public long TotalPlaytimeSeconds { get; set; }

        [DataMember(Name = "total_gold_collected")]
        public long TotalGoldCollected { get; set; }
    }

    [DataContract]
    public class LeaderboardEntry
    {
        [DataMember(Name = "rank")]
        public int Rank { get; set; }

        [DataMember(Name = "battle_tag")]
        public string BattleTag { get; set; }

        [DataMember(Name = "toon_name")]
        public string ToonName { get; set; }

        [DataMember(Name = "toon_class")]
        public string ToonClass { get; set; }

        [DataMember(Name = "level")]
        public int Level { get; set; }

        [DataMember(Name = "value")]
        public long Value { get; set; }
    }

    [DataContract]
    public class LeaderboardResponse
    {
        [DataMember(Name = "category")]
        public string Category { get; set; }

        [DataMember(Name = "count")]
        public int Count { get; set; }

        [DataMember(Name = "entries")]
        public List<LeaderboardEntry> Entries { get; set; } = new List<LeaderboardEntry>();
    }
}
