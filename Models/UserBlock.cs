using System;

namespace Lock.Models
{
    public class UserBlock
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BlockedUserId { get; set; }
        public DateTime DateBlocked { get; set; } = DateTime.UtcNow;
    }
}