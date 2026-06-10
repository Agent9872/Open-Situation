// Models/UserPrompt.cs
using System;
namespace Lock.Models
{
    public class UserPrompt
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}