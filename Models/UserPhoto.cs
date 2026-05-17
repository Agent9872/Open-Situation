using SQLite;

namespace Lock.Models
{
    [Table("UserPhotos")]
    public class UserPhoto
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        public string ImagePath { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsPrimary { get; set; }
        public string Category { get; set; } = "Profile"; // Profile, Travel, Friends, Hobby, etc.
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}