using SQLite;
using System;

namespace Lock.Models
{
    [Table("UserEvents")]
    public class UserEvent
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        public string EventName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime EventDate { get; set; }
        public string Category { get; set; } = string.Empty; // Music, Sports, Food, etc.
        public int MaxAttendees { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}