using System.Runtime.Serialization;

namespace DiIiS_NA.REST.Data.Api
{
    [DataContract]
    public class CommandResponse
    {
        [DataMember(Name = "success")]
        public bool Success { get; set; }

        [DataMember(Name = "output")]
        public string Output { get; set; }
    }
}
