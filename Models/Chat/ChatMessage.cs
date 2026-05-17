using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SQLite;

namespace Lock.Models.Chat
{
    [Table("ChatMessages")]
    public class ChatMessage : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string ConversationId { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;

        public string? Content { get; set; }

        // Encryption properties - add these new properties
        public bool IsEncrypted { get; set; } = false;
        public string? EncryptionIV { get; set; } // Initialization Vector for AES

        // Message Request Properties
        public bool IsMessageRequest { get; set; } = false;
        public bool IsDeclined { get; set; } = false;

        // Legacy single-media fields
        public string? MediaPath { get; set; }
        public string? MediaType { get; set; }

        // Voice-specific fields
        public int? VoiceDurationSeconds { get; set; }
        public string? VoiceWaveformData { get; set; }

        private bool _isVoiceMessage;

        public string? MessageType { get; set; } // "text", "image", "contact", etc.
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactProfileImage { get; set; }


        // Endorsement related properties
        public string? EndorsementRequestId { get; set; }
        public string? EndorsementRequestorId { get; set; }
        public string? EndorsementRequestorName { get; set; }
        public string? EndorsementTestimonial { get; set; }
        public string? EndorsementRating { get; set; }
        public string? EndorsementStatus { get; set; } // pending, accepted, declined

        public bool IsVoiceMessage
        {
            get => _isVoiceMessage;
            set
            {
                if (_isVoiceMessage != value)
                {
                    _isVoiceMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        // ========== NEW: JSON storage for MediaItems ==========
        // This will be stored in the database as TEXT
        private string? _mediaItemsJson;

        // Add these properties to your ChatMessage class in Lock.Models.Chat

        // Post sharing properties
        private int? _postId;
        public int? PostId
        {
            get => _postId;
            set
            {
                if (_postId != value)
                {
                    _postId = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _postAuthor;
        public string? PostAuthor
        {
            get => _postAuthor;
            set
            {
                if (_postAuthor != value)
                {
                    _postAuthor = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _postPreview;
        public string? PostPreview
        {
            get => _postPreview;
            set
            {
                if (_postPreview != value)
                {
                    _postPreview = value;
                    OnPropertyChanged();
                }
            }
        }

        private int? _postImageCount;
        public int? PostImageCount
        {
            get => _postImageCount;
            set
            {
                if (_postImageCount != value)
                {
                    _postImageCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? MediaItemsJson
        {
            get => _mediaItemsJson;
            set
            {
                _mediaItemsJson = value;
                // When the JSON is set, update the private _mediaItems field
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        _mediaItems = JsonSerializer.Deserialize<List<ChatMediaItem>>(value);
                    }
                    catch
                    {
                        _mediaItems = new List<ChatMediaItem>();
                    }
                }
                else
                {
                    _mediaItems = new List<ChatMediaItem>();
                }
                OnPropertyChanged();
            }
        }
        // ======================================================

        private DateTime _sentAt = DateTime.UtcNow;
        public DateTime SentAt
        {
            get => _sentAt;
            set
            {
                if (_sentAt != value)
                {
                    _sentAt = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isDelivered;
        public bool IsDelivered
        {
            get => _isDelivered;
            set
            {
                if (_isDelivered != value)
                {
                    _isDelivered = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TickSymbol));
                    OnPropertyChanged(nameof(TickColor));
                }
            }
        }

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead != value)
                {
                    _isRead = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TickSymbol));
                    OnPropertyChanged(nameof(TickColor));
                }
            }
        }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isStarred;
        public bool IsStarred
        {
            get => _isStarred;
            set
            {
                if (_isStarred != value)
                {
                    _isStarred = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isEdited;
        public bool IsEdited
        {
            get => _isEdited;
            set
            {
                if (_isEdited != value)
                {
                    _isEdited = value;
                    OnPropertyChanged();
                }
            }
        }

        // ===== DISAPPEARING MESSAGES PROPERTIES =====
        private bool _willDisappear;
        public bool WillDisappear
        {
            get => _willDisappear;
            set
            {
                if (_willDisappear != value)
                {
                    _willDisappear = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime? _expiresAt;
        public DateTime? ExpiresAt
        {
            get => _expiresAt;
            set
            {
                if (_expiresAt != value)
                {
                    _expiresAt = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsExpired));
                    OnPropertyChanged(nameof(TimeUntilDisappear));
                }
            }
        }

        private bool _isDisappearingMessage;
        public bool IsDisappearingMessage
        {
            get => _isDisappearingMessage;
            set
            {
                if (_isDisappearingMessage != value)
                {
                    _isDisappearingMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _disappearAfterSeconds;
        public int DisappearAfterSeconds
        {
            get => _disappearAfterSeconds;
            set
            {
                if (_disappearAfterSeconds != value)
                {
                    _disappearAfterSeconds = value;
                    OnPropertyChanged();
                }
            }
        }

        [Ignore]
        public bool IsFirstInGroup { get; set; } = true;
        [Ignore]
        public bool IsLastInGroup { get; set; } = true;
        [Ignore]
        public bool IsMiddleInGroup => !IsFirstInGroup && !IsLastInGroup;

        private bool _isLocalOutgoing;
        [Ignore]
        public bool IsLocalOutgoing
        {
            get => _isLocalOutgoing;
            set
            {
                if (_isLocalOutgoing != value)
                {
                    _isLocalOutgoing = value;
                    OnPropertyChanged();
                }
            }
        }

        // Voice playback state (runtime only)
        private bool _isVoicePlaying;
        [Ignore]
        public bool IsVoicePlaying
        {
            get => _isVoicePlaying;
            set
            {
                if (_isVoicePlaying != value)
                {
                    _isVoicePlaying = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayDuration));
                }
            }
        }

        private double _voicePlaybackProgress;
        [Ignore]
        public double VoicePlaybackProgress
        {
            get => _voicePlaybackProgress;
            set
            {
                if (Math.Abs(_voicePlaybackProgress - value) > 0.001)
                {
                    _voicePlaybackProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayDuration));
                }
            }
        }

        private bool _isBlocked;
        public bool IsBlocked
        {
            get => _isBlocked;
            set
            {
                if (_isBlocked != value)
                {
                    _isBlocked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TickSymbol));
                    OnPropertyChanged(nameof(TickColor));
                }
            }
        }

        // Add this method to ChatMessage class
        public bool IsVoiceMessageType()
        {
            return IsVoiceMessage ||
                   (!string.IsNullOrEmpty(MediaType) && MediaType == "audio") ||
                   (MediaItems?.Count == 1 && MediaItems[0]?.Type == "audio");
        }

        // Computed properties
        [Ignore]
        public bool HasMedia =>
            !string.IsNullOrEmpty(MediaPath) ||
            (MediaItems?.Count > 0) ||
            IsVoiceMessage;

        [Ignore]
        public bool HasText => !string.IsNullOrWhiteSpace(Content);

        [Ignore]
        public bool IsImageMessage =>
            (HasSingleImage || HasMultipleImages) &&
            (string.Equals(MediaType, "image", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrEmpty(MediaType) ||
             (MediaItems != null && MediaItems.All(m => string.Equals(m.Type, "image", StringComparison.OrdinalIgnoreCase))));

        [Ignore]
        public bool HasSingleImage =>
            !string.IsNullOrEmpty(MediaPath) &&
            (string.Equals(MediaType, "image", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrEmpty(MediaType)) &&
             !IsVoiceMessage;

        [Ignore]
        public bool HasMultipleImages => MediaItems != null && MediaItems.Count > 1 && !IsVoiceMessage;

        [Ignore]
        public bool IsImage =>
            (MediaItems != null && MediaItems.All(m => string.Equals(m.Type, "image", StringComparison.OrdinalIgnoreCase))) ||
            HasSingleImage;

        [Ignore]
        public bool IsVoice =>
            IsVoiceMessage ||
            (!string.IsNullOrEmpty(MediaType) &&
             string.Equals(MediaType, "audio", StringComparison.OrdinalIgnoreCase)) ||
            (MediaItems?.Count == 1 &&
             MediaItems[0] != null &&
             string.Equals(MediaItems[0].Type, "audio", StringComparison.OrdinalIgnoreCase));

        [Ignore]
        public string FormattedDuration
        {
            get
            {
                if (!VoiceDurationSeconds.HasValue || VoiceDurationSeconds.Value <= 0)
                    return "0:00";

                var ts = TimeSpan.FromSeconds(VoiceDurationSeconds.Value);
                return ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                    : $"0:{ts.Seconds:D2}";
            }
        }

        // ===== DISAPPEARING MESSAGES HELPER PROPERTIES =====
        [Ignore]
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

        [Ignore]
        public string TimeUntilDisappear
        {
            get
            {
                if (!WillDisappear || !ExpiresAt.HasValue)
                    return string.Empty;

                var timeLeft = ExpiresAt.Value - DateTime.UtcNow;
                if (timeLeft.TotalSeconds <= 0)
                    return "Expired";

                if (timeLeft.TotalHours >= 24)
                    return $"{timeLeft.Days}d";
                if (timeLeft.TotalHours >= 1)
                    return $"{timeLeft.Hours}h";
                if (timeLeft.TotalMinutes >= 1)
                    return $"{timeLeft.Minutes}m";
                return $"{timeLeft.Seconds}s";
            }
        }

        // ========== MODIFIED: MediaItems property with JSON sync ==========
        private List<ChatMediaItem>? _mediaItems;

        [Ignore]
        public List<ChatMediaItem> MediaItems
        {
            get
            {
                // If we have _mediaItems from JSON deserialization, return that
                if (_mediaItems != null && _mediaItems.Count > 0)
                    return _mediaItems;

                // Otherwise, create from legacy fields if available
                if (!string.IsNullOrEmpty(MediaPath))
                {
                    var legacyList = new List<ChatMediaItem>
                    {
                        new ChatMediaItem
                        {
                            Path = MediaPath!,
                            Type = MediaType ?? (IsVoiceMessage ? "audio" : "image"),
                            DurationSeconds = VoiceDurationSeconds,
                            WaveformData = VoiceWaveformData
                        }
                    };

                    // Update JSON for future saves
                    _mediaItemsJson = JsonSerializer.Serialize(legacyList);
                    return legacyList;
                }

                return new List<ChatMediaItem>();
            }
            set
            {
                _mediaItems = value;
                OnPropertyChanged();

                if (value != null && value.Count > 0)
                {
                    // Update legacy fields for backward compatibility
                    MediaPath = value[0].Path;
                    MediaType = value[0].Type;

                    if (string.Equals(value[0].Type, "audio", StringComparison.OrdinalIgnoreCase))
                    {
                        IsVoiceMessage = true;
                        VoiceDurationSeconds = value[0].DurationSeconds;
                        VoiceWaveformData = value[0].WaveformData;
                    }

                    // Update JSON for database storage
                    _mediaItemsJson = JsonSerializer.Serialize(value);
                }
                else
                {
                    MediaPath = null;
                    MediaType = null;
                    IsVoiceMessage = false;
                    VoiceDurationSeconds = null;
                    VoiceWaveformData = null;
                    _mediaItemsJson = null;
                }
            }
        }
        // ==================================================================

        [Ignore]
        public string TickSymbol
        {
            get
            {
                if (IsBlocked) return "blocked";
                if (IsRead) return "read";
                if (IsDelivered) return "delivered";
                return "sent";
            }
        }

        [Ignore]
        public string DisplayDuration
        {
            get
            {
                if (!VoiceDurationSeconds.HasValue || VoiceDurationSeconds.Value <= 0)
                    return "0:00";

                // If playing, show remaining time
                if (IsVoicePlaying && VoicePlaybackProgress > 0)
                {
                    var elapsedSeconds = VoiceDurationSeconds.Value * VoicePlaybackProgress;
                    var remainingSeconds = VoiceDurationSeconds.Value - elapsedSeconds;
                    var ts = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
                    return ts.TotalMinutes >= 1
                        ? $"{ts.Minutes}:{ts.Seconds:D2}"
                        : $"0:{ts.Seconds:D2}";
                }

                // Otherwise show total duration
                var totalTs = TimeSpan.FromSeconds(VoiceDurationSeconds.Value);
                return totalTs.TotalMinutes >= 1
                    ? $"{totalTs.Minutes}:{totalTs.Seconds:D2}"
                    : $"0:{totalTs.Seconds:D2}";
            }
        }

        [Ignore]
        public Color TickColor
        {
            get
            {
                if (IsBlocked) return Colors.Transparent;
                if (IsRead) return Color.FromArgb("#34B7F1");
                if (IsDelivered) return Color.FromArgb("#8E8E93");
                return Color.FromArgb("#8E8E93");
            }
        }

        [Ignore]
        public int MediaCount => MediaItems?.Count ?? (IsVoiceMessage ? 1 : 0);

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ConversationId) &&
                   !string.IsNullOrEmpty(SenderPhone) &&
                   !string.IsNullOrEmpty(RecipientPhone);
        }

        public string GetEditPreview()
        {
            if (HasText && !string.IsNullOrEmpty(Content))
            {
                return Content.Length > 50 ? Content.Substring(0, 50) + "..." : Content;
            }
            else if (IsImage)
            {
                if (HasMultipleImages)
                {
                    return $"?? {MediaCount} images" + (HasText ? " with caption" : "");
                }
                else
                {
                    return "?? Image" + (HasText ? " with caption" : "");
                }
            }
            else if (IsVoice)
            {
                return $"?? Voice message ({FormattedDuration})" + (HasText ? " with caption" : "");
            }
            return "Message";
        }

        // Add to ChatMessage model
        private string _postAuthorPhone = string.Empty;

        [SQLite.Column("PostAuthorPhone")]
        public string PostAuthorPhone
        {
            get => _postAuthorPhone;
            set
            {
                if (_postAuthorPhone != value)
                {
                    _postAuthorPhone = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _postAuthorProfileImage = string.Empty;

        [SQLite.Ignore] // Don't persist — resolved at runtime
        public string PostAuthorProfileImage
        {
            get => _postAuthorProfileImage;
            set
            {
                if (_postAuthorProfileImage != value)
                {
                    _postAuthorProfileImage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PostAuthorInitial));
                    OnPropertyChanged(nameof(HasPostAuthorImage));
                }
            }
        }

        [SQLite.Ignore]
        public bool HasPostAuthorImage =>
            !string.IsNullOrEmpty(PostAuthorProfileImage) && File.Exists(PostAuthorProfileImage);

        [SQLite.Ignore]
        public string PostAuthorInitial
        {
            get
            {
                if (!string.IsNullOrEmpty(PostAuthor))
                    return PostAuthor.Trim()[0].ToString().ToUpper();
                return "?";
            }
        }

        // ========== MODIFIED: CreateVoiceMessage with MediaItems support ==========
        public static ChatMessage CreateVoiceMessage(
            string conversationId,
            string senderPhone,
            string recipientPhone,
            string audioPath,
            int durationSeconds,
            string? waveformData = null,
            string? caption = null)
        {
            var mediaItem = new ChatMediaItem
            {
                Path = audioPath,
                Type = "audio",
                DurationSeconds = durationSeconds,
                WaveformData = waveformData
            };

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderPhone = senderPhone,
                RecipientPhone = recipientPhone,
                Content = caption,
                MediaPath = audioPath,
                MediaType = "audio",
                IsVoiceMessage = true,
                VoiceDurationSeconds = durationSeconds,
                VoiceWaveformData = waveformData,
                SentAt = DateTime.UtcNow,
                IsDelivered = true,
                IsRead = false,
                IsLocalOutgoing = true,
                IsEncrypted = false, // Will be encrypted when sent
                WillDisappear = false,
                IsDisappearingMessage = false,
                ExpiresAt = null,
                DisappearAfterSeconds = 0,
                MediaItems = new List<ChatMediaItem> { mediaItem }
            };

            // Ensure JSON is set
            message.MediaItemsJson = JsonSerializer.Serialize(message.MediaItems);

            return message;
        }
        // ==========================================================================

        /// <summary>
        /// Mark this message as a disappearing message
        /// </summary>
        public void SetDisappearing(int seconds)
        {
            WillDisappear = true;
            IsDisappearingMessage = true;
            DisappearAfterSeconds = seconds;
            ExpiresAt = DateTime.UtcNow.AddSeconds(seconds);
        }

        // ========== NEW: Helper method to ensure MediaItems is populated ==========
        public void EnsureMediaItemsFromLegacy()
        {
            if ((_mediaItems == null || _mediaItems.Count == 0) && !string.IsNullOrEmpty(MediaPath))
            {
                if (IsVoiceMessage)
                {
                    _mediaItems = new List<ChatMediaItem>
                    {
                        ChatMediaItem.CreateAudio(
                            MediaPath,
                            VoiceDurationSeconds > 0 ? VoiceDurationSeconds.Value : 5,
                            VoiceWaveformData)
                    };
                }
                else if (MediaType == "image")
                {
                    _mediaItems = new List<ChatMediaItem>
                    {
                        ChatMediaItem.CreateImage(MediaPath)
                    };
                }

                if (_mediaItems?.Count > 0)
                {
                    _mediaItemsJson = JsonSerializer.Serialize(_mediaItems);
                    OnPropertyChanged(nameof(MediaItems));
                }
            }
        }
        // ==========================================================================

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
