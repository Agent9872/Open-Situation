using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class EventAttendance
    {
        public int Id { get; set; }

        [JsonProperty("event_id")]
        public int EventId { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "Going";

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}