// Models/SparkTransaction.cs
using System;
namespace Lock.Models
{
    public class SparkTransaction
    {
        public int Id { get; set; }
        public string UserPhone { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string PostAuthorPhone { get; set; } = string.Empty;
        public DateTime SparkedAt { get; set; } = DateTime.UtcNow;
    }
}
