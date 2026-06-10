using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Lock.Models
{
    public class LiveSession
    {
        public int Id { get; set; }
        public string UserPhoneNumber { get; set; } = string.Empty;
        public string HostPhone { get; set; } = string.Empty;
        public string Mood { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool ChatAvailable { get; set; }
        public bool VoiceAvailable { get; set; }
        public bool VideoAvailable { get; set; }
        public bool IsLive { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int ViewCount { get; set; }
        public int ConnectionCount { get; set; }
        public int? DurationMinutes { get; set; }
        public DateTime? ScheduledEndTime { get; set; }
        public bool IsTimedLive { get; set; }
        public string ImagePathsJson { get; set; } = "[]";
    }

    public class LiveSessionImage
    {
        public string Path { get; set; } = string.Empty;
        public bool IsUploaded { get; set; }
    }
}