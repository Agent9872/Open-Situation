using Lock.Pages.Chat;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Lock.Models
{
    // ═══════════════════════════════════════════════════════════════
    // ENUMS
    // ═══════════════════════════════════════════════════════════════

    public enum GroupType
    {
        CommunityCircle,
        InterestBased,
        SquadDating,
        MoodRoom,
        PrivateGroup,
        EventGroup,
        SupportCircle
    }

    public enum GroupVisibility
    {
        Public,
        Private,
        Secret
    }

    public enum GroupMemberRole
    {
        Creator,
        Admin,
        Moderator,
        Member
    }

    public enum GroupMessageType
    {
        Text,
        Image,
        Voice,
        Poll,
        Event,
        SystemMessage,
        EndorsementRequest
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP MODEL
    // ═══════════════════════════════════════════════════════════════

    public class Group : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CoverImagePath { get; set; } = string.Empty;
        public string CreatedByPhone { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        public GroupType GroupType { get; set; } = GroupType.CommunityCircle;
        public GroupVisibility Visibility { get; set; } = GroupVisibility.Public;

        public string Category { get; set; } = string.Empty;
        public string MoodFilter { get; set; } = string.Empty;

        // Serialized lists stored as JSON
        public string InterestTagsJson { get; set; } = "[]";
        public string RulesJson { get; set; } = "[]";

        public List<string> InterestTags
        {
            get => string.IsNullOrEmpty(InterestTagsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(InterestTagsJson) ?? new();
            set => InterestTagsJson = JsonSerializer.Serialize(value);
        }

        public List<string> Rules
        {
            get => string.IsNullOrEmpty(RulesJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(RulesJson) ?? new();
            set => RulesJson = JsonSerializer.Serialize(value);
        }

        public int MaxMembers { get; set; } = 0;
        public int MemberCount { get; set; } = 0;
        public bool IsAnonymousAllowed { get; set; } = false;
        public bool IsEncrypted { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool RequireApproval { get; set; } = false;

        // Disappearing messages (seconds; 0 = off)
        public int DisappearingMessageSeconds { get; set; } = 0;

        // Last message preview for list display
        public string LastMessagePreview { get; set; } = string.Empty;
        public string LastMessageSenderName { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        // ── Runtime-only (not persisted) ─────────────────────────────────────
        public int CompatibilityScore { get; set; } = 0;
        public int UnreadCount { get; set; } = 0;

        /// <summary>True when the current user is a confirmed, non-banned member.</summary>
        public bool IsMember { get; set; } = false;

        /// <summary>True when the current user has a pending join request awaiting admin approval.</summary>
        public bool IsPendingJoin { get; set; } = false;

        /// <summary>
        /// Holds the GroupJoinRequest.Id for the current user's pending request.
        /// Populated by GetAllGroupsForExploreAsync so the user can cancel it.
        /// </summary>
        public string PendingJoinRequestId { get; set; } = string.Empty;

        public bool IsCreator { get; set; } = false;
        public bool IsAdmin { get; set; } = false;

        // ── Display Helpers ──────────────────────────────────────────────────
        public string GroupTypeDisplay => GroupType switch
        {
            GroupType.CommunityCircle => "Community Circle",
            GroupType.InterestBased => "Interest Based",
            GroupType.SquadDating => "Squad Dating",
            GroupType.MoodRoom => "Mood Room",
            GroupType.PrivateGroup => "Private Group",
            GroupType.EventGroup => "Event Group",
            GroupType.SupportCircle => "Support Circle",
            _ => "Group"
        };

        public string GroupTypeIcon => GroupType switch
        {
            GroupType.CommunityCircle => "🌍",
            GroupType.InterestBased => "🎯",
            GroupType.SquadDating => "👥",
            GroupType.MoodRoom => "🌙",
            GroupType.PrivateGroup => "🔒",
            GroupType.EventGroup => "🎉",
            GroupType.SupportCircle => "💙",
            _ => "💬"
        };

        public string MemberCountDisplay =>
            MaxMembers > 0
                ? $"{MemberCount}/{MaxMembers} members"
                : $"{MemberCount} members";

        public string LastActiveRelative
        {
            get
            {
                var span = DateTime.UtcNow - LastActiveAt;
                if (span.TotalSeconds < 60) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return LastActiveAt.ToString("MMM d");
            }
        }

        public bool HasUnread => UnreadCount > 0;

        public string UnreadDisplay =>
            UnreadCount > 99 ? "99+" : UnreadCount.ToString();

        public bool HasCoverImage =>
            !string.IsNullOrEmpty(CoverImagePath) &&
            System.IO.File.Exists(CoverImagePath);

        public string CompatibilityDisplay =>
            CompatibilityScore > 0 ? $"{CompatibilityScore}% match" : string.Empty;

        /// <summary>
        /// Join-button label shown in Explore:
        ///   "Joined ✓"   → confirmed member
        ///   "Pending…"   → request sent, awaiting admin approval (tap to cancel)
        ///   "Join"       → not a member yet
        /// </summary>
        public string JoinButtonText =>
            IsMember ? "Joined ✓" :
            IsPendingJoin ? "Pending…" :
                            "Join";

        /// <summary>
        /// Matching background colour for the join button.
        ///   Grey   → already joined
        ///   Amber  → pending approval
        ///   Teal   → open to join
        /// </summary>
        public string JoinButtonColor =>
            IsMember ? "#2A2A2A" :
            IsPendingJoin ? "#5C4A00" :
                            "#008080";

        /// <summary>
        /// True only when the user can press Join to send a fresh request.
        /// Pending users tap the button to get the cancel dialog instead.
        /// </summary>
        public bool IsJoinable => !IsMember && !IsPendingJoin;
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP MEMBER
    // ═══════════════════════════════════════════════════════════════

    public class GroupMember : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GroupId { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserProfileImagePath { get; set; } = string.Empty;
        public string AnonymousAlias { get; set; } = string.Empty; // e.g. "Member #7"

        public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastReadAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        public bool IsAnonymous { get; set; } = false;
        public bool IsMuted { get; set; } = false;
        public bool IsBanned { get; set; } = false;

        // ── Display Helpers ──────────────────────────────────────────
        public string DisplayName =>
            IsAnonymous && !string.IsNullOrEmpty(AnonymousAlias)
                ? AnonymousAlias
                : UserName;

        public string RoleDisplay => Role switch
        {
            GroupMemberRole.Creator => "Creator",
            GroupMemberRole.Admin => "Admin",
            GroupMemberRole.Moderator => "Mod",
            _ => string.Empty
        };

        public bool IsPrivileged =>
            Role == GroupMemberRole.Creator ||
            Role == GroupMemberRole.Admin ||
            Role == GroupMemberRole.Moderator;

        public string RoleBadgeColor => Role switch
        {
            GroupMemberRole.Creator => "#D4AF37",
            GroupMemberRole.Admin => "#008080",
            GroupMemberRole.Moderator => "#7F77DD",
            _ => "Transparent"
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP MESSAGE
    // ═══════════════════════════════════════════════════════════════

    public class GroupMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Id { get; set; } = Guid.NewGuid().ToString();  // Changed from int to string

        // Runtime properties
        public bool ShowSenderName { get; set; } = true;
        public string SenderInitial { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderProfileImage { get; set; } = string.Empty;
        public string SenderAnonymousAlias { get; set; } = string.Empty;

        public GroupMessageType MessageType { get; set; } = GroupMessageType.Text;
        public string Content { get; set; } = string.Empty;

        // Encrypted content
        public string EncryptedContent { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; } = false;

        private string? _decryptedContentCache;

        // Poll display properties (not persisted)
        public string PollQuestion { get; set; } = string.Empty;
        public List<PollOptionData> PollOptions { get; set; } = new();
        public string TotalVotesDisplay { get; set; } = "0 votes";
        public bool IsPollExpired { get; set; }
        public bool ShowVoteButton { get; set; }
        public string VoteButtonText { get; set; } = "Vote";
        public string VoteButtonColor { get; set; } = "#008080";

        public string DisplayContent
        {
            get
            {
                if (!IsEncrypted)
                    return Content;

                if (!string.IsNullOrEmpty(_decryptedContentCache))
                    return _decryptedContentCache;

                if (IsSystemMessage)
                    return Content;

                return "🔒 Encrypted message";
            }
            set
            {
                _decryptedContentCache = value;
                OnPropertyChanged();
            }
        }

        public void SetDecryptedContent(string decrypted)
        {
            _decryptedContentCache = decrypted;
            OnPropertyChanged(nameof(DisplayContent));
        }

        // Media
        public string MediaPathsJson { get; set; } = "[]";
        public List<string> MediaPaths
        {
            get => string.IsNullOrEmpty(MediaPathsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(MediaPathsJson) ?? new();
            set => MediaPathsJson = JsonSerializer.Serialize(value);
        }

        // Voice
        public string VoiceAudioPath { get; set; } = string.Empty;
        public double VoiceDurationSeconds { get; set; } = 0;

        // Poll
        public string PollJson { get; set; } = string.Empty;

        // Reply threading - Changed from int to string
        public string? ReplyToMessageId { get; set; } = null;
        public string ReplyToSenderName { get; set; } = string.Empty;
        public string ReplyToPreview { get; set; } = string.Empty;

        // Reactions (JSON map: emoji -> list of phones)
        public string ReactionsJson { get; set; } = "{}";
        public Dictionary<string, List<string>> Reactions
        {
            get => string.IsNullOrEmpty(ReactionsJson)
                ? new Dictionary<string, List<string>>()
                : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(ReactionsJson) ?? new();
            set => ReactionsJson = JsonSerializer.Serialize(value);
        }

        // Metadata
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsEdited { get; set; } = false;
        public DateTime? EditedAt { get; set; } = null;  // Added this field
        public bool IsDeleted { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        public bool IsSystemMessage { get; set; } = false;

        // Disappearing
        public DateTime? DisappearAt { get; set; } = null;

        // Read-by count (for group read receipts)
        public int ReadByCount { get; set; } = 0;
        public string ReadByPhonesJson { get; set; } = "[]";

        // ── Display Helpers ──────────────────────────────────────────
        public string DisplaySenderName =>
            !string.IsNullOrEmpty(SenderAnonymousAlias)
                ? SenderAnonymousAlias
                : SenderName;

        public string TimeDisplay
        {
            get
            {
                var span = DateTime.UtcNow - SentAt;
                if (span.TotalSeconds < 60) return "Just now";
                if (span.TotalMinutes < 60) return SentAt.ToString("h:mm tt");
                if (span.TotalHours < 24) return SentAt.ToString("h:mm tt");
                if (span.TotalDays < 7) return SentAt.ToString("ddd h:mm tt");
                return SentAt.ToString("MMM d");
            }
        }

        public string ContentPreview =>
            IsDeleted ? "🚫 Message deleted" :
            MessageType == GroupMessageType.Image ? "📷 Photo" :
            MessageType == GroupMessageType.Voice ? "🎙️ Voice message" :
            MessageType == GroupMessageType.Poll ? "📊 Poll" :
            MessageType == GroupMessageType.Event ? "📅 Event" :
            Content.Length > 80 ? Content[..80] + "…" : Content;

        public bool HasMedia => MediaPaths.Count > 0;
        public bool HasReply => !string.IsNullOrEmpty(ReplyToMessageId);  // Changed to check string
        public bool HasReactions => Reactions.Count > 0;
        public bool IsVoice => MessageType == GroupMessageType.Voice;
        public bool IsPoll => MessageType == GroupMessageType.Poll;
        public bool ShowAvatar { get; set; } = true;
        public bool IsOutgoing { get; set; } = false;
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP POLL
    // ═══════════════════════════════════════════════════════════════

    public class GroupPoll
    {
        public string Question { get; set; } = string.Empty;
        public List<GroupPollOption> Options { get; set; } = new();
        public bool AllowMultipleVotes { get; set; } = false;
        public DateTime? ExpiresAt { get; set; } = null;

        public bool IsExpired =>
            ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        public int TotalVotes =>
            Options.Sum(o => o.VoterPhones.Count);
    }

    public class GroupPollOption
    {
        public string Text { get; set; } = string.Empty;
        public List<string> VoterPhones { get; set; } = new();
        public int VoteCount => VoterPhones.Count;
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP INVITE
    // ═══════════════════════════════════════════════════════════════

    public class GroupInvite
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GroupId { get; set; } = string.Empty;
        public string InviteCode { get; set; } = string.Empty;
        public string CreatedByPhone { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; } = null;
        public int MaxUses { get; set; } = 0;
        public int UseCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public bool IsExpired =>
            ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        public bool IsUsable =>
            IsActive &&
            !IsExpired &&
            (MaxUses == 0 || UseCount < MaxUses);
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP JOIN REQUEST
    // ═══════════════════════════════════════════════════════════════

    public class GroupJoinRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GroupId { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserProfileImage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "pending"; // pending, approved, rejected
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP EVENT (linked to a group)
    // ═══════════════════════════════════════════════════════════════

    public class GroupEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string GroupId { get; set; } = string.Empty;
        public string CreatedByPhone { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime EventDate { get; set; } = DateTime.UtcNow.AddDays(7);
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int MaxAttendees { get; set; } = 0;
        public string AttendeePhonesJson { get; set; } = "[]";

        public List<string> AttendeePhones
        {
            get => string.IsNullOrEmpty(AttendeePhonesJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(AttendeePhonesJson) ?? new();
            set => AttendeePhonesJson = JsonSerializer.Serialize(value);
        }

        public int AttendeeCount => AttendeePhones.Count;

        public string EventDateDisplay =>
            EventDate.ToString("ddd, MMM d · h:mm tt");
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP PINNED MESSAGE (references GroupMessage.Id)
    // ═══════════════════════════════════════════════════════════════

    public class GroupPinnedMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();  // Changed to string
        public string GroupId { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;  // Changed from int to string
        public string PinnedByPhone { get; set; } = string.Empty;
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
        public string MessagePreview { get; set; } = string.Empty;
    }
}