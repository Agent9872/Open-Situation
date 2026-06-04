using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class Follow
    {
        public int Id { get; set; }

        [JsonProperty("follower_phone")]
        public string FollowerPhone { get; set; } = string.Empty;

        [JsonProperty("following_phone")]
        public string FollowingPhone { get; set; } = string.Empty;

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}