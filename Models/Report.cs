using SQLite;
using System;
using System.Collections.Generic;

namespace Lock.Models
{
    [Table("Reports")]
    public class Report
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Reporter info
        public string ReporterPhone { get; set; }
        public string ReporterName { get; set; }

        // Reported user info
        public string ReportedUserPhone { get; set; }
        public string ReportedUserName { get; set; }
        public string ReportedUserProfileImage { get; set; }

        // Report details
        public string Category { get; set; }
        public string Description { get; set; }
        public DateTime ReportedAt { get; set; }

        // Evidence — NOT stored in this table column
        // Loaded separately via ReportImage table
        [Ignore]
        public List<ReportImage> Images { get; set; } = new();

        // Optional screenshot path (single file reference, safe to store)
        public string MessageScreenshotPath { get; set; }

        // Conversation context
        public string ConversationId { get; set; }

        // Store as int with -1 meaning "no message" to avoid nullable issues
        // on older SQLite-NET builds
        private int _reportedMessageIdRaw = -1;

        [Ignore]
        public int? ReportedMessageId
        {
            get => _reportedMessageIdRaw == -1 ? (int?)null : _reportedMessageIdRaw;
            set => _reportedMessageIdRaw = value ?? -1;
        }

        // Backing column that SQLite actually stores
        [Column("ReportedMessageId")]
        public int ReportedMessageIdDb
        {
            get => _reportedMessageIdRaw;
            set => _reportedMessageIdRaw = value;
        }

        public string ReportedMessageContent { get; set; }

        // Status
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string AdminNotes { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }

        // Actions taken
        public AdminAction ActionTaken { get; set; } = AdminAction.None;
    }

    [Table("ReportImages")]
    public class ReportImage
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ReportId { get; set; }
        public string LocalPath { get; set; }
        public string RemoteUrl { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public enum ReportStatus
    {
        Pending,
        UnderReview,
        Resolved,
        Dismissed,
        ActionTaken
    }

    public enum AdminAction
    {
        None,
        Warning,
        TemporaryBan,
        PermanentBan,
        ContentRemoved
    }

    public static class ReportCategories
    {
        public static readonly List<string> All = new()
        {
            "Spam / Promotional",
            "Harassment / Bullying",
            "Hate Speech",
            "Inappropriate Content",
            "Impersonation",
            "Scam / Fraud",
            "Underage User",
            "Privacy Violation",
            "Violence / Threat",
            "Other"
        };
    }
}