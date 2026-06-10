// Models/UserEvent.cs
using System;
namespace Lock.Models
{
    public class UserEvent
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime EventDate { get; set; }
        public string Category { get; set; } = string.Empty;
        public int MaxAttendees { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}





