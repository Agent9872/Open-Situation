using System;
using System.Diagnostics;

namespace Lock.Models.Chat
{
    public class Conversation
    {
        // Primary key - Supabase will handle this
        public int Id { get; set; }

        // Unique constraint will be handled at database level in Supabase
        public string ConversationId { get; set; } = Guid.NewGuid().ToString();

        public string ParticipantA { get; set; } = string.Empty;
        public string ParticipantB { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMessageAt { get; set; } = DateTime.MinValue;
        public string LastMessagePreview { get; set; } = string.Empty;

        public bool IsMuted { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        public bool IsStarred { get; set; } = false;
        public bool IsArchived { get; set; } = false;

        public bool DisappearingMessagesEnabled { get; set; } = false;
        public string DisappearingMessagesSetting { get; set; } = "Off";
        public int DisappearingMessagesTimer { get; set; } = 0;
        public DateTime? DisappearingMessagesChangedAt { get; set; }
        public string? DisappearingMessagesChangedBy { get; set; }

        public string? LastMessageType { get; set; }

        // Runtime/computed properties (not persisted to database)
        public string OtherParticipant { get; set; } = string.Empty;
        public int UnreadCount { get; set; }

        // Computed properties for UI
        public string FormattedDisappearingSetting
        {
            get
            {
                if (!DisappearingMessagesEnabled || DisappearingMessagesTimer <= 0)
                    return "Off";

                return DisappearingMessagesTimer switch
                {
                    5 => "5 seconds",
                    300 => "5 minutes",
                    900 => "15 minutes",
                    3600 => "1 hour",
                    86400 => "24 hours",
                    604800 => "1 week",
                    _ => $"{DisappearingMessagesTimer} seconds"
                };
            }
        }

        public bool HasDisappearingMessages
        {
            get
            {
                return DisappearingMessagesEnabled && DisappearingMessagesTimer > 0;
            }
        }

        // Methods
        public string GetOtherParticipant(string currentUserPhone)
        {
            if (string.IsNullOrEmpty(currentUserPhone))
                return string.Empty;

            if (ParticipantA == currentUserPhone)
                return ParticipantB;
            return ParticipantA;
        }

        public void UpdateDisappearingSetting(string setting, string changedBy)
        {
            DisappearingMessagesSetting = setting;
            DisappearingMessagesChangedBy = changedBy;
            DisappearingMessagesChangedAt = DateTime.UtcNow;

            switch (setting)
            {
                case "5 seconds":
                    DisappearingMessagesEnabled = true;
                    DisappearingMessagesTimer = 5;
                    break;
                case "5 minutes":
                    DisappearingMessagesEnabled = true;
                    DisappearingMessagesTimer = 300;
                    break;
                case "15 minutes":
                    DisappearingMessagesEnabled = true;
                    DisappearingMessagesTimer = 900;
                    break;
                case "1 hour":
                    DisappearingMessagesEnabled = true;
                    DisappearingMessagesTimer = 3600;
                    break;
                case "24 hours":
                    DisappearingMessagesEnabled = true;
                    DisappearingMessagesTimer = 86400;
                    break;
                case "1 week":
                    DisappearingMessagesEnabled = true;
                    DisappearingMessagesTimer = 604800;
                    break;
                case "Off":
                default:
                    DisappearingMessagesEnabled = false;
                    DisappearingMessagesTimer = 0;
                    DisappearingMessagesSetting = "Off";
                    break;
            }
        }

        public string GetDisappearingDescription()
        {
            if (!DisappearingMessagesEnabled || DisappearingMessagesTimer <= 0)
                return "Off";

            return DisappearingMessagesTimer switch
            {
                5 => "5 seconds",
                300 => "5 minutes",
                900 => "15 minutes",
                3600 => "1 hour",
                86400 => "24 hours",
                604800 => "1 week",
                _ => "Custom"
            };
        }

        public int GetTimerSeconds() => DisappearingMessagesTimer;

        public bool ShouldMessageBeDeleted(DateTime messageSentAt)
        {
            if (!DisappearingMessagesEnabled || DisappearingMessagesTimer <= 0)
                return false;

            var timeSinceSent = DateTime.UtcNow - messageSentAt;
            return timeSinceSent.TotalSeconds >= DisappearingMessagesTimer;
        }
    }
}