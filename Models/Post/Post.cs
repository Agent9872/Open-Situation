using System;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Lock.Pages.Post;
using Newtonsoft.Json;

namespace Lock.Models
{
    public class Post : INotifyPropertyChanged
    {
        // ?? PERSISTED TO SUPABASE ?????????????????????????????????????????

        public int Id { get; set; }
        public string AuthorPhone { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Visibility { get; set; } = "Everyone";
        public string ImagePathsJson { get; set; } = "[]";
        public string Mood { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string StatusImagePath { get; set; } = string.Empty;
        public int LoveCount { get; set; } = 0;
        public string LovedByJson { get; set; } = "[]";
        public int SparkCount { get; set; } = 0;
        public string SparkedByJson { get; set; } = "[]";
        public string HiddenByJson { get; set; } = "[]";

        // ?? RUNTIME ONLY — never sent to Supabase ????????????????????????

        [JsonIgnore]
        public string AuthorMood { get; set; } = string.Empty;

        [JsonIgnore]
        public string AuthorLookingFor { get; set; } = string.Empty;

        [JsonIgnore]
        public string AuthorProfileImagePath { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsCurrentUserPost { get; set; }

        [JsonIgnore]
        public string AuthorDisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        public string SearchQuery { get; set; } = string.Empty;

        [JsonIgnore]
        public DateTime SearchTime { get; set; }

        [JsonIgnore]
        public int SearchResultCount { get; set; }

        [JsonIgnore]
        public string Country { get; set; } = string.Empty;

        [JsonIgnore]
        public string State { get; set; } = string.Empty;

        [JsonIgnore]
        public double? Latitude { get; set; }

        [JsonIgnore]
        public double? Longitude { get; set; }

        [JsonIgnore]
        public bool IsAuthorVerified { get; set; }

        [JsonIgnore]
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
        private LinkPreviewData? _linkPreview;

        [JsonIgnore]
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
        private int _matchPercent;

        [JsonIgnore]
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
        private bool _isLovedByCurrentUser;

        [JsonIgnore]
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
        private bool _isSparkedByCurrentUser;

        [JsonIgnore]
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
        private bool _isSavedByCurrentUser;

        [JsonIgnore]
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
        private bool _isExpanded;

        [JsonIgnore]
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
        private string _displayContent = string.Empty;

        [JsonIgnore]
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
        private int _commentCount;

        [JsonIgnore]
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
        private string _moodLastUpdatedRelative = string.Empty;

        [JsonIgnore]
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
        private string _truncatedPart = string.Empty;

        [JsonIgnore]
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
        private string _hiddenPart = string.Empty;

        [JsonIgnore]
        public FormattedString DisplayFormatted { get; private set; } = new FormattedString();

        [JsonIgnore]
        public FormattedString ToggleFormatted { get; private set; } = new FormattedString();

        // ?? COMPUTED (derived from persisted data, not sent to Supabase) ??

        [JsonIgnore]
        public string CreatedAtRelative => GetRelativeTime(CreatedAt);

        [JsonIgnore]
        public string SearchDisplayTime => SearchTime.ToString("hh:mm tt");

        [JsonIgnore]
        public string SearchDisplayResultCount => SearchResultCount > 0 ? $"({SearchResultCount})" : "(no results)";

        [JsonIgnore]
        public bool SearchHasResults => SearchResultCount > 0;

        [JsonIgnore]
        public string? FirstUrl => ExtractFirstUrl(Content);

        [JsonIgnore]
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

        [JsonIgnore]
        public string PreviewTitle => LinkPreview?.Title ?? FirstUrl ?? string.Empty;

        [JsonIgnore]
        public string PreviewDescription => LinkPreview?.Description ?? string.Empty;

        [JsonIgnore]
        public string PreviewImageUrl => LinkPreview?.ImageUrl ?? string.Empty;

        [JsonIgnore]
        public string PreviewSiteName => LinkPreview?.SiteName ?? LinkDomain.ToUpperInvariant();

        [JsonIgnore]
        public string PreviewFaviconUrl => LinkPreview?.FaviconUrl ?? string.Empty;

        [JsonIgnore]
        public bool PreviewHasImage => !string.IsNullOrEmpty(PreviewImageUrl);

        [JsonIgnore]
        public bool HasTextBesideUrl => HasLinkPreview && !string.IsNullOrWhiteSpace(ContentWithoutUrl);

        [JsonIgnore]
        public bool HasLinkPreview => FirstUrl != null;

        [JsonIgnore]
        public string ContentWithoutUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content)) return string.Empty;
                var url = FirstUrl;
                if (string.IsNullOrEmpty(url)) return Content;
                return Content.Replace(url, string.Empty).Trim().TrimEnd('\n', '\r').Trim();
            }
        }

        [JsonIgnore]
        public Color SparkIconColor => IsSparkedByCurrentUser ? Color.FromArgb("#FFA500") : Color.FromArgb("#888888");

        [JsonIgnore]
        public string SparkCountDisplay => SparkCount > 0 ? SparkCount.ToString() : string.Empty;

        [JsonIgnore]
        public string SaveIconFill => IsSavedByCurrentUser ? "#FFD24D" : "#888888";

        [JsonIgnore]
        public string DisplayToggleText => IsExpanded ? "Show less" : "Show all";

        [JsonIgnore]
        public bool NeedsToggle => !string.IsNullOrEmpty(Content) && Content.Length > 200;

        [JsonIgnore]
        public string CommentCountDisplay => CommentCount > 0 ? CommentCount.ToString() : "";

        [JsonIgnore]
        public Color LoveIconColor => IsLovedByCurrentUser ? Color.FromArgb("#C05050") : Color.FromArgb("#888888");

        [JsonIgnore]
        public string LoveIcon => IsLovedByCurrentUser ? "??" : "??";

        [JsonIgnore]
        public string LoveCountDisplay => LoveCount > 0 ? LoveCount.ToString() : string.Empty;

        [JsonIgnore]
        public bool IsMoodRecent
        {
            get
            {
                try { return (DateTime.UtcNow - MoodLastUpdated).TotalHours < 1; }
                catch { return false; }
            }
        }

        [JsonIgnore]
        public DateTime MoodLastUpdated { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public FormattedString MoodDisplayFormatted
        {
            get
            {
                var fs = new FormattedString();
                if (!string.IsNullOrEmpty(Mood))
                {
                    fs.Spans.Add(new Span
                    {
                        Text = Mood,
                        TextColor = Color.FromArgb("#B00020"),
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 13
                    });

                    if (!string.IsNullOrEmpty(MoodLastUpdatedRelative))
                    {
                        fs.Spans.Add(new Span { Text = " · ", TextColor = Color.FromArgb("#888888"), FontSize = 11 });
                        fs.Spans.Add(new Span { Text = MoodLastUpdatedRelative, TextColor = Color.FromArgb("#888888"), FontSize = 11 });
                    }
                }
                return fs;
            }
        }

        // ?? COLLECTIONS (derived from JSON, not sent separately) ??????????

        [JsonIgnore]
        public string[] ImagePathsList
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(ImagePathsJson)) return Array.Empty<string>();
                    var arr = System.Text.Json.JsonSerializer.Deserialize<string[]?>(ImagePathsJson);
                    return arr ?? Array.Empty<string>();
                }
                catch { return Array.Empty<string>(); }
            }
            set
            {
                ImagePathsJson = System.Text.Json.JsonSerializer.Serialize(value ?? Array.Empty<string>());
                OnPropertyChanged(nameof(ImagePathsList));
            }
        }

        [JsonIgnore]
        public List<string> SparkedBy
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(SparkedByJson)) return new List<string>();
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(SparkedByJson) ?? new List<string>();
                }
                catch { return new List<string>(); }
            }
            set
            {
                SparkedByJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
                SparkCount = value?.Count ?? 0;
                OnPropertyChanged(nameof(SparkedBy));
                OnPropertyChanged(nameof(SparkCount));
                OnPropertyChanged(nameof(SparkCountDisplay));
            }
        }

        [JsonIgnore]
        public List<string> LovedBy
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(LovedByJson)) return new List<string>();
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(LovedByJson) ?? new List<string>();
                }
                catch { return new List<string>(); }
            }
            set
            {
                LovedByJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
                LoveCount = value?.Count ?? 0;
                OnPropertyChanged(nameof(LovedBy));
                OnPropertyChanged(nameof(LoveCount));
                OnPropertyChanged(nameof(LoveCountDisplay));
            }
        }

        [JsonIgnore]
        public List<string> HiddenBy
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(HiddenByJson)) return new List<string>();
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(HiddenByJson) ?? new List<string>();
                }
                catch { return new List<string>(); }
            }
            set
            {
                HiddenByJson = System.Text.Json.JsonSerializer.Serialize(value ?? new List<string>());
                OnPropertyChanged(nameof(HiddenBy));
            }
        }

        // ?? METHODS ???????????????????????????????????????????????????????

        public bool IsHiddenByUser(string userPhone) => HiddenBy.Contains(userPhone);

        public void NotifyCommentCountChanged()
        {
            OnPropertyChanged(nameof(CommentCount));
            OnPropertyChanged(nameof(CommentCountDisplay));
        }

        public void ToggleSpark(string userPhone)
        {
            var sparkedBy = SparkedBy;
            if (sparkedBy.Contains(userPhone)) sparkedBy.Remove(userPhone);
            else sparkedBy.Add(userPhone);
            SparkedBy = sparkedBy;
        }

        public void ToggleLove(string userPhone)
        {
            var lovedBy = LovedBy;
            if (lovedBy.Contains(userPhone)) lovedBy.Remove(userPhone);
            else lovedBy.Add(userPhone);
            LovedBy = lovedBy;
        }

        public void RefreshSparkState()
        {
            OnPropertyChanged(nameof(IsSparkedByCurrentUser));
            OnPropertyChanged(nameof(SparkIconColor));
            OnPropertyChanged(nameof(SparkCountDisplay));
            OnPropertyChanged(nameof(SparkCount));
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
                    ToggleFormatted = FormatTextWithHashtags(TruncatedPart);
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
                formattedString.Spans.Add(new Span { Text = text, TextColor = Color.FromArgb("#F0EDE8") });
                return formattedString;
            }

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    formattedString.Spans.Add(new Span
                    {
                        Text = text.Substring(lastIndex, match.Index - lastIndex),
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
                formattedString.Spans.Add(new Span { Text = text.Substring(lastIndex), TextColor = Color.FromArgb("#F0EDE8") });

            return formattedString;
        }

        private static string? ExtractFirstUrl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(
                text,
                @"(https?://[^\s]+|www\.[a-zA-Z0-9\-]+\.[^\s]{2,}|[a-zA-Z0-9\-]+\.(com|org|net|io|co|app|dev|ai|me|tv|gg|ly|uk|ng|us|ca|au)[^\s]*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Value.TrimEnd('.', ',', ')', ']', '/') : null;
        }

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
                return nowUtc.Year == utcTime.Year ? local.ToString("MMM d") : local.ToString("MMM d, yyyy");
            }
            catch { return string.Empty; }
        }

        private static string GetMoodRelativeTime(DateTime moodTime)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                if (moodTime.Kind == DateTimeKind.Unspecified)
                    moodTime = DateTime.SpecifyKind(moodTime, DateTimeKind.Utc);
                var ts = nowUtc - moodTime;
                if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
                if (ts.TotalSeconds < 60) return "just now";
                if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}m ago";
                if (ts.TotalHours < 24) return $"{(int)ts.TotalHours}h ago";
                if (ts.TotalDays < 7) return $"{(int)ts.TotalDays}d ago";
                if (ts.TotalDays < 30) return $"{(int)(ts.TotalDays / 7)}w ago";
                if (ts.TotalDays < 365) return $"{(int)(ts.TotalDays / 30)}mo ago";
                return $"{(int)(ts.TotalDays / 365)}y ago";
            }
            catch { return "unknown"; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}