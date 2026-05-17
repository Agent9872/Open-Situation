using SQLite;

namespace Lock.Models
{
    [Table("EventAttendance")]
    public class EventAttendance
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int EventId { get; set; }

        [Indexed]
        public int UserId { get; set; }

        public string Status { get; set; } = "Going"; // Going, Interested, Went
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}