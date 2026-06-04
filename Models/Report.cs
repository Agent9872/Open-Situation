using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Lock.Models
{
    public class Report
    {
        public int Id { get; set; }

        [JsonProperty("reporter_phone")]
        public string ReporterPhone { get; set; } = string.Empty;

        [JsonProperty("reporter_name")]
        public string ReporterName { get; set; } = string.Empty;

        [JsonProperty("reported_user_phone")]
        public string ReportedUserPhone { get; set; } = string.Empty;

        [JsonProperty("reported_user_name")]
        public string ReportedUserName { get; set; } = string.Empty;

        [JsonProperty("reported_user_profile_image")]
        public string ReportedUserProfileImage { get; set; } = string.Empty;

        [JsonProperty("category")]
        public string Category { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("reported_at")]
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("message_screenshot_path")]
        public string MessageScreenshotPath { get; set; } = string.Empty;

        [JsonProperty("conversation_id")]
        public string ConversationId { get; set; } = string.Empty;

        [JsonProperty("reported_message_id_db")]
        public int ReportedMessageIdDb { get; set; } = -1;

        [JsonProperty("reported_message_content")]
        public string ReportedMessageContent { get; set; } = string.Empty;

        [JsonProperty("status")]
        public ReportStatus Status { get; set; } = ReportStatus.Pending;

        [JsonProperty("admin_notes")]
        public string AdminNotes { get; set; } = string.Empty;

        [JsonProperty("resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        [JsonProperty("resolved_by")]
        public string ResolvedBy { get; set; } = string.Empty;

        [JsonProperty("action_taken")]
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

        [JsonProperty("report_id")]
        public int ReportId { get; set; }

        [JsonProperty("local_path")]
        public string LocalPath { get; set; } = string.Empty;

        [JsonProperty("remote_url")]
        public string RemoteUrl { get; set; } = string.Empty;

        [JsonProperty("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    // Enums remain the same
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