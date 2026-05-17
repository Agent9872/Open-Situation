// Add this to Lock.Models namespace (create a new file: BlockedUser.cs)
using SQLite;

namespace Lock.Models
{
    [Table("BlockedUsers")]
    public class BlockedUser
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string UserPhone { get; set; } = string.Empty; // The user who blocked

        [Indexed]
        public string BlockedPhone { get; set; } = string.Empty; // The user being blocked

        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}