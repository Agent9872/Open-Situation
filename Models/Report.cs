// Models/Report.cs
using System;
using System.Collections.Generic;
namespace Lock.Models
{
    public class Report
    {
        public int Id { get; set; }
        public string ReporterPhone { get; set; } = string.Empty;
        public string ReporterName { get; set; } = string.Empty;
        public string ReportedUserPhone { get; set; } = string.Empty;
        public string ReportedUserName { get; set; } = string.Empty;
        public string ReportedUserProfileImage { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
        public string MessageScreenshotPath { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public int ReportedMessageIdDb { get; set; } = -1;
        public string ReportedMessageContent { get; set; } = string.Empty;
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string AdminNotes { get; set; } = string.Empty;
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; } = string.Empty;
        public AdminAction ActionTaken { get; set; } = AdminAction.None;

        // Not persisted - loaded separately
        public List<ReportImage> Images { get; set; } = new();

        // Computed property
        public int? ReportedMessageId
        {
            get => ReportedMessageIdDb == -1 ? (int?)null : ReportedMessageIdDb;
            set => ReportedMessageIdDb = value ?? -1;
        }
    }

    public class ReportImage
    {
        public int Id { get; set; }
        public int ReportId { get; set; }
        public string LocalPath { get; set; } = string.Empty;
        public string RemoteUrl { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
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