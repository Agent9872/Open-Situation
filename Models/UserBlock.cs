using SQLite;

namespace Lock.Models
{
    [Table("UserBlocks")]
    public class UserBlock
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        [Indexed]
        public int BlockedUserId { get; set; }

        public DateTime DateBlocked { get; set; } = DateTime.UtcNow;
    }
}