// Update your LiveSession model
using SQLite;
using System;
using System.Collections.Generic;

namespace Lock.Models
{
    [Table("LiveSessions")]
    public class LiveSession
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string UserPhoneNumber { get; set; } = string.Empty;

        public string Mood { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public bool ChatAvailable { get; set; }
        public bool VoiceAvailable { get; set; }
        public bool VideoAvailable { get; set; }

        public bool IsLive { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        // Optional: For analytics
        public int ViewCount { get; set; }
        public int ConnectionCount { get; set; }

        public int? DurationMinutes { get; set; } // Duration in minutes
        public DateTime? ScheduledEndTime { get; set; } // When the live session should end
        public bool IsTimedLive { get; set; } // Whether this is a timed live session

        // NEW: Store image paths as JSON string
        public string ImagePathsJson { get; set; } = "[]";
    }

    // Helper class for image management
    public class LiveSessionImage
    {
        public string Path { get; set; } = string.Empty;
        public bool IsUploaded { get; set; }
    }
}