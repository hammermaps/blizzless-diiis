using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DiIiS_NA.REST.Data.Api
{
    [DataContract]
    public class PlayerInfoResponse
    {
        [DataMember(Name = "battle_tag")]
        public string BattleTag { get; set; }

        [DataMember(Name = "user_level")]
        public string UserLevel { get; set; }

        [DataMember(Name = "in_game")]
        public bool InGame { get; set; }
    }

    [DataContract]
    public class PlayerListResponse
    {
        [DataMember(Name = "count")]
        public int Count { get; set; }

        [DataMember(Name = "players")]
        public List<PlayerInfoResponse> Players { get; set; } = new List<PlayerInfoResponse>();
    }
}
