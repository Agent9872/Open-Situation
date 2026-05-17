using SQLite;
using System;

namespace Lock.Models
{
    [Table("SparkTransactions")]
    public class SparkTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string UserPhone { get; set; } = string.Empty;

        public int PostId { get; set; }

        public string PostAuthorPhone { get; set; } = string.Empty;

        public DateTime SparkedAt { get; set; } = DateTime.UtcNow;
    }
}