using SQLite;
using System;

namespace Lock.Models.Chat
{
    [Table("MessageRequests")]
    public class MessageRequest
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string ConversationId { get; set; } = string.Empty;

        [Indexed]
        public string SenderPhone { get; set; } = string.Empty;

        [Indexed]
        public string RecipientPhone { get; set; } = string.Empty;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? DeclinedAt { get; set; }

        public bool IsAccepted { get; set; }
        public bool IsDeclined { get; set; }
        public bool IsPending => !IsAccepted && !IsDeclined;

        // Optional: Store a preview of the first message
        public string? MessagePreview { get; set; }
    }
}