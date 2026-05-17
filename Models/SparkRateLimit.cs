using SQLite;
using System;

namespace Lock.Models
{
    [Table("SparkRateLimits")]
    public class SparkRateLimit
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string UserPhone { get; set; } = string.Empty;

        public int SparkCount { get; set; } = 0;

        public DateTime HourStartTime { get; set; } = DateTime.UtcNow;

        // Track when each spark was sent (for precise counting)
        public string SparkTimestampsJson { get; set; } = "[]";
    }
}