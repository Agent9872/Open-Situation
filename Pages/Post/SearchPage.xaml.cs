using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;

namespace Lock.Pages.Post
{

    public class PostSearchResultViewModel : INotifyPropertyChanged
    {
        private readonly Lock.Models.Post _post;
        private string _searchTerm = string.Empty;

        public PostSearchResultViewModel(Lock.Models.Post post, string searchTerm = "")
        {
            _post = post;
            _searchTerm = searchTerm;
        }

        public int PostId => _post.Id;
        public string Content => _post.Content;
        public string AuthorName => _post.AuthorDisplayName ?? "Unknown";
        public string AuthorPhone => _post.AuthorPhone;
        public string AuthorProfileImagePath => _post.AuthorProfileImagePath;
        public bool HasImages => _post.ImagePathsList?.Any() == true;
        public string FirstImagePath => HasImages ? _post.ImagePathsList.First() : null;
        public string CreatedAtRelative => GetRelativeTimeString(_post.CreatedAt);
        public int SparkCount => _post.SparkCount;
        public int LoveCount => _post.LoveCount;
        public int CommentCount => _post.CommentCount;
        public bool IsAuthorVerified => _post.IsAuthorVerified;

        public FormattedString HighlightedContent
        {
            get
            {
                var formatted = new FormattedString();
                if (string.IsNullOrEmpty(_post.Content)) return formatted;

                if (!string.IsNullOrEmpty(_searchTerm) && _searchTerm.StartsWith("#"))
                {
                    // Highlight the searched hashtag
                    var hashtag = _searchTerm;
                    var content = _post.Content;
                    int lastIndex = 0;
                    int index = content.IndexOf(hashtag, StringComparison.OrdinalIgnoreCase);

                    while (index >= 0)
                    {
                        // Add text before hashtag
                        if (index > lastIndex)
                        {
                            formatted.Spans.Add(new Span
                            {
                                Text = content.Substring(lastIndex, index - lastIndex),
                                TextColor = Color.FromArgb("#F0EDE8")
                            });
                        }

                        // Add highlighted hashtag
                        formatted.Spans.Add(new Span
                        {
                            Text = content.Substring(index, hashtag.Length),
                            TextColor = Color.FromArgb("#1da1f2"),
                            FontAttributes = FontAttributes.Bold
                        });

                        lastIndex = index + hashtag.Length;
                        index = content.IndexOf(hashtag, lastIndex, StringComparison.OrdinalIgnoreCase);
                    }

                    // Add remaining text
                    if (lastIndex < content.Length)
                    {
                        formatted.Spans.Add(new Span
                        {
                            Text = content.Substring(lastIndex),
                            TextColor = Color.FromArgb("#F0EDE8")
                        });
                    }
                }
                else
                {
                    // Just show plain text with hashtags colored
                    formatted = FormatTextWithHashtags(_post.Content);
                }

                return formatted;
            }
        }

        private FormattedString FormatTextWithHashtags(string text)
        {
            var formatted = new FormattedString();
            if (string.IsNullOrEmpty(text)) return formatted;

            var hashtagPattern = new System.Text.RegularExpressions.Regex(@"#\w+");
            int lastIndex = 0;
            var matches = hashtagPattern.Matches(text);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    formatted.Spans.Add(new Span
                    {
                        Text = text.Substring(lastIndex, match.Index - lastIndex),
                        TextColor = Color.FromArgb("#F0EDE8")
                    });
                }

                formatted.Spans.Add(new Span
                {
                    Text = match.Value,
                    TextColor = Color.FromArgb("#1da1f2"),
                    FontAttributes = FontAttributes.Bold
                });

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                formatted.Spans.Add(new Span
                {
                    Text = text.Substring(lastIndex),
                    TextColor = Color.FromArgb("#F0EDE8")
                });
            }

            return formatted;
        }

        private string GetRelativeTimeString(DateTime dateTime)
        {
            var now = DateTime.UtcNow;
            var timeSpan = now - dateTime;

            if (timeSpan.TotalSeconds < 60) return "just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";
            if (timeSpan.TotalDays < 30) return $"{(int)(timeSpan.TotalDays / 7)}w ago";
            if (timeSpan.TotalDays < 365) return $"{(int)(timeSpan.TotalDays / 30)}mo ago";
            return $"{(int)(timeSpan.TotalDays / 365)}y ago";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
    
    // Add this converter class at the top of your file, after the using statements
    // Corrected PathGeometryConverter class
    public class PathGeometryConverter : System.ComponentModel.TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string pathData)
            {
                try
                {
                    var geometry = new PathGeometry();
                    var figure = new PathFigure();
                    var segments = new PathSegmentCollection(); // Don't pass list to constructor

                    // Parse the SVG path data - simplified for the verified badge path
                    // The path data: "m366-126-64-108-122-26 12-126-82-94 82-94-12-126 122-26 64-108 114 48 114-48 64 108 122 26-12 126 82 94-82 94 12 126-122 26-64 108-114-48-114 48"

                    // For the verified badge, we'll just create a simple star/check shape
                    // But to keep it simple, let's create a standard verified badge shape

                    figure.StartPoint = new Point(12, 2);
                    segments.Add(new LineSegment { Point = new Point(15, 9) });
                    segments.Add(new LineSegment { Point = new Point(22, 9) });
                    segments.Add(new LineSegment { Point = new Point(17, 14) });
                    segments.Add(new LineSegment { Point = new Point(19, 21) });
                    segments.Add(new LineSegment { Point = new Point(12, 17) });
                    segments.Add(new LineSegment { Point = new Point(5, 21) });
                    segments.Add(new LineSegment { Point = new Point(7, 14) });
                    segments.Add(new LineSegment { Point = new Point(2, 9) });
                    segments.Add(new LineSegment { Point = new Point(9, 9) });
                    segments.Add(new LineSegment { Point = new Point(12, 2) });

                    figure.Segments = segments;
                    geometry.Figures = new PathFigureCollection { figure };

                    return geometry;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PathGeometryConverter error: {ex}");
                    return new PathGeometry();
                }
            }
            return new PathGeometry();
        }
    }


    public partial class SearchPage : ContentPage, INotifyPropertyChanged
    {
        private List<User> _allUsers = new();
        private List<User> _filteredUsers = new();
        private string _currentTab = "All";
        private string _selectedLocation = "All Locations";
        private string _selectedMood = "";
        private int? _selectedMinAge;
        private int? _selectedMaxAge;
        private string _currentSearchQuery = "";

        // One active filter per type — key = FilterType, value = display label
        private readonly Dictionary<string, string> _activeFilterMap = new();

        // Debounce timer for search text
        private System.Timers.Timer? _debounceTimer;

        public ObservableCollection<UserCardViewModel> AllUsers { get; set; } = new();
        public ObservableCollection<UserCardViewModel> SearchResults { get; set; } = new();

        public SearchPage()
        {
            InitializeComponent();
            BindingContext = this;

            // Remove default navigation bar
            Shell.SetNavBarIsVisible(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
            NavigationPage.SetHasBackButton(this, false);
            this.Title = null;

            LoadData();
            SuppressScrollIndicators();
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── Platform scroll suppression ───────────────────────────────────

        private void SuppressScrollIndicators()
        {
#if ANDROID
            this.Loaded += (s, e) =>
            {
                var tabSv = this.FindByName<ScrollView>("TabScrollView");
                if (tabSv == null) return;
                tabSv.HandlerChanged += (_, _) =>
                {
                    if (tabSv.Handler?.PlatformView is Android.Widget.HorizontalScrollView hsv)
                    {
                        hsv.HorizontalScrollBarEnabled = false;
                        hsv.OverScrollMode = Android.Views.OverScrollMode.Never;
                    }
                };
            };
#endif
        }

        // ── Data ──────────────────────────────────────────────────────────
        // Add these properties to your SearchPage class
        private List<Lock.Models.Post> _allPosts = new();
        private List<Lock.Models.Post> _filteredPosts = new();
        private bool _isSearchingPosts = false;

        // Add these ObservableCollections to your existing ones
        public ObservableCollection<PostSearchResultViewModel> PostSearchResults { get; set; } = new();
        private System.Timers.Timer? _shuffleTimer;
        // Modify your LoadData method to also load posts
        private async void LoadData()
        {
            try
            {
                _allUsers = await SupabaseService.GetAsync<User>("Users", "limit=200");
                var me = Preferences.Get("current_user_phone", string.Empty);

                // LOAD ALL POSTS FOR TEXT SEARCH
                _allPosts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();

                // Filter out status images and ghost mode users
                _allPosts = _allPosts
                    .Where(p => string.IsNullOrEmpty(p.StatusImagePath))
                    .Where(p => !string.IsNullOrEmpty(p.Content))
                    .ToList();

                await ResolvePostAuthorNames();

                _filteredUsers = _allUsers
                    .Where(u => !string.Equals(u.PhoneNumber, me, StringComparison.OrdinalIgnoreCase)
                                && !u.GhostModeMoodShield)
                    .ToList();

                LoadLocationPickerItems();
                ShowAllUsers();
                StartShuffleTimer();

                // Load match percentages in background for all users
                _ = Task.Run(async () =>
                {
                    foreach (var user in _filteredUsers.Take(50))
                    {
                        var vm = AllUsers.FirstOrDefault(u => u.PhoneNumber == user.PhoneNumber);
                        if (vm != null)
                        {
                            await vm.LoadMatchPercentageAsync();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadData error: {ex}");
            }
        }

        private void StartShuffleTimer()
        {
            _shuffleTimer?.Dispose();
            _shuffleTimer = new System.Timers.Timer(30_000) { AutoReset = true };
            _shuffleTimer.Elapsed += (_, _) =>
            {
                // Only reshuffle if not actively filtering
                if (!AnyFilterActive())
                {
                    MainThread.BeginInvokeOnMainThread(ShowAllUsers);
                }
            };
            _shuffleTimer.Start();
        }

        // Add this method to resolve author names for posts
        private async Task ResolvePostAuthorNames()
        {
            try
            {
                var phones = _allPosts.Select(p => p.AuthorPhone).Distinct().ToList();
                var nameMap = new Dictionary<string, string>();

                foreach (var phone in phones)
                {
                    var users = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var user = users.FirstOrDefault();
                    if (user != null)
                    {
                        nameMap[phone] = string.IsNullOrWhiteSpace(user.Name) ? phone : user.Name;
                    }
                }

                foreach (var post in _allPosts)
                {
                    if (nameMap.TryGetValue(post.AuthorPhone, out var name))
                    {
                        post.AuthorDisplayName = name;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResolvePostAuthorNames error: {ex}");
            }
        }


        // ── FIXED: Rebuild _filteredUsers fresh every time (catches live toggle changes) ──
        private async Task RefreshFilteredUsersAsync()
        {
            try
            {
                _allUsers = await SupabaseService.GetAsync<User>("Users", "limit=200");
                var me = Preferences.Get("current_user_phone", string.Empty);

                _filteredUsers = _allUsers
                    .Where(u => !string.Equals(u.PhoneNumber, me, StringComparison.OrdinalIgnoreCase)
                                && !u.GhostModeMoodShield)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshFilteredUsersAsync error: {ex}");
            }
        }

        // ── FIXED SearchUsersByName: re-reads DB so shield/mood toggles take effect immediately ──
        private async void SearchUsersByName(string query)
        {
            await RefreshFilteredUsersAsync(); // always fresh

            var searchTerm = query.Trim().ToLowerInvariant();

            // Modified: For users with HidePhoneNumber = true, only search by name, not phone number
            var userResults = _filteredUsers
                .Where(u =>
                    // Always search by name
                    u.Name?.ToLowerInvariant().Contains(searchTerm) == true ||
                    // Only search by phone number if HidePhoneNumber is false
                    (!u.HidePhoneNumber && u.PhoneNumber?.Contains(searchTerm) == true))
                .Take(20)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (userResults.Any())
                {
                    SearchResults.Clear();
                    foreach (var u in userResults)
                        SearchResults.Add(new UserCardViewModel(u, GetLocation(u)));

                    SearchResultsSection.IsVisible = true;
                    AllUsersSection.IsVisible = false;
                }
                else
                {
                    // No user matches — don't force SearchResultsSection visible
                    // post results section will handle its own visibility
                }
            });
        }

        // ── FIXED SearchPostsByContent: exclude posts whose author has Ghost/Shield enabled ──
        private void SearchPostsByContent(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _isSearchingPosts = false;
                PostSearchResults.Clear();
                ShowAllUsers();
                return;
            }

            _isSearchingPosts = true;
            var searchTerm = query.Trim().ToLowerInvariant();
            var me = Preferences.Get("current_user_phone", string.Empty);

            // Build a set of hidden phones — ghost mode OR mood shield users must not appear
            var hiddenPhones = _allUsers
                .Where(u => u.GhostModeMoodShield
                            || string.Equals(u.PhoneNumber, me, StringComparison.OrdinalIgnoreCase))
                .Select(u => u.PhoneNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _filteredPosts = _allPosts
                .Where(p => !hiddenPhones.Contains(p.AuthorPhone))   // ← ghost/shield filter
                .Where(p =>
                    (p.Content?.ToLowerInvariant().Contains(searchTerm) == true) ||
                    (p.AuthorDisplayName?.ToLowerInvariant().Contains(searchTerm) == true) ||
                    (p.Category?.ToLowerInvariant().Contains(searchTerm) == true))
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AllUsersSection.IsVisible = false;
                SearchResultsSection.IsVisible = false;

                var postResultsSection = this.FindByName<ScrollView>("PostResultsSection");
                if (postResultsSection != null)
                    postResultsSection.IsVisible = true;

                PostSearchResults.Clear();
                foreach (var post in _filteredPosts)
                    PostSearchResults.Add(new PostSearchResultViewModel(post, query.Trim()));

                EmptyContainer.IsVisible = !_filteredPosts.Any();
                if (!_filteredPosts.Any())
                {
                    EmptyLabel.Text = "No posts found";
                    EmptySubLabel.Text = $"No posts matching \"{query}\"";
                }
            });
        }

        // Modify your OnTextChanged method to search posts
        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var query = e.NewTextValue ?? "";
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();

            if (string.IsNullOrWhiteSpace(query))
            {
                _currentSearchQuery = "";
                RemoveChip("Search");
                _isSearchingPosts = false;
                var postResultsSection = this.FindByName<ScrollView>("PostResultsSection");
                if (postResultsSection != null)
                    postResultsSection.IsVisible = false;
                ShowAllUsers();
                return;
            }

            _debounceTimer = new System.Timers.Timer(500) { AutoReset = false };
            _debounceTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentSearchQuery = query.Trim();
                UpsertChip("Search", _currentSearchQuery);

                // Check which tab is active
                if (_currentTab == "Phone")
                {
                    // For Phone tab, we still use SearchUsersByName but it now respects HidePhoneNumber
                    SearchUsersByName(query.Trim());
                    // Also search posts if needed
                    SearchPostsByContent(query.Trim());
                }
                else
                {
                    // Search BOTH users and posts
                    SearchUsersByName(query.Trim());
                    SearchPostsByContent(query.Trim());
                }
            });
            _debounceTimer.Start();
        }

        private async void OnPostResultTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is PostSearchResultViewModel vm)
                {
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                    if (string.IsNullOrEmpty(currentUserPhone))
                    {
                        await DisplayAlert("Not Logged In", "Please log in to view posts", "OK");
                        return;
                    }

                    // Navigate to CommentsPage with the post ID
                    var commentsPage = new CommentsPage(vm.PostId, currentUserPhone);
                    await Navigation.PushAsync(commentsPage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPostResultTapped error: {ex}");
                await DisplayAlert("Error", "Could not open post", "OK");
            }
        }
        private string GetLocation(User u)
        {
            if (!string.IsNullOrEmpty(u.Country) && !string.IsNullOrEmpty(u.State))
                return $"{u.State}, {u.Country}";
            if (!string.IsNullOrEmpty(u.Country)) return u.Country;
            if (!string.IsNullOrEmpty(u.State)) return u.State;
            return "";
        }

        private int CalcAge(DateTime dob)
        {
            if (dob == DateTime.MinValue) return 0;
            var t = DateTime.Today;
            var a = t.Year - dob.Year;
            if (dob > t.AddYears(-a)) a--;
            return a;
        }

        // ── Display ───────────────────────────────────────────────────────

        private void ShowAllUsers()
        {
            SearchResultsSection.IsVisible = false;
            AllUsersSection.IsVisible = true;
            EmptyContainer.IsVisible = false;

            var layout = this.FindByName<HorizontalStackLayout>("AllUsersStackLayout");
            if (layout == null) return;

            layout.Children.Clear();

            var shuffled = _filteredUsers
                .OrderBy(_ => Guid.NewGuid())
                .Take(20)
                .ToList();

            foreach (var u in shuffled)
            {
                var vm = new UserCardViewModel(u, GetLocation(u));
                var card = BuildUserCard(vm);
                layout.Children.Add(card);
            }

            if (!layout.Children.Any())
            {
                EmptyContainer.IsVisible = true;
                EmptyLabel.Text = "No users yet";
                EmptySubLabel.Text = "Be the first to join!";
            }
        }

        private View BuildUserCard(UserCardViewModel vm)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#12121A"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#2A2A38"),
                StrokeShape = new RoundRectangle { CornerRadius = 24 },
                WidthRequest = 240,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 8),
                VerticalOptions = LayoutOptions.Start
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                await Shell.Current.GoToAsync("///profile",
                    new Dictionary<string, object>
                    {
                        ["phone"] = vm.PhoneNumber,
                        ["viewOnly"] = "true"
                    });
            };
            card.GestureRecognizers.Add(tap);

            var root = new VerticalStackLayout { Spacing = 0 };

            // ── Photo area ──
            var photoGrid = new Grid { HeightRequest = 200 };

            var photo = new Image { Aspect = Aspect.AspectFill, HeightRequest = 200 };
            if (!string.IsNullOrEmpty(vm.ProfileImagePath) && File.Exists(vm.ProfileImagePath))
                photo.Source = ImageSource.FromFile(vm.ProfileImagePath);
            else
            {
                var initials = Uri.EscapeDataString(string.IsNullOrEmpty(vm.Name) ? "?" : vm.Name);
                photo.Source = ImageSource.FromUri(new Uri(
                    $"https://ui-avatars.com/api/?name={initials}&background=1A1A2E&color=00C9C9&size=400&bold=true&font-size=0.4"));
            }
            photoGrid.Children.Add(photo);

            var gradient = new BoxView { VerticalOptions = LayoutOptions.End, HeightRequest = 90, Opacity = 1 };
            gradient.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
        {
            new GradientStop { Color = Colors.Transparent, Offset = 0 },
            new GradientStop { Color = Color.FromArgb("#12121A"), Offset = 1 }
        }
            };
            photoGrid.Children.Add(gradient);

            var nameStack = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.End,
                Padding = new Thickness(12, 0, 12, 10),
                Spacing = 3
            };

            var nameRow = new HorizontalStackLayout { Spacing = 6 };
            nameRow.Children.Add(new Label
            {
                Text = vm.Name,
                FontAttributes = FontAttributes.Bold,
                FontSize = 17,
                TextColor = Colors.White,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation
            });

            if (vm.IsVerified)
            {
                var verifiedPath = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    StrokeThickness = 0.5,
                    HeightRequest = 16,
                    WidthRequest = 16,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                var vc = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();
                verifiedPath.Data = (Geometry)vc.ConvertFromInvariantString(
                    "m366-126-64-108-122-26 12-126-82-94 82-94-12-126 122-26 64-108 114 48 114-48 64 108 122 26-12 126 82 94-82 94 12 126-122 26-64 108-114-48-114 48Zm12-36 102-42 102 42 58-96 110-24-10-114 74-84-74-84 10-114-110-24-58-96-102 42-102-42-58 96-110 24 10 114-74 84 74 84-10 114 110 24 58 96Zm102-318Zm-42 106 190-190-20-20-170 170-86-86-20 20 106 106Z");
                nameRow.Children.Add(verifiedPath);
            }

            if (vm.HasAge)
            {
                nameRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#00000070"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(6, 2),
                    Content = new Label
                    {
                        Text = vm.Age.ToString(),
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5")
                    }
                });
            }

            nameStack.Children.Add(nameRow);

            if (vm.HasLocation)
            {
                var locRow = new HorizontalStackLayout { Spacing = 4 };
                var locIcon = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    HeightRequest = 11,
                    WidthRequest = 11,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                var lc = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();
                locIcon.Data = (Geometry)lc.ConvertFromInvariantString(
                    "M522.5-511.5Q540-529 540-554t-17.5-42.5Q505-614 480-614t-42.5 17.5Q420-579 420-554t17.5 42.5Q455-494 480-494t42.5-17.5ZM480-169q110-94 177.5-198.5T725-547q0-110-69.5-182T480-801q-106 0-175.5 72T235-547q0 75 67.5 179.5T480-169Zm0 38Q345-252 276-357t-69-190q0-120 78.5-200.5T480-828q116 0 194.5 80.5T753-547q0 85-69 190T480-131Zm0-423Z");
                locRow.Children.Add(locIcon);
                locRow.Children.Add(new Label
                {
                    Text = vm.Location,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#D0D0E0"),
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                });
                nameStack.Children.Add(locRow);
            }

            photoGrid.Children.Add(nameStack);
            root.Children.Add(photoGrid);

            // ── Card body ──
            var body = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(12, 10, 12, 12) };

            if (vm.HasMood)
            {
                body.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#2A1520"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#FF3B6F"),
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(8, 4),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = new Label
                    {
                        Text = vm.Mood,
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#FF3B6F"),
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation
                    }
                });
            }

            if (vm.HasMatchPercent)
            {
                body.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#0D1F1F"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#22C55E"),
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(8, 4),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = new Label
                    {
                        Text = vm.MatchPercentDisplay,
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#22C55E")
                    }
                });
            }

            if (vm.HasBio)
            {
                body.Children.Add(new Label
                {
                    Text = vm.ShortBio,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#A0A0B0"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    MaxLines = 2
                });
            }

            body.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#1E1E2A"),
                HorizontalOptions = LayoutOptions.Fill
            });

            // ── Gender → Interest row ──
            var genderInterestRow = new HorizontalStackLayout { Spacing = 6 };

            string genderIcon = vm.Gender?.ToLower() switch
            {
                "male" or "man" => "♂",
                "female" or "woman" => "♀",
                _ => "⚧"
            };
            Color genderColor = vm.Gender?.ToLower() switch
            {
                "male" or "man" => Color.FromArgb("#60A5FA"),
                "female" or "woman" => Color.FromArgb("#F472B6"),
                _ => Color.FromArgb("#A78BFA")
            };
            string interestIcon = vm.Interest?.ToLower() switch
            {
                "men" => "♂",
                "women" => "♀",
                "everyone" => "✦",
                _ => "✦"
            };
            Color interestColor = vm.Interest?.ToLower() switch
            {
                "men" => Color.FromArgb("#60A5FA"),
                "women" => Color.FromArgb("#F472B6"),
                "everyone" => Color.FromArgb("#FBBF24"),
                _ => Color.FromArgb("#A78BFA")
            };

            genderInterestRow.Children.Add(new Label { Text = genderIcon, FontSize = 13, TextColor = genderColor, VerticalOptions = LayoutOptions.Center });
            genderInterestRow.Children.Add(new Label { Text = vm.Gender, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = genderColor, VerticalOptions = LayoutOptions.Center });
            genderInterestRow.Children.Add(new Label { Text = "→", FontSize = 11, TextColor = Color.FromArgb("#3A3A4A"), VerticalOptions = LayoutOptions.Center });
            genderInterestRow.Children.Add(new Label { Text = interestIcon, FontSize = 13, TextColor = interestColor, VerticalOptions = LayoutOptions.Center });
            genderInterestRow.Children.Add(new Label { Text = vm.Interest, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = interestColor, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation, VerticalOptions = LayoutOptions.Center });
            body.Children.Add(genderInterestRow);

            // ── Physical chips ──
            var chipRow = new HorizontalStackLayout { Spacing = 6 };
            void AddChip(string text, string color = "#7A7A8C")
            {
                if (string.IsNullOrEmpty(text)) return;
                chipRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#1C1C28"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#2A2A40"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(7, 3),
                    Content = new Label { Text = text, FontSize = 10, TextColor = Color.FromArgb(color) }
                });
            }
            if (vm.HasHeight) AddChip(vm.HeightDisplay);
            if (vm.HasBodyType) AddChip(vm.BodyType);
            if (vm.HasEthnicity) AddChip(vm.EthnicityDisplay);
            if (chipRow.Children.Any()) body.Children.Add(chipRow);

            // ── Personality row ──
            var persRow = new HorizontalStackLayout { Spacing = 6 };
            if (vm.HasPersonalityType)
            {
                persRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#0D1F1F"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(7, 3),
                    Content = new Label { Text = vm.PersonalityTypeShort, FontSize = 10, TextColor = Color.FromArgb("#00B5B5") }
                });
            }
            if (vm.HasLoveLanguage)
            {
                persRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#2A1520"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(7, 3),
                    Content = new Label { Text = vm.LoveLanguageShort, FontSize = 10, TextColor = Color.FromArgb("#FF3B6F") }
                });
            }
            if (persRow.Children.Any()) body.Children.Add(persRow);

            body.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#1E1E2A"),
                HorizontalOptions = LayoutOptions.Fill
            });

            // ── Lifestyle icons ──
            var lifeRow = new HorizontalStackLayout { Spacing = 10 };

            void AddLifeIcon(string pathData, string color, string label = null)
            {
                var row = new HorizontalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
                var icon = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb(color)),
                    HeightRequest = 14,
                    WidthRequest = 14,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                var c = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();
                icon.Data = (Geometry)c.ConvertFromInvariantString(pathData);
                row.Children.Add(icon);
                if (!string.IsNullOrEmpty(label))
                    row.Children.Add(new Label
                    {
                        Text = label,
                        FontSize = 9,
                        TextColor = Color.FromArgb(color),
                        VerticalOptions = LayoutOptions.Center
                    });
                lifeRow.Children.Add(row);
            }

            if (vm.HasDrinks)
            {
                string drinkColor = vm.Drinks == "Yes" ? "#FF6B6B" :
                                    vm.Drinks == "Socially" ? "#FFD93D" : "#7A7A8C";
                AddLifeIcon(
                    "M280-80v-160q-51-9-85.5-49T160-380v-300h560v300q0 52-34.5 92T600-240v160H280Zm80-440h240v-80H360v80Zm-80 0v-80h-40v80h40Zm400 0v-80h-40v80h40ZM360-160h240v-100q-29 9-60 9t-60-9q-29 9-60 9t-60-9v100ZM280-340h400q26 0 43-17t17-43v-60H240v60q0 26 17 43t23 17Z",
                    drinkColor);
            }

            if (vm.Smokes)
                AddLifeIcon(
                    "M840-360v-80h80v80h-80Zm-80 0v-80h40v80h-40ZM80-280v-80h600v80H80Zm680 0v-80h80v80h-80ZM680-440q-33 0-56.5-23.5T600-520q0-20 8-38t22-32l50-60q10-12 15-26t5-24q0-20-13.5-34T660-748q-16 0-29 8t-19 20l-62-44q16-28 43-42t47-14q48 0 83 32.5T758-700q0 23-7 43.5T731-618l-51 62q-8 10-11.5 20t-3.5 16q0 20 14 34t34 14v80Z",
                    "#FF8C69");

            if (vm.HasPets)
                AddLifeIcon(
                    "M182-200q-51 0-79-35.5T82-322l42-301q6-43 37-70t74-27q43 0 73.5 27.5T349-622l13 94q15-5 31-7.5t35-2.5q17 0 32 2.5t30 7.5l13-95q6-42 36.5-69.5T576-720q43 0 74 27t37 70l42 301q9 51-19 86.5T631-200q-35 0-65.5-19.5T520-270l-28-80h-64l-28 80q-15 51-45 60.5T182-200Z",
                    "#00B5B5");

            if (vm.HasFavoriteMusic)
                AddLifeIcon(
                    "M400-120q-66 0-113-47t-47-113q0-66 47-113t113-47q23 0 42.5 5.5T480-418v-422h240v160H560v422q0 66-47 113t-113 47Z",
                    "#A78BFA");

            if (vm.HasTopMovie)
                AddLifeIcon(
                    "M160-720v-80h480v80H160Zm-80 560v-480h640v480H80Zm80-80h480v-320H160v320Zm-80-560v-80h640v80H80Zm560 560v-320l160 160-160 160Zm-400-80Z",
                    "#FBBF24");

            if (vm.HasVoiceIntro)
                AddLifeIcon(
                    "M480-400q-50 0-85-35t-35-85v-240q0-50 35-85t85-35q50 0 85 35t35 85v240q0 50-35 85t-85 35Zm0-240Zm-40 520v-123q-104-14-172-93t-68-184h80q0 83 58.5 141.5T480-320q83 0 141.5-58.5T680-520h80q0 105-68 184t-172 93v123h-80Zm40-360q17 0 28.5-11.5T520-520v-240q0-17-11.5-28.5T480-800q-17 0-28.5 11.5T440-760v240q0 17 11.5 28.5T480-480Z",
                    "#FF3B6F", "Voice");

            if (lifeRow.Children.Any()) body.Children.Add(lifeRow);

            // ── Interests chips ──
            if (vm.HasInterests)
            {
                var intRow = new HorizontalStackLayout { Spacing = 5 };
                foreach (var interest in vm.InterestsList.Take(3))
                {
                    intRow.Children.Add(new Border
                    {
                        BackgroundColor = Color.FromArgb("#1C1C28"),
                        StrokeThickness = 1,
                        Stroke = Color.FromArgb("#2A2A40"),
                        StrokeShape = new RoundRectangle { CornerRadius = 8 },
                        Padding = new Thickness(7, 3),
                        Content = new Label { Text = interest, FontSize = 10, TextColor = Color.FromArgb("#00B5B5") }
                    });
                }
                body.Children.Add(intRow);
            }

            body.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#1E1E2A"),
                HorizontalOptions = LayoutOptions.Fill
            });

            // ── Action bar ──
            var actionRow = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Start };

            void AddActionBtn(string pathData, string color)
            {
                var btn = new Border
                {
                    BackgroundColor = Color.FromArgb("#0E0E12"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#2A2A38"),
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Padding = new Thickness(9, 6)
                };
                var iconPath = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb(color)),
                    HeightRequest = 13,
                    WidthRequest = 13,
                    Aspect = Stretch.Uniform,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
                var ac = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();
                iconPath.Data = (Geometry)ac.ConvertFromInvariantString(pathData);
                btn.Content = iconPath;
                actionRow.Children.Add(btn);
            }

            // Message button
            AddActionBtn(
                "M880-80 720-240H160q-33 0-56.5-23.5T80-320v-480q0-33 23.5-56.5T160-880h640q33 0 56.5 23.5T880-800v720ZM160-320h594l46 45v-525H160v480Zm0 0v-480 480Z",
                "#00B5B5");

            // Spark button
            AddActionBtn(
                "M420-120q-25 0-42.5-17.5T360-180v-220L168-724q-15-26-3.5-51T204-800h552q30 0 41.5 25t-3.5 51L600-400v220q0 25-17.5 42.5T540-120h-120Z",
                "#FFD700");

            body.Children.Add(actionRow);
            root.Children.Add(body);
            card.Content = root;
            return card;
        }

        // Add a method to search for posts by hashtag
        public async Task SearchByHashtagAsync(string hashtag)
        {
            try
            {
                string searchTerm = hashtag.StartsWith("#") ? hashtag.Substring(1) : hashtag;

                if (SearchBar != null)
                {
                    SearchBar.Text = hashtag;
                }

                // Get all posts
                var allPosts = await SupabaseService.GetAsync<Lock.Models.Post>("Posts", "");

                // Filter posts that contain the hashtag in their content
                var hashtagPattern = $"#{searchTerm}";
                var matchingPosts = allPosts
                    .Where(p => !string.IsNullOrEmpty(p.Content) &&
                               p.Content.Contains(hashtagPattern, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                // Load author info for these posts
                await LoadPostAuthorInfo(matchingPosts);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AllUsersSection.IsVisible = false;
                    SearchResultsSection.IsVisible = false;

                    var postResultsSection = this.FindByName<ScrollView>("PostResultsSection");
                    if (postResultsSection != null)
                        postResultsSection.IsVisible = true;

                    PostSearchResults.Clear();
                    foreach (var post in matchingPosts)
                    {
                        var vm = new PostSearchResultViewModel(post);
                        PostSearchResults.Add(vm);
                    }

                    EmptyContainer.IsVisible = !matchingPosts.Any();
                    if (!matchingPosts.Any())
                    {
                        EmptyLabel.Text = "No posts found";
                        EmptySubLabel.Text = $"No posts with {hashtag}";
                    }
                    else
                    {
                        EmptyContainer.IsVisible = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SearchByHashtagAsync error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", "Could not search for hashtag", "OK");
                });
            }
        }



        // Helper method to load author info for posts
        private async Task LoadPostAuthorInfo(List<Lock.Models.Post> posts)
        {
            try
            {
                var authorPhones = posts.Select(p => p.AuthorPhone).Distinct().ToList();
                var authorInfo = new Dictionary<string, (string Name, string ProfileImage)>();

                foreach (var phone in authorPhones)
                {
                    var users = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var user = users.FirstOrDefault();
                    if (user != null)
                    {
                        authorInfo[phone] = (user.Name ?? phone, user.ProfileImagePath ?? "");
                    }
                }

                foreach (var post in posts)
                {
                    if (authorInfo.TryGetValue(post.AuthorPhone, out var info))
                    {
                        post.AuthorDisplayName = info.Name;
                        post.AuthorProfileImagePath = info.ProfileImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPostAuthorInfo error: {ex}");
            }
        }

        // Add this method to SearchPage class
        public void SetSearchText(string searchQuery)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    // Set the search bar text
                    if (SearchBar != null)
                    {
                        SearchBar.Text = searchQuery;
                    }

                    // Check if it's a hashtag search
                    if (searchQuery.StartsWith("#"))
                    {
                        await SearchByHashtagAsync(searchQuery);
                    }
                    else
                    {
                        // Regular search
                        OnSearch(SearchBar, EventArgs.Empty);
                    }
                }
            });
        }

        private void ShowFilteredResults()
        {
            IEnumerable<User> q = _filteredUsers;

            // Name / phone search - with HidePhoneNumber respect
            if (!string.IsNullOrWhiteSpace(_currentSearchQuery))
            {
                var sq = _currentSearchQuery.Trim();
                q = q.Where(u =>
                    u.Name?.Contains(sq, StringComparison.OrdinalIgnoreCase) == true ||
                    // Only search by phone if HidePhoneNumber is false
                    (!u.HidePhoneNumber && u.PhoneNumber?.Contains(sq) == true));
            }

            // Mood — no AllowMoodSearch gate needed
            if (!string.IsNullOrEmpty(_selectedMood))
            {
                var ml = _selectedMood.Trim().ToLowerInvariant();
                q = q.Where(u =>
                    !string.IsNullOrEmpty(u.Mood) &&
                    u.Mood.Trim().ToLowerInvariant() == ml);
                System.Diagnostics.Debug.WriteLine(
                    $"[Mood] '{_selectedMood}' → {q.Count()} hits");
            }

            // Location
            if (_selectedLocation != "All Locations" && !string.IsNullOrEmpty(_selectedLocation))
                q = q.Where(u => string.Equals(GetLocation(u), _selectedLocation,
                                               StringComparison.OrdinalIgnoreCase));

            // Age
            if (_selectedMinAge.HasValue || _selectedMaxAge.HasValue)
            {
                q = q.Where(u =>
                {
                    var a = CalcAge(u.DateOfBirth);
                    if (_selectedMinAge.HasValue && a < _selectedMinAge.Value) return false;
                    if (_selectedMaxAge.HasValue && a > _selectedMaxAge.Value) return false;
                    return true;
                });
            }

            var list = q.Take(20).ToList();

            SearchResultsSection.IsVisible = true;
            AllUsersSection.IsVisible = false;
            SearchResults.Clear();
            foreach (var u in list)
                SearchResults.Add(new UserCardViewModel(u, GetLocation(u)));

            EmptyContainer.IsVisible = !SearchResults.Any();
            if (!SearchResults.Any())
            {
                EmptyLabel.Text = "No results found";
                EmptySubLabel.Text = BuildEmptyText();
            }
        }
        private string BuildEmptyText()
        {
            if (!string.IsNullOrEmpty(_selectedMood)) return $"No users with mood \"{_selectedMood}\"";
            if (_selectedLocation != "All Locations") return $"No users in {_selectedLocation}";
            if (_selectedMinAge.HasValue || _selectedMaxAge.HasValue) return "No users in that age range";
            if (!string.IsNullOrWhiteSpace(_currentSearchQuery)) return $"No users match \"{_currentSearchQuery}\"";
            return "Try a different filter";
        }

        private bool AnyFilterActive() =>
            !string.IsNullOrWhiteSpace(_currentSearchQuery) ||
            _selectedLocation != "All Locations" ||
            !string.IsNullOrEmpty(_selectedMood) ||
            _selectedMinAge.HasValue ||
            _selectedMaxAge.HasValue;

        private void Refresh() { if (AnyFilterActive()) ShowFilteredResults(); else ShowAllUsers(); }

        // ── Chip management (built in code — close button top-right) ─────

        /// <summary>
        /// Add or replace a chip. One chip per FilterType maximum.
        /// </summary>
        private void UpsertChip(string filterType, string displayValue)
        {
            _activeFilterMap[filterType] = displayValue;
            RebuildChips();
        }

        private void RemoveChip(string filterType)
        {
            _activeFilterMap.Remove(filterType);
            RebuildChips();
        }

        private void RebuildChips()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var layout = this.FindByName<HorizontalStackLayout>("ActiveFiltersLayout");
                var scrollView = this.FindByName<ScrollView>("ActiveFiltersScrollView");
                if (layout == null || scrollView == null) return;

                layout.Children.Clear();
                scrollView.IsVisible = _activeFilterMap.Any();

                foreach (var kv in _activeFilterMap)
                {
                    var capturedType = kv.Key;

                    // Create the chip container with GREEN border (or your theme color)
                    var chipBorder = new Border
                    {
                        BackgroundColor = Color.FromArgb("#2A2A2A"),
                        StrokeThickness = 1,
                        Stroke = Color.FromArgb("#008080"),
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Padding = new Thickness(12, 6, 8, 6), // Right padding for X button
                        VerticalOptions = LayoutOptions.Center,
                        HeightRequest = 32
                    };

                    // Grid with 2 columns: label (stretch) + X button (auto)
                    var grid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                        VerticalOptions = LayoutOptions.Center
                    };

                    // Label in column 0
                    var label = new Label
                    {
                        Text = kv.Value,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#F0F0F0"),
                        VerticalOptions = LayoutOptions.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        Margin = new Thickness(0, 0, 8, 0) // Space before X button
                    };
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    // Close button (X) in column 1 - using Frame for reliable red background
                    var closeButton = new Frame
                    {
                        Content = new Label
                        {
                            Text = "X",
                            FontSize = 10,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center,
                            Margin = 0,
                            Padding = 0,
                            LineHeight = 1.0
                        },
                        BackgroundColor = Color.FromArgb("#C05050"), // 🔴 BRIGHT RED
                        CornerRadius = 9,
                        HasShadow = false,
                        Padding = 0,
                        WidthRequest = 18,
                        HeightRequest = 18,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        IsClippedToBounds = true
                    };

                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (_, _) => ClearFilterByType(capturedType);
                    closeButton.GestureRecognizers.Add(tap);

                    Grid.SetColumn(closeButton, 1);
                    grid.Children.Add(closeButton);

                    chipBorder.Content = grid;
                    chipBorder.Margin = new Thickness(0, 0, 8, 0);

                    layout.Children.Add(chipBorder);
                }
            });
        }

        private async void OnMatchHeaderTapped(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new MatchPage());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnMatchHeaderTapped error: {ex}");
                await DisplayAlert("Error", "Could not open Match page", "OK");
            }
        }

        private void ClearFilterByType(string filterType)
        {
            switch (filterType)
            {
                case "Search":
                    _currentSearchQuery = "";
                    MainThread.BeginInvokeOnMainThread(() => { if (SearchBar != null) SearchBar.Text = ""; });
                    break;
                case "Mood":
                    _selectedMood = "";
                    MainThread.BeginInvokeOnMainThread(() => { if (MoodPicker != null) MoodPicker.SelectedIndex = 0; });
                    break;
                case "Location":
                    _selectedLocation = "All Locations";
                    MainThread.BeginInvokeOnMainThread(() => { if (LocationPicker != null) LocationPicker.SelectedIndex = 0; });
                    break;
                case "Age":
                    _selectedMinAge = null; _selectedMaxAge = null;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (MinAgeEntry != null) MinAgeEntry.Text = "";
                        if (MaxAgeEntry != null) MaxAgeEntry.Text = "";
                    });
                    break;
            }
            RemoveChip(filterType);
            Refresh();
        }

        // ── Age entry: strip non-digits live ─────────────────────────────

        private void OnAgeEntryChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is Entry entry)
            {
                var clean = new string(e.NewTextValue?.Where(char.IsDigit).ToArray());
                if (clean != e.NewTextValue) entry.Text = clean;
            }
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnMoodSelected(object sender, EventArgs e)
        {
            if (MoodPicker.SelectedIndex < 0) return;
            var sel = MoodPicker.Items[MoodPicker.SelectedIndex];
            if (sel == "All moods") { _selectedMood = ""; RemoveChip("Mood"); }
            else { _selectedMood = sel; UpsertChip("Mood", sel); }
            Refresh();
        }

        private void OnLocationSelected(object sender, EventArgs e)
        {
            if (LocationPicker.SelectedIndex < 0) return;
            var sel = LocationPicker.Items[LocationPicker.SelectedIndex];
            if (sel == "All Locations") { _selectedLocation = "All Locations"; RemoveChip("Location"); }
            else { _selectedLocation = sel; UpsertChip("Location", sel); }
            Refresh();
        }

        private void ApplyAgeFilter_Clicked(object sender, EventArgs e)
        {
            int? minAge = null, maxAge = null;

            if (!string.IsNullOrWhiteSpace(MinAgeEntry?.Text) &&
                int.TryParse(MinAgeEntry.Text.Trim(), out var mn)) minAge = mn;
            if (!string.IsNullOrWhiteSpace(MaxAgeEntry?.Text) &&
                int.TryParse(MaxAgeEntry.Text.Trim(), out var mx)) maxAge = mx;

            System.Diagnostics.Debug.WriteLine($"[Age] min={minAge} max={maxAge}");

            if (!minAge.HasValue && !maxAge.HasValue)
            {
                _selectedMinAge = null; _selectedMaxAge = null;
                RemoveChip("Age"); Refresh(); return;
            }

            // Swap if reversed
            if (minAge.HasValue && maxAge.HasValue && minAge > maxAge)
                (minAge, maxAge) = (maxAge, minAge);

            _selectedMinAge = minAge;
            _selectedMaxAge = maxAge;

            var label = (minAge.HasValue && maxAge.HasValue) ? $"{minAge}–{maxAge}"
                       : minAge.HasValue ? $"{minAge}+"
                       : $"Up to {maxAge}";

            UpsertChip("Age", label);
            Refresh();
        }


        private void OnSearch(object sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            _currentSearchQuery = SearchBar.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(_currentSearchQuery)) UpsertChip("Search", _currentSearchQuery);
            else RemoveChip("Search");
            Refresh();
        }

        private void TabClicked(object sender, EventArgs e)
        {
            var btn = (Button)sender;

            var allBorders = new[] { AllTabFrame, NameTabFrame, PhoneTabFrame, MoodTabFrame, LocationTabFrame, AgeRangeTabFrame };
            var allBtns = new[] { AllTab, NameTab, PhoneTab, MoodTab, LocationTab, AgeRangeTab };

            // Reset all borders
            foreach (var border in allBorders)
            {
                if (border == null) continue;
                border.BackgroundColor = Color.FromArgb("#1E1E1E");
                border.Stroke = Color.FromArgb("#2E2E2E");  // Changed from BorderColor to Stroke
            }

            // Reset all buttons
            foreach (var b in allBtns)
            {
                if (b == null) continue;
                b.TextColor = Color.FromArgb("#AAAAAA");
                b.FontAttributes = FontAttributes.None;
            }

            btn.TextColor = Colors.White;
            btn.FontAttributes = FontAttributes.Bold;
            SetActiveTab(btn);
            _currentTab = btn.Text.Trim();

            if (MoodDropdownFrame != null) MoodDropdownFrame.IsVisible = false;
            if (LocationDropdownFrame != null) LocationDropdownFrame.IsVisible = false;
            if (AgeRangeFrame != null) AgeRangeFrame.IsVisible = false;

            switch (_currentTab)
            {
                case "Mood": if (MoodDropdownFrame != null) MoodDropdownFrame.IsVisible = true; break;
                case "Location": LoadLocationPickerItems(); if (LocationDropdownFrame != null) LocationDropdownFrame.IsVisible = true; break;
                case "Age": if (AgeRangeFrame != null) AgeRangeFrame.IsVisible = true; break;
            }

            if (_currentTab != "Mood" && !string.IsNullOrEmpty(_selectedMood))
            {
                _selectedMood = "";
                if (MoodPicker != null) MoodPicker.SelectedIndex = 0;
                RemoveChip("Mood");
            }
            if (_currentTab != "Location" && _selectedLocation != "All Locations")
            {
                _selectedLocation = "All Locations";
                if (LocationPicker != null) LocationPicker.SelectedIndex = 0;
                RemoveChip("Location");
            }
            if (_currentTab != "Age" && (_selectedMinAge.HasValue || _selectedMaxAge.HasValue))
            {
                _selectedMinAge = null;
                _selectedMaxAge = null;
                if (MinAgeEntry != null) MinAgeEntry.Text = "";
                if (MaxAgeEntry != null) MaxAgeEntry.Text = "";
                RemoveChip("Age");
            }

            Refresh();
        }
        private void SetActiveTab(Button btn)
        {
            Border? border = btn switch
            {
                var b when b == AllTab => AllTabFrame,
                var b when b == NameTab => NameTabFrame,
                var b when b == PhoneTab => PhoneTabFrame,
                var b when b == MoodTab => MoodTabFrame,
                var b when b == LocationTab => LocationTabFrame,
                var b when b == AgeRangeTab => AgeRangeTabFrame,
                _ => null
            };

            if (border != null)
            {
                border.BackgroundColor = Colors.Transparent;
                border.Stroke = Color.FromArgb("#008080");  // Border uses Stroke, not BorderColor
            }
        }

        private void LoadLocationPickerItems()
        {
            var locs = _filteredUsers.Select(GetLocation).Where(l => !string.IsNullOrEmpty(l)).Distinct().OrderBy(l => l).ToList();
            LocationPicker.Items.Clear();
            LocationPicker.Items.Add("All Locations");
            foreach (var l in locs) LocationPicker.Items.Add(l);
            if (LocationPicker.SelectedIndex == -1) LocationPicker.SelectedIndex = 0;
        }

        private async void OnUserCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is UserCardViewModel vm)
                await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                { ["phone"] = vm.PhoneNumber, ["viewOnly"] = "true" });
        }

        private async void UserHeaderTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string phone)
                await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                { ["phone"] = phone, ["viewOnly"] = "true" });
        }

        private async void AuthorName_Tapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is UserCardViewModel vm)
                await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                { ["phone"] = vm.PhoneNumber, ["viewOnly"] = "true" });
        }
    }

    // ── Supporting types ──────────────────────────────────────────────────

    public class FilterItem : INotifyPropertyChanged
    {
        public string FilterType { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;
        public string FilterText => FilterValue;
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public override bool Equals(object? obj) => obj is FilterItem o && FilterType == o.FilterType && FilterValue == o.FilterValue;
        public override int GetHashCode() => HashCode.Combine(FilterType, FilterValue);
    }

    public class UserCardViewModel : INotifyPropertyChanged
    {
        private readonly User _user;
        private readonly string _location;
        private int _matchPercent;

        public UserCardViewModel(User user, string location)
        {
            _user = user;
            _location = location;
            ComputeAge();
            _ = LoadMatchPercentageAsync(); // Load match percentage asynchronously
        }

        // Add this property
        public int MatchPercent
        {
            get => _matchPercent;
            set
            {
                if (_matchPercent != value)
                {
                    _matchPercent = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasMatchPercent));
                    OnPropertyChanged(nameof(MatchPercentDisplay));
                }
            }
        }

        public bool HasMatchPercent => MatchPercent > 0;
        public string MatchPercentDisplay => $"{MatchPercent}% match";

        // Add this method to load match percentage
        public async Task LoadMatchPercentageAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();

                if (string.Equals(currentUserPhone, _user.PhoneNumber, StringComparison.OrdinalIgnoreCase))
                {
                    MatchPercent = 0;
                    return;
                }

                if (string.IsNullOrEmpty(currentUserPhone) || string.IsNullOrEmpty(_user.PhoneNumber))
                {
                    MatchPercent = 0;
                    return;
                }

                var currentUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var currentUser = currentUsers.FirstOrDefault();

                if (currentUser == null)
                {
                    MatchPercent = 0;
                    return;
                }

                MatchPercent = await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, _user);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadMatchPercentageAsync error: {ex}");
                MatchPercent = 0;
            }
        }


        // Basic Info
        public string Name => _user.Name ?? "Unknown";
        public string PhoneNumber => _user.PhoneNumber;
        public string ProfileImagePath => _user.ProfileImagePath;
        public bool IsVerified => _user.IsVerified;
        public string Mood => _user.Mood ?? "No mood set";
        public string Gender => _user.Gender ?? "—";
        public string Interest => _user.Interest ?? "—";
        public string Location => _location;
        public int? Age { get; private set; }
        public bool HasAge => Age is > 0;
        public bool HasMood => !string.IsNullOrEmpty(_user.Mood);
        public bool HasLocation => !string.IsNullOrEmpty(_location);
        public bool HasBio => !string.IsNullOrEmpty(_user.Bio);
        public string ShortBio => _user.Bio?.Length > 80 ? _user.Bio[..80] + "…" : _user.Bio ?? string.Empty;

        // Physical Attributes
        public int? HeightCm => _user.HeightCm;
        public bool HasHeight => _user.HeightCm.HasValue && _user.HeightCm.Value > 0;
        public string HeightDisplay
        {
            get
            {
                if (!HasHeight) return null;
                int feet = (int)(_user.HeightCm.Value / 30.48);
                int inches = (int)((_user.HeightCm.Value % 30.48) / 2.54);
                return $"{feet}'{inches}\"";
            }
        }

        public string BodyType => _user.BodyType;
        public bool HasBodyType => !string.IsNullOrEmpty(_user.BodyType);

        public string Ethnicity => _user.Ethnicity;
        public string Tribe => _user.Tribe;
        public bool HasEthnicity => !string.IsNullOrEmpty(_user.Ethnicity) || !string.IsNullOrEmpty(_user.Tribe);
        public string EthnicityDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(_user.Ethnicity) && !string.IsNullOrEmpty(_user.Tribe))
                    return $"{_user.Ethnicity} · {_user.Tribe}";
                return _user.Ethnicity ?? _user.Tribe;
            }
        }

        // Family & Lifestyle
        public string KidsPreference => _user.KidsPreference;
        public string HasChildren => _user.HasChildren;
        public string DietaryPreference => _user.DietaryPreference;
        public string ExerciseFrequency => _user.ExerciseFrequency;
        public string Drinks => _user.Drinks;
        public bool HasDrinks => !string.IsNullOrEmpty(_user.Drinks);
        public Color DrinkColor => GetDrinkColor();
        public bool Smokes => _user.Smokes;
        public bool HasPets => _user.HasPets;

        // Personality
        public string PersonalityType => _user.PersonalityType;
        public bool HasPersonalityType => !string.IsNullOrEmpty(_user.PersonalityType);
        public string PersonalityTypeShort => HasPersonalityType ? _user.PersonalityType.Split('-')[0].Trim() : null;

        public string LoveLanguage => _user.LoveLanguage;
        public bool HasLoveLanguage => !string.IsNullOrEmpty(_user.LoveLanguage);
        public string LoveLanguageShort => HasLoveLanguage && _user.LoveLanguage.Length > 15
            ? _user.LoveLanguage.Substring(0, 12) + "…"
            : _user.LoveLanguage;

        public string EnergyLevel => _user.EnergyLevel;
        public bool HasEnergyLevel => !string.IsNullOrEmpty(_user.EnergyLevel);
        public string EnergyLevelShort => HasEnergyLevel && _user.EnergyLevel.Length > 8
            ? _user.EnergyLevel.Substring(0, 6) + "…"
            : _user.EnergyLevel;

        // Background
        public string Religion => _user.Religion;
        public string PoliticalViews => _user.PoliticalViews;
        public string Occupation => _user.Occupation;
        public string Education => _user.Education;
        public string Languages => _user.Languages;

        // Interests & Entertainment
        public List<string> InterestsList => (_user.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim()).ToList();
        public bool HasInterests => InterestsList.Any();
        public string TopInterest => _user.TopInterest;
        public string TopArtist => _user.TopArtist;
        public string TopMovie => _user.TopMovie;
        public bool HasTopMovie => !string.IsNullOrEmpty(_user.TopMovie);
        public string MusicGenres => _user.MusicGenres;
        public string FavoriteMusicGenre => _user.FavoriteMusicGenre;
        public bool HasFavoriteMusic => !string.IsNullOrEmpty(_user.FavoriteMusicGenre);
        public string BestMusic => _user.BestMusic;
        public string FavoriteMovies => _user.FavoriteMovies;
        public string FavoriteBooks => _user.FavoriteBooks;
        public string FavoriteArtists => _user.FavoriteArtists;

        // Voice Intro
        public bool HasVoiceIntro => !string.IsNullOrEmpty(_user.VoiceIntroPath) && File.Exists(_user.VoiceIntroPath);

        // Join Date
        public string JoinYear => _user.JoinDate.Year.ToString();
        public string JoinDateDisplay => _user.JoinDate.ToString("MMM yyyy");

        private void ComputeAge()
        {
            if (_user.DateOfBirth == DateTime.MinValue) return;
            var t = DateTime.Today;
            Age = t.Year - _user.DateOfBirth.Year;
            if (_user.DateOfBirth > t.AddYears(-Age.Value)) Age--;
        }

        private Color GetDrinkColor()
        {
            if (string.IsNullOrEmpty(_user.Drinks)) return Color.FromArgb("#888880");
            return _user.Drinks == "Yes" ? Color.FromArgb("#FF6B6B") :
                   _user.Drinks == "Socially" ? Color.FromArgb("#FFD93D") :
                   Color.FromArgb("#888880");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
    public class ImagePathConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] is string p && !string.IsNullOrEmpty(p) && File.Exists(p)) return ImageSource.FromFile(p);
            return "default_avatar.png";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class DefaultImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => "default_avatar.png";
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
