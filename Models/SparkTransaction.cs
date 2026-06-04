using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class SparkTransaction
    {
        public int Id { get; set; }

        [JsonProperty("user_phone")]
        public string UserPhone { get; set; } = string.Empty;

        [JsonProperty("post_id")]
        public int PostId { get; set; }

        [JsonProperty("post_author_phone")]
        public string PostAuthorPhone { get; set; } = string.Empty;

        [JsonProperty("sparked_at")]
        public DateTime SparkedAt { get; set; } = DateTime.UtcNow;
    }
}