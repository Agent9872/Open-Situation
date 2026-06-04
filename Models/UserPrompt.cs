using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class UserPrompt
    {
        public int Id { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("question")]
        public string Question { get; set; } = string.Empty;

        [JsonProperty("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonProperty("order")]
        public int Order { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}