using System;

namespace Lock.Models
{
    public class UserPhoto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsPrimary { get; set; }
        public string Category { get; set; } = "Profile"; // Profile, Travel, Friends, Hobby, etc.
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}