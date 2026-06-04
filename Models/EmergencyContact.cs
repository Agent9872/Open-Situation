// Models/EmergencyContact.cs
using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class EmergencyContact
    {
        public int Id { get; set; }

        [JsonProperty("user_phone")]
        public string UserPhone { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonProperty("relationship")]
        public string Relationship { get; set; } = string.Empty;

        [JsonProperty("is_primary")]
        public bool IsPrimary { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("notes")]
        public string? Notes { get; set; }
    }
}