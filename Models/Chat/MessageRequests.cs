using System;

namespace Lock.Models.Chat
{
    public class MessageRequest
    {
        public int Id { get; set; }
        public string ConversationId { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public DateTime? DeclinedAt { get; set; }
        public bool IsAccepted { get; set; }
        public bool IsDeclined { get; set; }

        // Computed property (not persisted)
        public bool IsPending => !IsAccepted && !IsDeclined;

        // Optional: Store a preview of the first message
        public string? MessagePreview { get; set; }
    }
}