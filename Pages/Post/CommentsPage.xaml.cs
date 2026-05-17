using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Data.Post;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PostModel = Lock.Models.Post;
using Microsoft.Maui.Controls;

namespace Lock.Pages.Post
{
    public partial class CommentsPage : ContentPage
    {
        private readonly int _postId;
        private readonly string _currentUserPhone;
        private ObservableCollection<Comment> _comments;
        private Comment _selectedComment;
        private PostModel _post;


        // Suggested users (shown at the bottom like SearchPage)
        public ObservableCollection<UserCardViewModel> SuggestedUsers { get; set; } = new();

        public CommentsPage(int postId, string currentUserPhone)
        {
            InitializeComponent();

            _postId = postId;
            _currentUserPhone = currentUserPhone;
            _comments = new ObservableCollection<Comment>();

            Debug.WriteLine($"CommentsPage initialized with PostId: {_postId}, User: {_currentUserPhone}");

            if (!string.IsNullOrEmpty(_currentUserPhone))
                CommentsCollectionView.ItemTemplate = (DataTemplate)Resources["CommentWithActionsTemplate"];
            else
                CommentsCollectionView.ItemTemplate = (DataTemplate)Resources["CommentTemplate"];

            BindingContext = this;

            // Subscribe to SparkToggled messages
            MessagingCenter.Subscribe<SparkChangedMessage>(this, "SparkToggled", (msg) =>
            {
                Debug.WriteLine($"[CommentsPage] SparkToggled received: PostId={msg.PostId}, IsSparked={msg.IsSparked}");

                if (_post == null || _post.Id != msg.PostId) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        // Update the model with the authoritative values from the message
                        _post.IsSparkedByCurrentUser = msg.IsSparked;
                        _post.SparkCount = msg.SparkCount;

                        // RefreshSparkState fires OnPropertyChanged for all spark bindings
                        _post.RefreshSparkState();

                        // Force-rebind the header view — DataTemplate views don't always
                        // react to property changes if the binding context was set once
                        if (PostHeaderContainer?.Content is View postView)
                        {
                            var ctx = postView.BindingContext;
                            postView.BindingContext = null;
                            postView.BindingContext = ctx;
                        }

                        Debug.WriteLine($"[CommentsPage] Header spark updated: {msg.IsSparked}, count={msg.SparkCount}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CommentsPage] SparkToggled error: {ex}");
                    }
                });
            });
        }

        public ObservableCollection<Comment> Comments
        {
            get => _comments;
            set { _comments = value; OnPropertyChanged(nameof(Comments)); }
        }

  // ─────────────────────────────────────────────────────────────
//  LoadingOverlay — Code-behind animations
//  Paste these members into your ContentPage class.
//  Requires: SpinRing, RingDot, HeartIcon, Dot1-Dot5, LoadingOverlay
// ─────────────────────────────────────────────────────────────


// ── 1. Kick off all overlay animations ──────────────────────
private CancellationTokenSource _loaderCts = new();

    private void StartLoadingAnimations()
    {
        _loaderCts = new CancellationTokenSource();
        var token = _loaderCts.Token;

        // Ring spin — continuous 360° rotation
        _ = SpinRingAsync(token);

        // Heart pulse — scale beat
        _ = HeartPulseAsync(token);

        // Dot wave — sequential colour pulse
        _ = DotWaveAsync(token);
    }

    private void StopLoadingAnimations()
    {
        _loaderCts.Cancel();
        SpinRing.Rotation = 0;
        HeartIcon.ScaleTo(1, 80);
    }

    // ── 2. Ring: continuous rotation ────────────────────────────
    private async Task SpinRingAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await SpinRing.RotateTo(360, 2000, Easing.Linear);
            SpinRing.Rotation = 0;
        }
    }

    // ── 3. Heart: scale pulse (1 → 1.22 → 0.95 → 1) ────────────
    private async Task HeartPulseAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await HeartIcon.ScaleTo(1.22, 200, Easing.CubicOut);
            await HeartIcon.ScaleTo(0.95, 120, Easing.CubicIn);
            await HeartIcon.ScaleTo(1.00, 120, Easing.CubicOut);
            await Task.Delay(900, token).ContinueWith(_ => { }); // rest between beats
        }
    }

    // ── 4. Dots: wave colour sweep ───────────────────────────────
    private readonly Color _dotActive = Color.FromArgb("#FF3B6F");
    private readonly Color _dotInactive = Color.FromArgb("#3A3A4C");

    private async Task DotWaveAsync(CancellationToken token)
    {
        var dots = new[] { Dot1, Dot2, Dot3, Dot4, Dot5 };
        int i = 0;
        while (!token.IsCancellationRequested)
        {
            // Activate current dot
            dots[i].Fill = new SolidColorBrush(_dotActive);

            // Stretch up
            await dots[i].ScaleYTo(1.6, 120, Easing.CubicOut);
            await dots[i].ScaleYTo(1.0, 120, Easing.CubicIn);

            // Fade back
            dots[i].Fill = new SolidColorBrush(_dotInactive);

            i = (i + 1) % dots.Length;
            await Task.Delay(160, token).ContinueWith(_ => { });
        }
    }

    // ── 5. OnAppearing (replace your existing one) ───────────────
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Show overlay
        if (LoadingOverlay != null)
        {
            LoadingOverlay.IsVisible = true;
            LoadingOverlay.Opacity = 0;
            await LoadingOverlay.FadeTo(1, 300, Easing.CubicOut);
            StartLoadingAnimations();
        }

        // Subscribe to refresh messages
        MessagingCenter.Subscribe<RefreshPostMessage>(this, "RefreshPostData", async (msg) =>
        {
            if (_post != null && _post.Id == msg.PostId)
            {
                var refreshedPost = await PostRepository.GetByIdAsync(msg.PostId);
                if (refreshedPost != null)
                {
                    _post.IsSparkedByCurrentUser = refreshedPost.IsSparkedByCurrentUser;
                    _post.SparkCount = refreshedPost.SparkCount;
                    RefreshPostView(_post);
                }
            }
        });

        // Load data
        await LoadPostAsync();
        await LoadCommentsAsync();
        await LoadSuggestedUsersAsync();

        // Hide overlay with smooth fade
        if (LoadingOverlay != null)
        {
            StopLoadingAnimations();
            await LoadingOverlay.FadeTo(0, 400, Easing.CubicIn);
            LoadingOverlay.IsVisible = false;
        }
    }
    public class RefreshPostMessage
        {
            public int PostId { get; set; }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _discoverShuffleTimer?.Dispose();
            _discoverShuffleTimer = null;
            MessagingCenter.Unsubscribe<SparkChangedMessage>(this, "SparkToggled");
            MessagingCenter.Unsubscribe<RefreshPostMessage>(this, "RefreshPostData");
        }

        // Suggested Users loading
        private System.Timers.Timer? _discoverShuffleTimer;

        private async Task LoadSuggestedUsersAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var allUsers = await db.Table<User>().ToListAsync();

                var filtered = allUsers
                    .Where(u => !string.Equals(u.PhoneNumber, _currentUserPhone,
                                StringComparison.OrdinalIgnoreCase)
                             && !u.GhostModeMoodShield
                             && !u.HidePhoneNumber)  // ADD THIS - exclude users hiding phone number
                    .ToList();

                // Store for reshuffling
                _allSuggestedUsers = filtered;

                PopulateSuggestedCards();
                StartDiscoverShuffleTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSuggestedUsersAsync error: {ex}");
            }
        }
        private List<User> _allSuggestedUsers = new();

        private void PopulateSuggestedCards()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var layout = this.FindByName<HorizontalStackLayout>("SuggestedUsersLayout");
                if (layout == null) return;

                layout.Children.Clear();

                var shuffled = _allSuggestedUsers
                    .Where(u => !u.HidePhoneNumber)  // ADD THIS - safety check
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(15)
                    .ToList();

                foreach (var u in shuffled)
                {
                    var vm = new UserCardViewModel(u, GetLocation(u));
                    var card = BuildDiscoverCard(vm);
                    layout.Children.Add(card);
                }
            });
        }
        private void StartDiscoverShuffleTimer()
        {
            _discoverShuffleTimer?.Dispose();
            _discoverShuffleTimer = new System.Timers.Timer(30_000) { AutoReset = true };
            _discoverShuffleTimer.Elapsed += (_, _) => PopulateSuggestedCards();
            _discoverShuffleTimer.Start();
        }

        private View BuildDiscoverCard(UserCardViewModel vm)
        {
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#12121A"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#2A2A38"),
                StrokeShape = new RoundRectangle { CornerRadius = 24 },
                WidthRequest = 200,
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
            var photoGrid = new Grid { HeightRequest = 140 };

            var photo = new Image { Aspect = Aspect.AspectFill, HeightRequest = 140 };
            if (!string.IsNullOrEmpty(vm.ProfileImagePath) && File.Exists(vm.ProfileImagePath))
                photo.Source = ImageSource.FromFile(vm.ProfileImagePath);
            else
                photo.Source = "default_avatar.png";
            photoGrid.Children.Add(photo);

            // Gradient overlay
            var grad = new BoxView { VerticalOptions = LayoutOptions.End, HeightRequest = 80, Opacity = 1 };
            grad.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
        {
            new GradientStop { Color = Colors.Transparent, Offset = 0 },
            new GradientStop { Color = Color.FromArgb("#12121A"), Offset = 1 }
        }
            };
            photoGrid.Children.Add(grad);

            // Name + verified row overlay
            var nameRow = new HorizontalStackLayout
            {
                VerticalOptions = LayoutOptions.End,
                Padding = new Thickness(10, 0, 10, 10),
                Spacing = 5
            };

            nameRow.Children.Add(new Label
            {
                Text = vm.Name,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
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
                    HeightRequest = 15,
                    WidthRequest = 15,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                var converter = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();
                verifiedPath.Data = (Geometry)converter.ConvertFromInvariantString(
                    "m366-126-64-108-122-26 12-126-82-94 82-94-12-126 122-26 64-108 114 48 114-48 64 108 122 26-12 126 82 94-82 94 12 126-122 26-64 108-114-48-114 48Zm12-36 102-42 102 42 58-96 110-24-10-114 74-84-74-84 10-114-110-24-58-96-102 42-102-42-58 96-110 24 10 114-74 84 74 84-10 114 110 24 58 96Zm102-318Zm-42 106 190-190-20-20-170 170-86-86-20 20 106 106Z");
                nameRow.Children.Add(verifiedPath);
            }

            if (vm.HasAge)
            {
                nameRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#00000070"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    Padding = new Thickness(5, 2),
                    Content = new Label
                    {
                        Text = vm.Age?.ToString(),
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5")
                    }
                });
            }

            photoGrid.Children.Add(nameRow);
            root.Children.Add(photoGrid);

            // ── Card body ──
            var body = new VerticalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(10, 10, 10, 12)
            };

            // Mood pill
            if (vm.HasMood)
            {
                body.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#2A1520"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#FF3B6F"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(7, 3),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = new Label
                    {
                        Text = vm.Mood,
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#FF3B6F"),
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation
                    }
                });
            }

            // Match percent
            if (vm.HasMatchPercent)
            {
                body.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#0D1F1F"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#22C55E"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(7, 3),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = new Label
                    {
                        Text = vm.MatchPercentDisplay,
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#22C55E")
                    }
                });
            }

            // Thin divider
            body.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#1E1E2A"),
                HorizontalOptions = LayoutOptions.Fill
            });

            // ── Gender · Interest row with colors ──
            var genderInterestRow = new HorizontalStackLayout { Spacing = 6 };

            // Gender icon + label
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

            genderInterestRow.Children.Add(new Label
            {
                Text = genderIcon,
                FontSize = 13,
                TextColor = genderColor,
                VerticalOptions = LayoutOptions.Center
            });
            genderInterestRow.Children.Add(new Label
            {
                Text = vm.Gender,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = genderColor,
                VerticalOptions = LayoutOptions.Center
            });

            genderInterestRow.Children.Add(new Label
            {
                Text = "→",
                FontSize = 11,
                TextColor = Color.FromArgb("#3A3A4A"),
                VerticalOptions = LayoutOptions.Center
            });

            // Interest with color
            string interestIcon = vm.Interest?.ToLower() switch
            {
                "men" => "♂",
                "women" => "♀",
                "everyone" => "⚡",
                _ => "✦"
            };
            Color interestColor = vm.Interest?.ToLower() switch
            {
                "men" => Color.FromArgb("#60A5FA"),
                "women" => Color.FromArgb("#F472B6"),
                "everyone" => Color.FromArgb("#FBBF24"),
                _ => Color.FromArgb("#A78BFA")
            };

            genderInterestRow.Children.Add(new Label
            {
                Text = interestIcon,
                FontSize = 13,
                TextColor = interestColor,
                VerticalOptions = LayoutOptions.Center
            });
            genderInterestRow.Children.Add(new Label
            {
                Text = vm.Interest,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = interestColor,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center
            });

            body.Children.Add(genderInterestRow);

            // ── Location row with SVG pin ──
            if (vm.HasLocation)
            {
                var locationRow = new HorizontalStackLayout { Spacing = 5 };

                var locationIcon = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    HeightRequest = 12,
                    WidthRequest = 12,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                var locConverter = new Microsoft.Maui.Controls.Shapes.PathGeometryConverter();
                locationIcon.Data = (Geometry)locConverter.ConvertFromInvariantString(
                    "M522.5-511.5Q540-529 540-554t-17.5-42.5Q505-614 480-614t-42.5 17.5Q420-579 420-554t17.5 42.5Q455-494 480-494t42.5-17.5ZM480-169q110-94 177.5-198.5T725-547q0-110-69.5-182T480-801q-106 0-175.5 72T235-547q0 75 67.5 179.5T480-169Zm0 38Q345-252 276-357t-69-190q0-120 78.5-200.5T480-828q116 0 194.5 80.5T753-547q0 85-69 190T480-131Zm0-423Z");

                locationRow.Children.Add(locationIcon);
                locationRow.Children.Add(new Label
                {
                    Text = vm.Location,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#8A8A9A"),
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                });

                body.Children.Add(locationRow);
            }

            // ── Physical chips ──
            var chipRow = new HorizontalStackLayout { Spacing = 5 };
            void AddChip(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                chipRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#1C1C28"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#2A2A40"),
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    Padding = new Thickness(7, 3),
                    Content = new Label
                    {
                        Text = text,
                        FontSize = 9,
                        TextColor = Color.FromArgb("#7A7A9A")
                    }
                });
            }
            if (vm.HasHeight) AddChip(vm.HeightDisplay);
            if (vm.HasBodyType) AddChip(vm.BodyType);
            if (chipRow.Children.Any()) body.Children.Add(chipRow);

            root.Children.Add(body);
            card.Content = root;
            return card;
        }
        private string GetLocation(User u)
        {
            if (!string.IsNullOrEmpty(u.Country) && !string.IsNullOrEmpty(u.State))
                return $"{u.State}, {u.Country}";
            if (!string.IsNullOrEmpty(u.Country)) return u.Country;
            if (!string.IsNullOrEmpty(u.State)) return u.State;
            return "";
        }

        private async void OnUserCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is UserCardViewModel vm)
                await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                { ["phone"] = vm.PhoneNumber, ["viewOnly"] = "true" });
        }

        private async void OnBackTapped(object sender, TappedEventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnSendTapped(object sender, TappedEventArgs e)
        {
            await PostCommentAsync();
        }

        // Load Post - only once on page load
        private async Task LoadPostAsync()
        {
            try
            {
                Debug.WriteLine($"Loading post with ID: {_postId}");
                _post = await PostRepository.GetByIdAsync(_postId);

                if (_post != null)
                {
                    _post.UpdateDisplayContent(200);

                    if (!string.IsNullOrEmpty(_currentUserPhone))
                    {
                        _post.IsLovedByCurrentUser = _post.LovedBy.Contains(_currentUserPhone);
                        _post.IsSparkedByCurrentUser = _post.SparkedBy.Contains(_currentUserPhone);
                        await CalculateMatchPercentageForPost(_post);
                    }

                    if (string.IsNullOrEmpty(_post.AuthorProfileImagePath))
                        _post.AuthorProfileImagePath = await GetUserProfileImagePathAsync(_post.AuthorPhone);

                    // Load author mood and display name
                    if (!string.IsNullOrEmpty(_post.AuthorPhone))
                    {
                        await DatabaseService.InitializeAsync();
                        var db = DatabaseService.GetConnection();
                        var authorUser = await db.Table<User>()
                            .Where(u => u.PhoneNumber == _post.AuthorPhone)
                            .FirstOrDefaultAsync();

                        if (authorUser != null)
                        {
                            _post.AuthorMood = authorUser.Mood ?? string.Empty;
                            _post.AuthorDisplayName = string.IsNullOrWhiteSpace(authorUser.Name) ? _post.AuthorPhone : authorUser.Name;

                            // ── Hide phone number if user has toggled it on ──────────────
                            var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                            bool isOwnPost = string.Equals(currentUserPhone, authorUser.PhoneNumber,
                                              StringComparison.OrdinalIgnoreCase);
                            if (authorUser.HidePhoneNumber && !isOwnPost)
                                _post.AuthorPhone = authorUser.Name ?? authorUser.PhoneNumber;
                        }
                    }

                    // Check if post has a URL and load preview in background
                    if (_post.HasLinkPreview && !string.IsNullOrEmpty(_post.FirstUrl))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Debug.WriteLine($"Loading link preview for post {_post.Id}: {_post.FirstUrl}");
                                var preview = await LinkPreviewService.FetchAsync(_post.FirstUrl);

                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    if (_post != null && preview != null)
                                    {
                                        _post.LinkPreview = preview;
                                        if (PostHeaderContainer?.Content is View postView && postView.BindingContext == _post)
                                        {
                                            var ctx = postView.BindingContext;
                                            postView.BindingContext = null;
                                            postView.BindingContext = ctx;
                                        }
                                        Debug.WriteLine($"Link preview loaded successfully for post {_post.Id}");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Link preview load failed for post {_post.Id}: {ex.Message}");
                            }
                        });
                    }

                    // Create and set the post header view
                    var postTemplate = (DataTemplate)Resources["PostHeaderTemplate"];
                    var postView = (View)postTemplate.CreateContent();
                    postView.BindingContext = _post;
                    PostHeaderContainer.Content = postView;
                }
                else
                {
                    Debug.WriteLine($"Post with ID {_postId} not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading post: {ex}");
                await DisplayAlert("Error", "Could not load post", "OK");
            }
        }

        private async void OnLinkPreviewTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is string url && !string.IsNullOrEmpty(url))
                {
                    var uri = new Uri(url);
                    await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening link: {ex.Message}");
                await DisplayAlert("Error", "Could not open link", "OK");
            }
        }

        private async Task CalculateMatchPercentageForPost(PostModel post)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();

                if (string.Equals(currentUserPhone, post.AuthorPhone?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    post.MatchPercent = 0;
                    return;
                }

                if (string.IsNullOrEmpty(currentUserPhone) || string.IsNullOrEmpty(post.AuthorPhone))
                {
                    post.MatchPercent = 0;
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var currentUser = await db.Table<User>().Where(u => u.PhoneNumber == currentUserPhone).FirstOrDefaultAsync();
                var targetUser = await db.Table<User>().Where(u => u.PhoneNumber == post.AuthorPhone).FirstOrDefaultAsync();

                if (currentUser == null || targetUser == null)
                {
                    post.MatchPercent = 0;
                    return;
                }

                string userInterest = currentUser.Interest ?? "Everyone";

                if (userInterest != "Everyone")
                {
                    bool matchesInterest = DoesUserMatchInterest(targetUser, userInterest);
                    if (!matchesInterest)
                    {
                        post.MatchPercent = 0;
                        return;
                    }
                }

                post.MatchPercent = await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, targetUser);
                Debug.WriteLine($"Match percentage for post {post.Id}: {post.MatchPercent}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalculateMatchPercentageForPost error: {ex}");
                post.MatchPercent = 0;
            }
        }

        private bool DoesUserMatchInterest(User targetUser, string userInterest)
        {
            if (string.IsNullOrEmpty(userInterest) || userInterest == "Everyone")
                return true;

            if (string.IsNullOrEmpty(targetUser.Gender))
                return false;

            switch (userInterest.ToLower())
            {
                case "men":
                    return targetUser.Gender.Equals("Male", StringComparison.OrdinalIgnoreCase) ||
                           targetUser.Gender.Equals("Man", StringComparison.OrdinalIgnoreCase);
                case "women":
                    return targetUser.Gender.Equals("Female", StringComparison.OrdinalIgnoreCase) ||
                           targetUser.Gender.Equals("Woman", StringComparison.OrdinalIgnoreCase);
                case "everyone":
                    return true;
                default:
                    return userInterest.Equals("Everyone", StringComparison.OrdinalIgnoreCase) ||
                           targetUser.Gender.Equals(userInterest, StringComparison.OrdinalIgnoreCase);
            }
        }

        private async Task LoadCommentsAsync()
        {
            try
            {
                var comments = await CommentRepository.GetCommentsForPostAsync(_postId, _currentUserPhone);

                foreach (var comment in comments)
                {
                    comment.IsOwnedByCurrentUser = comment.AuthorPhone == _currentUserPhone;
                    if (string.IsNullOrEmpty(comment.AuthorProfileImagePath))
                        comment.AuthorProfileImagePath = await GetUserProfileImagePathAsync(comment.AuthorPhone);
                }

                Comments = new ObservableCollection<Comment>(comments);
                await UpdateCommentCountAsync(Comments.Count);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not load comments", "OK");
                Debug.WriteLine($"Error loading comments: {ex}");
            }
        }

        private async Task UpdateCommentCountAsync(int count)
        {
            try
            {
                if (_post == null) return;

                _post.CommentCount = count;
                await PostRepository.UpdateAsync(_post);

                if (PostHeaderContainer?.Content is View pv && pv.BindingContext is PostModel p)
                {
                    p.CommentCount = count;
                    p.NotifyCommentCountChanged();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCommentCountAsync error: {ex}");
            }
        }

        private async Task PostCommentAsync()
        {
            if (string.IsNullOrWhiteSpace(CommentEditor.Text)) return;

            if (string.IsNullOrEmpty(_currentUserPhone))
            {
                await DisplayAlert("Error", "Please log in to comment", "OK");
                return;
            }

            try
            {
                var commentText = CommentEditor.Text.Trim();
                var authorName = await GetUserDisplayNameAsync(_currentUserPhone);
                var authorProfileImage = await GetUserProfileImagePathAsync(_currentUserPhone);

                var newComment = await CommentRepository.AddCommentAsync(_postId, _currentUserPhone, commentText);

                if (newComment != null)
                {
                    newComment.AuthorDisplayName = authorName;
                    newComment.AuthorProfileImagePath = authorProfileImage;
                    newComment.IsOwnedByCurrentUser = true;

                    var updatedComments = new List<Comment>(Comments) { newComment };
                    Comments = new ObservableCollection<Comment>(updatedComments);
                }

                CommentEditor.Text = string.Empty;
                await UpdateCommentCountAsync(Comments.Count);

                await Task.Delay(100);
                if (CommentsCollectionView != null && newComment != null)
                    CommentsCollectionView.ScrollTo(newComment, position: ScrollToPosition.End, animate: true);

                // Notification to post owner
                try
                {
                    var postOwnerPhone = _post?.AuthorPhone ?? string.Empty;
                    if (!string.IsNullOrEmpty(postOwnerPhone) && postOwnerPhone != _currentUserPhone)
                    {
                        var postOwnerName = !string.IsNullOrWhiteSpace(_post?.AuthorDisplayName)
                            ? _post.AuthorDisplayName
                            : await GetUserDisplayNameAsync(postOwnerPhone);

                        var preview = commentText.Length > 120 ? commentText[..120] + "..." : commentText;
                        var actorProfileImage = await GetUserProfileImagePathAsync(_currentUserPhone);

                        var notif = new NotificationItem
                        {
                            Actor = authorName,
                            ActorPhone = _currentUserPhone,
                            ActorProfileImagePath = actorProfileImage ?? string.Empty,
                            Action = "commented",
                            Target = postOwnerName,
                            TargetPhone = postOwnerPhone,
                            Preview = preview,
                            PostId = _postId,
                            CommentId = newComment?.Id,
                            Timestamp = DateTime.UtcNow
                        };

                        PersistNotification(notif);
                        MessagingCenter.Send<object, NotificationItem>(this, "NewNotificationStructured", notif);
                        MessagingCenter.Send<object, NotificationItem>(this, "NotificationStore_Add", notif);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not post comment: " + ex.Message, "OK");
                Debug.WriteLine($"Error posting comment: {ex}");
            }
        }

        private async Task DeleteCommentAsync(Comment comment)
        {
            bool confirm = await DisplayAlert("Delete Comment",
                "Are you sure you want to delete this comment?", "Yes", "No");

            if (confirm)
            {
                try
                {
                    await CommentRepository.DeleteCommentAsync(comment.Id);

                    var updatedComments = Comments.Where(c => c.Id != comment.Id).ToList();
                    Comments = new ObservableCollection<Comment>(updatedComments);
                    await UpdateCommentCountAsync(Comments.Count);

                    try
                    {
                        MessagingCenter.Send<object, int>(
                            this, "NotificationStoreChanged_RemoveComment", comment.Id);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "Could not delete comment", "OK");
                    Debug.WriteLine($"Error deleting comment: {ex}");
                }
            }
        }

        private async Task<string> GetUserDisplayNameAsync(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == phone).FirstOrDefaultAsync();
                return user?.Name ?? phone;
            }
            catch { return phone; }
        }

        private async Task<string> GetUserProfileImagePathAsync(string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

                try
                {
                    var prefKey = $"user_profile_image_{phone}";
                    var cached = Preferences.Get(prefKey, string.Empty);
                    if (!string.IsNullOrWhiteSpace(cached) && File.Exists(cached)) return cached;
                }
                catch { }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == phone).FirstOrDefaultAsync();

                if (user != null && !string.IsNullOrWhiteSpace(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                {
                    try { Preferences.Set($"user_profile_image_{phone}", user.ProfileImagePath); } catch { }
                    return user.ProfileImagePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserProfileImagePathAsync error: {ex}");
            }
            return "default_avatar.png";
        }

        private void OnToggleExpandClicked(object sender, EventArgs e)
        {
            try
            {
                PostModel post = null;

                if (sender is Button button && button.CommandParameter is PostModel postFromButton)
                    post = postFromButton;
                else if (sender is VisualElement ve && ve.BindingContext is PostModel postFromBinding)
                    post = postFromBinding;

                if (post == null)
                {
                    // Try getting from TapGestureRecognizer CommandParameter
                    if (sender is TapGestureRecognizer tgr && tgr.CommandParameter is PostModel postFromTgr)
                        post = postFromTgr;
                }

                if (post == null) return;

                post.IsExpanded = !post.IsExpanded;
                post.UpdateDisplayContent(200);

                // Force rebind the header view
                if (PostHeaderContainer?.Content is View postView)
                {
                    var ctx = postView.BindingContext;
                    postView.BindingContext = null;
                    postView.BindingContext = ctx;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnToggleExpandClicked: {ex}");
            }
        }
        private async void OnSendButtonClicked(object sender, EventArgs e) => await PostCommentAsync();

        private async void OnMenuButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.CommandParameter is Comment comment)
                {
                    _selectedComment = comment;

                    string action = await DisplayActionSheet(
                        "Comment Options",
                        "Cancel",
                        null,
                        new[] { "Edit", "Delete" });

                    if (action == "Edit")
                    {
                        await EditCommentAsync(comment);
                    }
                    else if (action == "Delete")
                    {
                        await DeleteCommentAsync(comment);
                    }
                }
                else
                {
                    if (sender is VisualElement element && element.BindingContext is Comment bindingComment)
                    {
                        _selectedComment = bindingComment;

                        string action = await DisplayActionSheet(
                            "Comment Options",
                            "Cancel",
                            null,
                            new[] { "Edit", "Delete" });

                        if (action == "Edit")
                        {
                            await EditCommentAsync(bindingComment);
                        }
                        else if (action == "Delete")
                        {
                            await DeleteCommentAsync(bindingComment);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnMenuButtonClicked error: {ex}");
                await DisplayAlert("Error", "Could not open comment options", "OK");
            }
        }

        private async Task EditCommentAsync(Comment comment)
        {
            string newContent = await DisplayPromptAsync("Edit Comment", "Edit your comment:",
                initialValue: comment.Content, maxLength: 1000, keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(newContent))
            {
                try
                {
                    await CommentRepository.UpdateCommentAsync(comment.Id, newContent);
                    comment.Content = newContent;

                    var index = Comments.IndexOf(comment);
                    if (index >= 0)
                    {
                        var updatedComments = Comments.ToList();
                        updatedComments[index] = comment;
                        Comments = new ObservableCollection<Comment>(updatedComments);
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "Could not edit comment", "OK");
                    Debug.WriteLine($"Error editing comment: {ex}");
                }
            }
        }

        // Love / react with animation for COMMENTS - FIXED VERSION
        private async void OnLoveTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is Comment comment && !string.IsNullOrEmpty(_currentUserPhone))
            {
                try
                {
                    bool wasLoved = comment.IsLovedByCurrentUser;
                    await CommentRepository.ToggleLoveAsync(comment.Id, _currentUserPhone);
                    comment.ToggleLove(_currentUserPhone);

                    // Find and update the comment in the list
                    var index = Comments.IndexOf(comment);
                    if (index >= 0)
                    {
                        var updatedComments = Comments.ToList();
                        updatedComments[index] = comment;
                        Comments = new ObservableCollection<Comment>(updatedComments);
                    }

                    // Show enhanced animation ONLY when loving (not when unloving)
                    if (!wasLoved && comment.IsLovedByCurrentUser)
                    {
                        // Find the visual element to animate
                        VisualElement loveElement = null;

                        // Try to get the Border that was tapped
                        if (sender is VisualElement ve)
                            loveElement = ve;
                        else if (sender is TapGestureRecognizer tap && tap.Parent is VisualElement parentVe)
                            loveElement = parentVe;

                        if (loveElement != null)
                        {
                            await AnimateLoveButton(loveElement);
                            ShowMultipleHeartsAnimation(loveElement);
                        }
                        else
                        {
                            // Fallback: use the CommentsCollectionView as reference
                            ShowMultipleHeartsAnimation(CommentsCollectionView);
                        }
                    }

                    // Send notification for love
                    if (comment.IsLovedByCurrentUser && !wasLoved && _currentUserPhone != comment.AuthorPhone)
                    {
                        var currentUserName = await GetUserDisplayNameAsync(_currentUserPhone);
                        var actorProfileImage = await GetUserProfileImagePathAsync(_currentUserPhone);

                        var notif = new NotificationItem
                        {
                            Actor = currentUserName,
                            ActorPhone = _currentUserPhone,
                            ActorProfileImagePath = actorProfileImage ?? string.Empty,
                            Action = "reacted",
                            Target = comment.AuthorDisplayName ?? comment.AuthorPhone,
                            TargetPhone = comment.AuthorPhone ?? string.Empty,
                            Preview = string.Empty,
                            PostId = comment.PostId,
                            CommentId = comment.Id,
                            Timestamp = DateTime.UtcNow
                        };

                        PersistNotification(notif);
                        MessagingCenter.Send<object, NotificationItem>(this, "NewNotificationStructured", notif);
                        MessagingCenter.Send<object, NotificationItem>(this, "NotificationStore_Add", notif);
                    }
                    else if (!comment.IsLovedByCurrentUser && wasLoved)
                    {
                        try
                        {
                            var payload = $"{comment.PostId}|{_currentUserPhone}";
                            MessagingCenter.Send<object, string>(this, "NotificationStoreChanged_RemoveReaction", payload);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error toggling love: {ex}");
                }
            }
        }

        // Enhanced love button animation
        private async Task AnimateLoveButton(VisualElement button)
        {
            try
            {
                if (button == null) return;
                await button.ScaleTo(0.8, 80, Easing.CubicIn);
                await button.ScaleTo(1.25, 120, Easing.SpringOut);
                await button.ScaleTo(1.0, 100, Easing.CubicOut);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AnimateLoveButton error: {ex}");
            }
        }

        // Enhanced multiple hearts animation
        private void ShowMultipleHeartsAnimation(VisualElement targetElement)
        {
            try
            {
                var parentGrid = this.FindByName<Grid>("MainGrid");
                if (parentGrid == null) return;

                // Always use center of screen — most reliable in ScrollView contexts
                double cx = parentGrid.Width / 2;
                double cy = parentGrid.Height / 2;

                var random = new Random();

                var particles = new (string text, string color, double angle)[]
                {
            ("•", "#E0245E", 0),
            ("•", "#F5A623", 45),
            ("•", "#E0245E", 90),
            ("•", "#9B59B6", 135),
            ("•", "#F5A623", 180),
            ("•", "#E0245E", 225),
            ("•", "#9B59B6", 270),
            ("•", "#F5A623", 315),
            ("❤", "#E0245E", 22),
            ("❤", "#E0245E", 202),
                };

                foreach (var (text, color, angleDeg) in particles)
                {
                    double angleRad = angleDeg * Math.PI / 180.0;
                    double burstDist = random.NextDouble() * 35 + 40;
                    double targetX = Math.Cos(angleRad) * burstDist;
                    double targetY = Math.Sin(angleRad) * burstDist;

                    const double particleSize = 20;

                    var particle = new Label
                    {
                        Text = text,
                        FontSize = text == "❤" ? 12 : 16,
                        TextColor = Color.FromArgb(color),
                        Opacity = 1,
                        Scale = 0,
                        BackgroundColor = Colors.Transparent,
                        ZIndex = 999,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(cx - particleSize / 2, cy - particleSize / 2, 0, 0),
                        WidthRequest = particleSize,
                        HeightRequest = particleSize,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    };

                    parentGrid.Children.Add(particle);

                    uint duration = (uint)random.Next(500, 750);

                    var combined = new Animation();

                    combined.Add(0, 1, new Animation(t =>
                    {
                        particle.TranslationX = targetX * t;
                        particle.TranslationY = targetY * t;
                        particle.Scale = t < 0.3
                            ? (t / 0.3) * 1.2
                            : 1.2 - ((t - 0.3) / 0.7 * 0.4);
                    }, 0, 1, Easing.CubicOut));

                    combined.Add(0.6, 1, new Animation(t =>
                    {
                        particle.Opacity = 1 - t;
                    }, 0, 1, Easing.CubicIn));

                    combined.Commit(particle, $"Particle_{Guid.NewGuid():N}", 16, duration,
                        finished: (v, cancelled) =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                parentGrid.Children.Remove(particle);
                            });
                        });
                }

                ShowRippleRing(parentGrid, cx, cy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowMultipleHeartsAnimation error: {ex}");
            }
        }
        private void ShowRippleRing(Grid parentGrid, double cx, double cy)
        {
            try
            {
                const double rippleSize = 40;

                var ripple = new Border
                {
                    WidthRequest = rippleSize,
                    HeightRequest = rippleSize,
                    StrokeThickness = 2,
                    Stroke = Color.FromArgb("#E0245E"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    BackgroundColor = Colors.Transparent,
                    Opacity = 1,
                    Scale = 0.5,
                    ZIndex = 998,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(cx - rippleSize / 2, cy - rippleSize / 2, 0, 0)
                };

                parentGrid.Children.Add(ripple);

                new Animation(t =>
                {
                    ripple.Scale = 0.5 + t * 2.0;
                    ripple.Opacity = 1 - t;
                }, 0, 1, Easing.CubicOut)
                .Commit(ripple, "RippleRing", 16, 400,
                    finished: (v, cancelled) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            parentGrid.Children.Remove(ripple);
                        });
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowRippleRing error: {ex}");
            }
        }
        // Ripple ring animation

        // Love post with animation
        private async void OnLovePostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PostModel post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to love posts", "OK");
                    return;
                }

                VisualElement loveElement = null;
                if (sender is Border border)
                    loveElement = border;
                else if (sender is VisualElement ve)
                    loveElement = ve;
                else if (sender is TapGestureRecognizer tap && tap.CommandParameter != null)
                    loveElement = PostHeaderContainer;

                bool wasLoved = post.IsLovedByCurrentUser;

                await PostRepository.ToggleLoveAsync(post.Id, currentUserPhone);
                post.ToggleLove(currentUserPhone);

                bool isNowLoved = post.IsLovedByCurrentUser;

                if (isNowLoved && !wasLoved)
                {
                    if (loveElement != null)
                    {
                        var animTask = AnimateLoveButton(loveElement);
                        ShowMultipleHeartsAnimation(loveElement);
                        await animTask;
                    }
                }

                if (PostHeaderContainer.Content is View postView && postView.BindingContext == post)
                {
                    postView.BindingContext = null;
                    postView.BindingContext = post;
                }

                post.RefreshLoveState();

                if (post.IsLovedByCurrentUser && !wasLoved && currentUserPhone != post.AuthorPhone)
                {
                    var currentUserName = await GetUserDisplayNameAsync(currentUserPhone);
                    var actorProfileImage = await GetUserProfileImagePathAsync(currentUserPhone);

                    var notif = new NotificationItem
                    {
                        Actor = currentUserName,
                        ActorPhone = currentUserPhone,
                        ActorProfileImagePath = actorProfileImage ?? string.Empty,
                        Action = "reacted",
                        Target = post.AuthorDisplayName ?? post.AuthorPhone,
                        TargetPhone = post.AuthorPhone ?? string.Empty,
                        Preview = !string.IsNullOrWhiteSpace(post.Content)
                            ? (post.Content.Length > 120 ? post.Content[..120] + "..." : post.Content)
                            : (post.ImagePathsList?.FirstOrDefault() ?? string.Empty),
                        PostId = post.Id,
                        Timestamp = DateTime.UtcNow
                    };

                    PersistNotification(notif);
                    MessagingCenter.Send<object, NotificationItem>(this, "NewNotificationStructured", notif);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling love on post: {ex}");
                await DisplayAlert("Error", "Could not update love status", "OK");
            }
        }

        private async void OnSparkPostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PostModel post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // ── REMOVE spark (optimistic, no dialog) ──────────────────────
                if (post.IsSparkedByCurrentUser)
                {
                    post.ToggleSpark(currentUserPhone);
                    RefreshPostView(post);

                    var removeMsg = new SparkChangedMessage
                    {
                        PostId = post.Id,
                        IsSparked = false,
                        SparkCount = post.SparkCount,
                        UserPhone = currentUserPhone
                    };

                    _ = Task.Run(async () =>
                    {
                        await SparkService.RemoveSparkAsync(currentUserPhone, post.Id);
                        MessagingCenter.Send(this, "SparkToggled", removeMsg);
                    });
                    return;
                }

                // ── RATE LIMIT CHECK ──────────────────────────────────────────
                var (canSpark, remaining, waitMinutes) = await SparkService.CanSendSparkAsync(currentUserPhone);

                if (!canSpark)
                {
                    ShowRateLimitToast(waitMinutes);
                    return;
                }

                // ── OPTIMISTIC UPDATE — instant ───────────────────────────────
                post.ToggleSpark(currentUserPhone);
                RefreshPostView(post);

                var sparkMsg = new SparkChangedMessage
                {
                    PostId = post.Id,
                    IsSparked = true,
                    SparkCount = post.SparkCount,
                    UserPhone = currentUserPhone
                };

                // Animate immediately
                VisualElement sparkEl = sender as Border ?? sender as VisualElement;
                if (sparkEl != null)
                {
                    _ = AnimateSparkButton(sparkEl);
                    ShowSparkAnimation(sparkEl);
                }

                ShowTopRightSparkToast(remaining - 1);

                // All heavy work off UI thread
                _ = Task.Run(async () =>
                {
                    bool sparkSent = await SparkService.RecordSparkAsync(
                        currentUserPhone, post.Id, post.AuthorPhone);

                    if (sparkSent)
                        await CreateSparkNotification(post, currentUserPhone);

                    MessagingCenter.Send(this, "SparkToggled", sparkMsg);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSparkPostTapped error: {ex}");
            }
        }

        private async Task AnimateSparkButton(VisualElement button)
        {
            try
            {
                if (button == null) return;
                await button.ScaleTo(0.82, 90, Easing.CubicIn);
                await button.ScaleTo(1.18, 140, Easing.SpringOut);
                await button.ScaleTo(1.0, 100, Easing.CubicOut);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AnimateSparkButton error: {ex}");
            }
        }

        private void ShowSparkAnimation(VisualElement targetElement)
        {
            try
            {
                var parentGrid = this.FindByName<Grid>("MainGrid");
                if (parentGrid == null) return;

                double cx = parentGrid.Width / 2;
                double cy = parentGrid.Height / 2;

                var canvas = new GraphicsView
                {
                    WidthRequest = parentGrid.Width,
                    HeightRequest = parentGrid.Height,
                    BackgroundColor = Colors.Transparent,
                    InputTransparent = true,
                    ZIndex = 9999,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                Grid.SetRow(canvas, 0);
                Grid.SetRowSpan(canvas, Math.Max(1, parentGrid.RowDefinitions.Count));
                Grid.SetColumnSpan(canvas, Math.Max(1, parentGrid.ColumnDefinitions.Count));
                parentGrid.Children.Add(canvas);

                var drawable = new SparkParticleDrawable(cx, cy);
                canvas.Drawable = drawable;

                var startTime = DateTime.UtcNow;
                const int durationMs = 1100;

                Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), () =>
                {
                    double raw = (DateTime.UtcNow - startTime).TotalMilliseconds / durationMs;

                    if (raw >= 1.0)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                            parentGrid.Children.Remove(canvas));
                        return false;
                    }

                    // Ease-in-out the whole timeline
                    double eased = raw < 0.5
                        ? 2 * raw * raw
                        : 1 - Math.Pow(-2 * raw + 2, 2) / 2;

                    drawable.Progress = eased;
                    canvas.Invalidate();
                    return true;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowSparkAnimation error: {ex}");
            }
        }

        private void ShowTopRightSparkToast(int remainingSparks)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var mainGrid = this.FindByName<Grid>("MainGrid");
                    if (mainGrid == null) return;

                    var toast = new Border
                    {
                        BackgroundColor = Color.FromArgb("#1C1C25"),
                        StrokeThickness = 1,
                        Stroke = Color.FromArgb("#FFD700"),
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Padding = new Thickness(12, 6),
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(0, 52, 12, 0),
                        Opacity = 0,
                        ZIndex = 9999,
                        Content = new HorizontalStackLayout
                        {
                            Spacing = 6,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                    {
                        new Label { Text = "⚡", FontSize = 13, VerticalOptions = LayoutOptions.Center },
                        new Label
                        {
                            Text = $"{remainingSparks} left",
                            FontSize = 12,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#FFD700"),
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                        }
                    };

                    Grid.SetRowSpan(toast, Math.Max(1, mainGrid.RowDefinitions.Count));
                    mainGrid.Children.Add(toast);

                    toast.TranslationX = 40;
                    toast.FadeTo(1, 150);
                    toast.TranslateTo(0, 0, 180, Easing.CubicOut);

                    Task.Run(async () =>
                    {
                        await Task.Delay(1800);
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await toast.FadeTo(0, 150);
                            mainGrid.Children.Remove(toast);
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ShowTopRightSparkToast error: {ex}");
                }
            });
        }

        private void ShowRateLimitToast(int waitMinutes)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var mainGrid = this.FindByName<Grid>("MainGrid");
                    if (mainGrid == null) return;

                    var toast = new Border
                    {
                        BackgroundColor = Color.FromArgb("#2A1520"),
                        StrokeThickness = 1.5,
                        Stroke = Color.FromArgb("#FF3B6F"),
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Padding = new Thickness(12, 6),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.End,
                        Margin = new Thickness(0, 0, 0, 100),
                        Opacity = 0,
                        ZIndex = 9999,
                        Content = new HorizontalStackLayout
                        {
                            Spacing = 6,
                            Children =
                    {
                        new Label { Text = "⚡", FontSize = 13, VerticalOptions = LayoutOptions.Center },
                        new Label
                        {
                            Text = $"Spark limit reached. Wait {waitMinutes}m",
                            FontSize = 12,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#FF3B6F"),
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                        }
                    };

                    Grid.SetRowSpan(toast, Math.Max(1, mainGrid.RowDefinitions.Count));
                    mainGrid.Children.Add(toast);

                    toast.FadeTo(1, 200);
                    Task.Run(async () =>
                    {
                        await Task.Delay(2500);
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await toast.FadeTo(0, 200);
                            mainGrid.Children.Remove(toast);
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ShowRateLimitToast error: {ex}");
                }
            });
        }

        // ── Shared particle drawable — identical to PostPage ─────────────────────
        private sealed class SparkParticleDrawable : IDrawable
        {
            private readonly double _cx, _cy;

            private readonly struct Particle
            {
                public readonly double Angle;
                public readonly float Speed;
                public readonly float Size;
                public readonly uint Color;
                public readonly byte Shape;
                public readonly float LifeFrac;
                public readonly float Delay;
                public Particle(double a, float s, float sz, uint c, byte sh, float lf, float d)
                { Angle = a; Speed = s; Size = sz; Color = c; Shape = sh; LifeFrac = lf; Delay = d; }
            }

            private readonly Particle[] _particles;

            private readonly (float maxR, float stroke, string hex, float delay, float dur)[] _rings =
            {
        (48f, 1.6f, "#FFD700", 0.00f, 0.75f),
        (34f, 1.0f, "#FFA500", 0.05f, 0.60f),
        (62f, 0.7f, "#FF3B6F", 0.02f, 0.85f),
    };

            private readonly float _shockMaxR = 36f;
            public double Progress { get; set; }

            public SparkParticleDrawable(double cx, double cy)
            {
                _cx = cx; _cy = cy;
                var rng = new Random();
                uint[] cols = {
            0xFFFFD700, 0xFFFFAA00, 0xFFFFCC44,
            0xFFFF3B6F, 0xFFFFFFFF, 0xFFFFD700, 0xFFFFA500
        };
                var list = new List<Particle>(26);
                for (int i = 0; i < 26; i++)
                {
                    double angle = (i / 26.0) * Math.PI * 2 + (rng.NextDouble() - 0.5) * 0.28;
                    float speed = 34f + (float)(rng.NextDouble() * 26f);
                    float size = 2.4f + (float)(rng.NextDouble() * 3.2f);
                    uint color = cols[i % cols.Length];
                    byte shape = i % 5 == 0 ? (byte)1 : i % 7 == 0 ? (byte)2 : (byte)0;
                    float life = 0.72f + (float)(rng.NextDouble() * 0.25f);
                    float delay = (float)(rng.NextDouble() * 0.10f);
                    list.Add(new Particle(angle, speed, size, color, shape, life, delay));
                }
                _particles = list.ToArray();
            }

            private static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
            private static float EaseOutQuart(float t) => 1f - (1f - t) * (1f - t) * (1f - t) * (1f - t);
            private static float SmoothStep(float t) => t * t * (3f - 2f * t);

            private static Microsoft.Maui.Graphics.Color Unpack(uint argb)
            {
                float a = ((argb >> 24) & 0xFF) / 255f;
                float r = ((argb >> 16) & 0xFF) / 255f;
                float g = ((argb >> 8) & 0xFF) / 255f;
                float b = (argb & 0xFF) / 255f;
                return new Microsoft.Maui.Graphics.Color(r, g, b, a);
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                float p = (float)Math.Clamp(Progress, 0.0, 1.0);
                float cx = (float)_cx;
                float cy = (float)_cy;

                // 1. Shockwave glow
                {
                    float tp = Math.Clamp(p / 0.40f, 0f, 1f);
                    float ease = EaseOutQuart(tp);
                    float r = ease * _shockMaxR;
                    float alpha = SmoothStep(Math.Clamp(tp, 0f, 0.5f) / 0.5f)
                                * (1f - SmoothStep(Math.Clamp((tp - 0.3f) / 0.7f, 0f, 1f)))
                                * 0.22f;
                    if (alpha > 0f)
                    {
                        canvas.FillColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFD700").WithAlpha(alpha);
                        canvas.FillCircle(cx, cy, r);
                    }
                }

                // 2. Rings
                foreach (var (maxR, stroke, hex, delay, dur) in _rings)
                {
                    float tp = Math.Clamp((p - delay) / dur, 0f, 1f);
                    if (tp <= 0f) continue;
                    float ease = EaseOutCubic(tp);
                    float r = ease * maxR;
                    float alpha = tp < 0.5f ? 0.75f : 0.75f * SmoothStep(1f - (tp - 0.5f) / 0.5f);
                    if (alpha <= 0f) continue;
                    canvas.StrokeColor = Microsoft.Maui.Graphics.Color.FromArgb(hex).WithAlpha(alpha);
                    canvas.StrokeSize = stroke;
                    canvas.DrawCircle(cx, cy, r);
                }

                // 3. Streaks
                {
                    float streakP = Math.Clamp(p / 0.35f, 0f, 1f);
                    float ease = EaseOutCubic(streakP);
                    float baseAlpha = SmoothStep(1f - streakP);
                    if (baseAlpha > 0f)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = (i / 8.0) * Math.PI * 2;
                            float x1 = cx + (float)(Math.Cos(angle) * ease * 9f);
                            float y1 = cy + (float)(Math.Sin(angle) * ease * 9f);
                            float x2 = cx + (float)(Math.Cos(angle) * ease * 30f);
                            float y2 = cy + (float)(Math.Sin(angle) * ease * 30f);
                            canvas.StrokeColor = Microsoft.Maui.Graphics.Color.FromArgb(i % 3 == 0 ? "#FF3B6F" : "#FFD700").WithAlpha(baseAlpha);
                            canvas.StrokeSize = i % 2 == 0 ? 1.4f : 0.8f;
                            canvas.DrawLine(x1, y1, x2, y2);
                        }
                    }
                }

                // 4. Particles
                foreach (var part in _particles)
                {
                    float tp = Math.Clamp((p - part.Delay) / part.LifeFrac, 0f, 1f);
                    if (tp <= 0f) continue;

                    float travelT = Math.Min(tp / 0.55f, 1f);
                    float dist = EaseOutCubic(travelT) * part.Speed
                                  + (tp > 0.55f ? (tp - 0.55f) * part.Speed * 0.06f : 0f);

                    float px = cx + (float)(Math.Cos(part.Angle) * dist);
                    float py = cy + (float)(Math.Sin(part.Angle) * dist);

                    float scale = tp < 0.18f ? SmoothStep(tp / 0.18f) : 1f;
                    float alpha = tp < 0.60f ? 1f : SmoothStep(1f - (tp - 0.60f) / 0.40f);
                    if (alpha <= 0f || scale <= 0f) continue;

                    float r = part.Size * scale;
                    canvas.FillColor = Unpack(part.Color).WithAlpha(alpha);

                    switch (part.Shape)
                    {
                        case 1:
                            {
                                var path = new PathF();
                                path.MoveTo(px, py - r * 1.35f);
                                path.LineTo(px + r, py);
                                path.LineTo(px, py + r * 1.35f);
                                path.LineTo(px - r, py);
                                path.Close();
                                canvas.FillPath(path);
                                break;
                            }
                        case 2:
                            {
                                float outer = r * 1.3f, inner = r * 0.45f;
                                var path = new PathF();
                                for (int k = 0; k < 8; k++)
                                {
                                    double a = k * Math.PI / 4 - Math.PI / 8;
                                    float rad = k % 2 == 0 ? outer : inner;
                                    float sx = px + (float)(Math.Cos(a) * rad);
                                    float sy = py + (float)(Math.Sin(a) * rad);
                                    if (k == 0) path.MoveTo(sx, sy); else path.LineTo(sx, sy);
                                }
                                path.Close();
                                canvas.FillPath(path);
                                break;
                            }
                        default:
                            canvas.FillCircle(px, py, r);
                            break;
                    }
                }
            }
        }

        private async void OnSeeAllTapped(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new SearchPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSeeAllTapped error: {ex}");
                await DisplayAlert("Error", "Could not open search page", "OK");
            }
        }

       
        private async Task CreateSparkNotification(PostModel post, string sparkerPhone)
        {
            try
            {
                var sparkerName = await GetUserDisplayNameAsync(sparkerPhone);
                var sparkerProfileImage = await GetUserProfileImagePathAsync(sparkerPhone);

                var notif = new NotificationItem
                {
                    Actor = sparkerName ?? "Someone",
                    ActorPhone = sparkerPhone,
                    ActorProfileImagePath = sparkerProfileImage ?? string.Empty,
                    Action = "sparked",
                    Target = "your post",
                    TargetPhone = post.AuthorPhone,
                    Preview = post.Content?.Length > 100 ? post.Content.Substring(0, 100) + "..." : post.Content ?? "a post",
                    PostId = post.Id,
                    Timestamp = DateTime.UtcNow
                };

                PersistNotification(notif);
                MessagingCenter.Send<object, NotificationItem>(this, "NewNotificationStructured", notif);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating spark notification: {ex}");
            }
        }

        private void RefreshPostView(PostModel post)
        {
            if (PostHeaderContainer.Content is View pv && pv.BindingContext == post)
            {
                pv.BindingContext = null;
                pv.BindingContext = post;
                post.RefreshSparkState();
            }
        }

        private async void OnSavePostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PostModel post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to save posts", "OK");
                    return;
                }

                bool isSaved = SavePostService.IsPostSaved(post.Id, currentUserPhone);

                if (isSaved)
                {
                    var action = await DisplayActionSheet("Post Options", "Cancel", null, "Unsave", "Move to Category", "View Post");

                    if (action == "Unsave")
                    {
                        bool success = await SavePostService.UnsavePostAsync(post.Id, currentUserPhone);
                        if (success)
                        {
                            post.IsSavedByCurrentUser = false;
                            await DisplayAlert("Removed", "Post removed from your bookmarks.", "OK");
                            MessagingCenter.Send(this, "PostUnsaved", post.Id);
                            RefreshPostView(post);
                        }
                    }
                    else if (action == "Move to Category")
                    {
                        await MovePostToCategory(post, currentUserPhone);
                    }
                    else if (action == "View Post")
                    {
                        await Shell.Current.GoToAsync($"postdetails?postId={post.Id}");
                    }
                }
                else
                {
                    var allSaved = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                    var existingFolders = allSaved
                        .Select(s => string.IsNullOrEmpty(s.FolderName) ? "Saved" : s.FolderName)
                        .Distinct().OrderBy(f => f).ToList();

                    string chosenFolder;

                    if (existingFolders.Any())
                    {
                        var options = existingFolders.Concat(new[] { "? New Category" }).ToArray();
                        var picked = await DisplayActionSheet("Save to Category", "Cancel", null, options);
                        if (picked == null || picked == "Cancel") return;
                        if (picked == "? New Category")
                        {
                            var newName = await DisplayPromptAsync("New Category", "Enter category name:", placeholder: "e.g. Inspiration", maxLength: 30, keyboard: Keyboard.Text);
                            if (newName == null) return;
                            chosenFolder = string.IsNullOrWhiteSpace(newName) ? "Saved" : newName.Trim();
                        }
                        else chosenFolder = picked;
                    }
                    else
                    {
                        var newName = await DisplayPromptAsync("Save to Category", "Enter a category name:", placeholder: "e.g. Inspiration", maxLength: 30, keyboard: Keyboard.Text);
                        if (newName == null) return;
                        chosenFolder = string.IsNullOrWhiteSpace(newName) ? "Saved" : newName.Trim();
                    }

                    bool success = await SavePostService.SavePostAsync(post.Id, currentUserPhone, chosenFolder);
                    if (success)
                    {
                        post.IsSavedByCurrentUser = true;
                        await DisplayAlert("Saved!", $"Post saved to '{chosenFolder}'.", "OK");
                        MessagingCenter.Send(this, "PostSaved", post.Id);
                        RefreshPostView(post);
                    }
                    else
                    {
                        await DisplayAlert("Already Saved", "This post is already in your bookmarks.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSavePostTapped error: {ex}");
                await DisplayAlert("Error", "Could not save post", "OK");
            }
        }

        private async Task MovePostToCategory(PostModel post, string currentUserPhone)
        {
            try
            {
                var savedItems = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                var existingCategories = savedItems
                    .Where(s => s.Post.Id != post.Id)
                    .Select(s => s.FolderName).Distinct().ToList();

                var options = new List<string>();
                if (existingCategories.Any()) options.AddRange(existingCategories);
                options.Add("Create New Category");
                options.Add("Cancel");

                var selectedCategory = await DisplayActionSheet("Move to Category", "Cancel", null, options.ToArray());
                if (string.IsNullOrEmpty(selectedCategory) || selectedCategory == "Cancel") return;

                string finalCategory;
                if (selectedCategory == "Create New Category")
                {
                    finalCategory = await DisplayPromptAsync("New Category", "Enter category name:", maxLength: 30, keyboard: Keyboard.Text);
                    if (string.IsNullOrWhiteSpace(finalCategory)) return;
                }
                else finalCategory = selectedCategory;

                bool success = await SavePostService.MovePostToFolderAsync(post.Id, currentUserPhone, finalCategory);
                if (success) await DisplayAlert("Moved", $"Post moved to '{finalCategory}' category.", "OK");
                else await DisplayAlert("Error", "Could not move post.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MovePostToCategory error: {ex}");
                await DisplayAlert("Error", "Could not move post.", "OK");
            }
        }

        private async void OnPostMenuButtonClicked(object sender, EventArgs e)
        {
            try
            {
                PostModel post = null;
                if (sender is Grid grid && grid.BindingContext is PostModel postFromGrid)
                    post = postFromGrid;
                else if (sender is Button button && button.CommandParameter is PostModel postFromButton)
                    post = postFromButton;
                else if (sender is TapGestureRecognizer tap && tap.CommandParameter is PostModel postFromTap)
                    post = postFromTap;

                if (post == null) return;

                var actionsPage = new PostActionsPage(
                    post,
                    onEdit: async (postToEdit) => await DisplayAlert("Edit", "Edit post functionality would go here", "OK"),
                    onDelete: async (postToDelete) =>
                    {
                        var confirm = await DisplayAlert("Delete Post", "Are you sure you want to delete this post?", "Yes", "No");
                        if (confirm) { await PostRepository.DeleteAsync(postToDelete.Id); await Navigation.PopAsync(); }
                    }
                );
                await Navigation.PushModalAsync(actionsPage, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnPostMenuButtonClicked: {ex}");
                await DisplayAlert("Error", "Could not open post menu", "OK");
            }
        }

        private async void OnCommentButtonTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (CommentsCollectionView?.ItemsSource is System.Collections.IEnumerable items && items.Cast<object>().Any())
                {
                    var lastItem = items.Cast<object>().LastOrDefault();
                    if (lastItem != null)
                    {
                        CommentsCollectionView.ScrollTo(lastItem, position: ScrollToPosition.End, animate: true);
                    }
                }
                CommentEditor?.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnCommentButtonTapped: {ex}");
            }
        }

        private async void OnAddToListTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PostModel post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to add posts to lists", "OK");
                    return;
                }

                var savedItems = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                var existingFolders = savedItems
                    .Select(s => string.IsNullOrEmpty(s.FolderName) ? "Saved" : s.FolderName)
                    .Distinct()
                    .OrderBy(f => f)
                    .ToList();

                string chosenList;

                if (existingFolders.Any())
                {
                    var options = existingFolders.Concat(new[] { "? Create New List" }).ToArray();
                    var picked = await DisplayActionSheet("Add to List", "Cancel", null, options);

                    if (picked == null || picked == "Cancel") return;

                    if (picked == "? Create New List")
                    {
                        var newName = await DisplayPromptAsync("New List", "Enter list name:", placeholder: "e.g. Favorites, Watch Later", maxLength: 30, keyboard: Keyboard.Text);
                        if (string.IsNullOrWhiteSpace(newName)) return;
                        chosenList = newName.Trim();
                    }
                    else
                    {
                        chosenList = picked;
                    }
                }
                else
                {
                    var newName = await DisplayPromptAsync("Create List", "Enter a list name:", placeholder: "e.g. Favorites, Watch Later", maxLength: 30, keyboard: Keyboard.Text);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    chosenList = newName.Trim();
                }

                bool isAlreadySaved = SavePostService.IsPostSaved(post.Id, currentUserPhone);

                if (isAlreadySaved)
                {
                    var moveOption = await DisplayAlert("Already Saved", $"This post is already in your saved items. Do you want to move it to '{chosenList}'?", "Move", "Cancel");
                    if (moveOption)
                    {
                        bool success = await SavePostService.MovePostToFolderAsync(post.Id, currentUserPhone, chosenList);
                        if (success)
                        {
                            await DisplayAlert("Added", $"Post added to '{chosenList}' list.", "OK");
                            RefreshPostView(post);
                        }
                        else
                        {
                            await DisplayAlert("Error", "Could not move post to list.", "OK");
                        }
                    }
                }
                else
                {
                    bool success = await SavePostService.SavePostAsync(post.Id, currentUserPhone, chosenList);
                    if (success)
                    {
                        post.IsSavedByCurrentUser = true;
                        await DisplayAlert("Added!", $"Post added to '{chosenList}' list.", "OK");
                        MessagingCenter.Send(this, "PostSaved", post.Id);
                        RefreshPostView(post);
                    }
                    else
                    {
                        await DisplayAlert("Error", "Could not add post to list.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAddToListTapped error: {ex}");
                await DisplayAlert("Error", "Could not add post to list", "OK");
            }
        }

        private async void OnReplyTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is Comment comment && !string.IsNullOrEmpty(_currentUserPhone))
            {
                string replyContent = await DisplayPromptAsync("Reply to Comment", "Write your reply:", maxLength: 1000, keyboard: Keyboard.Text);

                if (!string.IsNullOrWhiteSpace(replyContent))
                {
                    try
                    {
                        var authorName = await GetUserDisplayNameAsync(_currentUserPhone);
                        var authorProfileImage = await GetUserProfileImagePathAsync(_currentUserPhone);

                        var reply = await CommentRepository.AddCommentAsync(_postId, _currentUserPhone, replyContent, comment.Id);

                        if (reply != null)
                        {
                            reply.AuthorDisplayName = authorName;
                            reply.AuthorProfileImagePath = authorProfileImage;
                            reply.IsOwnedByCurrentUser = true;
                        }

                        var updatedComments = new List<Comment>(Comments) { reply };
                        Comments = new ObservableCollection<Comment>(updatedComments);
                        await UpdateCommentCountAsync(Comments.Count);

                        if (_currentUserPhone != comment.AuthorPhone)
                        {
                            try
                            {
                                var commentOwnerName = comment.AuthorDisplayName ?? comment.AuthorPhone;
                                var preview = replyContent.Length > 120 ? replyContent[..120] + "..." : replyContent;
                                var actorProfileImage = await GetUserProfileImagePathAsync(_currentUserPhone);

                                var notif = new NotificationItem
                                {
                                    Actor = authorName,
                                    ActorPhone = _currentUserPhone,
                                    ActorProfileImagePath = actorProfileImage ?? string.Empty,
                                    Action = "replied",
                                    Target = commentOwnerName,
                                    TargetPhone = comment.AuthorPhone ?? string.Empty,
                                    Preview = preview,
                                    PostId = _postId,
                                    CommentId = reply?.Id,
                                    Timestamp = DateTime.UtcNow
                                };

                                PersistNotification(notif);
                                MessagingCenter.Send<object, NotificationItem>(this, "NewNotificationStructured", notif);
                                MessagingCenter.Send<object, NotificationItem>(this, "NotificationStore_Add", notif);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", "Could not post reply", "OK");
                        Debug.WriteLine($"Error posting reply: {ex}");
                    }
                }
            }
        }

        private async void OnAuthorNameTapped(object sender, TappedEventArgs e)
        {
            try
            {
                string phone = string.Empty;

                if (e.Parameter is Comment comment && !string.IsNullOrEmpty(comment.AuthorPhone))
                    phone = comment.AuthorPhone;
                else if (e.Parameter is PostModel post && !string.IsNullOrEmpty(post.AuthorPhone))
                {
                    phone = post.AuthorPhone;
                    if (phone.Contains("·"))
                    {
                        var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                        phone = parts.Length > 1 ? parts[1].Trim() : phone;
                    }
                }

                if (!string.IsNullOrEmpty(phone))
                    await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                    { ["phone"] = phone.Trim(), ["viewOnly"] = "true" });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to profile: {ex}");
            }
        }

        private async void OnSharePostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PostModel post) return;
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var sharePopup = new PostSharePopup(post, currentUserPhone);
                await this.ShowPopupAsync(sharePopup);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sharing post: {ex}");
                await DisplayAlert("Error", "Could not share post", "OK");
            }
        }

        private async void OnPostImageTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not string imagePath || string.IsNullOrEmpty(imagePath)) return;
                if (_post?.ImagePathsList == null || !_post.ImagePathsList.Any()) return;

                int startIndex = _post.ImagePathsList.ToList().FindIndex(p => string.Equals(p, imagePath, StringComparison.OrdinalIgnoreCase));
                if (startIndex < 0) startIndex = 0;

                var fullScreenPage = new Lock.Pages.Profile.FullScreenMediaPage(_post.ImagePathsList.ToList(), startIndex);
                await Navigation.PushModalAsync(fullScreenPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnPostImageTapped: {ex}");
            }
        }

        // Notification helper
        private static void PersistNotification(NotificationItem notif)
        {
            try
            {
                const string key = "notifications_v2";
                var json = Preferences.Get(key, string.Empty);
                var list = string.IsNullOrEmpty(json)
                    ? new List<NotificationItem>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<NotificationItem>>(json) ?? new List<NotificationItem>();

                list.Insert(0, notif);
                if (list.Count > 200) list = list.Take(200).ToList();
                Preferences.Set(key, System.Text.Json.JsonSerializer.Serialize(list));
            }
            catch { }
        }
    }
}