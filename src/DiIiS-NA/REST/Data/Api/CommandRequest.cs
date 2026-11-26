using System.Runtime.Serialization;

namespace DiIiS_NA.REST.Data.Api
{
    [DataContract]
    public class CommandRequest
    {
        /// <summary>The full command string including the command prefix (e.g. "!players").</summary>
        [DataMember(Name = "command")]
        public string Command { get; set; }
    }
}
