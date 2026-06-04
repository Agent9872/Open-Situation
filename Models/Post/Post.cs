using System;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Lock.Pages.Post;

namespace Lock.Models
{
    public class Post : INotifyPropertyChanged
    {
        public int Id { get; set; }

        // Author identifier (stores phone like User.PhoneNumber)
        public string AuthorPhone { get; set; } = string.Empty;

        // Text content of the post
        public string Content { get; set; } = string.Empty;

        // Category for the post (optional)
        public string Category { get; set; } = string.Empty;

        // Visibility for the post
        public string Visibility { get; set; } = "Everyone";  // "Everyone" or "By Mood"

        // Runtime property (not persisted)
        public string AuthorMood { get; set; } = string.Empty;

        // Stored as JSON array of local file paths
        public string ImagePathsJson { get; set; } = "[]";

        // Mood for the post/status (new)
        public string Mood { get; set; } = string.Empty;

        // Mood last updated timestamp - this will be populated from User model when loading
        public DateTime MoodLastUpdated { get; set; } = DateTime.UtcNow;

        // Created timestamp (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Computed relative time string for UI (e.g. "12m", "1h", "2d", "Mar 3")
        public string CreatedAtRelative => GetRelativeTime(CreatedAt);

        public string StatusImagePath { get; set; } = string.Empty;
        public string AuthorLookingFor { get; set; } = string.Empty;
        public string AuthorProfileImagePath { get; set; } = string.Empty;
        public bool IsCurrentUserPost { get; set; }
        public string AuthorDisplayName { get; set; } = string.Empty;

        // Search properties (runtime only)
        public string SearchQuery { get; set; } = string.Empty;
        public DateTime SearchTime { get; set; }
        public int SearchResultCount { get; set; }

        // Computed properties
        public string SearchDisplayTime => SearchTime.ToString("hh:mm tt");
        public string SearchDisplayResultCount => SearchResultCount > 0 ? $"({SearchResultCount})" : "(no results)";
        public bool SearchHasResults => SearchResultCount > 0;

        // Location properties (runtime only)
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Hidden by users (JSON array)
        public string HiddenByJson { get; set; } = "[]";

        // Link preview properties
        public string? FirstUrl => ExtractFirstUrl(Content);

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

        public string PreviewTitle => LinkPreview?.Title ?? FirstUrl ?? string.Empty;
        public string PreviewDescription => LinkPreview?.Description ?? string.Empty;
        public string PreviewImageUrl => LinkPreview?.ImageUrl ?? string.Empty;
        public string PreviewSiteName => LinkPreview?.SiteName ?? LinkDomain.ToUpperInvariant();
        public string PreviewFaviconUrl => LinkPreview?.FaviconUrl ?? string.Empty;
        public bool PreviewHasImage => !string.IsNullOrEmpty(PreviewImageUrl);

        private static string? ExtractFirstUrl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var m = System.Text.RegularExpressions.Regex.Match(
                text,
                @"(https?://[^\s]+|www\.[a-zA-Z0-9\-]+\.[^\s]{2,}|[a-zA-Z0-9\-]+\.(com|org|net|io|co|app|dev|ai|me|tv|gg|ly|uk|ng|us|ca|au)[^\s]*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return m.Success ? m.Value.TrimEnd('.', ',', ')', ']', '/') : null;
        }

        public string ContentWithoutUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content)) return string.Empty;

                var url = FirstUrl;
                if (string.IsNullOrEmpty(url)) return Content;

                var cleaned = Content
                    .Replace(url, string.Empty)
                    .Trim()
                    .TrimEnd('\n', '\r')
                    .Trim();

                return cleaned;
            }
        }

        public bool HasTextBesideUrl => HasLinkPreview && !string.IsNullOrWhiteSpace(ContentWithoutUrl);
        public bool HasLinkPreview => FirstUrl != null;

        private int _matchPercent;
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

        // Spark reactions
        public int SparkCount { get; set; } = 0;
        public string SparkedByJson { get; set; } = "[]";

        private bool _isSparkedByCurrentUser;
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

        public Color SparkIconColor => IsSparkedByCurrentUser ? Color.FromArgb("#FFA500") : Color.FromArgb("#888888");
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

        public bool IsHiddenByUser(string userPhone)
        {
            return HiddenBy.Contains(userPhone);
        }

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

        private string _moodLastUpdatedRelative = string.Empty;
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

        public FormattedString MoodDisplayFormatted
        {
            get
            {
                var fs = new FormattedString();

                if (!string.IsNullOrEmpty(Mood))
                {
                    var moodSpan = new Span
                    {
                        Text = Mood,
                        TextColor = Color.FromArgb("#B00020"),
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 13
                    };
                    fs.Spans.Add(moodSpan);

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

        // UI-only: the text actually displayed
        private string _displayContent = string.Empty;
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

        private string _truncatedPart = string.Empty;
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

        public string DisplayToggleText => IsExpanded ? "Show less" : "Show all";
        public bool NeedsToggle => !string.IsNullOrEmpty(Content) && Content.Length > 200;

        public FormattedString DisplayFormatted { get; private set; } = new FormattedString();
        public FormattedString ToggleFormatted { get; private set; } = new FormattedString();

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
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    formattedString.Spans.Add(new Span
                    {
                        Text = beforeText,
                        TextColor = Color.FromArgb("#F0EDE8")
                    });
                }

                var hashtagSpan = new Span
                {
                    Text = match.Value,
                    TextColor = Color.FromArgb("#1da1f2"),
                    FontAttributes = FontAttributes.Bold
                };

                var tapGesture = new TapGestureRecognizer();
                var hashtagText = match.Value;
                tapGesture.Tapped += async (s, e) =>
                {
                    var searchPage = new SearchPage();
                    await Application.Current.MainPage.Navigation.PushAsync(searchPage);
                    await Task.Delay(100);
                    searchPage.SetSearchText(hashtagText);
                };
                hashtagSpan.GestureRecognizers.Add(tapGesture);

                formattedString.Spans.Add(hashtagSpan);

                lastIndex = match.Index + match.Length;
            }

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
                DisplayContent = Content;
                TruncatedPart = Content;
                HiddenPart = string.Empty;
                DisplayFormatted = FormatTextWithHashtags(DisplayContent);
                ToggleFormatted = DisplayFormatted;
            }
            else
            {
                var first = Content.Substring(0, Math.Min(limit, Content.Length)).TrimEnd();
                var rest = Content.Substring(Math.Min(limit, Content.Length)).TrimStart();

                if (IsExpanded)
                {
                    DisplayContent = Content;
                    TruncatedPart = first;
                    HiddenPart = rest;
                    DisplayFormatted = FormatTextWithHashtags(DisplayContent);
                    ToggleFormatted = DisplayFormatted;
                }
                else
                {
                    DisplayContent = first + "…";
                    TruncatedPart = first + "…";
                    HiddenPart = string.Empty;
                    DisplayFormatted = FormatTextWithHashtags(DisplayContent);
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

        public void UpdateMoodRelativeTime()
        {
            MoodLastUpdatedRelative = GetMoodRelativeTime(MoodLastUpdated);
            OnPropertyChanged(nameof(MoodLastUpdatedRelative));
            OnPropertyChanged(nameof(MoodDisplayFormatted));
        }

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

        // Love reactions
        public int LoveCount { get; set; } = 0;
        public string LovedByJson { get; set; } = "[]";

        private int _commentCount;
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

        public string CommentCountDisplay => CommentCount > 0 ? CommentCount.ToString() : "";

        private bool _isLovedByCurrentUser;
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

        public Color LoveIconColor => IsLovedByCurrentUser ? Color.FromArgb("#C05050") : Color.FromArgb("#888888");
        public string LoveIcon => IsLovedByCurrentUser ? "??" : "??";
        public string LoveCountDisplay => LoveCount > 0 ? LoveCount.ToString() : string.Empty;

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

        private static string GetRelativeTime(DateTime utcTime)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
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

                var local = utcTime.ToLocalTime();
                if (nowUtc.Year == utcTime.Year)
                {
                    return local.ToString("MMM d");
                }

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