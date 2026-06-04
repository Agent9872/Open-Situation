using System;

namespace Lock.Models
{
    public class CoinTransaction
    {
        public int Id { get; set; }
        public string UserPhone { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}