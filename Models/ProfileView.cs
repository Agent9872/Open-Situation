using SQLite;
using System;

namespace Lock.Models
{
    [Table("ProfileViews")]
    public class ProfileView
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ViewedUserId { get; set; }  // User whose profile was viewed
        public string ViewedUserPhone { get; set; } = string.Empty;

        public int ViewerUserId { get; set; }  // User who viewed the profile
        public string ViewerUserPhone { get; set; } = string.Empty;

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
        public bool IsNew { get; set; } = true;
    }
}