using SQLite;

namespace Lock.Models
{
    [Table("Follows")]
    public class Follow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string FollowerPhone { get; set; } = string.Empty;

        public string FollowingPhone { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}