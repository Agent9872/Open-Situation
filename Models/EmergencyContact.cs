// Models/EmergencyContact.cs
using System;
namespace Lock.Models
{
    public class EmergencyContact
    {
        public int Id { get; set; }
        public string UserPhone { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
    }
}