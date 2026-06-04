using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class BlockedUser
    {
        public int Id { get; set; }

        [JsonProperty("user_phone")]
        public string UserPhone { get; set; } = string.Empty;

        [JsonProperty("blocked_phone")]
        public string BlockedPhone { get; set; } = string.Empty;

        [JsonProperty("blocked_at")]
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}