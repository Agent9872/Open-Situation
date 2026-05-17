using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using SQLite;
using Microsoft.Maui.Graphics;

namespace Lock.Models
{
    [Table("Comments")]
    public class Comment : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // The post this comment belongs to
        public int PostId { get; set; }

        // For nested comments - if this is a reply to another comment
        public int? ParentCommentId { get; set; }

        // Author info
        public string AuthorPhone { get; set; } = string.Empty;

        [Ignore]
        public string AuthorDisplayName { get; set; } = string.Empty;

        [Ignore]
        public string AuthorProfileImagePath { get; set; } = string.Empty;

        // Comment content
        public string Content { get; set; } = string.Empty;

        // Timestamp
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Love reactions for comments
        public int LoveCount { get; set; } = 0;

        // Store which users have loved this comment (JSON array)
        public string LovedByJson { get; set; } = "[]";

        // For UI binding

        private bool _isLovedByCurrentUser;
        [Ignore]
        public bool IsLovedByCurrentUser
        {
            get => _isLovedByCurrentUser;
            set
            {
                if (_isLovedByCurrentUser != value)
                {
                    _isLovedByCurrentUser = value;
                    OnPropertyChanged(nameof(IsLovedByCurrentUser));
                    OnPropertyChanged(nameof(LoveIconColor));
                    OnPropertyChanged(nameof(LoveIcon));
                }
            }
        }

        [Ignore]
        public Color LoveIconColor => IsLovedByCurrentUser ? Color.FromArgb("#C05050") : Color.FromArgb("#888888");

        [Ignore]
        public string LoveIcon => IsLovedByCurrentUser ? "❤️" : "🤍";

        [Ignore]
        public string LoveCountDisplay => LoveCount > 0 ? LoveCount.ToString() : string.Empty;

        // Helper to get/set loved by list
        [Ignore]
        public List<string> LovedBy
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(LovedByJson))
                        return new List<string>();
                    return JsonSerializer.Deserialize<List<string>>(LovedByJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                LovedByJson = JsonSerializer.Serialize(value ?? new List<string>());
                LoveCount = value?.Count ?? 0;
                OnPropertyChanged(nameof(LovedBy));
                OnPropertyChanged(nameof(LoveCount));
                OnPropertyChanged(nameof(LoveCountDisplay));
            }
        }

        // Method to toggle love
        public void ToggleLove(string userPhone)
        {
            var lovedBy = LovedBy;

            if (lovedBy.Contains(userPhone))
            {
                lovedBy.Remove(userPhone);
                IsLovedByCurrentUser = false;
            }
            else
            {
                lovedBy.Add(userPhone);
                IsLovedByCurrentUser = true;
            }

            LovedBy = lovedBy;
        }

        // Nested comments (replies)
        [Ignore]
        public List<Comment> Replies { get; set; } = new List<Comment>();

        [Ignore]
        public bool HasReplies => Replies.Any();

        [Ignore]
        public int ReplyCount => Replies.Count;

        [Ignore]
        public string ReplyCountDisplay => ReplyCount > 0 ? $"{ReplyCount} {(ReplyCount == 1 ? "reply" : "replies")}" : "";

        // In your Comment class
        private bool _isOwnedByCurrentUser;
        [Ignore]
        public bool IsOwnedByCurrentUser
        {
            get => _isOwnedByCurrentUser;
            set
            {
                if (_isOwnedByCurrentUser != value)
                {
                    _isOwnedByCurrentUser = value;
                    OnPropertyChanged(nameof(IsOwnedByCurrentUser));
                    OnPropertyChanged(nameof(ShowMenuButton)); // This is important
                }
            }
        }

        [Ignore]
        public bool ShowMenuButton => IsOwnedByCurrentUser;

      
        // For UI - expand/collapse replies
        private bool _areRepliesExpanded;
        [Ignore]
        public bool AreRepliesExpanded
        {
            get => _areRepliesExpanded;
            set
            {
                if (_areRepliesExpanded != value)
                {
                    _areRepliesExpanded = value;
                    OnPropertyChanged(nameof(AreRepliesExpanded));
                    OnPropertyChanged(nameof(RepliesVisibility));
                    OnPropertyChanged(nameof(ExpandCollapseIcon));
                }
            }
        }

        [Ignore]
        public bool RepliesVisibility => AreRepliesExpanded && HasReplies;

        [Ignore]
        public string ExpandCollapseIcon => AreRepliesExpanded ? "▼" : "▶";

        // For UI
        [Ignore]
        public string CreatedAtRelative => GetRelativeTime(CreatedAt);

        private static string GetRelativeTime(DateTime utcTime)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (utcTime.Kind == DateTimeKind.Unspecified)
                    utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);

                var ts = nowUtc - utcTime;
                if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;

                if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds}s";
                if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}m";
                if (ts.TotalHours < 24) return $"{(int)ts.TotalHours}h";
                if (ts.TotalDays < 7) return $"{(int)ts.TotalDays}d";

                var local = utcTime.ToLocalTime();
                if (nowUtc.Year == utcTime.Year)
                    return local.ToString("MMM d");

                return local.ToString("MMM d, yyyy");
            }
            catch
            {
                return string.Empty;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}