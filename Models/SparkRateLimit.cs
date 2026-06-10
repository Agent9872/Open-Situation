// Models/SparkRateLimit.cs
using System;
namespace Lock.Models
{
    public class SparkRateLimit
    {
        public int Id { get; set; }
        public string UserPhone { get; set; } = string.Empty;
        public int SparkCount { get; set; } = 0;
        public DateTime HourStartTime { get; set; } = DateTime.UtcNow;
        public string SparkTimestampsJson { get; set; } = "[]";
    }
}
