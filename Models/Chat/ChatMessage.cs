using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Newtonsoft.Json;

namespace Lock.Models.Chat
{
    public class ChatMessage : INotifyPropertyChanged
    {
        // Primary key for Supabase
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string ConversationId { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;

        public string? Content { get; set; }

        // Encryption properties
        public bool IsEncrypted { get; set; } = false;
        public string? EncryptionIV { get; set; }

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

        public string? MessageType { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactProfileImage { get; set; }

        // Endorsement related properties
        public string? EndorsementRequestId { get; set; }
        public string? EndorsementRequestorId { get; set; }
        public string? EndorsementRequestorName { get; set; }
        public string? EndorsementTestimonial { get; set; }
        public string? EndorsementRating { get; set; }
        public string? EndorsementStatus { get; set; }

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

        private string? _postAuthorPhone = string.Empty;
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

        // JSON storage for MediaItems
        private string? _mediaItemsJson;
        public string? MediaItemsJson
        {
            get => _mediaItemsJson;
            set
            {
                _mediaItemsJson = value;
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        _mediaItems = System.Text.Json.JsonSerializer.Deserialize<List<ChatMediaItem>>(value);
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

        // Disappearing messages properties
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

        // ?? RUNTIME ONLY — never sent to Supabase ??????????????????????????

        [JsonIgnore]
        private List<ChatMediaItem>? _mediaItems;

        [JsonIgnore]
        public List<ChatMediaItem> MediaItems
        {
            get
            {
                if (_mediaItems != null && _mediaItems.Count > 0)
                    return _mediaItems;

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
                    _mediaItemsJson = System.Text.Json.JsonSerializer.Serialize(legacyList);
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
                    MediaPath = value[0].Path;
                    MediaType = value[0].Type;

                    if (string.Equals(value[0].Type, "audio", StringComparison.OrdinalIgnoreCase))
                    {
                        IsVoiceMessage = true;
                        VoiceDurationSeconds = value[0].DurationSeconds;
                        VoiceWaveformData = value[0].WaveformData;
                    }

                    _mediaItemsJson = System.Text.Json.JsonSerializer.Serialize(value);
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

        [JsonIgnore]
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
        private bool _isLocalOutgoing;

        [JsonIgnore]
        public bool IsFirstInGroup { get; set; } = true;

        [JsonIgnore]
        public bool IsLastInGroup { get; set; } = true;

        [JsonIgnore]
        public bool IsMiddleInGroup => !IsFirstInGroup && !IsLastInGroup;

        [JsonIgnore]
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
        private bool _isVoicePlaying;

        [JsonIgnore]
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
        private double _voicePlaybackProgress;

        [JsonIgnore]
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
        private string _postAuthorProfileImage = string.Empty;

        // ?? COMPUTED / UI PROPERTIES (not persisted) ???????????????????????

        [JsonIgnore]
        public bool HasPostAuthorImage => !string.IsNullOrEmpty(PostAuthorProfileImage) && File.Exists(PostAuthorProfileImage);

        [JsonIgnore]
        public string PostAuthorInitial
        {
            get
            {
                if (!string.IsNullOrEmpty(PostAuthor))
                    return PostAuthor.Trim()[0].ToString().ToUpper();
                return "?";
            }
        }

        [JsonIgnore]
        public bool HasMedia => !string.IsNullOrEmpty(MediaPath) || (MediaItems?.Count > 0) || IsVoiceMessage;

        [JsonIgnore]
        public bool HasText => !string.IsNullOrWhiteSpace(Content);

        [JsonIgnore]
        public bool IsImageMessage => (HasSingleImage || HasMultipleImages) &&
            (string.Equals(MediaType, "image", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrEmpty(MediaType) ||
             (MediaItems != null && MediaItems.All(m => string.Equals(m.Type, "image", StringComparison.OrdinalIgnoreCase))));

        [JsonIgnore]
        public bool HasSingleImage => !string.IsNullOrEmpty(MediaPath) &&
            (string.Equals(MediaType, "image", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrEmpty(MediaType)) && !IsVoiceMessage;

        [JsonIgnore]
        public bool HasMultipleImages => MediaItems != null && MediaItems.Count > 1 && !IsVoiceMessage;

        [JsonIgnore]
        public bool IsImage => (MediaItems != null && MediaItems.All(m => string.Equals(m.Type, "image", StringComparison.OrdinalIgnoreCase))) || HasSingleImage;

        [JsonIgnore]
        public bool IsVoice => IsVoiceMessage ||
            (!string.IsNullOrEmpty(MediaType) && string.Equals(MediaType, "audio", StringComparison.OrdinalIgnoreCase)) ||
            (MediaItems?.Count == 1 && MediaItems[0] != null && string.Equals(MediaItems[0].Type, "audio", StringComparison.OrdinalIgnoreCase));

        [JsonIgnore]
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

        [JsonIgnore]
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;

        [JsonIgnore]
        public string TimeUntilDisappear
        {
            get
            {
                if (!WillDisappear || !ExpiresAt.HasValue) return string.Empty;
                var timeLeft = ExpiresAt.Value - DateTime.UtcNow;
                if (timeLeft.TotalSeconds <= 0) return "Expired";
                if (timeLeft.TotalHours >= 24) return $"{timeLeft.Days}d";
                if (timeLeft.TotalHours >= 1) return $"{timeLeft.Hours}h";
                if (timeLeft.TotalMinutes >= 1) return $"{timeLeft.Minutes}m";
                return $"{timeLeft.Seconds}s";
            }
        }

        [JsonIgnore]
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

        [JsonIgnore]
        public string DisplayDuration
        {
            get
            {
                if (!VoiceDurationSeconds.HasValue || VoiceDurationSeconds.Value <= 0)
                    return "0:00";

                if (IsVoicePlaying && VoicePlaybackProgress > 0)
                {
                    var elapsedSeconds = VoiceDurationSeconds.Value * VoicePlaybackProgress;
                    var remainingSeconds = VoiceDurationSeconds.Value - elapsedSeconds;
                    var ts = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
                    return ts.TotalMinutes >= 1
                        ? $"{ts.Minutes}:{ts.Seconds:D2}"
                        : $"0:{ts.Seconds:D2}";
                }

                var totalTs = TimeSpan.FromSeconds(VoiceDurationSeconds.Value);
                return totalTs.TotalMinutes >= 1
                    ? $"{totalTs.Minutes}:{totalTs.Seconds:D2}"
                    : $"0:{totalTs.Seconds:D2}";
            }
        }

        [JsonIgnore]
        public Microsoft.Maui.Graphics.Color TickColor
        {
            get
            {
                if (IsBlocked) return Microsoft.Maui.Graphics.Colors.Transparent;
                if (IsRead) return Microsoft.Maui.Graphics.Color.FromArgb("#34B7F1");
                if (IsDelivered) return Microsoft.Maui.Graphics.Color.FromArgb("#8E8E93");
                return Microsoft.Maui.Graphics.Color.FromArgb("#8E8E93");
            }
        }

        [JsonIgnore]
        public int MediaCount => MediaItems?.Count ?? (IsVoiceMessage ? 1 : 0);

        [JsonIgnore]
        public string GiftEmoji
        {
            get
            {
                if (MessageType != "gift" || string.IsNullOrEmpty(Content)) return string.Empty;
                var def = Lock.Models.GiftDefinition.FindById(Content);
                return def?.Name ?? "Gift";
            }
        }

        [JsonIgnore]
        public string GiftName
        {
            get
            {
                if (MessageType != "gift" || string.IsNullOrEmpty(Content)) return string.Empty;
                var def = Lock.Models.GiftDefinition.FindById(Content);
                return def?.Name ?? "Gift";
            }
        }

        // ?? METHODS ???????????????????????????????????????????????????????

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ConversationId) &&
                   !string.IsNullOrEmpty(SenderPhone) &&
                   !string.IsNullOrEmpty(RecipientPhone);
        }

        public bool IsVoiceMessageType()
        {
            return IsVoiceMessage ||
                   (!string.IsNullOrEmpty(MediaType) && MediaType == "audio") ||
                   (MediaItems?.Count == 1 && MediaItems[0]?.Type == "audio");
        }

        public string GetEditPreview()
        {
            if (HasText && !string.IsNullOrEmpty(Content))
                return Content.Length > 50 ? Content.Substring(0, 50) + "..." : Content;
            else if (IsImage)
                return HasMultipleImages ? $"?? {MediaCount} images" : "?? Image";
            else if (IsVoice)
                return $"?? Voice message ({FormattedDuration})";
            return "Message";
        }

        public void SetDisappearing(int seconds)
        {
            WillDisappear = true;
            IsDisappearingMessage = true;
            DisappearAfterSeconds = seconds;
            ExpiresAt = DateTime.UtcNow.AddSeconds(seconds);
        }

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
                    _mediaItemsJson = System.Text.Json.JsonSerializer.Serialize(_mediaItems);
                    OnPropertyChanged(nameof(MediaItems));
                }
            }
        }

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
                IsEncrypted = false,
                WillDisappear = false,
                IsDisappearingMessage = false,
                ExpiresAt = null,
                DisappearAfterSeconds = 0,
                MediaItems = new List<ChatMediaItem> { mediaItem }
            };

            message.MediaItemsJson = System.Text.Json.JsonSerializer.Serialize(message.MediaItems);
            return message;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}