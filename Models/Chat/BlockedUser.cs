using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class BlockedUser
    {
        public int Id { get; set; }
        public string UserPhone { get; set; } = string.Empty;
        public string BlockedPhone { get; set; } = string.Empty;
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}