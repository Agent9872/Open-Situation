using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class ProfileView
    {
        public int Id { get; set; }

        [JsonProperty("viewed_user_id")]
        public int ViewedUserId { get; set; }

        [JsonProperty("viewed_user_phone")]
        public string ViewedUserPhone { get; set; } = string.Empty;

        [JsonProperty("viewer_user_id")]
        public int ViewerUserId { get; set; }

        [JsonProperty("viewer_user_phone")]
        public string ViewerUserPhone { get; set; } = string.Empty;

        [JsonProperty("viewed_at")]
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("is_new")]
        public bool IsNew { get; set; } = true;
    }
}