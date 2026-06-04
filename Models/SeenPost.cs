using System;

namespace Lock.Models
{
    public class SeenPost
    {
        public int Id { get; set; }
        public string UserPhone { get; set; } = string.Empty; // The user who saw the post
        public string AuthorPhone { get; set; } = string.Empty; // The post author
        public int PostId { get; set; } // The post that was seen
        public DateTime SeenAt { get; set; } = DateTime.UtcNow;
    }
}