using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Lock.Models
{
    public class LiveSession
    {
        public int Id { get; set; }

        [JsonProperty("user_phone_number")]
        public string UserPhoneNumber { get; set; } = string.Empty;

        [JsonProperty("mood")]
        public string Mood { get; set; } = string.Empty;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("chat_available")]
        public bool ChatAvailable { get; set; }

        [JsonProperty("voice_available")]
        public bool VoiceAvailable { get; set; }

        [JsonProperty("video_available")]
        public bool VideoAvailable { get; set; }

        [JsonProperty("is_live")]
        public bool IsLive { get; set; }

        [JsonProperty("started_at")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("ended_at")]
        public DateTime? EndedAt { get; set; }

        [JsonProperty("view_count")]
        public int ViewCount { get; set; }

        [JsonProperty("connection_count")]
        public int ConnectionCount { get; set; }

        [JsonProperty("duration_minutes")]
        public int? DurationMinutes { get; set; }

        [JsonProperty("scheduled_end_time")]
        public DateTime? ScheduledEndTime { get; set; }

        [JsonProperty("is_timed_live")]
        public bool IsTimedLive { get; set; }

        [JsonProperty("image_paths_json")]
        public string ImagePathsJson { get; set; } = "[]";
    }

    // Helper class for image management
    public class LiveSessionImage
    {
        public string Path { get; set; } = string.Empty;
        public bool IsUploaded { get; set; }
    }
}