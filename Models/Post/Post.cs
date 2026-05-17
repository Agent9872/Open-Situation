// Added Category property (keeps same style as other persisted properties)
using System;
using System.Text.Json;
using System.ComponentModel;
using SQLite;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Lock.Pages.Post;

namespace Lock.Models
{
    [Table("Posts")]
    public class Post : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Author identifier (stores phone like User.PhoneNumber)
        public string AuthorPhone { get; set; } = string.Empty;

        // Text content of the post
        public string Content { get; set; } = string.Empty;

        // Category for the post (optional)
        public string Category { get; set; } = string.Empty;

        // Add these properties to your Post class (near the Category property)
        public string Visibility { get; set; } = "Everyone";  // "Everyone" or "By Mood"

        [Ignore]
        public string AuthorMood { get; set; } = string.Empty;

        // Stored as JSON array of local file paths
        public string ImagePathsJson { get; set; } = "[]";

        // Mood for the post/status (new)
        public string Mood { get; set; } = string.Empty;

        // Mood last updated timestamp - this will be populated from User model when loading
        [Ignore]
        public DateTime MoodLastUpdated { get; set; } = DateTime.UtcNow;

        // Created timestamp (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Computed relative time string for UI (e.g. "12m", "1h", "2d", "Mar 3")
        [Ignore]
        public string CreatedAtRelative => GetRelativeTime(CreatedAt);

        // Added StatusImagePath to store a separate "status" image (keeps same style as other persisted properties)
        public string StatusImagePath { get; set; } = string.Empty;

        public string AuthorLookingFor { get; set; } = string.Empty;

        // Add this property
        public string AuthorProfileImagePath { get; set; }

        public bool IsCurrentUserPost { get; set; }
        public string AuthorDisplayName { get; set; }

        // Add these to your existing Post class
        [Ignore]
        public string SearchQuery { get; set; } = string.Empty;

        [Ignore]
        public DateTime SearchTime { get; set; }

        [Ignore]
        public int SearchResultCount { get; set; }

        // Computed properties
        [Ignore]
        public string SearchDisplayTime => SearchTime.ToString("hh:mm tt");

        [Ignore]
        public string SearchDisplayResultCount => SearchResultCount > 0 ? $"({SearchResultCount})" : "(no results)";

        [Ignore]
        public bool SearchHasResults => SearchResultCount > 0;

        [Ignore]
        public string Country { get; set; } = string.Empty;

        [Ignore]
        public string State { get; set; } = string.Empty;

        [Ignore]
        public double? Latitude { get; set; }

        [Ignore]
        public double? Longitude { get; set; }

        // Add this property to your Post class
        public string HiddenByJson { get; set; } = "[]";

        // ?? Link preview properties ??????????????????????????????????????
        [Ignore]
        public string? FirstUrl => ExtractFirstUrl(Content);


        [Ignore]
        public string LinkDomain
        {
            get
            {
                var url = FirstUrl;
                if (string.IsNullOrEmpty(url)) return string.Empty;
                try
                {
                    if (!url.StartsWith("http")) url = "https://" + url;
                    return new Uri(url).Host.Replace("www.", "");
                }
                catch { return url; }
            }
        }

        // Live-updated by LinkPreviewService — triggers UI refresh
        private LinkPreviewData? _linkPreview;

        [Ignore]
        public LinkPreviewData? LinkPreview
        {
            get => _linkPreview;
            set
            {
                _linkPreview = value;
                OnPropertyChanged(nameof(LinkPreview));
                OnPropertyChanged(nameof(PreviewTitle));
                OnPropertyChanged(nameof(PreviewDescription));
                OnPropertyChanged(nameof(PreviewImageUrl));
                OnPropertyChanged(nameof(PreviewSiteName));
                OnPropertyChanged(nameof(PreviewFaviconUrl));
                OnPropertyChanged(nameof(PreviewHasImage));
            }
        }

        [Ignore] public string PreviewTitle => LinkPreview?.Title ?? FirstUrl ?? string.Empty;
        [Ignore] public string PreviewDescription => LinkPreview?.Description ?? string.Empty;
        [Ignore] public string PreviewImageUrl => LinkPreview?.ImageUrl ?? string.Empty;
        [Ignore] public string PreviewSiteName => LinkPreview?.SiteName ?? LinkDomain.ToUpperInvariant();
        [Ignore] public string PreviewFaviconUrl => LinkPreview?.FaviconUrl ?? string.Empty;
        [Ignore] public bool PreviewHasImage => !string.IsNullOrEmpty(PreviewImageUrl);

        private static string? ExtractFirstUrl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var m = System.Text.RegularExpressions.Regex.Match(
                text,
                @"(https?://[^\s]+|www\.[a-zA-Z0-9\-]+\.[^\s]{2,}|[a-zA-Z0-9\-]+\.(com|org|net|io|co|app|dev|ai|me|tv|gg|ly|uk|ng|us|ca|au)[^\s]*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return m.Success ? m.Value.TrimEnd('.', ',', ')', ']', '/') : null;
        }

        [Ignore]
        public string ContentWithoutUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content)) return string.Empty;

                var url = FirstUrl;
                if (string.IsNullOrEmpty(url)) return Content;

                // Remove the URL and clean up leftover whitespace/newlines
                var cleaned = Content
                    .Replace(url, string.Empty)
                    .Trim()
                    .TrimEnd('\n', '\r')
                    .Trim();

                return cleaned;
            }
        }

        [Ignore]
        public bool HasTextBesideUrl => HasLinkPreview && !string.IsNullOrWhiteSpace(ContentWithoutUrl);

        [Ignore]
        public bool HasLinkPreview => FirstUrl != null;

        // Add this property near your other properties (around line 20-30)
        private int _matchPercent;
        [Ignore]
        public int MatchPercent
        {
            get => _matchPercent;
            set
            {
                if (_matchPercent != value)
                {
                    _matchPercent = value;
                    OnPropertyChanged(nameof(MatchPercent));
                }
            }
        }

        // Add these properties to your Post class (near the Love reactions section)
        // Spark reactions
        public int SparkCount { get; set; } = 0;

        // Store which users have sparked this post (JSON array of user phones)
        public string SparkedByJson { get; set; } = "[]";

        // For UI binding - check if current user sparked this post
        private bool _isSparkedByCurrentUser;
        [Ignore]
        public bool IsSparkedByCurrentUser
        {
            get => _isSparkedByCurrentUser;
            set
            {
                if (_isSparkedByCurrentUser != value)
                {
                    _isSparkedByCurrentUser = value;
                    OnPropertyChanged(nameof(IsSparkedByCurrentUser));
                    OnPropertyChanged(nameof(SparkIconColor));
                }
            }
        }

        [Ignore]
        public Color SparkIconColor => IsSparkedByCurrentUser ? Color.FromArgb("#FFA500") : Color.FromArgb("#888888");

        [Ignore]
        public string SparkCountDisplay => SparkCount > 0 ? SparkCount.ToString() : string.Empty;

        private bool _isAuthorVerified;
        public bool IsAuthorVerified
        {
            get => _isAuthorVerified;
            set
            {
                if (_isAuthorVerified != value)
                {
                    _isAuthorVerified = value;
                    OnPropertyChanged(nameof(IsAuthorVerified));
                }
            }
        }

        // Helper to get/set sparked by list
        [Ignore]
        public List<string> SparkedBy
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(SparkedByJson))
                        return new List<string>();
                    return JsonSerializer.Deserialize<List<string>>(SparkedByJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                SparkedByJson = JsonSerializer.Serialize(value ?? new List<string>());
                SparkCount = value?.Count ?? 0;
                OnPropertyChanged(nameof(SparkedBy));
                OnPropertyChanged(nameof(SparkCount));
                OnPropertyChanged(nameof(SparkCountDisplay));
            }
        }

        // Method to toggle spark
        public void ToggleSpark(string userPhone)
        {
            var sparkedBy = SparkedBy;

            if (sparkedBy.Contains(userPhone))
            {
                sparkedBy.Remove(userPhone);
                IsSparkedByCurrentUser = false;
            }
            else
            {
                sparkedBy.Add(userPhone);
                IsSparkedByCurrentUser = true;
            }

            SparkedBy = sparkedBy;
        }

        public void RefreshSparkState()
        {
            OnPropertyChanged(nameof(IsSparkedByCurrentUser));
            OnPropertyChanged(nameof(SparkIconColor));
            OnPropertyChanged(nameof(SparkCountDisplay));
            OnPropertyChanged(nameof(SparkCount));
        }

        private bool _isSavedByCurrentUser;
        public bool IsSavedByCurrentUser
        {
            get => _isSavedByCurrentUser;
            set
            {
                if (_isSavedByCurrentUser != value)
                {
                    _isSavedByCurrentUser = value;
                    OnPropertyChanged(nameof(IsSavedByCurrentUser));
                    OnPropertyChanged(nameof(SaveIconFill));
                }
            }
        }

        public string SaveIconFill => IsSavedByCurrentUser ? "#FFD24D" : "#888888";

        // Add this ignore property for easy access
        [Ignore]
        public List<string> HiddenBy
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(HiddenByJson))
                        return new List<string>();
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(HiddenByJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                HiddenByJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
                OnPropertyChanged(nameof(HiddenBy));
            }
        }

        // Add this method to check if a user has hidden this post
        public bool IsHiddenByUser(string userPhone)
        {
            return HiddenBy.Contains(userPhone);
        }

        // Add these public methods to your Post class in Post.cs
        public void RefreshLoveState()
        {
            OnPropertyChanged(nameof(IsLovedByCurrentUser));
            OnPropertyChanged(nameof(LoveIconColor));
            OnPropertyChanged(nameof(LoveIcon));
            OnPropertyChanged(nameof(LoveCountDisplay));
            OnPropertyChanged(nameof(LoveCount));
        }

        public void RefreshAllProperties()
        {
            OnPropertyChanged(nameof(IsLovedByCurrentUser));
            OnPropertyChanged(nameof(LoveIconColor));
            OnPropertyChanged(nameof(LoveIcon));
            OnPropertyChanged(nameof(LoveCountDisplay));
            OnPropertyChanged(nameof(LoveCount));
            OnPropertyChanged(nameof(Content));
            OnPropertyChanged(nameof(DisplayContent));
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(DisplayToggleText));
            OnPropertyChanged(nameof(NeedsToggle));
        }

        [Ignore]
        public bool IsMoodRecent
        {
            get
            {
                try
                {
                    var timeSpan = DateTime.UtcNow - MoodLastUpdated;
                    return timeSpan.TotalHours < 1;
                }
                catch
                {
                    return false;
                }
            }
        }

        // NEW: Relative time for mood last updated
        private string _moodLastUpdatedRelative;
        [Ignore]
        public string MoodLastUpdatedRelative
        {
            get => _moodLastUpdatedRelative;
            set
            {
                if (_moodLastUpdatedRelative != value)
                {
                    _moodLastUpdatedRelative = value;
                    OnPropertyChanged(nameof(MoodLastUpdatedRelative));
                }
            }
        }

        // NEW: Formatted mood display with timer (for XAML binding)
        [Ignore]
        public FormattedString MoodDisplayFormatted
        {
            get
            {
                var fs = new FormattedString();

                if (!string.IsNullOrEmpty(Mood))
                {
                    // Add mood text
                    var moodSpan = new Span
                    {
                        Text = Mood,
                        TextColor = Color.FromArgb("#B00020"),
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 13
                    };
                    fs.Spans.Add(moodSpan);

                    // Add separator and timer if available
                    if (!string.IsNullOrEmpty(MoodLastUpdatedRelative))
                    {
                        var separatorSpan = new Span
                        {
                            Text = " · ",
                            TextColor = Color.FromArgb("#888888"),
                            FontSize = 11
                        };
                        fs.Spans.Add(separatorSpan);

                        var timerSpan = new Span
                        {
                            Text = MoodLastUpdatedRelative,
                            TextColor = Color.FromArgb("#888888"),
                            FontSize = 11
                        };
                        fs.Spans.Add(timerSpan);
                    }
                }

                return fs;
            }
        }

        // Helper (ignored by SQLite) to expose paths as string[]
        [Ignore]
        public string[] ImagePathsList
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(ImagePathsJson))
                        return Array.Empty<string>();

                    var arr = JsonSerializer.Deserialize<string[]?>(ImagePathsJson);
                    return arr ?? Array.Empty<string>();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
            set
            {
                ImagePathsJson = JsonSerializer.Serialize(value ?? Array.Empty<string>());
                OnPropertyChanged(nameof(ImagePathsList));
            }
        }

        // UI-only: whether content is expanded (not persisted)
        private bool _isExpanded;
        [Ignore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(DisplayToggleText));
                UpdateDisplayContent();
            }
        }

        // UI-only: the text actually displayed (used when no toggle needed)
        private string _displayContent = string.Empty;
        [Ignore]
        public string DisplayContent
        {
            get => _displayContent;
            set
            {
                if (_displayContent == value) return;
                _displayContent = value;
                OnPropertyChanged(nameof(DisplayContent));
            }
        }

        public void NotifyCommentCountChanged()
        {
            OnPropertyChanged(nameof(CommentCount));
            OnPropertyChanged(nameof(CommentCountDisplay));
        }

        // UI-only: split parts for FormattedString (first part + remaining)
        private string _truncatedPart = string.Empty;
        [Ignore]
        public string TruncatedPart
        {
            get => _truncatedPart;
            set
            {
                if (_truncatedPart == value) return;
                _truncatedPart = value;
                OnPropertyChanged(nameof(TruncatedPart));
            }
        }

        private string _hiddenPart = string.Empty;
        [Ignore]
        public string HiddenPart
        {
            get => _hiddenPart;
            set
            {
                if (_hiddenPart == value) return;
                _hiddenPart = value;
                OnPropertyChanged(nameof(HiddenPart));
            }
        }


        // UI-only: computed toggle text
        [Ignore]
        public string DisplayToggleText => IsExpanded ? "Show less" : "Show all";

        // UI-only: whether content needs a toggle button
        [Ignore]
        public bool NeedsToggle => !string.IsNullOrEmpty(Content) && Content.Length > 200;

        // FormattedString properties (for binding directly to Label.FormattedText)
        // DisplayFormatted: used when there is no toggle (full content)
        [Ignore]
        public FormattedString DisplayFormatted { get; private set; } = new FormattedString();

        // ToggleFormatted: used when a toggle exists; combines TruncatedPart + HiddenPart
        [Ignore]
        public FormattedString ToggleFormatted { get; private set; } = new FormattedString();

        /// <summary>
        /// Formats text with hashtags highlighted in blue - catches ALL hashtags anywhere
        /// </summary>
        private FormattedString FormatTextWithHashtags(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new FormattedString { Spans = { new Span { Text = text ?? "", TextColor = Color.FromArgb("#F0EDE8") } } };

            var formattedString = new FormattedString();

            var hashtagPattern = new System.Text.RegularExpressions.Regex(@"#\w+");
            var lastIndex = 0;
            var matches = hashtagPattern.Matches(text);

            if (matches.Count == 0)
            {
                formattedString.Spans.Add(new Span
                {
                    Text = text,
                    TextColor = Color.FromArgb("#F0EDE8")
                });
                return formattedString;
            }

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Add text before the hashtag
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    formattedString.Spans.Add(new Span
                    {
                        Text = beforeText,
                        TextColor = Color.FromArgb("#F0EDE8")
                    });
                }

                // Add the hashtag with click handler
                var hashtagSpan = new Span
                {
                    Text = match.Value,
                    TextColor = Color.FromArgb("#1da1f2"), // Twitter blue
                    FontAttributes = FontAttributes.Bold
                };

                // Add tap gesture to the span
                var tapGesture = new TapGestureRecognizer();
                var hashtagText = match.Value; // Capture the hashtag text
                tapGesture.Tapped += async (s, e) =>
                {
                    // Navigate to SearchPage with the hashtag
                    var searchPage = new SearchPage();
                    await Application.Current.MainPage.Navigation.PushAsync(searchPage);

                    // Set the search text after a short delay to ensure page is loaded
                    await Task.Delay(100);
                    searchPage.SetSearchText(hashtagText);
                };
                hashtagSpan.GestureRecognizers.Add(tapGesture);

                formattedString.Spans.Add(hashtagSpan);

                lastIndex = match.Index + match.Length;
            }

            // Add any remaining text after the last hashtag
            if (lastIndex < text.Length)
            {
                var afterText = text.Substring(lastIndex);
                formattedString.Spans.Add(new Span
                {
                    Text = afterText,
                    TextColor = Color.FromArgb("#F0EDE8")
                });
            }

            return formattedString;
        }
        
        // Updated UpdateDisplayContent with hashtag highlighting
        public void UpdateDisplayContent(int limit = 200)
        {
            if (string.IsNullOrEmpty(Content))
            {
                DisplayContent = string.Empty;
                TruncatedPart = string.Empty;
                HiddenPart = string.Empty;
                DisplayFormatted = new FormattedString();
                ToggleFormatted = new FormattedString();
                OnPropertyChanged(nameof(DisplayFormatted));
                OnPropertyChanged(nameof(ToggleFormatted));
                OnPropertyChanged(nameof(NeedsToggle));
                OnPropertyChanged(nameof(DisplayToggleText));
                return;
            }

            if (Content.Length <= limit)
            {
                // short content: show everything with hashtag formatting
                DisplayContent = Content;
                TruncatedPart = Content;
                HiddenPart = string.Empty;
                DisplayFormatted = FormatTextWithHashtags(DisplayContent);
                ToggleFormatted = DisplayFormatted; // IMPORTANT: Set ToggleFormatted too
            }
            else
            {
                var first = Content.Substring(0, Math.Min(limit, Content.Length)).TrimEnd();
                var rest = Content.Substring(Math.Min(limit, Content.Length)).TrimStart();

                if (IsExpanded)
                {
                    // expanded: full content visible with hashtag formatting
                    DisplayContent = Content;
                    TruncatedPart = first;
                    HiddenPart = rest;
                    DisplayFormatted = FormatTextWithHashtags(DisplayContent);
                    // For expanded state, ToggleFormatted should show full content
                    ToggleFormatted = DisplayFormatted;
                }
                else
                {
                    // collapsed: show truncated preview with ellipsis with hashtag formatting
                    DisplayContent = first + "…";
                    TruncatedPart = first + "…";
                    HiddenPart = string.Empty;
                    DisplayFormatted = FormatTextWithHashtags(DisplayContent);

                    // Build toggle formatted for collapsed state
                    var truncatedFs = FormatTextWithHashtags(TruncatedPart);
                    ToggleFormatted = truncatedFs;
                }
            }

            OnPropertyChanged(nameof(DisplayContent));
            OnPropertyChanged(nameof(TruncatedPart));
            OnPropertyChanged(nameof(HiddenPart));
            OnPropertyChanged(nameof(DisplayFormatted));
            OnPropertyChanged(nameof(ToggleFormatted));
            OnPropertyChanged(nameof(DisplayToggleText));
            OnPropertyChanged(nameof(NeedsToggle));
            OnPropertyChanged(nameof(CreatedAtRelative));
            OnPropertyChanged(nameof(MoodDisplayFormatted));
        }
        // NEW: Update mood and its relative time
        public void UpdateMood(string newMood, DateTime? lastUpdated = null)
        {
            if (Mood != newMood)
            {
                Mood = newMood;
                MoodLastUpdated = lastUpdated ?? DateTime.UtcNow;
                UpdateMoodRelativeTime();
                OnPropertyChanged(nameof(Mood));
                OnPropertyChanged(nameof(MoodDisplayFormatted));
            }
        }

        // NEW: Update the relative time string for mood
        public void UpdateMoodRelativeTime()
        {
            MoodLastUpdatedRelative = GetMoodRelativeTime(MoodLastUpdated);
            OnPropertyChanged(nameof(MoodLastUpdatedRelative));
            OnPropertyChanged(nameof(MoodDisplayFormatted));
        }

        // NEW: Get relative time string specifically for mood
        private static string GetMoodRelativeTime(DateTime moodTime)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (moodTime.Kind == DateTimeKind.Unspecified)
                {
                    moodTime = DateTime.SpecifyKind(moodTime, DateTimeKind.Utc);
                }

                var ts = nowUtc - moodTime;
                if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;

                if (ts.TotalSeconds < 60)
                    return "just now";
                if (ts.TotalMinutes < 60)
                    return $"{(int)ts.TotalMinutes}m ago";
                if (ts.TotalHours < 24)
                    return $"{(int)ts.TotalHours}h ago";
                if (ts.TotalDays < 7)
                    return $"{(int)ts.TotalDays}d ago";
                if (ts.TotalDays < 30)
                    return $"{(int)(ts.TotalDays / 7)}w ago";
                if (ts.TotalDays < 365)
                    return $"{(int)(ts.TotalDays / 30)}mo ago";

                return $"{(int)(ts.TotalDays / 365)}y ago";
            }
            catch
            {
                return "unknown";
            }
        }

        // Add to your Post class in Lock.Models
        // Love reactions
        public int LoveCount { get; set; } = 0;

        // Store which users have loved this post (JSON array of user phones)
        public string LovedByJson { get; set; } = "[]";

        // Comment count (not stored, loaded on demand)
        private int _commentCount;
        [Ignore]
        public int CommentCount
        {
            get => _commentCount;
            set
            {
                if (_commentCount != value)
                {
                    _commentCount = value;
                    OnPropertyChanged(nameof(CommentCount));
                    OnPropertyChanged(nameof(CommentCountDisplay));
                }
            }
        }

        // Optional: Add a display property for formatting
        [Ignore]
        public string CommentCountDisplay => CommentCount > 0 ? CommentCount.ToString() : "";

        // For UI binding - check if current user loved this post
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
        public string LoveIcon => IsLovedByCurrentUser ? "??" : "??";

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

            LovedBy = lovedBy; // This updates LoveCount and saves JSON
        }

        // Returns a compact relative-time string for the provided UTC timestamp.
        // Examples: "5s", "12m", "3h", "2d", "Mar 3", "Mar 3, 2022"
        private static string GetRelativeTime(DateTime utcTime)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                // ensure the provided time is treated as UTC
                if (utcTime.Kind == DateTimeKind.Unspecified)
                {
                    utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
                }

                var ts = nowUtc - utcTime;
                if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;

                if (ts.TotalSeconds < 60)
                    return $"{(int)ts.TotalSeconds}s";

                if (ts.TotalMinutes < 60)
                    return $"{(int)ts.TotalMinutes}m";

                if (ts.TotalHours < 24)
                    return $"{(int)ts.TotalHours}h";

                if (ts.TotalDays < 7)
                    return $"{(int)ts.TotalDays}d";

                // older than a week: show short date in local time
                var local = utcTime.ToLocalTime();
                if (nowUtc.Year == utcTime.Year)
                {
                    // same year -> "Mar 3"
                    return local.ToString("MMM d");
                }

                // different year -> "Mar 3, 2022"
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