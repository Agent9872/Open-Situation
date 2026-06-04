using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class SparkRateLimit
    {
        public int Id { get; set; }

        [JsonProperty("user_phone")]
        public string UserPhone { get; set; } = string.Empty;

        [JsonProperty("spark_count")]
        public int SparkCount { get; set; } = 0;

        [JsonProperty("hour_start_time")]
        public DateTime HourStartTime { get; set; } = DateTime.UtcNow;

        [JsonProperty("spark_timestamps_json")]
        public string SparkTimestampsJson { get; set; } = "[]";
    }
}