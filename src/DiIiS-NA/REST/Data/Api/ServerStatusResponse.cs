using System.Runtime.Serialization;

namespace DiIiS_NA.REST.Data.Api
{
    [DataContract]
    public class ServerStatusResponse
    {
        [DataMember(Name = "status")]
        public string Status { get; set; }

        [DataMember(Name = "version")]
        public string Version { get; set; }

        [DataMember(Name = "build")]
        public int Build { get; set; }

        [DataMember(Name = "stage")]
        public int Stage { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "uptime_seconds")]
        public long UptimeSeconds { get; set; }

        [DataMember(Name = "online_players")]
        public int OnlinePlayers { get; set; }

        [DataMember(Name = "in_game_players")]
        public int InGamePlayers { get; set; }
    }
}
