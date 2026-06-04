using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Data.Post;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChatDatabaseService = Lock.Chat.Services.DatabaseService;
using IOFile = System.IO.File;
using Path = Microsoft.Maui.Controls.Shapes.Path;
using PostModel = Lock.Models.Post;
using System.Threading;
using Lock.Pages.Discover;
using System.Timers;
using Lock.Services.Admin;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Timer = System.Threading.Timer;

namespace Lock.Pages.Post
{
    // Updated PostTemplateSelector - removed all search-related properties
    public class PostTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FullPostTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            // Always return the full post template since search is removed
            return FullPostTemplate;
        }
    }

    public class SparkChangedMessage
    {
        public int PostId { get; set; }
        public bool IsSparked { get; set; }
        public int SparkCount { get; set; }
        public string UserPhone { get; set; }
    }



    // UserHeader class remains unchanged
    public class UserHeader
    {
        public string Phone { get; set; }
        public string Name { get; set; }
        public string ProfileImagePath { get; set; }
        public string Mood { get; set; }
        public DateTime MoodLastUpdated { get; set; }

        public List<Lock.Models.Post> Posts { get; set; } = new List<Lock.Models.Post>();

        public string MoodLastUpdatedRelative
        {
            get
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var timeSpan = now - MoodLastUpdated;

                    if (timeSpan.TotalSeconds < 60)
                        return "just now";
                    if (timeSpan.TotalMinutes < 60)
                        return $"{(int)timeSpan.TotalMinutes}m ago";
                    if (timeSpan.TotalHours < 24)
                        return $"{(int)timeSpan.TotalHours}h ago";
                    if (timeSpan.TotalDays < 7)
                        return $"{(int)timeSpan.TotalDays}d ago";
                    if (timeSpan.TotalDays < 30)
                        return $"{(int)(timeSpan.TotalDays / 7)}w ago";
                    if (timeSpan.TotalDays < 365)
                        return $"{(int)(timeSpan.TotalDays / 30)}mo ago";

                    return $"{(int)(timeSpan.TotalDays / 365)}y ago";
                }
                catch
                {
                    return "unknown";
                }
            }
        }
    }

    public partial class PostPage : ContentPage
    {
        // Fields
        private List<string> _pickedImagePaths = new();
        private int? _editingPostId;
        private Border? _saveBtn;
        private Grid? _updateGrid;
        private Grid? _cancelGrid;
        private bool _refreshingRelativeTimes;
        private readonly TimeSpan _relativeRefreshInterval = TimeSpan.FromSeconds(30);
        private Microsoft.Maui.Controls.VisualElement? TopImagePlaceholder;
        private const double TopImageMinScale = 0.6;
        private const double TopImageShrinkThreshold = 120.0;
        private List<Lock.Models.Post> _allFeedPosts = new();
        private List<Lock.Models.Post> _allStatusPosts = new();  // ADD THIS

        private bool _pendingUserFilter = false;
        private string _pendingFilterPhone = string.Empty;
        private string _pendingFilterName = string.Empty;
        private bool _pendingScrollToLatest = false;

        private double _lastScrollY = 0;
        private bool _isNavBarVisible = true;
        private bool _isHeaderVisible = true;
        private const double NavBarHideThreshold = 10.0;
        private bool _isNavigatingToComments = false;
        private string _selectedVisibility = "Everyone";
        // Add this field at the top with your other fields
        private System.Timers.Timer? _liveStatusCheckTimer;
        private System.Timers.Timer? _blinkingTimer;

        private Image? TopImage => this.FindByName<Image>("TopImage");
        private GraphicsView? TopImageRing => this.FindByName<GraphicsView>("TopImageRing");
        private StackLayout? TopImageActions => this.FindByName<StackLayout>("TopImageActions");

        // Cache fields for faster loading
        private static List<Lock.Models.Post> _cachedFeedPosts = null;
        private static List<Lock.Models.Post> _cachedStatusPosts = null;
        private static DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5); // Cache lasts 5 minutes
        private bool _isRefreshingInBackground = false;

        private List<User> _allNearbyUsers = new();
        private List<User> _filteredNearbyUsers = new();

        private List<PostPageLiveUserCard> _liveCards = new();
        private Timer? _liveUsersPollTimer;
        private CancellationTokenSource _postPageLoaderCts = new();

        // ── Post Page Loading Overlay Animations ──────────────────────────────────

        private void StartPostPageLoadingAnimations()
        {
            _postPageLoaderCts = new CancellationTokenSource();
            var token = _postPageLoaderCts.Token;
            _ = PostPageSpinRingAsync(token);
            _ = PostPageHeartPulseAsync(token);
            _ = PostPageDotWaveAsync(token);
        }

        private void StopPostPageLoadingAnimations()
        {
            _postPageLoaderCts.Cancel();
            PostPageSpinRing.Rotation = 0;
            PostPageHeartIcon.ScaleTo(1, 80);
        }

        private async Task PostPageSpinRingAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await PostPageSpinRing.RotateTo(360, 2000, Easing.Linear);
                PostPageSpinRing.Rotation = 0;
            }
        }

        private async Task PostPageHeartPulseAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await PostPageHeartIcon.ScaleTo(1.22, 200, Easing.CubicOut);
                await PostPageHeartIcon.ScaleTo(0.95, 120, Easing.CubicIn);
                await PostPageHeartIcon.ScaleTo(1.00, 120, Easing.CubicOut);
                await Task.Delay(900, token).ContinueWith(_ => { });
            }
        }

        private readonly Color _postDotActive = Color.FromArgb("#FF3B6F");
        private readonly Color _postDotInactive = Color.FromArgb("#3A3A4C");

        private async Task PostPageDotWaveAsync(CancellationToken token)
        {
            var dots = new[] { PostDot1, PostDot2, PostDot3, PostDot4, PostDot5 };
            int i = 0;
            while (!token.IsCancellationRequested)
            {
                dots[i].Fill = new SolidColorBrush(_postDotActive);
                await dots[i].ScaleYTo(1.6, 120, Easing.CubicOut);
                await dots[i].ScaleYTo(1.0, 120, Easing.CubicIn);
                dots[i].Fill = new SolidColorBrush(_postDotInactive);
                i = (i + 1) % dots.Length;
                await Task.Delay(160, token).ContinueWith(_ => { });
            }
        }

        private async void OnViewAllPeopleTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SearchPage());
        }

        private async Task LoadLiveUsersForPostPageAsync()
        {
            try
            {
                var currentPhone = Preferences.Get("current_user_phone", string.Empty);

                // FIXED: Use Supabase instead of SQLite
                var allSessions = await SupabaseService.GetAsync<LiveSession>("LiveSessions",
                    $"IsLive=eq.true&EndedAt=is.null&UserPhoneNumber=neq.{Uri.EscapeDataString(currentPhone)}");

                var sessions = allSessions
                    .GroupBy(s => s.UserPhoneNumber)
                    .Select(g => g.First())
                    .Take(10)
                    .ToList();

                var cards = new List<PostPageLiveUserCard>();

                foreach (var session in sessions)
                {
                    try
                    {
                        if (session.ScheduledEndTime.HasValue && session.ScheduledEndTime.Value <= DateTime.UtcNow)
                        {
                            session.IsLive = false;
                            session.EndedAt = DateTime.UtcNow;
                            await SupabaseService.UpdateAsync("LiveSessions", $"Id=eq.{session.Id}", session);
                            continue;
                        }

                        // FIXED: Get user from Supabase
                        var users = await SupabaseService.GetAsync<User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(session.UserPhoneNumber)}&limit=1");
                        var user = users.FirstOrDefault();
                        if (user == null) continue;

                        string heightText = string.Empty;
                        if (user.HeightCm.HasValue && user.HeightCm.Value > 0)
                        {
                            int feet = (int)(user.HeightCm.Value / 30.48);
                            int inches = (int)((user.HeightCm.Value % 30.48) / 2.54);
                            heightText = $"{feet}'{inches}\"";
                        }

                        var card = new PostPageLiveUserCard
                        {
                            PhoneNumber = session.UserPhoneNumber,
                            Name = string.IsNullOrEmpty(user.Name) ? session.UserPhoneNumber : user.Name,
                            ProfileImagePath = user.ProfileImagePath ?? string.Empty,
                            Mood = session.Mood,
                            Message = session.Message,
                            Location = session.Location,
                            ChatAvailable = session.ChatAvailable,
                            VoiceAvailable = session.VoiceAvailable,
                            VideoAvailable = session.VideoAvailable,
                            StartedAt = session.StartedAt,
                            ScheduledEndTime = session.ScheduledEndTime,
                            Age = GetAgeFromDateOfBirth(user.DateOfBirth),
                            Bio = user.Bio ?? string.Empty,
                            Interests = user.Interests ?? string.Empty,
                            Gender = user.Gender ?? string.Empty,
                            LookingFor = user.Mood ?? string.Empty,
                            Height = heightText,
                            BodyType = user.BodyType ?? string.Empty,
                            Ethnicity = user.Ethnicity ?? string.Empty,
                            PersonalityType = user.PersonalityType ?? string.Empty,
                            LoveLanguage = user.LoveLanguage ?? string.Empty,
                            EnergyLevel = user.EnergyLevel ?? string.Empty,
                            IsVerified = user.IsVerified
                        };

                        if (!string.IsNullOrEmpty(session.ImagePathsJson))
                        {
                            try
                            {
                                var imagePaths = System.Text.Json.JsonSerializer
                                    .Deserialize<List<string>>(session.ImagePathsJson)
                                    ?? new List<string>();
                                card.ImageCarouselPaths = imagePaths
                                    .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                                    .ToList();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"PostPage carousel images error: {ex}");
                                card.ImageCarouselPaths = new List<string>();
                            }
                        }
                        card.StartCarousel();
                        cards.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LoadLiveUsersForPostPage error: {ex}");
                    }
                }

                _liveCards = cards;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var layout = this.FindByName<HorizontalStackLayout>("PostPageLiveStackLayout");
                    if (layout == null) return;

                    layout.Children.Clear();

                    if (PostPageLiveCountLabel != null)
                    {
                        PostPageLiveCountLabel.Text = _liveCards.Any()
                            ? $"• {_liveCards.Count} live"
                            : "";
                    }

                    var liveSection = this.FindByName<VerticalStackLayout>("LiveNowSection");
                    if (liveSection != null)
                    {
                        liveSection.IsVisible = _liveCards.Any();
                    }

                    foreach (var card in _liveCards)
                    {
                        var liveCardView = BuildLiveCardView(card);
                        layout.Children.Add(liveCardView);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadLiveUsersForPostPageAsync error: {ex}");
            }
        }

        private int GetAgeFromDateOfBirth(DateTime dateOfBirth)
        {
            if (dateOfBirth == default) return 0;
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth > today.AddYears(-age)) age--;
            return age;
        }

        private View BuildLiveCardView(PostPageLiveUserCard card)
        {
            var cardBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#16161C"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#2A2A38"),
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                WidthRequest = 280,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 8),
                VerticalOptions = LayoutOptions.Fill
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await OnPostPageLiveCardTapped(card);
            cardBorder.GestureRecognizers.Add(tap);

            var root = new VerticalStackLayout { Spacing = 0 };

            // ── HERO IMAGE ──
            var imageGrid = new Grid { HeightRequest = 220 };

            // SINGLE image — pre-loaded source, no crossfade, no blink
            var heroImage = new Image
            {
                Aspect = Aspect.AspectFill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            heroImage.Source = card.CurrentCarouselImage;

            // Subscribe to carousel changes — just swap source directly
            card.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PostPageLiveUserCard.CurrentCarouselImage))
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        heroImage.Source = card.CurrentCarouselImage;
                    });
                }
            };

            imageGrid.Children.Add(heroImage);

            // Gradient overlay
            var gradient = new BoxView { VerticalOptions = LayoutOptions.Fill };
            gradient.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
        {
            new GradientStop { Color = Colors.Transparent, Offset = 0.3f },
            new GradientStop { Color = Color.FromArgb("#CC000000"), Offset = 0.75f },
            new GradientStop { Color = Color.FromArgb("#F0000000"), Offset = 1.0f }
        }
            };
            imageGrid.Children.Add(gradient);

            // LIVE badge - static, no animation
            var liveBadge = new Border
            {
                BackgroundColor = Color.FromArgb("#CC0D2218"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#22C55E"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(10, 5),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(12, 12, 0, 0)
            };
            liveBadge.Content = new HorizontalStackLayout
            {
                Spacing = 5,
                Children =
        {
            new BoxView
            {
                WidthRequest = 6, HeightRequest = 6,
                BackgroundColor = Color.FromArgb("#22C55E"),
                CornerRadius = 3,
                VerticalOptions = LayoutOptions.Center
            },
            new Label
            {
                Text = "LIVE",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#22C55E"),
                CharacterSpacing = 1.5,
                VerticalOptions = LayoutOptions.Center
            }
        }
            };
            imageGrid.Children.Add(liveBadge);

            // Bottom overlay
            var bottomOverlay = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.End,
                Padding = new Thickness(14, 0, 14, 12),
                Spacing = 6
            };

            // Name + age + verified row
            var nameRow = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
            nameRow.Children.Add(new Label
            {
                Text = card.Name,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center
            });

            if (card.HasAge)
            {
                nameRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#AA16161C"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#3A3A4A"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(7, 3),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = card.AgeText,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#F0EDE8")
                    }
                });
            }

            if (card.IsVerified)
            {
                nameRow.Children.Add(new Label
                {
                    Text = "✓",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00B5B5"),
                    VerticalOptions = LayoutOptions.Center
                });
            }
            bottomOverlay.Children.Add(nameRow);

            // Mood + Location
            var infoRow = new HorizontalStackLayout { Spacing = 8 };
            if (card.HasMood)
            {
                infoRow.Children.Add(new Border
                {
                    StrokeThickness = 1.5f,
                    Stroke = card.MoodBlinkColor,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(9, 4),
                    BackgroundColor = card.MoodBlinkColor.WithAlpha(0.15f),
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 5,
                        Children =
                {
                    new BoxView
                    {
                        WidthRequest = 6, HeightRequest = 6,
                        BackgroundColor = card.MoodBlinkColor,
                        CornerRadius = 3,
                        VerticalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = card.Mood,
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = card.MoodBlinkColor,
                        VerticalOptions = LayoutOptions.Center
                    }
                }
                    }
                });
            }

            if (card.HasLocation)
            {
                infoRow.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 4,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
            {
                new Label { Text = "📍", FontSize = 10, VerticalOptions = LayoutOptions.Center },
                new Label
                {
                    Text = card.Location,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#CCCCCC"),
                    VerticalOptions = LayoutOptions.Center
                }
            }
                });
            }

            if (infoRow.Children.Any())
                bottomOverlay.Children.Add(infoRow);

            imageGrid.Children.Add(bottomOverlay);
            root.Children.Add(imageGrid);

            // ── DETAILS ──
            var details = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(14, 10, 14, 10) };

            details.Children.Add(new HorizontalStackLayout
            {
                Spacing = 5,
                Children =
        {
            new Label { Text = "🕐", FontSize = 12, VerticalOptions = LayoutOptions.Center },
            new Label
            {
                Text = card.LiveSince,
                FontSize = 11,
                TextColor = Color.FromArgb("#5A5A6A"),
                VerticalOptions = LayoutOptions.Center
            }
        }
            });

            if (!string.IsNullOrEmpty(card.Message))
            {
                details.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#1C1C25"),
                    StrokeThickness = 1,
                    Stroke = Color.FromArgb("#2A2A38"),
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(12, 10),
                    Content = new Label
                    {
                        Text = card.Message,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#D0D0E0"),
                        LineBreakMode = LineBreakMode.WordWrap,
                        MaxLines = 2
                    }
                });
            }

            if (card.HasBio && !string.IsNullOrWhiteSpace(card.Bio))
            {
                details.Children.Add(new Label
                {
                    Text = card.BioPreview,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#8A8A9A"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    MaxLines = 2
                });
            }

            root.Children.Add(details);

            // Divider
            root.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#2A2A38"),
                Margin = new Thickness(14, 0)
            });

            // ── CONNECTION BUTTONS ──
            var availableOptions = new List<(string emoji, string label, string color, bool available)>
    {
        ("💬", "Chat",  "#00B5B5", card.ChatAvailable),
        ("🎤", "Voice", "#FF3B6F", card.VoiceAvailable),
        ("📹", "Video", "#F59E0B", card.VideoAvailable),
    }.Where(o => o.available).ToList();

            var connectionGrid = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(14, 10, 14, 16),
                HeightRequest = 76
            };

            for (int i = 0; i < availableOptions.Count; i++)
                connectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            for (int i = 0; i < availableOptions.Count; i++)
            {
                var opt = availableOptions[i];
                var col = i;

                var optBorder = new Border
                {
                    StrokeThickness = 1.5,
                    Stroke = Color.FromArgb(opt.color),
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    BackgroundColor = Color.FromArgb(opt.color).WithAlpha(0.08f),
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Padding = new Thickness(0, 10)
                };
                Grid.SetColumn(optBorder, col);

                optBorder.Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
            {
                new Label
                {
                    Text = opt.emoji,
                    FontSize = 20,
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = opt.label,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(opt.color),
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
                };

                var btnTap = new TapGestureRecognizer();
                btnTap.Tapped += async (s, e) => await OnPostPageLiveCardTapped(card);
                optBorder.GestureRecognizers.Add(btnTap);
                connectionGrid.Children.Add(optBorder);
            }

            if (!availableOptions.Any())
            {
                connectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var viewBorder = new Border
                {
                    StrokeThickness = 1.5,
                    Stroke = Color.FromArgb("#00B5B5"),
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    BackgroundColor = Color.FromArgb("#00B5B5").WithAlpha(0.08f),
                    HorizontalOptions = LayoutOptions.Fill,
                    Padding = new Thickness(0, 10),
                    Content = new Label
                    {
                        Text = "👁  View Profile",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5"),
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                };
                var vt = new TapGestureRecognizer();
                vt.Tapped += async (s, e) => await OnPostPageLiveCardTapped(card);
                viewBorder.GestureRecognizers.Add(vt);
                Grid.SetColumn(viewBorder, 0);
                connectionGrid.Children.Add(viewBorder);
            }

            root.Children.Add(connectionGrid);
            cardBorder.Content = root;
            return cardBorder;
        }

        private async Task OnPostPageLiveCardTapped(PostPageLiveUserCard card)
        {
            try
            {
                var options = new List<string>();
                if (card.ChatAvailable) options.Add("Send a message");
                if (card.VoiceAvailable) options.Add("Voice call");
                if (card.VideoAvailable) options.Add("Video call");
                options.Add("👤 View profile");

                var action = await DisplayActionSheet($"Connect with {card.Name}", "Cancel", null, options.ToArray());

                if (action == null || action == "Cancel") return;

                if (action.Contains("message"))
                {
                    await Shell.Current.GoToAsync("conversations",
                        new Dictionary<string, object>
                        {
                            ["recipientPhone"] = card.PhoneNumber,
                            ["recipientName"] = card.Name,
                            ["openChat"] = "true"
                        });
                }
                else if (action.Contains("profile"))
                {
                    await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(card.PhoneNumber)}&viewOnly=true");

                }
                else
                {
                    await Shell.Current.GoToAsync("conversations",
                        new Dictionary<string, object>
                        {
                            ["recipientPhone"] = card.PhoneNumber,
                            ["recipientName"] = card.Name,
                            ["openChat"] = "true"
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPostPageLiveCardTapped error: {ex}");
            }
        }

        private async void OnViewAllLiveTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new LiveFeedPage());
        }

        private void StartLiveUsersPolling()
        {
            _liveUsersPollTimer?.Dispose();
            _liveUsersPollTimer = new Timer(async _ =>
            {
                await LoadLiveUsersForPostPageAsync();
            }, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));
        }

        private void StopLiveUsersPolling()
        {
            _liveUsersPollTimer?.Dispose();
            _liveUsersPollTimer = null;
        }
        private async Task LoadNearbyUsersAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // FIXED: Use Supabase instead of SQLite
                var allUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=neq.{Uri.EscapeDataString(currentUserPhone)}");

                _allNearbyUsers = allUsers.ToList();

                _filteredNearbyUsers = _allNearbyUsers
                    .Where(u => !u.GhostModeMoodShield)
                    .ToList();

                // Get 20 random users
                var randomUsers = _filteredNearbyUsers
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(20)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var layout = this.FindByName<HorizontalStackLayout>("PeopleNearYouStackLayout");
                    if (layout == null) return;

                    layout.Children.Clear();

                    foreach (var user in randomUsers)
                    {
                        var location = GetUserLocation(user);
                        var card = BuildNearbyUserCard(user, location);
                        layout.Children.Add(card);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadNearbyUsersAsync error: {ex}");
            }
        }

        private View BuildNearbyUserCard(User user, string location)
        {
            var vm = new NearbyUserCardViewModel(user, location);

            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#12121A"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#2A2A38"),
                StrokeShape = new RoundRectangle { CornerRadius = 24 },
                WidthRequest = 260,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 8),
                VerticalOptions = LayoutOptions.Start
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(vm.PhoneNumber)}&viewOnly=true");

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
                var verifiedPath = new Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    StrokeThickness = 0.5,
                    HeightRequest = 16,
                    WidthRequest = 16,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                verifiedPath.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(
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
                var locIcon = new Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    HeightRequest = 11,
                    WidthRequest = 11,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                };
                locIcon.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(
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

            root.Children.Add(body);
            card.Content = root;
            return card;
        }
        private string GetUserLocation(User user)
        {
            if (!string.IsNullOrEmpty(user.Country) && !string.IsNullOrEmpty(user.State))
                return $"{user.State}, {user.Country}";
            if (!string.IsNullOrEmpty(user.Country)) return user.Country;
            if (!string.IsNullOrEmpty(user.State)) return user.State;
            return "";
        }
        private async Task<bool> IsCurrentUserLiveAsync()
        {
            try
            {
                var phone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(phone)) return false;

                // FIXED: Use Supabase instead of SQLite
                var sessions = await SupabaseService.GetAsync<LiveSession>("LiveSessions",
                    $"UserPhoneNumber=eq.{Uri.EscapeDataString(phone)}&IsLive=eq.true&EndedAt=is.null&limit=1");

                return sessions.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IsCurrentUserLiveAsync error: {ex}");
                return false;
            }
        }



        // ── SMOOTH DISCOVER LIVE DOT BLINKING ─────────────────────────────────
        private void StartDiscoverLiveBlinking()
        {
            if (DiscoverLiveDot == null)
            {
                Debug.WriteLine("❌ DiscoverLiveDot is NULL - Check XAML x:Name");
                return;
            }

            DiscoverLiveDot.AbortAnimation("DiscoverBlink");

            var pulseAnimation = new Animation();

            // Fade out smoothly
            pulseAnimation.Add(0, 0.5, new Animation(v =>
            {
                if (DiscoverLiveDot != null) DiscoverLiveDot.Opacity = v;
            }, 1.0, 0.25, Easing.SinInOut));

            // Fade back in smoothly
            pulseAnimation.Add(0.5, 1.0, new Animation(v =>
            {
                if (DiscoverLiveDot != null) DiscoverLiveDot.Opacity = v;
            }, 0.25, 1.0, Easing.SinInOut));

            pulseAnimation.Commit(DiscoverLiveDot, "DiscoverBlink", 16, 900, Easing.Linear, null, () => true);

            Debug.WriteLine("✅ Discover Live Dot blinking STARTED");
        }


        private void StopDiscoverLiveBlinking()
        {
            if (DiscoverLiveDot == null) return;

            DiscoverLiveDot.AbortAnimation("DiscoverBlink");
            DiscoverLiveDot.Opacity = 1.0;

            Debug.WriteLine("✅ Discover Live Dot blinking STOPPED");
        }

        // Add this method to update the Discover button live dot
        private async Task UpdateDiscoverLiveDotAsync()
        {
            try
            {
                bool isLive = await IsCurrentUserLiveAsync();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (DiscoverLiveDot == null) return;

                    // Always abort first — prevents stacking from repeated calls
                    DiscoverLiveDot.AbortAnimation("DiscoverBlink");

                    if (isLive)
                    {
                        DiscoverLiveDot.IsVisible = true;
                        DiscoverLiveDot.BackgroundColor = Color.FromArgb("#4CAF50");
                        DiscoverLiveDot.Opacity = 1.0;

                        var pulse = new Animation();
                        pulse.Add(0, 0.5, new Animation(
                            v => { if (DiscoverLiveDot != null) DiscoverLiveDot.Opacity = v; },
                            1.0, 0.2, Easing.SinInOut));
                        pulse.Add(0.5, 1.0, new Animation(
                            v => { if (DiscoverLiveDot != null) DiscoverLiveDot.Opacity = v; },
                            0.2, 1.0, Easing.SinInOut));

                        pulse.Commit(DiscoverLiveDot, "DiscoverBlink",
                            length: 900, easing: Easing.Linear,
                            finished: null, repeat: () => true);

                        Debug.WriteLine("✅ DiscoverLiveDot blinking STARTED");
                    }
                    else
                    {
                        DiscoverLiveDot.Opacity = 1.0;
                        DiscoverLiveDot.IsVisible = false;
                        Debug.WriteLine("✅ DiscoverLiveDot hidden");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateDiscoverLiveDotAsync error: {ex}");
            }
        }


        public class FolderInfo
        {
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }
        // Constructor
        public PostPage()
        {
            InitializeComponent();

            // ── Connect XAML Elements ─────────────────────────────────────
            DiscoverLiveDot = this.FindByName<Border>("DiscoverLiveDot");

            if (DiscoverLiveDot == null)
                Debug.WriteLine("❌ DiscoverLiveDot not found in XAML! Check x:Name.");
            else
                Debug.WriteLine("✅ DiscoverLiveDot successfully connected");

            _updateGrid = UpdateButton;
            _cancelGrid = CancelEditButton;

            Shell.SetNavBarIsVisible(this, false);

            // Category Picker Setup
            if (CategoryPicker != null && CategoryPicker.Items.Count == 0)
            {
                var defaults = new[]
                {
            "None", "Shoutout", "WCW", "love", "Celebration",
            "Event", "Other"
        };
                foreach (var c in defaults)
                    CategoryPicker.Items.Add(c);
                CategoryPicker.SelectedIndex = 0;
            }

            if (CategoryOtherEntry != null)
                CategoryOtherEntry.IsVisible = false;

            // ContentEditor Focus Styling
            if (ContentEditor != null)
            {
                ContentEditor.Focused += (s, e) =>
                {
                    ContentEditor.BackgroundColor = Color.FromArgb("#2A2A2A");
                    if (ContentEditor.Parent is Border border)
                        border.Stroke = Color.FromArgb("#008080");
                };

                ContentEditor.Unfocused += (s, e) =>
                {
                    ContentEditor.BackgroundColor = Colors.Transparent;
                    if (ContentEditor.Parent is Border border)
                        border.Stroke = Color.FromArgb("#333333");
                };
            }

            // Messaging Center Subscriptions
            MessagingCenter.Subscribe<object>(this, "MoodUpdated", async (sender) =>
            {
                Debug.WriteLine("MoodUpdated received");
                await LoadPostsAsync(forceRefresh: true);  // <-- NEW
            });

            MessagingCenter.Subscribe<object>(this, "MoodSaved", async (sender) =>
            {
                Debug.WriteLine("MoodSaved received");
                await LoadPostsAsync();
            });

            MessagingCenter.Subscribe<object, PostFilterInfo>(this, "FilterUserPosts",
                (sender, filterInfo) =>
                {
                    if (filterInfo == null) return;
                    _pendingFilterPhone = filterInfo.UserPhone;
                    _pendingFilterName = filterInfo.UserName;
                    _pendingScrollToLatest = filterInfo.ScrollToLatest;
                    _pendingUserFilter = true;
                });

            MessagingCenter.Subscribe<object>(this, "LiveStatusChanged", async (sender) =>
            {
                Debug.WriteLine("LiveStatusChanged received in PostPage");
                await UpdateDiscoverLiveDotAsync();
            });

            // Find the buttons
            _updateGrid = this.FindByName<Grid>("UpdateButton");
            _cancelGrid = this.FindByName<Grid>("CancelEditButton");
            _saveBtn = this.FindByName<Border>("PostButton");

            // Default Visibility
            _selectedVisibility = "Everyone";
            UpdateVisibilityIconColor("#008080");

            Debug.WriteLine("PostPage Constructor Completed");
        }

        private async void ShareButton_Tapped(object sender, TappedEventArgs e)
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

        private async Task<string?> GetUserDisplayName(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                return user?.Name ?? phone;
            }
            catch
            {
                return phone;
            }
        }

        private async Task<string> GetCurrentUserMoodAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return string.Empty;

                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var user = users.FirstOrDefault();

                return user?.Mood ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }


        private async Task<bool> IsFollowerAsync(string followerPhone, string followingPhone)
        {
            try
            {
                var follows = await SupabaseService.GetAsync<Follow>("Follows",
                    $"FollowerPhone=eq.{Uri.EscapeDataString(followerPhone)}&FollowingPhone=eq.{Uri.EscapeDataString(followingPhone)}&limit=1");

                return follows.Any();
            }
            catch
            {
                return false;
            }
        }

        private System.Threading.Timer? _statusCleanupTimer;

        // In OnAppearing method, start the cleanup timer
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Show overlay immediately
            if (LoadingOverlay != null)
            {
                LoadingOverlay.IsVisible = true;
                LoadingOverlay.Opacity = 0;
                await LoadingOverlay.FadeTo(1, 300, Easing.CubicOut);
                StartPostPageLoadingAnimations();
            }

            // Configure skeleton count
            ConfigureSkeletonCount();

            StartPollingForNewPosts();
            ResetBottomNavBar();
            StartStatusCleanupTimer();

            // Start live status check for Discover button dot
            StartLiveStatusCheck();
            StartLiveUsersPolling();

            try
            {
                await LoadPostsAsync(forceRefresh: false);
                await LoadUnreadConversationsCount();
                UpdateNotificationBadge();

                // Chat badge updates
                await UpdateBottomNavChatBadge();

                await LoadLiveUsersForPostPageAsync();
                await UpdateDiscoverLiveDotAsync();
                await LoadNearbyUsersAsync();

                // Subscribe to message updates
                MessagingCenter.Subscribe<object>(this, "MessagesUpdated", async (sender) =>
                {
                    Debug.WriteLine("MessagesUpdated received in PostPage");
                    await UpdateBottomNavChatBadge();
                });

                MessagingCenter.Subscribe<object>(this, "ConversationsUpdated", async (sender) =>
                {
                    Debug.WriteLine("ConversationsUpdated received in PostPage");
                    await UpdateBottomNavChatBadge();
                });

                // Subscribe to chat badge updates
                MessagingCenter.Subscribe<object>(this, "UpdateChatBadge", async (sender) =>
                {
                    Debug.WriteLine("UpdateChatBadge received in PostPage");
                    await UpdateBottomNavChatBadge();
                });

                // Subscribe to new message notifications
                MessagingCenter.Subscribe<object, string>(this, "NewMessage", async (sender, phone) =>
                {
                    Debug.WriteLine($"NewMessage received from {phone} in PostPage");
                    await UpdateBottomNavChatBadge();
                });

                // Subscribe to spark changes from ProfilePage
                MessagingCenter.Subscribe<Lock.Services.SparkUpdateMessage>(this, "SparkToggled", (message) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var postToUpdate = _allFeedPosts?.FirstOrDefault(p => p.Id == message.PostId);
                        if (postToUpdate != null)
                        {
                            postToUpdate.IsSparkedByCurrentUser = message.IsSparked;
                            postToUpdate.SparkCount = message.SparkCount;
                            postToUpdate.RefreshSparkState();
                            Debug.WriteLine($"[PostPage] Updated post {message.PostId} - Sparked: {message.IsSparked}");
                        }
                    });
                });

                if (_pendingUserFilter && !string.IsNullOrEmpty(_pendingFilterPhone))
                {
                    await FilterByUser(_pendingFilterPhone, _pendingFilterName, _pendingScrollToLatest);
                    _pendingUserFilter = false;
                    _pendingFilterPhone = string.Empty;
                    _pendingFilterName = string.Empty;
                    _pendingScrollToLatest = false;
                }
                else
                {
                    var isFiltering = PostsCollectionView.ItemsSource != _allFeedPosts;
                    if (!isFiltering && PostsCollectionView.ItemsSource == null)
                    {
                        PostsCollectionView.ItemsSource = _allFeedPosts;
                    }
                }

                MessagingCenter.Subscribe<object, NotificationItem>(this, "NewUnreadNotification", (s, n) =>
                {
                    UpdateNotificationBadge();
                });

                MessagingCenter.Subscribe<object>(this, "NotificationRead", (s) =>
                {
                    UpdateNotificationBadge();
                });

                MessagingCenter.Subscribe<object>(this, "AllNotificationsRead", (s) =>
                {
                    UpdateNotificationBadge();
                });

                MessagingCenter.Subscribe<object, int>(this, "NavigateToPost", async (s, postId) =>
                {
                    try
                    {
                        await HandleNavigateToPostAsync(postId);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"NavigateToPost handler error: {ex}");
                    }
                });

                MessagingCenter.Subscribe<object, int>(this, "PostHidden", async (sender, postId) =>
                {
                    try
                    {
                        Debug.WriteLine($"PostHidden received for post ID: {postId}");
                        await LoadPostsAsync(forceRefresh: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error handling PostHidden: {ex}");
                    }
                });

                MessagingCenter.Subscribe<object, int>(this, "PostUnhidden", async (sender, postId) =>
                {
                    try
                    {
                        Debug.WriteLine($"PostUnhidden received for post ID: {postId}");
                        await LoadPostsAsync(forceRefresh: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error handling PostUnhidden: {ex}");
                    }
                });

                MessagingCenter.Subscribe<object, string>(this, "UserMuted", async (sender, phone) =>
                {
                    try
                    {
                        await LoadPostsAsync(forceRefresh: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UserMuted refresh error: {ex}");
                    }
                });

                MessagingCenter.Subscribe<object, string>(this, "UserUnmuted", async (sender, phone) =>
                {
                    try
                    {
                        await LoadPostsAsync(forceRefresh: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UserUnmuted refresh error: {ex}");
                    }
                });

                // Subscribe to live status updates from DiscoverPage
                MessagingCenter.Subscribe<object>(this, "LiveStatusChanged", async (sender) =>
                {
                    Debug.WriteLine("LiveStatusChanged received in PostPage");
                    await UpdateDiscoverLiveDotAsync();
                });

                StartRelativeTimeTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAppearing error: {ex}");
            }
            finally
            {
                // Hide overlay when all loading is done
                if (LoadingOverlay != null)
                {
                    StopPostPageLoadingAnimations();
                    await LoadingOverlay.FadeTo(0, 400, Easing.CubicIn);
                    LoadingOverlay.IsVisible = false;
                }
            }
        }


        // Add this method to start the periodic check for live status
        private void StartLiveStatusCheck()
        {
            _liveStatusCheckTimer?.Dispose();
            _liveStatusCheckTimer = new System.Timers.Timer(4000); // every 4 seconds
            _liveStatusCheckTimer.Elapsed += async (s, e) => await UpdateDiscoverLiveDotAsync();
            _liveStatusCheckTimer.Start();
        }

        // Add this method to stop the timer
        private void StopLiveStatusCheck()
        {
            _liveStatusCheckTimer?.Dispose();
            _liveStatusCheckTimer = null;

            // Stop the blinking animation
            StopDiscoverLiveBlinking();
        }

        private List<Lock.Models.Post> _pendingNewPosts = new();
        private System.Threading.Timer? _pollTimer;

        // Call this in OnAppearing
        private void StartPollingForNewPosts()
        {
            _pollTimer?.Dispose();
            _pollTimer = new System.Threading.Timer(async _ =>
            {
                await CheckForNewPostsAsync();
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private async Task CleanupExpiredStatuses()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var duration = Preferences.Get($"status_duration_{currentUserPhone}", "24 hours");
                var expirationHours = GetExpirationHours(duration);

                // FIXED: Use Supabase instead of SQLite
                var allStatuses = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                    "StatusImagePath=not.is.null");

                var now = DateTime.UtcNow;
                var expiredStatuses = new List<Lock.Models.Post>();

                foreach (var status in allStatuses)
                {
                    var age = now - status.CreatedAt;
                    if (age.TotalHours >= expirationHours)
                    {
                        expiredStatuses.Add(status);
                    }
                }

                bool needReload = false;

                foreach (var expired in expiredStatuses)
                {
                    // FIXED: Delete from Supabase
                    await SupabaseService.DeleteAsync("Posts", $"Id=eq.{expired.Id}");

                    // Delete the actual image file
                    if (!string.IsNullOrEmpty(expired.StatusImagePath) && System.IO.File.Exists(expired.StatusImagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(expired.StatusImagePath);
                        }
                        catch { }
                    }
                    needReload = true;
                }

                if (needReload)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await LoadPostsAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up expired statuses: {ex}");
            }
        }


        private void StartStatusCleanupTimer()
        {
            _statusCleanupTimer?.Dispose();
            _statusCleanupTimer = new System.Threading.Timer(async _ =>
            {
                await CleanupExpiredStatuses();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        }

        public static class StatusVisibilityHelper
        {
            public static bool CanUserSeeStatus(string currentUserPhone, string viewerPhone)
            {
                if (string.IsNullOrEmpty(currentUserPhone) || string.IsNullOrEmpty(viewerPhone))
                    return false;

                // If viewer is the current user, they can always see their own status
                if (currentUserPhone == viewerPhone)
                    return true;

                // Get privacy setting
                var privacy = Preferences.Get($"status_privacy_{currentUserPhone}", "Everyone");

                switch (privacy)
                {
                    case "Only Me":
                        return false;

                    case "Everyone":
                        return true;

                    case "My Contacts":
                        // Check if viewer is in conversations list (is a contact)
                        var conversations = ChatRepository.GetConversationsForUserAsync(currentUserPhone).GetAwaiter().GetResult();
                        return conversations.Any(c =>
                            c.ParticipantA == viewerPhone ||
                            c.ParticipantB == viewerPhone);

                    case "Custom":
                        // Check allowed contacts
                        var allowedJson = Preferences.Get($"status_allowed_contacts_{currentUserPhone}", string.Empty);
                        var allowedPhones = string.IsNullOrEmpty(allowedJson)
                            ? new HashSet<string>()
                            : System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(allowedJson) ?? new HashSet<string>();

                        // Check blocked contacts
                        var blockedJson = Preferences.Get($"status_blocked_contacts_{currentUserPhone}", string.Empty);
                        var blockedPhones = string.IsNullOrEmpty(blockedJson)
                            ? new HashSet<string>()
                            : System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(blockedJson) ?? new HashSet<string>();

                        // If in blocked list, they cannot see
                        if (blockedPhones.Contains(viewerPhone))
                            return false;

                        // If allowed list is empty, everyone can see (except blocked)
                        if (!allowedPhones.Any())
                            return true;

                        // Otherwise, only allowed contacts can see
                        return allowedPhones.Contains(viewerPhone);

                    default:
                        return true;
                }
            }
        }

        private async void OnStatusMenuTapped(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new StatusSettingsPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex}");
            }
        }

        private async Task CheckForNewPostsAsync()
        {
            try
            {
                var latestPostTime = _allFeedPosts?.FirstOrDefault()?.CreatedAt ?? DateTime.MinValue;

                var allPosts = await PostRepository.GetAllAsync() ?? new();
                var newPosts = allPosts
                    .Where(p => p.CreatedAt > latestPostTime
                             && string.IsNullOrEmpty(p.StatusImagePath))
                    .ToList();

                if (!newPosts.Any()) return;

                _pendingNewPosts = newPosts;

                // Get distinct authors (up to 3)
                var authors = newPosts
                    .GroupBy(p => p.AuthorPhone)
                    .Select(g => g.First())
                    .Take(3)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowNewPostsPill(authors);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckForNewPostsAsync error: {ex}");
            }
        }

        private void ShowNewPostsPill(List<Lock.Models.Post> authors)
        {
            try
            {
                var pill = this.FindByName<Border>("NewPostsPill");
                var label = this.FindByName<Label>("NewPostsPillLabel");
                if (pill == null) return;

                int count = _pendingNewPosts.Count;
                if (label != null)
                    label.Text = count == 1 ? "new post" : $"{count} new posts";

                var avatarControls = new[]
                {
            (this.FindByName<Border>("PillAvatar1"), this.FindByName<Image>("PillAvatarImage1")),
            (this.FindByName<Border>("PillAvatar2"), this.FindByName<Image>("PillAvatarImage2")),
            (this.FindByName<Border>("PillAvatar3"), this.FindByName<Image>("PillAvatarImage3")),
        };

                for (int i = 0; i < avatarControls.Length; i++)
                {
                    var (border, image) = avatarControls[i];
                    if (border == null || image == null) continue;

                    if (i < authors.Count)
                    {
                        var post = authors[i];
                        border.IsVisible = true;
                        border.BackgroundColor = Color.FromArgb("#444");

                        if (!string.IsNullOrEmpty(post.AuthorProfileImagePath)
                            && File.Exists(post.AuthorProfileImagePath))
                        {
                            image.Source = ImageSource.FromFile(post.AuthorProfileImagePath);
                            image.IsVisible = true;
                            // Force clip the image to the circle
                            image.Clip = new EllipseGeometry
                            {
                                Center = new Point(13, 13),
                                RadiusX = 13,
                                RadiusY = 13
                            };
                        }
                        else
                        {
                            // No image — show initials-style colored background instead
                            image.IsVisible = false;
                            border.BackgroundColor = Color.FromArgb("#FF3B6F");
                        }
                    }
                    else
                    {
                        border.IsVisible = false;
                    }
                }

                pill.IsVisible = true;
                pill.TranslationY = -60;
                pill.Opacity = 0;
                pill.TranslateTo(0, 0, 300, Easing.CubicOut);
                pill.FadeTo(1, 300);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowNewPostsPill error: {ex}");
            }
        }

        private async void NewPostsPill_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                // Hide pill
                var pill = this.FindByName<Border>("NewPostsPill");
                if (pill != null)
                {
                    await pill.FadeTo(0, 200);
                    pill.IsVisible = false;
                }

                // Reload posts and scroll to top
                await LoadPostsAsync();
                await MainScrollView?.ScrollToAsync(0, 0, true);

                _pendingNewPosts.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NewPostsPill_Tapped error: {ex}");
            }
        }



        // Call in OnDisappearing to stop polling
        private void StopPolling()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        // ── Helper method to reset bottom nav bar and header ──
        private void ResetBottomNavBar()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (MainGrid?.RowDefinitions.Count > 2)
                        MainGrid.RowDefinitions[2].Height = GridLength.Auto;

                    var bottomNavBar = this.FindByName<Border>("BottomNavBar");
                    if (bottomNavBar != null)
                    {
                        bottomNavBar.TranslationY = 0;
                        bottomNavBar.Opacity = 1;
                        bottomNavBar.IsVisible = true;
                    }

                    _isNavBarVisible = true;
                    _lastScrollY = 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ResetBottomNavBar error: {ex}");
                }
            });
        }
        private async Task HandleNavigateToPostAsync(int postId)
        {
            try
            {
                if (postId <= 0) return;

                if (_allFeedPosts == null || !_allFeedPosts.Any())
                    await LoadPostsAsync();

                var target = _allFeedPosts?.FirstOrDefault(p => p.Id == postId);

                if (PostsCollectionView != null)
                {
                    var currentSource = PostsCollectionView.ItemsSource as System.Collections.IEnumerable;
                    var containsTarget = false;

                    if (currentSource != null && target != null)
                    {
                        foreach (var it in currentSource)
                        {
                            if (it is Lock.Models.Post pp && pp.Id == postId)
                            {
                                containsTarget = true;
                                break;
                            }
                        }
                    }

                    if (!containsTarget && _allFeedPosts != null && _allFeedPosts.Any())
                    {
                        PostsCollectionView.ItemsSource = _allFeedPosts;
                        RefreshCollectionView();
                    }
                }

                await Task.Delay(120);

                if (target != null && PostsCollectionView != null)
                {
                    try
                    {
                        PostsCollectionView.ScrollTo(target, position: ScrollToPosition.Center, animate: true);
                        Debug.WriteLine($"Scrolled to post {postId} in feed");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ScrollTo failed for post {postId}: {ex}");
                    }
                }

                try
                {
                    await Shell.Current.GoToAsync($"postdetails?postId={postId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fallback navigation to postdetails failed for {postId}: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HandleNavigateToPostAsync error for {postId}: {ex}");
            }
        }

        private async Task FilterByUser(string userPhone, string userName, bool scrollToLatest = false)
        {
            try
            {
                Debug.WriteLine($"Filtering posts for user: {userName} ({userPhone}), ScrollToLatest: {scrollToLatest}");

                if (CreatePostSection != null)
                {
                    CreatePostSection.IsVisible = false;
                }

                if (_allFeedPosts == null || _allFeedPosts.Count == 0)
                {
                    await LoadPostsAsync();
                }

                string cleanUserPhone = userPhone.Trim();

                var userPosts = _allFeedPosts
                    .Where(p => !string.IsNullOrEmpty(p.AuthorPhone))
                    .Where(p =>
                    {
                        var authorPhone = p.AuthorPhone;
                        if (authorPhone.Contains("·"))
                        {
                            var parts = authorPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                            authorPhone = parts.Length > 1 ? parts[1].Trim() : authorPhone;
                        }
                        return authorPhone.Trim() == cleanUserPhone;
                    })
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                Debug.WriteLine($"Found {userPosts.Count} posts for user {userName}");

                PostsCollectionView.ItemsSource = userPosts;

                if (scrollToLatest && userPosts.Any())
                {
                    await Task.Delay(100);
                    MainScrollView?.ScrollToAsync(0, 0, true);
                    PostsCollectionView?.ScrollTo(userPosts.First(), position: ScrollToPosition.Start, animate: true);
                    Debug.WriteLine("Scrolled to latest post");
                }

                if (userPosts.Count == 0)
                {
                    await DisplayAlert("No Posts", $"{userName} hasn't made any posts yet.", "OK");
                }

                _pendingUserFilter = false;
                _pendingScrollToLatest = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error filtering by user: {ex}");
                await DisplayAlert("Error", "Could not filter posts by user", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Stop polling for new posts
            StopPolling();

            // Dispose status cleanup timer
            _statusCleanupTimer?.Dispose();
            _statusCleanupTimer = null;

            // Stop live status check timer
            StopLiveStatusCheck();
            StopLiveUsersPolling();
            foreach (var card in _liveCards)
            {
                card.StopCarousel();
            }

            // Unsubscribe from all messaging center events
            MessagingCenter.Unsubscribe<object, NotificationItem>(this, "NewNotificationStructured");
            MessagingCenter.Unsubscribe<object, string>(this, "NewNotification");
            MessagingCenter.Unsubscribe<object, int>(this, "NotificationStoreChanged_RemoveComment");
            MessagingCenter.Unsubscribe<object, string>(this, "NotificationStoreChanged_RemoveReaction");
            MessagingCenter.Unsubscribe<object, int>(this, "PostUnhidden");
            MessagingCenter.Unsubscribe<object, int>(this, "PostHidden");
            MessagingCenter.Unsubscribe<object, string>(this, "UserMuted");
            MessagingCenter.Unsubscribe<object, string>(this, "UserUnmuted");

            // Unsubscribe from chat events
            MessagingCenter.Unsubscribe<object>(this, "MessagesUpdated");
            MessagingCenter.Unsubscribe<object>(this, "ConversationsUpdated");
            MessagingCenter.Unsubscribe<object>(this, "UpdateChatBadge");
            MessagingCenter.Unsubscribe<object, string>(this, "NewMessage");

            // ADD THESE - Unsubscribe from spark changes
            MessagingCenter.Unsubscribe<Lock.Services.SparkUpdateMessage>(this, "SparkToggled");
            MessagingCenter.Unsubscribe<RefreshPostMessage>(this, "RefreshPostData");

            // Unsubscribe from live status updates
            MessagingCenter.Unsubscribe<object>(this, "LiveStatusChanged");

            try
            {
                MessagingCenter.Send(this, "NotificationRead", this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnDisappearing messaging error: {ex}");
            }
        }

        // Add this class alongside SparkChangedMessage
        public class RefreshPostMessage
        {
            public int PostId { get; set; }
        }

        private void UserHeaderTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is string phone && !string.IsNullOrEmpty(phone))
                {
                    var navigationParams = new Dictionary<string, object>
                    {
                        ["phone"] = phone,
                        ["viewOnly"] = "true"
                    };
                    Shell.Current.GoToAsync("///profile", navigationParams);
                }
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Could not navigate to profile: {ex.Message}", "OK");
            }
        }


        private void MainScrollView_Scrolled(object? sender, ScrolledEventArgs e)
        {
            // Status bar stays fully visible at all times — no shrinking, no hiding
        }
        private void SetTopImagePlaceholderVisible(bool visible)
        {
            try
            {
                if (TopImage != null)
                    TopImage.IsVisible = visible;

                if (TopImageRing != null)
                {
                    TopImageRing.IsVisible = visible && TopImageRing.Drawable != null;
                }
            }
            catch
            {
                // swallow
            }
        }

        private void StartRelativeTimeTimer()
        {
            if (_refreshingRelativeTimes) return;
            _refreshingRelativeTimes = true;

            Dispatcher.StartTimer(_relativeRefreshInterval, () =>
            {
                try
                {
                    RefreshRelativeTimes();
                }
                catch
                {
                    // ignore
                }

                return _refreshingRelativeTimes;
            });
        }

        private sealed class StatusRingDrawable : IDrawable
        {
            private readonly int _segments;
            private readonly Microsoft.Maui.Graphics.Color _ringColor;
            private readonly float _stroke;

            public StatusRingDrawable(int segments, Microsoft.Maui.Graphics.Color ringColor, float stroke = 2.0f)
            {
                _segments = Math.Max(1, segments);
                _ringColor = ringColor;
                _stroke = stroke;
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                if (_segments <= 0) return;

                float cx = dirtyRect.Center.X;
                float cy = dirtyRect.Center.Y;
                float radius = (Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f) - (_stroke / 2f) - 1f;

                canvas.SaveState();
                canvas.StrokeColor = _ringColor;
                canvas.StrokeSize = _stroke;
                canvas.StrokeLineCap = LineCap.Round;

                float gapDegrees = _segments == 1 ? 0f
                   : _segments == 2 ? 30f
                   : _segments == 3 ? 24f
                   : _segments == 4 ? 20f
                   : 16f;

                float totalGap = gapDegrees * _segments;
                float arcSweep = (360f - totalGap) / _segments;
                float startAngle = -90f; // top

                for (int i = 0; i < _segments; i++)
                {
                    // Convert to radians for manual arc drawing
                    float startRad = (float)(startAngle * Math.PI / 180.0);
                    float endRad = (float)((startAngle + arcSweep) * Math.PI / 180.0);

                    var path = new PathF();
                    bool first = true;
                    int steps = Math.Max(20, (int)(arcSweep));

                    for (int s = 0; s <= steps; s++)
                    {
                        float t = startRad + (endRad - startRad) * s / steps;
                        float x = cx + radius * (float)Math.Cos(t);
                        float y = cy + radius * (float)Math.Sin(t);

                        if (first) { path.MoveTo(x, y); first = false; }
                        else path.LineTo(x, y);
                    }

                    canvas.DrawPath(path);

                    startAngle += arcSweep + gapDegrees;
                }

                canvas.RestoreState();
            }
        }
        private void UpdateStatusRing(int segments)
        {
            if (TopImageRing == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateStatusRing: TopImageRing not found!");
                return;
            }

            TopImageRing.BackgroundColor = Colors.Transparent;
            TopImageRing.InputTransparent = true;

            if (TopImage != null)
                TopImage.BackgroundColor = Colors.Transparent;

            if (segments <= 0)
            {
                TopImageRing.Drawable = null;
                TopImageRing.IsVisible = false;
                System.Diagnostics.Debug.WriteLine("UpdateStatusRing: Ring hidden (0 segments)");
                return;
            }

            var color = Microsoft.Maui.Graphics.Color.FromArgb("#008080");

            var drawable = new StatusRingDrawable(segments, color, stroke: 2.0f);
            TopImageRing.Drawable = drawable;
            TopImageRing.IsVisible = true;
            TopImageRing.Invalidate();

            System.Diagnostics.Debug.WriteLine($"UpdateStatusRing: Ring updated with {segments} segments and invalidated");

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                TopImageRing.Invalidate();
                System.Diagnostics.Debug.WriteLine("UpdateStatusRing: Second invalidation triggered");
            });
        }

        private void UpdateNotificationBadge()
        {
            try
            {
                var json = Preferences.Get("notifications_v2", string.Empty);
                int unreadCount = 0;

                if (!string.IsNullOrEmpty(json))
                {
                    var notifications = System.Text.Json.JsonSerializer.Deserialize<List<NotificationItem>>(json);
                    if (notifications != null)
                    {
                        unreadCount = notifications.Count(n => !n.IsRead);
                    }
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (NotificationBadge != null && NotificationBadgeLabel != null)
                    {
                        if (unreadCount > 0)
                        {
                            NotificationBadgeLabel.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
                            NotificationBadge.IsVisible = true;
                        }
                        else
                        {
                            NotificationBadge.IsVisible = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateNotificationBadge error: {ex}");
            }
        }

        // Add this method to calculate match percentage for a user
        private async Task<int> GetMatchPercentageForUserAsync(string targetUserPhone)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();

                if (string.Equals(currentUserPhone, targetUserPhone, StringComparison.OrdinalIgnoreCase))
                    return 0;

                if (string.IsNullOrEmpty(currentUserPhone) || string.IsNullOrEmpty(targetUserPhone))
                    return 0;

                // FIXED: Use Supabase instead of SQLite
                var currentUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var currentUser = currentUsers.FirstOrDefault();

                var targetUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(targetUserPhone)}&limit=1");
                var targetUser = targetUsers.FirstOrDefault();

                if (currentUser == null || targetUser == null)
                    return 0;

                return await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, targetUser);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetMatchPercentageForUserAsync error: {ex}");
                return 0;
            }
        }



        #region Match Percentage Methods

        /// <summary>
        /// Calculates match percentages for all posts based on current user's dating interest
        /// </summary>
        private async Task CalculateMatchPercentagesForPostsAsync(List<Lock.Models.Post> posts)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();

                if (string.IsNullOrEmpty(currentUserPhone))
                    return;

                // FIXED: Use Supabase instead of SQLite
                var currentUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var currentUser = currentUsers.FirstOrDefault();

                if (currentUser == null)
                    return;

                // Get current user's dating interest preference
                string userInterest = currentUser.Interest ?? "Everyone";
                Debug.WriteLine($"Current user interest: {userInterest}");

                // Get unique author phones that are not the current user
                var authorPhones = posts
                    .Where(p => !string.Equals(p.AuthorPhone?.Trim(), currentUserPhone, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.AuthorPhone?.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .ToList();

                if (!authorPhones.Any())
                {
                    Debug.WriteLine("No other users to calculate matches for");
                    return;
                }

                // Load all target users at once for efficiency
                var targetUsers = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
                foreach (var phone in authorPhones)
                {
                    // FIXED: Use Supabase instead of SQLite
                    var targetUsersList = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var targetUser = targetUsersList.FirstOrDefault();
                    if (targetUser != null)
                    {
                        targetUsers[phone] = targetUser;
                    }
                    else
                    {
                        Debug.WriteLine($"User not found for phone: {phone}");
                    }
                }

                // Calculate match percentage for each post with interest filtering
                foreach (var post in posts)
                {
                    var authorPhone = post.AuthorPhone?.Trim();

                    // Skip if it's the current user's own post
                    if (string.Equals(authorPhone, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                    {
                        post.MatchPercent = 0;
                        continue;
                    }

                    // Get target user
                    if (!string.IsNullOrEmpty(authorPhone) && targetUsers.TryGetValue(authorPhone, out var targetUser))
                    {
                        // Check if the target user matches the current user's interest preference
                        bool matchesInterest = DoesUserMatchInterest(targetUser, userInterest);

                        if (matchesInterest)
                        {
                            // Calculate compatibility score only if interest matches
                            post.MatchPercent = await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, targetUser);
                            Debug.WriteLine($"Match for {post.AuthorDisplayName} (Gender: {targetUser.Gender}): {post.MatchPercent}% ✓");
                        }
                        else
                        {
                            post.MatchPercent = 0;
                            Debug.WriteLine($"Skipped match for {post.AuthorDisplayName} (Gender: {targetUser.Gender}) - does not match interest: {userInterest} ✗");
                        }
                    }
                    else
                    {
                        post.MatchPercent = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalculateMatchPercentagesForPostsAsync error: {ex}");
            }
        }

        /// <summary>
        /// Checks if a user matches the current user's dating interest
        /// </summary>
        private bool DoesUserMatchInterest(User targetUser, string userInterest)
        {
            if (string.IsNullOrEmpty(userInterest) || userInterest == "Everyone")
                return true;

            if (string.IsNullOrEmpty(targetUser.Gender))
                return false;

            // Map gender to interest matching
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
                    // For custom interests like "Non-binary", "Everyone", etc.
                    return userInterest.Equals("Everyone", StringComparison.OrdinalIgnoreCase) ||
                           targetUser.Gender.Equals(userInterest, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Optional: Completely filter out posts from users who don't match the dating interest
        /// </summary>
        private async Task<List<Lock.Models.Post>> FilterPostsByInterestAsync(List<Lock.Models.Post> posts)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                if (string.IsNullOrEmpty(currentUserPhone))
                    return posts;

                // FIXED: Use Supabase instead of SQLite
                var currentUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var currentUser = currentUsers.FirstOrDefault();

                if (currentUser == null || string.IsNullOrEmpty(currentUser.Interest) || currentUser.Interest == "Everyone")
                    return posts;

                string userInterest = currentUser.Interest;
                var filteredPosts = new List<Lock.Models.Post>();

                // Get all unique author phones from posts
                var authorPhones = posts
                    .Select(p => p.AuthorPhone?.Trim())
                    .Where(p => !string.IsNullOrEmpty(p) && !string.Equals(p, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                // Load author users
                var authorUsers = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
                foreach (var phone in authorPhones)
                {
                    // FIXED: Use Supabase instead of SQLite
                    var authorUsersList = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var authorUser = authorUsersList.FirstOrDefault();
                    if (authorUser != null)
                    {
                        authorUsers[phone] = authorUser;
                    }
                }

                foreach (var post in posts)
                {
                    var authorPhone = post.AuthorPhone?.Trim();

                    // Always keep current user's own posts
                    if (string.Equals(authorPhone, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                    {
                        filteredPosts.Add(post);
                        continue;
                    }

                    // Check if author matches interest
                    if (!string.IsNullOrEmpty(authorPhone) && authorUsers.TryGetValue(authorPhone, out var authorUser))
                    {
                        if (DoesUserMatchInterest(authorUser, userInterest))
                        {
                            filteredPosts.Add(post);
                            Debug.WriteLine($"Post {post.Id} kept - author {authorUser.Gender} matches interest {userInterest}");
                        }
                        else
                        {
                            Debug.WriteLine($"Post {post.Id} filtered out - author {authorUser.Gender} does not match interest {userInterest}");
                        }
                    }
                    else
                    {
                        // If we can't determine the author's gender, keep the post but don't show match %
                        filteredPosts.Add(post);
                    }
                }

                Debug.WriteLine($"Interest filter: {posts.Count} -> {filteredPosts.Count} posts");
                return filteredPosts;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterPostsByInterestAsync error: {ex}");
                return posts;
            }
        }

        #endregion

        private async Task LoadUnreadConversationsCount()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    if (ChatNavBadge != null)
                        ChatNavBadge.IsVisible = false;
                    return;
                }

                // FIXED: Use Supabase instead of SQLite
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(ParticipantA.eq.{Uri.EscapeDataString(currentUserPhone)},ParticipantB.eq.{Uri.EscapeDataString(currentUserPhone)})");

                int unreadConversationsCount = 0;

                foreach (var conv in conversations)
                {
                    try
                    {
                        var unreadMessages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                            $"ConversationId=eq.{conv.ConversationId}&RecipientPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&IsRead=eq.false");

                        if (unreadMessages.Count > 0)
                        {
                            unreadConversationsCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking conversation {conv.ConversationId}: {ex}");
                    }
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (ChatNavBadge != null && ChatNavBadgeLabel != null)
                    {
                        if (unreadConversationsCount > 0)
                        {
                            ChatNavBadgeLabel.Text = unreadConversationsCount.ToString();
                            ChatNavBadge.IsVisible = true;
                        }
                        else
                        {
                            ChatNavBadge.IsVisible = false;
                        }
                    }
                });

                await UpdateBottomNavChatBadge();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading unread count: {ex}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (ChatNavBadge != null)
                        ChatNavBadge.IsVisible = false;
                });
            }
        }
        private async void AddImageFloatingButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== AddImageFloatingButton_Clicked started ===");

                var hasPermission = await PermissionsHelper.RequestStoragePermissionsAsync();
                if (!hasPermission)
                {
                    await DisplayAlert("Permission Required",
                        "Storage permission is needed to select images. Please grant permission in app settings.",
                        "OK");
                    return;
                }

                var results = await FilePicker.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select one or more images for status",
                    FileTypes = FilePickerFileType.Images
                });

                if (results == null || !results.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No images selected");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Selected {results.Count()} images");

                var moodChoice = await DisplayActionSheet(
                    $"Select mood for {results.Count()} image(s)",
                    "Cancel",
                    null,
                    "Happy", "Sad", "Excited", "Angry", "Neutral", "Custom");

                if (string.IsNullOrEmpty(moodChoice) || moodChoice == "Cancel")
                {
                    System.Diagnostics.Debug.WriteLine("User cancelled mood selection");
                    return;
                }

                string mood = string.Empty;
                if (string.Equals(moodChoice, "Custom", StringComparison.OrdinalIgnoreCase))
                {
                    mood = await DisplayPromptAsync(
                        "Custom mood",
                        "Enter mood (e.g. 'Thoughtful')",
                        initialValue: string.Empty) ?? string.Empty;
                    mood = mood.Trim();

                    if (string.IsNullOrEmpty(mood))
                    {
                        await DisplayAlert("Info", "No mood entered. Status creation cancelled.", "OK");
                        return;
                    }
                }
                else
                {
                    mood = moodChoice;
                }

                var savedPaths = new List<string>();
                var authorPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;

                if (string.IsNullOrEmpty(authorPhone))
                {
                    await DisplayAlert("Error", "You must be logged in to add status images", "OK");
                    return;
                }

                foreach (var r in results)
                {
                    try
                    {
                        var destFileName = $"status_{Guid.NewGuid():N}{System.IO.Path.GetExtension(r.FileName)}";
                        var saved = await SavePickedFileAsync(r, destFileName);

                        if (string.IsNullOrEmpty(saved))
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to save file: {r.FileName}");
                            continue;
                        }

                        savedPaths.Add(saved);
                        System.Diagnostics.Debug.WriteLine($"Saved file to: {saved} with mood: {mood}");

                        // FIXED: Insert into Supabase
                        var statusPost = new Lock.Models.Post
                        {
                            AuthorPhone = authorPhone,
                            Content = string.Empty,
                            ImagePathsList = Array.Empty<string>(),
                            StatusImagePath = saved,
                            Mood = mood,
                            CreatedAt = DateTime.UtcNow
                        };

                        await SupabaseService.InsertAsync("Posts", statusPost);
                        System.Diagnostics.Debug.WriteLine($"Status post inserted with ID: {statusPost.Id}, mood: {mood}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing image {r.FileName}: {ex}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Total saved paths: {savedPaths.Count}");

                if (savedPaths.Any())
                {
                    try
                    {
                        if (TopImage != null)
                            TopImage.Source = ImageSource.FromFile(savedPaths.First());

                        SetTopImagePlaceholderVisible(true);

                        if (TopImageActions != null)
                            TopImageActions.IsVisible = false;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating top image: {ex}");
                    }

                    await LoadPostsAsync();
                    await DisplayAlert("Success", $"{savedPaths.Count} status image(s) added with mood: {mood}", "OK");
                }
                else
                {
                    await DisplayAlert("Info", "No images were saved.", "OK");
                }

                System.Diagnostics.Debug.WriteLine("=== AddImageFloatingButton_Clicked completed ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddImageFloatingButton_Clicked: {ex}");
                await DisplayAlert("Error", "Could not add image(s): " + ex.Message, "OK");
            }
        }

        private async void TopImage_Tapped(object? sender, EventArgs e)
        {
            try
            {
                var authorPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;

                // FIXED: Use Supabase instead of SQLite
                var statusPosts = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(authorPhone)}&StatusImagePath=not.is.null&order=CreatedAt.desc");

                if (statusPosts == null || statusPosts.Count == 0)
                {
                    if (TopImageActions != null)
                        TopImageActions.IsVisible = !TopImageActions.IsVisible;
                    return;
                }

                var imagePaths = statusPosts
                    .Where(s => !string.IsNullOrEmpty(s.StatusImagePath))
                    .Select(s => s.StatusImagePath!)
                    .ToList();

                if (!imagePaths.Any())
                {
                    if (TopImageActions != null)
                        TopImageActions.IsVisible = !TopImageActions.IsVisible;
                    return;
                }

                int startIndex = 0;
                if (TopImage?.Source is FileImageSource fis && !string.IsNullOrEmpty(fis.File))
                {
                    var current = fis.File;
                    startIndex = imagePaths.FindIndex(p => string.Equals(p, current, StringComparison.OrdinalIgnoreCase));
                    if (startIndex < 0) startIndex = 0;
                }

                var fullScreenPage = new Lock.Pages.Profile.FullScreenMediaPage(imagePaths, startIndex);
                await Navigation.PushModalAsync(fullScreenPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TopImage_Tapped: {ex}");
                try
                {
                    if (TopImageActions != null)
                        TopImageActions.IsVisible = !TopImageActions.IsVisible;
                }
                catch { }
            }
        }

        private async void TopImageEdit_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var authorPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;

                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select replacement image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null) return;

                var destFileName = $"status_{Guid.NewGuid():N}{System.IO.Path.GetExtension(result.FileName)}";
                var saved = await SavePickedFileAsync(result, destFileName);
                if (string.IsNullOrEmpty(saved)) return;

                var moodChoice = await DisplayActionSheet("Change mood for this status (or Cancel)", "Keep", null,
                    "Happy", "Sad", "Excited", "Angry", "Neutral", "Custom");
                string mood = string.Empty;
                if (string.Equals(moodChoice, "Custom", StringComparison.OrdinalIgnoreCase))
                {
                    mood = await DisplayPromptAsync("Custom mood", "Enter mood (e.g. 'Thoughtful')", initialValue: string.Empty) ?? string.Empty;
                    mood = mood.Trim();
                }
                else if (!string.Equals(moodChoice, "Keep", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(moodChoice))
                {
                    mood = moodChoice;
                }

                string? currentPath = null;
                if (TopImage?.Source is FileImageSource fis && !string.IsNullOrEmpty(fis.File))
                    currentPath = fis.File;
                else if (TopImage?.Source is UriImageSource uis && uis.Uri != null)
                    currentPath = uis.Uri.ToString();

                // FIXED: Use Supabase instead of SQLite
                Lock.Models.Post? target = null;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    var statuses = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                        $"AuthorPhone=eq.{Uri.EscapeDataString(authorPhone)}&StatusImagePath=eq.{Uri.EscapeDataString(currentPath)}&order=CreatedAt.desc");
                    target = statuses.FirstOrDefault();
                }

                if (target != null)
                {
                    target.StatusImagePath = saved;
                    if (!string.IsNullOrEmpty(mood)) target.Mood = mood;
                    target.CreatedAt = DateTime.UtcNow;
                    await SupabaseService.UpdateAsync("Posts", $"Id=eq.{target.Id}", target);
                }
                else
                {
                    var statusPost = new Lock.Models.Post
                    {
                        AuthorPhone = authorPhone,
                        Content = string.Empty,
                        ImagePathsList = Array.Empty<string>(),
                        StatusImagePath = saved,
                        Mood = mood,
                        CreatedAt = DateTime.UtcNow
                    };
                    await SupabaseService.InsertAsync("Posts", statusPost);
                }

                if (TopImage != null)
                    TopImage.Source = ImageSource.FromFile(saved);

                SetTopImagePlaceholderVisible(true);
                if (TopImageActions != null)
                    TopImageActions.IsVisible = false;

                await LoadPostsAsync();
            }
            catch
            {
                // ignore transient errors
            }
        }

        private async void TopImageDelete_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var confirm = await DisplayAlert("Remove Image", "Remove this status image?", "Yes", "No");
                if (!confirm) return;

                string? currentPath = null;
                if (TopImage?.Source is FileImageSource fis && !string.IsNullOrEmpty(fis.File))
                    currentPath = fis.File;
                else if (TopImage?.Source is UriImageSource uis && uis.Uri != null)
                    currentPath = uis.Uri.ToString();

                if (string.IsNullOrEmpty(currentPath)) return;

                try
                {
                    var authorPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;

                    // FIXED: Use Supabase instead of SQLite
                    var statuses = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                        $"AuthorPhone=eq.{Uri.EscapeDataString(authorPhone)}&StatusImagePath=eq.{Uri.EscapeDataString(currentPath)}&order=CreatedAt.desc");
                    var toDelete = statuses.FirstOrDefault();

                    if (toDelete != null)
                    {
                        await SupabaseService.DeleteAsync("Posts", $"Id=eq.{toDelete.Id}");
                    }

                    var remaining = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                        $"AuthorPhone=eq.{Uri.EscapeDataString(authorPhone)}&StatusImagePath=not.is.null&order=CreatedAt.desc");

                    if (remaining.Any() && !string.IsNullOrEmpty(remaining.First().StatusImagePath) && System.IO.File.Exists(remaining.First().StatusImagePath))
                    {
                        if (TopImage != null)
                            TopImage.Source = ImageSource.FromFile(remaining.First().StatusImagePath);

                        SetTopImagePlaceholderVisible(true);
                        if (TopImageActions != null)
                            TopImageActions.IsVisible = false;
                        UpdateStatusRing(remaining.Count);
                    }
                    else
                    {
                        if (TopImage != null)
                            TopImage.Source = null;

                        SetTopImagePlaceholderVisible(false);
                        if (TopImageActions != null)
                            TopImageActions.IsVisible = false;
                        UpdateStatusRing(0);
                    }

                    await LoadPostsAsync();
                }
                catch
                {
                    // ignore DB deletion errors
                }
            }
            catch
            {
                // ignore
            }
        }

        private async void OnDiscoverTapped(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new DiscoverPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discover navigation error: {ex}");
                await DisplayAlert("Navigation Error",
                    "Could not open Discover page. Please try again.", "OK");
            }
        }

        private void RefreshRelativeTimes()
        {
            try
            {
                var items = PostsCollectionView?.ItemsSource as System.Collections.IEnumerable;
                if (items == null) return;

                foreach (var item in items)
                {
                    if (item is Lock.Models.Post post)
                    {
                        InvokeUpdateDisplayContent(post, 200);

                        if (!string.IsNullOrEmpty(post.Mood) && post.MoodLastUpdated != DateTime.MinValue)
                        {
                            post.MoodLastUpdatedRelative = GetRelativeTimeString(post.MoodLastUpdated);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RefreshRelativeTimes: {ex}");
            }
        }

        private void EditMenu_Clicked(object? sender, EventArgs e)
        {
            if (sender is MenuFlyoutItem mfi && mfi.CommandParameter is Lock.Models.Post post)
            {
                var tg = new TapGestureRecognizer { CommandParameter = post };
                EditIcon_Tapped(tg, EventArgs.Empty);
            }
        }

        private void DeleteMenu_Clicked(object? sender, EventArgs e)
        {
            if (sender is MenuFlyoutItem mfi && mfi.CommandParameter is Lock.Models.Post post)
            {
                var tg = new TapGestureRecognizer { CommandParameter = post };
                DeleteIcon_Tapped(tg, EventArgs.Empty);
            }
        }

        private async Task LoadPostsAsync(bool forceRefresh = false)
        {
            try
            {
                // Check if we have valid cached data for instant display
                bool hasValidCache = !forceRefresh &&
                                     _cachedFeedPosts != null &&
                                     _cachedStatusPosts != null &&
                                     (DateTime.UtcNow - _lastCacheTime) < CacheExpiry;

                if (hasValidCache)
                {
                    // INSTANT DISPLAY FROM CACHE - No skeleton loading!
                    System.Diagnostics.Debug.WriteLine("=== LoadPostsAsync: Loading from CACHE (instant display) ===");

                    _allFeedPosts = _cachedFeedPosts;
                    _allStatusPosts = _cachedStatusPosts;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Hide skeleton if it's visible
                        if (SkeletonCollectionView != null)
                            SkeletonCollectionView.IsVisible = false;
                        if (PostsCollectionView != null)
                        {
                            PostsCollectionView.IsVisible = true;
                            PostsCollectionView.ItemsSource = _allFeedPosts;
                        }

                        // Update status ring
                        var statusCount = _allStatusPosts.GroupBy(p => p.AuthorPhone).Count();
                        UpdateStatusRing(statusCount);
                    });

                    System.Diagnostics.Debug.WriteLine($"=== Cached display: {_allFeedPosts.Count} feed posts, {_allStatusPosts.Count} status posts ===");

                    // Refresh in background without blocking UI
                    if (!_isRefreshingInBackground)
                    {
                        _ = Task.Run(async () => await RefreshPostsInBackground());
                    }
                    return;
                }

                // No cache available - show skeleton and load fresh
                System.Diagnostics.Debug.WriteLine("=== LoadPostsAsync: No cache, loading fresh with skeleton ===");
                await ShowSkeletonLoadingAsync();

                var posts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                System.Diagnostics.Debug.WriteLine($"=== LoadPostsAsync: Retrieved {posts.Count} total posts from repository ===");

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;
                currentUserPhone = currentUserPhone.Trim();

                // ========== SEPARATE STATUS POSTS FROM REGULAR POSTS FIRST ==========
                var allRegularPosts = posts.Where(p => string.IsNullOrEmpty(p.StatusImagePath)).ToList();
                var allStatusPostsRaw = posts.Where(p => !string.IsNullOrEmpty(p.StatusImagePath)).ToList();

                System.Diagnostics.Debug.WriteLine($"Regular posts: {allRegularPosts.Count}, Status posts: {allStatusPostsRaw.Count}");

                // Process status posts separately for the top bar
                await ProcessStatusPostsForTopBarAsync(allStatusPostsRaw, currentUserPhone);

                // Now work ONLY with regular posts for the feed
                var filteredPosts = allRegularPosts.ToList();

                // Get current user's mood
                var currentUserMood = await GetCurrentUserMoodAsync();
                System.Diagnostics.Debug.WriteLine($"Current user mood: {currentUserMood ?? "none"}");

                // Get hidden post IDs and filter them out
                var hiddenPostIds = await HidePostService.GetHiddenPostIdsAsync(currentUserPhone);
                filteredPosts = filteredPosts.Where(p => !hiddenPostIds.Contains(p.Id)).ToList();
                System.Diagnostics.Debug.WriteLine($"After hidden filter: {filteredPosts.Count} posts remaining");

                // Filter out muted users' posts
                var mutedPhones = MuteUserService.GetMutedPhones(currentUserPhone);
                filteredPosts = mutedPhones.Count > 0
                    ? filteredPosts.Where(p =>
                        !mutedPhones.Any(m =>
                            string.Equals(m.Trim(), (p.AuthorPhone ?? "").Trim(),
                                StringComparison.OrdinalIgnoreCase))).ToList()
                    : filteredPosts;

                Debug.WriteLine($"After mute filter: {filteredPosts.Count} posts ({mutedPhones.Count} muted users)");

                // ── Filter out posts from Ghost Mode + Mood Shield users ──────────
                try
                {
                    var ghostUsers = await SupabaseService.GetAsync<User>("Users",
                        "GhostModeMoodShield=eq.true");

                    var ghostedPhones = ghostUsers
                        .Where(u => !string.Equals(u.PhoneNumber, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                        .Select(u => (u.PhoneNumber ?? "").Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    if (ghostedPhones.Count > 0)
                    {
                        int beforeCount = filteredPosts.Count;
                        filteredPosts = filteredPosts
                            .Where(p => !ghostedPhones.Contains((p.AuthorPhone ?? "").Trim()))
                            .ToList();
                        Debug.WriteLine($"Ghost filter removed {beforeCount - filteredPosts.Count} posts " +
                                        $"from {ghostedPhones.Count} ghosted users");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ghost mode filter error: {ex.Message}");
                }

                // ========== RESOLVE AUTHOR DATA FOR FEED POSTS ==========
                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();

                    var phones = filteredPosts
                        .Select(p => p.AuthorPhone ?? string.Empty)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // DECLARE ALL DICTIONARIES
                    var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var profileImageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var locationMap = new Dictionary<string, (string Country, string State)>(StringComparer.OrdinalIgnoreCase);
                    var lookingForMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var moodMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var verifiedMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    // ── ADD THIS: Dictionary to store HidePhoneNumber setting ──
                    var hidePhoneMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                    foreach (var phone in phones)
                    {
                        try
                        {
                            // FIXED: Use Supabase instead of SQLite
                            var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                                $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                            var user = users.FirstOrDefault();

                            if (user != null)
                            {
                                nameMap[phone] = string.IsNullOrWhiteSpace(user.Name) ? phone : user.Name;
                                profileImageMap[phone] = user.ProfileImagePath ?? string.Empty;
                                locationMap[phone] = (Country: user.Country ?? string.Empty,
                                                          State: user.State ?? string.Empty);
                                lookingForMap[phone] = user.Mood ?? string.Empty;
                                moodMap[phone] = user.Mood ?? string.Empty;
                                verifiedMap[phone] = user.IsVerified;
                                // ── ADD THIS: Store HidePhoneNumber ──
                                hidePhoneMap[phone] = user.HidePhoneNumber;

                                // Debug log to verify
                                Debug.WriteLine($"User {phone}: IsVerified = {user.IsVerified}, HidePhoneNumber = {user.HidePhoneNumber}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading user data for {phone}: {ex}");
                        }
                    }

                    foreach (var p in filteredPosts)
                    {
                        var rawPhone = p.AuthorPhone ?? string.Empty;
                        if (!string.IsNullOrEmpty(rawPhone))
                        {
                            // Clean phone number for dictionary lookup
                            string cleanPhone = rawPhone;
                            if (cleanPhone.Contains("·"))
                            {
                                var parts = cleanPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                                cleanPhone = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                            }
                            cleanPhone = cleanPhone.Trim();

                            if (nameMap.TryGetValue(cleanPhone, out var resolvedName))
                                p.AuthorDisplayName = resolvedName;

                            if (profileImageMap.TryGetValue(cleanPhone, out var profileImage))
                                p.AuthorProfileImagePath = profileImage;

                            if (locationMap.TryGetValue(cleanPhone, out var location))
                            {
                                p.Country = location.Country;
                                p.State = location.State;
                            }

                            if (lookingForMap.TryGetValue(cleanPhone, out var lookingFor))
                                p.AuthorLookingFor = lookingFor;

                            if (moodMap.TryGetValue(cleanPhone, out var authorMood))
                                p.AuthorMood = authorMood;

                            if (verifiedMap.TryGetValue(cleanPhone, out var isVerified))
                                p.IsAuthorVerified = isVerified;

                            // ── ADD THIS BLOCK: Hide Phone Number if toggle is on ──
                            // Show only name, no phone suffix when HidePhoneNumber is true
                            if (hidePhoneMap.TryGetValue(cleanPhone, out var hidePhone) && hidePhone)
                            {
                                // Replace AuthorPhone with just the display name (no phone number)
                                p.AuthorPhone = resolvedName ?? cleanPhone;
                                Debug.WriteLine($"Post {p.Id}: HidePhoneNumber=true for {cleanPhone}, showing name only");
                            }
                            else
                            {
                                // Keep the original format (Name · PhoneNumber)
                                p.AuthorPhone = !string.IsNullOrEmpty(resolvedName) && resolvedName != cleanPhone
                                    ? $"{resolvedName} · {cleanPhone}"
                                    : cleanPhone;
                            }

                            // Debug log to verify
                            Debug.WriteLine($"Post {p.Id} - Author: {p.AuthorDisplayName}, IsVerified: {p.IsAuthorVerified}, HidePhone: {hidePhone}");
                        }

                        if (!string.IsNullOrEmpty(currentUserPhone))
                        {
                            p.IsLovedByCurrentUser = p.LovedBy.Contains(currentUserPhone);
                            p.IsSparkedByCurrentUser = p.SparkedBy.Contains(currentUserPhone);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error resolving user data: {ex}");
                }

                // ── FILTER BY MOOD VISIBILITY ─────────────────────────────────────
                var moodFilteredPosts = new List<Lock.Models.Post>();

                foreach (var post in filteredPosts)
                {
                    if (post.Visibility == "By Mood")
                    {
                        var authorMood = post.AuthorMood;

                        if (string.IsNullOrEmpty(currentUserMood))
                        {
                            Debug.WriteLine($"Skipping post {post.Id} - current user has no mood, but post requires mood match");
                            continue;
                        }

                        if (string.Equals(authorMood, currentUserMood, StringComparison.OrdinalIgnoreCase))
                        {
                            moodFilteredPosts.Add(post);
                            Debug.WriteLine($"Added post {post.Id} - mood match: {authorMood} == {currentUserMood}");
                        }
                        else
                        {
                            Debug.WriteLine($"Skipping post {post.Id} - mood mismatch: {authorMood} != {currentUserMood}");
                        }
                    }
                    else
                    {
                        moodFilteredPosts.Add(post);
                    }
                }

                Debug.WriteLine($"Mood filter: {filteredPosts.Count} -> {moodFilteredPosts.Count} posts");
                filteredPosts = moodFilteredPosts;

                // ── FILTER BY DATING INTEREST (hide posts from users who don't match current user's interest) ──
                try
                {
                    var currentUserPhoneForInterest = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                    if (!string.IsNullOrEmpty(currentUserPhoneForInterest))
                    {
                        // FIXED: Use Supabase instead of SQLite
                        var currentUsers = await SupabaseService.GetAsync<User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhoneForInterest)}&limit=1");
                        var currentUser = currentUsers.FirstOrDefault();

                        if (currentUser != null && !string.IsNullOrEmpty(currentUser.Interest) && currentUser.Interest != "Everyone")
                        {
                            filteredPosts = await FilterPostsByInterestAsync(filteredPosts);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Interest filter error: {ex}");
                }

                // ── Prepare display content ───────────────────────────────────────
                foreach (var p in filteredPosts)
                {
                    p.IsExpanded = false;
                    p.UpdateDisplayContent(200);
                }

                // ── LOAD COMMENT COUNTS BEFORE SETTING ItemsSource ────────────────
                try
                {
                    foreach (var p in filteredPosts)
                    {
                        int count = await Lock.Data.Post.CommentRepository
                                                            .GetCommentCountForPostAsync(p.Id);
                        p.CommentCount = count;
                    }
                    System.Diagnostics.Debug.WriteLine("Comment counts loaded for all posts");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading comment counts: {ex}");
                }

                // ── Set IsCurrentUserPost ─────────────────────────────────────────
                string cleanCurrentUserPhone = currentUserPhone;
                if (cleanCurrentUserPhone.Contains("·"))
                {
                    var parts = cleanCurrentUserPhone.Split(new[] { '·' },
                        StringSplitOptions.RemoveEmptyEntries);
                    cleanCurrentUserPhone = parts.Length > 1
                        ? parts[1].Trim()
                        : cleanCurrentUserPhone;
                }
                cleanCurrentUserPhone = cleanCurrentUserPhone.Trim();

                foreach (var p in filteredPosts)
                {
                    var postAuthorPhone = p.AuthorPhone ?? string.Empty;
                    if (postAuthorPhone.Contains("·"))
                    {
                        var parts = postAuthorPhone.Split(new[] { '·' },
                            StringSplitOptions.RemoveEmptyEntries);
                        postAuthorPhone = parts.Length > 1 ? parts[1].Trim() : postAuthorPhone;
                    }
                    postAuthorPhone = postAuthorPhone.Trim();

                    p.IsCurrentUserPost = string.Equals(postAuthorPhone, cleanCurrentUserPhone,
                        StringComparison.OrdinalIgnoreCase);
                }

                // ========== CALCULATE MATCH PERCENTAGES ==========
                await CalculateMatchPercentagesForPostsAsync(filteredPosts);

                // ── Assign list and update cache ──
                _allFeedPosts = filteredPosts.ToList();

                // ========== LOAD LINK PREVIEWS IN BACKGROUND ==========
                _ = Task.Run(() => LoadLinkPreviewsAsync(_allFeedPosts));

                _cachedFeedPosts = _allFeedPosts;
                _cachedStatusPosts = _allStatusPosts.ToList();
                _lastCacheTime = DateTime.UtcNow;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PostsCollectionView.ItemsSource = _allFeedPosts;
                });

                // Wait for images to start loading before hiding skeleton
                await Task.Delay(800);
                await HideSkeletonLoadingAsync();

                System.Diagnostics.Debug.WriteLine($"=== LoadPostsAsync completed: {_allFeedPosts.Count} feed posts, " +
                                                    $"{_allStatusPosts.Count} status posts ===");
            }
            catch (Exception ex)
            {
                await HideSkeletonLoadingAsync();
                await DisplayAlert("Error", "Failed to load posts: " + ex.Message, "OK");
                System.Diagnostics.Debug.WriteLine($"Error in LoadPostsAsync: {ex}");
            }
        }

        private async Task LoadLinkPreviewsAsync(List<Lock.Models.Post> posts)
        {
            if (posts == null) return;

            // Only process posts that actually have a URL
            var postsWithLinks = posts.Where(p => p.HasLinkPreview).ToList();
            Debug.WriteLine($"Loading link previews for {postsWithLinks.Count} posts");

            // Load them concurrently but throttled (max 4 at once)
            var semaphore = new SemaphoreSlim(4);
            var tasks = postsWithLinks.Select(async post =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var url = post.FirstUrl;
                    if (string.IsNullOrEmpty(url)) return;

                    var preview = await LinkPreviewService.FetchAsync(url);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        post.LinkPreview = preview;
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Preview load failed for post {post.Id}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
        private async void OnPostAuthorVerificationTapped(object sender, TappedEventArgs e)
        {
            try
            {
                Lock.Models.Post? post = null;

                if (e.Parameter is Lock.Models.Post paramPost)
                {
                    post = paramPost;
                }
                else if (sender is VisualElement ve && ve.BindingContext is Lock.Models.Post bindingPost)
                {
                    post = bindingPost;
                }

                if (post == null) return;

                string phone = post.AuthorPhone ?? string.Empty;

                if (phone.Contains("·"))
                {
                    var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                    phone = parts.Length > 1 ? parts[1].Trim() : phone;
                }

                phone = phone.Trim();
                if (string.IsNullOrWhiteSpace(phone)) return;

                // FIXED: Use Supabase instead of SQLite
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    if (user.IsVerified)
                    {
                        await DisplayAlert(
                            "Verified Account",
                            $"✓ {user.Name} is a verified user.\n\n" +
                            $"Verified on: {(user.VerifiedAt.HasValue ? user.VerifiedAt.Value.ToString("MMMM dd, yyyy") : "Unknown")}\n" +
                            $"Verification Score: {user.VerificationScore:F1}%\n\n" +
                            "Verified users have completed ID verification for added trust and safety.",
                            "OK");
                    }
                    else
                    {
                        await DisplayAlert(
                            "Not Verified",
                            $"{user.Name} is not yet verified.\n\n" +
                            "Verification helps build trust in the community.",
                            "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPostAuthorVerificationTapped error: {ex}");
            }
        }


        private async Task LoadPostsFreshAsync()
        {
            try
            {
                var posts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;
                currentUserPhone = currentUserPhone.Trim();

                // Separate status posts from regular posts
                var allRegularPosts = posts.Where(p => string.IsNullOrEmpty(p.StatusImagePath)).ToList();
                var allStatusPostsRaw = posts.Where(p => !string.IsNullOrEmpty(p.StatusImagePath)).ToList();

                // Process status posts for top bar
                await ProcessStatusPostsForTopBarAsync(allStatusPostsRaw, currentUserPhone);

                // Filter and process regular posts
                var filteredPosts = await FilterAndProcessPosts(allRegularPosts, currentUserPhone);

                // Update cache
                _cachedFeedPosts = filteredPosts.ToList();
                _cachedStatusPosts = _allStatusPosts.ToList();
                _lastCacheTime = DateTime.UtcNow;

                _allFeedPosts = _cachedFeedPosts;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    PostsCollectionView.ItemsSource = _allFeedPosts;

                    // Hide skeleton
                    if (SkeletonCollectionView != null)
                        SkeletonCollectionView.IsVisible = false;
                    if (PostsCollectionView != null)
                        PostsCollectionView.IsVisible = true;
                });

                System.Diagnostics.Debug.WriteLine($"Loaded fresh: {_allFeedPosts.Count} feed posts, {_allStatusPosts.Count} status posts");
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                await HideSkeletonLoadingAsync();
            }
        }

        // Add this overloaded method right after your existing LoadPostsAsync method
        private async Task LoadPostsAsync()
        {
            await LoadPostsAsync(forceRefresh: false);
        }

        private async Task RefreshPostsInBackground()
        {
            if (_isRefreshingInBackground) return;

            _isRefreshingInBackground = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("Background refresh started");

                var posts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;
                currentUserPhone = currentUserPhone.Trim();

                var allRegularPosts = posts.Where(p => string.IsNullOrEmpty(p.StatusImagePath)).ToList();
                var allStatusPostsRaw = posts.Where(p => !string.IsNullOrEmpty(p.StatusImagePath)).ToList();

                await ProcessStatusPostsForTopBarAsync(allStatusPostsRaw, currentUserPhone);
                var filteredPosts = await FilterAndProcessPosts(allRegularPosts, currentUserPhone);

                // Check if data has changed
                bool hasChanges = _cachedFeedPosts.Count != filteredPosts.Count ||
                                  _cachedStatusPosts.Count != _allStatusPosts.Count;

                if (hasChanges)
                {
                    System.Diagnostics.Debug.WriteLine("Background refresh found changes - updating UI");

                    _cachedFeedPosts = filteredPosts.ToList();
                    _cachedStatusPosts = _allStatusPosts.ToList();
                    _lastCacheTime = DateTime.UtcNow;
                    _allFeedPosts = _cachedFeedPosts;

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        PostsCollectionView.ItemsSource = _allFeedPosts;

                        // Update status ring if needed
                        var statusCount = _allStatusPosts.GroupBy(p => p.AuthorPhone).Count();
                        UpdateStatusRing(statusCount);
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Background refresh - no changes found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Background refresh error: {ex}");
            }
            finally
            {
                _isRefreshingInBackground = false;
            }
        }

        private async Task<List<Lock.Models.Post>> FilterAndProcessPosts(List<Lock.Models.Post> posts, string currentUserPhone)
        {
            var filteredPosts = posts.ToList();

            // Get current user's mood
            var currentUserMood = await GetCurrentUserMoodAsync();

            // Get hidden post IDs and filter them out
            var hiddenPostIds = await HidePostService.GetHiddenPostIdsAsync(currentUserPhone);
            filteredPosts = filteredPosts.Where(p => !hiddenPostIds.Contains(p.Id)).ToList();

            // Filter out muted users' posts
            var mutedPhones = MuteUserService.GetMutedPhones(currentUserPhone);
            filteredPosts = mutedPhones.Count > 0
                ? filteredPosts.Where(p => !mutedPhones.Any(m => string.Equals(m.Trim(), (p.AuthorPhone ?? "").Trim(), StringComparison.OrdinalIgnoreCase))).ToList()
                : filteredPosts;

            // Ghost mode filter - FIXED: Use Supabase
            try
            {
                var ghostUsers = await SupabaseService.GetAsync<User>("Users",
                    "GhostModeMoodShield=eq.true");

                var ghostedPhones = ghostUsers
                    .Where(u => !string.Equals(u.PhoneNumber, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                    .Select(u => (u.PhoneNumber ?? "").Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (ghostedPhones.Count > 0)
                {
                    filteredPosts = filteredPosts.Where(p => !ghostedPhones.Contains((p.AuthorPhone ?? "").Trim())).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ghost mode filter error: {ex.Message}");
            }

            // Resolve author data
            await ResolveAuthorData(filteredPosts, currentUserPhone);

            // Mood visibility filter
            var moodFilteredPosts = new List<Lock.Models.Post>();
            foreach (var post in filteredPosts)
            {
                if (post.Visibility == "By Mood")
                {
                    if (string.IsNullOrEmpty(currentUserMood)) continue;
                    if (string.Equals(post.AuthorMood, currentUserMood, StringComparison.OrdinalIgnoreCase))
                        moodFilteredPosts.Add(post);
                }
                else
                {
                    moodFilteredPosts.Add(post);
                }
            }
            filteredPosts = moodFilteredPosts;

            // Dating interest filter - FIXED: Use Supabase
            try
            {
                var currentUserPhoneForInterest = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                if (!string.IsNullOrEmpty(currentUserPhoneForInterest))
                {
                    var currentUsers = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhoneForInterest)}&limit=1");
                    var currentUser = currentUsers.FirstOrDefault();

                    if (currentUser != null && !string.IsNullOrEmpty(currentUser.Interest) && currentUser.Interest != "Everyone")
                    {
                        filteredPosts = await FilterPostsByInterestAsync(filteredPosts);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Interest filter error: {ex}");
            }

            // Prepare display content
            foreach (var p in filteredPosts)
            {
                p.IsExpanded = false;
                p.UpdateDisplayContent(200);
            }

            // Load comment counts
            foreach (var p in filteredPosts)
            {
                p.CommentCount = await Lock.Data.Post.CommentRepository.GetCommentCountForPostAsync(p.Id);
            }

            // Set IsCurrentUserPost
            string cleanCurrentUserPhone = currentUserPhone;
            if (cleanCurrentUserPhone.Contains("·"))
            {
                var parts = cleanCurrentUserPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                cleanCurrentUserPhone = parts.Length > 1 ? parts[1].Trim() : cleanCurrentUserPhone;
            }
            cleanCurrentUserPhone = cleanCurrentUserPhone.Trim();

            foreach (var p in filteredPosts)
            {
                var postAuthorPhone = p.AuthorPhone ?? string.Empty;
                if (postAuthorPhone.Contains("·"))
                {
                    var parts = postAuthorPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                    postAuthorPhone = parts.Length > 1 ? parts[1].Trim() : postAuthorPhone;
                }
                postAuthorPhone = postAuthorPhone.Trim();
                p.IsCurrentUserPost = string.Equals(postAuthorPhone, cleanCurrentUserPhone, StringComparison.OrdinalIgnoreCase);
            }

            // Calculate match percentages
            await CalculateMatchPercentagesForPostsAsync(filteredPosts);

            return filteredPosts;
        }


        private async Task ResolveAuthorData(List<Lock.Models.Post> posts, string currentUserPhone)
        {
            try
            {
                var phones = posts.Select(p =>
                {
                    var phone = p.AuthorPhone ?? string.Empty;
                    if (phone.Contains("·"))
                    {
                        var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                        phone = parts.Length > 1 ? parts[1].Trim() : phone;
                    }
                    return phone.Trim();
                })
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

                var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var profileImageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var moodMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var verifiedMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                var hidePhoneMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                foreach (var phone in phones)
                {
                    try
                    {
                        // FIXED: Get user from Supabase
                        var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                        var user = users.FirstOrDefault();

                        if (user != null)
                        {
                            nameMap[phone] = string.IsNullOrWhiteSpace(user.Name) ? phone : user.Name;
                            profileImageMap[phone] = user.ProfileImagePath ?? string.Empty;
                            moodMap[phone] = user.Mood ?? string.Empty;
                            verifiedMap[phone] = user.IsVerified;
                            hidePhoneMap[phone] = user.HidePhoneNumber;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading user {phone}: {ex.Message}");
                    }
                }

                foreach (var p in posts)
                {
                    var rawPhone = p.AuthorPhone ?? string.Empty;
                    var cleanPhone = rawPhone;

                    if (cleanPhone.Contains("·"))
                    {
                        var parts = cleanPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                        cleanPhone = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
                    }
                    cleanPhone = cleanPhone.Trim();

                    if (!string.IsNullOrEmpty(cleanPhone))
                    {
                        if (nameMap.TryGetValue(cleanPhone, out var resolvedName))
                            p.AuthorDisplayName = resolvedName;
                        if (profileImageMap.TryGetValue(cleanPhone, out var profileImage))
                            p.AuthorProfileImagePath = profileImage;
                        if (moodMap.TryGetValue(cleanPhone, out var authorMood))
                            p.AuthorMood = authorMood;
                        if (verifiedMap.TryGetValue(cleanPhone, out var isVerified))
                            p.IsAuthorVerified = isVerified;

                        if (hidePhoneMap.TryGetValue(cleanPhone, out var hidePhone) && hidePhone)
                        {
                            p.AuthorPhone = resolvedName ?? cleanPhone;
                        }
                    }

                    if (!string.IsNullOrEmpty(currentUserPhone))
                        p.IsLovedByCurrentUser = p.LovedBy.Contains(currentUserPhone);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving user data: {ex}");
            }
        }


        public static void ClearPostCache()
        {
            _cachedFeedPosts = null;
            _cachedStatusPosts = null;
            _lastCacheTime = DateTime.MinValue;
            Debug.WriteLine("Post cache cleared");
        }

        // Add this field with your other fields (around line 68)
        private bool _isLoadingPosts = false;

        // Add these methods after your LoadPostsAsync method

        private async Task ShowSkeletonLoadingAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isLoadingPosts = true;

                if (SkeletonCollectionView != null)
                {
                    SkeletonCollectionView.IsVisible = true;
                    SkeletonCollectionView.InputTransparent = true;
                }

                if (PostsCollectionView != null)
                {
                    PostsCollectionView.IsVisible = false;
                }
            });
        }

        private async Task HideSkeletonLoadingAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _isLoadingPosts = false;

                if (SkeletonCollectionView != null)
                {
                    SkeletonCollectionView.IsVisible = false;
                }

                if (PostsCollectionView != null)
                {
                    PostsCollectionView.IsVisible = true;
                }
            });
        }

        // Add this helper method to configure skeleton count
        private void ConfigureSkeletonCount()
        {
            try
            {
                // Create 5 skeleton items (enough to fill the screen)
                var skeletonItems = new List<int> { 1, 2, 3, 4, 5 };

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (SkeletonCollectionView != null)
                    {
                        SkeletonCollectionView.ItemsSource = skeletonItems;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error configuring skeleton count: {ex}");
            }
        }

        // Helper method to get expiration hours
        private int GetExpirationHours(string duration)
        {
            return duration switch
            {
                "24 hours" => 24,
                "48 hours" => 48,
                "7 days" => 168,
                "Never expire" => int.MaxValue,
                _ => 24
            };
        }

        // Add this new method to create the Add Story button programmatically
        private View CreateAddStoryButton()
        {
            var addButton = new VerticalStackLayout
            {
                Spacing = 2,
                WidthRequest = 54,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += AddImageFloatingButton_Clicked;
            addButton.GestureRecognizers.Add(tapGesture);

            var grid = new Grid
            {
                WidthRequest = 46,
                HeightRequest = 46,
                HorizontalOptions = LayoutOptions.Center
            };

            // Gradient ring
            var ring = new Ellipse
            {
                WidthRequest = 46,
                HeightRequest = 46,
                StrokeThickness = 2
            };
            ring.Stroke = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = {
            new GradientStop { Color = Color.FromArgb("#FF3B6F"), Offset = 0 },
            new GradientStop { Color = Color.FromArgb("#00B5B5"), Offset = 1 }
        }
            };
            grid.Children.Add(ring);

            // Dark inner circle
            var innerCircle = new Ellipse
            {
                WidthRequest = 38,
                HeightRequest = 38,
                Fill = Color.FromArgb("#1C1C25"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            grid.Children.Add(innerCircle);

            // Pink plus sign - USING FRAME INSTEAD OF BORDER
            var plusFrame = new Frame
            {
                WidthRequest = 20,
                HeightRequest = 20,
                BackgroundColor = Color.FromArgb("#FF3B6F"),
                CornerRadius = 10,
                Padding = 0,
                IsClippedToBounds = true,
                HasShadow = false,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = "+",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            grid.Children.Add(plusFrame);

            addButton.Children.Add(grid);
            addButton.Children.Add(new Label
            {
                Text = "Add",
                FontSize = 8,
                TextColor = Color.FromArgb("#7A7A8C"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            });

            return addButton;
        }
        // Replace your existing ProcessStatusPostsForTopBarAsync with this:
        private async Task ProcessStatusPostsForTopBarAsync(List<Lock.Models.Post> statusPosts, string currentUserPhone)
        {
            try
            {
                var validStatusPosts = new List<Lock.Models.Post>();

                foreach (var status in statusPosts)
                {
                    if (string.IsNullOrEmpty(status.AuthorPhone)) continue;

                    var userDuration = Preferences.Get($"status_duration_{status.AuthorPhone}", "24 hours");
                    var expirationHours = GetExpirationHours(userDuration);
                    var age = DateTime.UtcNow - status.CreatedAt;

                    if (age.TotalHours < expirationHours)
                    {
                        validStatusPosts.Add(status);
                    }
                    else
                    {
                        try
                        {
                            // FIXED: Delete from Supabase
                            await SupabaseService.DeleteAsync("Posts", $"Id=eq.{status.Id}");

                            if (!string.IsNullOrEmpty(status.StatusImagePath) &&
                                System.IO.File.Exists(status.StatusImagePath))
                            {
                                System.IO.File.Delete(status.StatusImagePath);
                            }
                            Debug.WriteLine($"Deleted expired status for user {status.AuthorPhone} " +
                                            $"(age: {age.TotalHours} hours)");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error deleting expired status: {ex}");
                        }
                    }
                }

                _allStatusPosts = validStatusPosts.ToList();

                // Resolve author names for status posts
                try
                {
                    foreach (var sp in _allStatusPosts)
                    {
                        if (string.IsNullOrEmpty(sp.AuthorPhone)) continue;
                        try
                        {
                            // FIXED: Get user from Supabase
                            var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                                $"PhoneNumber=eq.{Uri.EscapeDataString(sp.AuthorPhone)}&limit=1");
                            var user = users.FirstOrDefault();

                            if (user != null)
                            {
                                sp.AuthorDisplayName = string.IsNullOrWhiteSpace(user.Name)
                                    ? sp.AuthorPhone
                                    : user.Name;
                                sp.AuthorProfileImagePath = user.ProfileImagePath ?? string.Empty;
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                var usersWithStatus = _allStatusPosts
                    .GroupBy(p => p.AuthorPhone ?? string.Empty)
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .Select(g => (
                        UserPhone: g.Key,
                        StatusPosts: g.OrderByDescending(p => p.CreatedAt).ToList()
                    ))
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var statusScrollView = this.FindByName<ScrollView>("StatusImagesScrollView");
                    var statusLayout = this.FindByName<HorizontalStackLayout>("StatusImagesLayout");
                    var menuButton = this.FindByName<Grid>("StatusMenuButton");

                    if (statusLayout != null && statusScrollView != null)
                    {
                        statusLayout.Children.Clear();

                        var addStoryButton = CreateAddStoryButton();
                        statusLayout.Children.Add(addStoryButton);

                        foreach (var userStatus in usersWithStatus)
                        {
                            var userPhone = userStatus.UserPhone;
                            var userStatusPosts = userStatus.StatusPosts;
                            var latestStatus = userStatusPosts.First();

                            var statusView = CreateStatusImageView(latestStatus, userPhone, userStatusPosts);
                            statusLayout.Children.Add(statusView);
                        }

                        statusScrollView.IsVisible = true;
                        statusLayout.IsVisible = true;

                        if (menuButton != null)
                            menuButton.IsVisible = true;
                    }

                    var originalTopImageLayout = this.FindByName<HorizontalStackLayout>("OriginalTopImageLayout");
                    if (originalTopImageLayout != null)
                        originalTopImageLayout.IsVisible = false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing status posts for top bar: {ex}");
            }
        }
        private async Task<List<Lock.Models.Post>> GetNonExpiredStatuses(string userPhone, bool deleteExpired = true)
        {
            try
            {
                var duration = Preferences.Get($"status_duration_{userPhone}", "24 hours");
                var expirationHours = GetExpirationHours(duration);

                // FIXED: Use Supabase instead of SQLite
                var allStatuses = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(userPhone)}&StatusImagePath=not.is.null&order=CreatedAt.desc");

                var now = DateTime.UtcNow;
                var validStatuses = new List<Lock.Models.Post>();

                foreach (var status in allStatuses)
                {
                    var age = now - status.CreatedAt;
                    if (age.TotalHours < expirationHours)
                    {
                        validStatuses.Add(status);
                    }
                    else if (deleteExpired)
                    {
                        // Delete expired status
                        await SupabaseService.DeleteAsync("Posts", $"Id=eq.{status.Id}");

                        // Delete the actual image file if it exists
                        if (!string.IsNullOrEmpty(status.StatusImagePath) && System.IO.File.Exists(status.StatusImagePath))
                        {
                            try
                            {
                                System.IO.File.Delete(status.StatusImagePath);
                            }
                            catch { }
                        }
                        Debug.WriteLine($"Deleted expired status for user {userPhone} (age: {age.TotalHours} hours)");
                    }
                }

                return validStatuses;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting non-expired statuses: {ex}");
                return new List<Lock.Models.Post>();
            }
        }
        private async void OnHiddenPostsButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new HiddenPostsPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to hidden posts: {ex}");
                await DisplayAlert("Error", "Could not open hidden posts", "OK");
            }
        }

        private View CreateStatusImageView(
            Lock.Models.Post statusPost,
            string authorPhone,
            List<Lock.Models.Post> allUserStatusPosts)
        {
            try
            {
                const double outerSize = 54.0;
                const double imageSize = 46.0;

                // Outer container: circle + name label stacked vertically
                var outerStack = new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    WidthRequest = 64
                };

                // ── Circle grid (ring + image) ──
                var grid = new Grid
                {
                    HeightRequest = outerSize,
                    WidthRequest = outerSize,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                // Profile image
                var image = new Image
                {
                    Aspect = Aspect.AspectFill,
                    HeightRequest = imageSize,
                    WidthRequest = imageSize,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                if (!string.IsNullOrEmpty(statusPost.StatusImagePath) && File.Exists(statusPost.StatusImagePath))
                    image.Source = ImageSource.FromFile(statusPost.StatusImagePath);
                else
                    image.BackgroundColor = Color.FromArgb("#333333");

                // Clip image to circle
                image.Clip = new EllipseGeometry
                {
                    Center = new Point(imageSize / 2, imageSize / 2),
                    RadiusX = imageSize / 2,
                    RadiusY = imageSize / 2
                };

                grid.Children.Add(image);

                // Ring overlay
                int imageCount = allUserStatusPosts?.Count ?? 1;
                var ringDrawable = new StatusRingDrawable(
                    imageCount,
                    Color.FromArgb("#008080"),
                    stroke: imageCount > 1 ? 2.0f : 1.5f);

                var ringView = new GraphicsView
                {
                    HeightRequest = outerSize,
                    WidthRequest = outerSize,
                    Drawable = ringDrawable,
                    BackgroundColor = Colors.Transparent,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    InputTransparent = true
                };
                grid.Children.Add(ringView);
                ringView.Invalidate();

                // ── Name label ──
                // Resolve display name: prefer AuthorDisplayName, fallback to phone
                string displayName = statusPost.AuthorDisplayName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = authorPhone ?? string.Empty;

                // If it's a phone number, trim to first 6 chars so it fits
                if (displayName.Length > 8)
                    displayName = displayName.Substring(0, 8);

                var nameLabel = new Label
                {
                    Text = displayName,
                    FontSize = 9,
                    TextColor = Color.FromArgb("#BBBBBB"),
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1,
                    WidthRequest = 64
                };

                outerStack.Children.Add(grid);
                outerStack.Children.Add(nameLabel);

                // ── Tap gesture on the whole stack ──
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += async (s, e) =>
                {
                    try
                    {
                        var allUsersStatuses = _allStatusPosts?
                            .Where(p => !string.IsNullOrEmpty(p.StatusImagePath) && !string.IsNullOrEmpty(p.AuthorPhone))
                            .GroupBy(p => p.AuthorPhone)
                            .Select(g => (
                                UserPhone: g.Key,
                                ImagePaths: g.OrderByDescending(p => p.CreatedAt)
                                             .Where(p => !string.IsNullOrEmpty(p.StatusImagePath))
                                             .Select(p => p.StatusImagePath!)
                                             .ToList(),
                                UserName: g.First().AuthorDisplayName ?? g.Key ?? string.Empty,
                                Moods: g.OrderByDescending(p => p.CreatedAt)
                                        .Where(p => !string.IsNullOrEmpty(p.StatusImagePath))
                                        .Select(p => p.Mood ?? string.Empty)
                                        .ToList()
                            ))
                            .Where(u => u.ImagePaths.Any())
                            .ToList();

                        if (allUsersStatuses == null || !allUsersStatuses.Any())
                        {
                            await Application.Current.MainPage.DisplayAlert("Info", "No status images available", "OK");
                            return;
                        }

                        int userIndex = allUsersStatuses.FindIndex(u =>
                            string.Equals(u.UserPhone, authorPhone, StringComparison.OrdinalIgnoreCase));
                        if (userIndex < 0) userIndex = 0;

                        int imageIndex = allUserStatusPosts?.FindIndex(p =>
                            string.Equals(p.StatusImagePath, statusPost.StatusImagePath, StringComparison.OrdinalIgnoreCase)) ?? 0;
                        if (imageIndex < 0) imageIndex = 0;

                        var viewerPage = new StatusViewerPage(
                            allUsersStatuses.Select(u => (u.UserPhone, u.ImagePaths, u.UserName, u.Moods)).ToList(),
                            userIndex,
                            imageIndex);

                        await Navigation.PushModalAsync(viewerPage);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in status tap: {ex}");
                        await Application.Current.MainPage.DisplayAlert("Error", $"Could not open status viewer: {ex.Message}", "OK");
                    }
                };

                outerStack.GestureRecognizers.Add(tapGesture);
                return outerStack;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating status image view: {ex}");
                return new BoxView { HeightRequest = 54, WidthRequest = 54, Color = Colors.Gray };
            }
        }

        private async void LinkPreview_Tapped(object sender, TappedEventArgs e)
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

        private async Task HandleStatusTap(string authorPhone, Lock.Models.Post statusPost, List<Lock.Models.Post> allUserStatusPosts)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Status tapped for user: {authorPhone} ===");

                // Build the list of all users with their status images AND moods
                var allUsersStatuses = _allFeedPosts?
                    .Where(p => !string.IsNullOrEmpty(p.StatusImagePath) && !string.IsNullOrEmpty(p.AuthorPhone))
                    .GroupBy(p => p.AuthorPhone)
                    .Select(g => (
                        UserPhone: g.Key,
                        ImagePaths: g.OrderByDescending(p => p.CreatedAt)
                                     .Where(p => !string.IsNullOrEmpty(p.StatusImagePath))
                                     .Select(p => p.StatusImagePath!)
                                     .ToList(),
                        UserName: GetAuthorDisplayName(g.Key),
                        Moods: g.OrderByDescending(p => p.CreatedAt)
                                .Where(p => !string.IsNullOrEmpty(p.StatusImagePath))
                                .Select(p => p.Mood ?? "")
                                .ToList()
                    ))
                    .Where(u => u.ImagePaths.Any())
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Found {allUsersStatuses?.Count ?? 0} users with status images");

                if (allUsersStatuses == null || !allUsersStatuses.Any())
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: No users with status images found");
                    await Application.Current.MainPage.DisplayAlert("Info", "No status images available", "OK");
                    return;
                }

                // Find the current user index
                int userIndex = allUsersStatuses.FindIndex(u => u.UserPhone == authorPhone);
                if (userIndex < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"User {authorPhone} not found, defaulting to index 0");
                    userIndex = 0;
                }

                // Find the current image index
                int imageIndex = allUserStatusPosts?.FindIndex(p =>
                    p.StatusImagePath == statusPost.StatusImagePath) ?? 0;

                if (imageIndex < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Image not found, defaulting to 0");
                    imageIndex = 0;
                }

                System.Diagnostics.Debug.WriteLine($"Opening StatusViewerPage at user {userIndex}, image {imageIndex}");

                // Create and show the StatusViewerPage
                var viewerPage = new StatusViewerPage(allUsersStatuses, userIndex, imageIndex);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        await Navigation.PushModalAsync(viewerPage);
                        System.Diagnostics.Debug.WriteLine("StatusViewerPage opened successfully");
                    }
                    catch (Exception navEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Navigation error: {navEx}");
                        await Application.Current.MainPage.DisplayAlert("Error", $"Navigation failed: {navEx.Message}", "OK");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== Error in status tap handler ===");
                System.Diagnostics.Debug.WriteLine($"Error message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await Application.Current.MainPage.DisplayAlert("Error",
                    $"Could not open status viewer: {ex.Message}", "OK");
            }
        }
        private void UpdateInfoLabel(Label label, (string Path, string Mood, DateTime CreatedAt) item, Func<DateTime, string> getRelativeTimeFunc)
        {
            var mood = string.IsNullOrWhiteSpace(item.Mood) ? "" : $"Mood: {item.Mood}";
            var time = getRelativeTimeFunc(item.CreatedAt);
            label.Text = $"{mood} • {time}".TrimStart(' ', '•');
        }

        private async Task LoadImageAsync(Image image, string path)
        {
            try
            {
                if (File.Exists(path))
                    image.Source = ImageSource.FromFile(path);
                else if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
                    image.Source = ImageSource.FromUri(new Uri(path));
            }
            catch { }
        }

        private void UpdateInfoLabel(Label label, (string Path, string Mood, DateTime CreatedAt) item)
        {
            var mood = string.IsNullOrWhiteSpace(item.Mood) ? "" : $"Mood: {item.Mood}";
            var time = GetRelativeTimeString(item.CreatedAt);
            label.Text = $"{mood} • {time}".TrimStart(' ', '•');
        }

        private string GetAuthorDisplayName(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return "Unknown";

            var post = _allFeedPosts?.FirstOrDefault(p => p.AuthorPhone == phone);
            if (post != null && !string.IsNullOrEmpty(post.AuthorDisplayName))
            {
                return post.AuthorDisplayName;
            }

            return phone;
        }

        private Color GetMoodColor(string mood)
        {
            return mood?.ToLower() switch
            {
                "happy" => Color.FromArgb("#FFD700"),
                "sad" => Color.FromArgb("#4169E1"),
                "excited" => Color.FromArgb("#FF4500"),
                "angry" => Color.FromArgb("#DC143C"),
                "neutral" => Color.FromArgb("#808080"),
                "love" => Color.FromArgb("#FF69B4"),
                _ => Color.FromArgb("#008080")
            };
        }

        private string GetRelativeTimeString(DateTime dateTime)
        {
            try
            {
                var now = DateTime.UtcNow;
                var timeSpan = now - dateTime;

                if (timeSpan.TotalSeconds < 60)
                    return "just now";
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays}d ago";
                if (timeSpan.TotalDays < 30)
                    return $"{(int)(timeSpan.TotalDays / 7)}w ago";
                if (timeSpan.TotalDays < 365)
                    return $"{(int)(timeSpan.TotalDays / 30)}mo ago";

                return $"{(int)(timeSpan.TotalDays / 365)}y ago";
            }
            catch
            {
                return "unknown";
            }
        }

        private void ShowAllPosts(object sender, EventArgs e)
        {
            try
            {
                PostsCollectionView.ItemsSource = _allFeedPosts;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing all posts: {ex}");
            }
        }

        private void InvokeUpdateDisplayContent(Lock.Models.Post post, int limit)
        {
            if (post == null) return;

            var mi = post.GetType().GetMethod("UpdateDisplayContent", new[] { typeof(int) });
            if (mi != null)
            {
                mi.Invoke(post, new object[] { limit });
            }
            else
            {
                var miNoParam = post.GetType().GetMethod("UpdateDisplayContent", Type.EmptyTypes);
                miNoParam?.Invoke(post, null);
            }
        }

        private void ToggleExpand_Clicked(object? sender, EventArgs e)
        {
            Lock.Models.Post? post = null;

            if (sender is Button btn)
            {
                if (btn.CommandParameter is Lock.Models.Post cp)
                    post = cp;
                else if (btn.BindingContext is Lock.Models.Post bc)
                    post = bc;
            }
            else if (sender is VisualElement ve && ve.BindingContext is Lock.Models.Post vePost)
            {
                post = vePost;
            }

            if (post == null)
                return;

            post.IsExpanded = !post.IsExpanded;
            InvokeUpdateDisplayContent(post, 200);

            try
            {
                if (PostsCollectionView != null)
                {
                    PostsCollectionView.ScrollTo(post, position: ScrollToPosition.Start);
                }
            }
            catch
            {
                // ignore any scroll failures
            }
        }

        private void BoldButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                if (ContentEditor == null) return;

                var text = ContentEditor.Text ?? string.Empty;

                var selStart = Math.Max(0, ContentEditor.CursorPosition);
                var selLength = Math.Max(0, ContentEditor.SelectionLength);

                if (selStart > text.Length) selStart = text.Length;
                if (selStart + selLength > text.Length) selLength = text.Length - selStart;

                if (selLength > 0)
                {
                    var before = text.Substring(0, selStart);
                    var selected = text.Substring(selStart, selLength);
                    var after = text.Substring(selStart + selLength);
                    var newText = before + "**" + selected + "**" + after;
                    ContentEditor.Text = newText;

                    ContentEditor.CursorPosition = selStart + 2 + selLength + 2;
                    ContentEditor.SelectionLength = 0;
                    ContentEditor.Focus();
                }
                else
                {
                    var insert = "**bold**";
                    var before = text.Substring(0, selStart);
                    var after = text.Substring(selStart);
                    ContentEditor.Text = before + insert + after;

                    ContentEditor.CursorPosition = selStart + 2;
                    ContentEditor.SelectionLength = 4;
                    ContentEditor.Focus();
                }
            }
            catch
            {
                // swallow
            }
        }

        private void ItalicButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                if (ContentEditor == null) return;

                var text = ContentEditor.Text ?? string.Empty;

                var selStart = Math.Max(0, ContentEditor.CursorPosition);
                var selLength = Math.Max(0, ContentEditor.SelectionLength);

                if (selStart > text.Length) selStart = text.Length;
                if (selStart + selLength > text.Length) selLength = text.Length - selStart;

                if (selLength > 0)
                {
                    var before = text.Substring(0, selStart);
                    var selected = text.Substring(selStart, selLength);
                    var after = text.Substring(selStart + selLength);
                    var newText = before + "*" + selected + "*" + after;
                    ContentEditor.Text = newText;

                    ContentEditor.CursorPosition = selStart + 1 + selLength + 1;
                    ContentEditor.SelectionLength = 0;
                    ContentEditor.Focus();
                }
                else
                {
                    var insert = "*italic*";
                    var before = text.Substring(0, selStart);
                    var after = text.Substring(selStart);
                    ContentEditor.Text = before + insert + after;

                    ContentEditor.CursorPosition = selStart + 1;
                    ContentEditor.SelectionLength = 6;
                    ContentEditor.Focus();
                }
            }
            catch
            {
                // swallow
            }
        }

        private async void PickImageButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                // Request permissions first
                var hasPermission = await PermissionsHelper.RequestStoragePermissionsAsync();
                if (!hasPermission)
                {
                    await DisplayAlert("Permission Required", "Storage permission needed to select images. Please grant permission in app settings.", "OK");
                    return;
                }

                // Pick multiple images
                var results = await FilePicker.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select images for your post",
                    FileTypes = FilePickerFileType.Images
                });

                if (results == null || !results.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No images selected for post");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Selected {results.Count()} images for post");

                // Save each selected image
                foreach (var r in results)
                {
                    var destFileName = $"post_{Guid.NewGuid():N}{System.IO.Path.GetExtension(r.FileName)}";
                    var saved = await SavePickedFileAsync(r, destFileName);
                    if (!string.IsNullOrEmpty(saved))
                    {
                        _pickedImagePaths.Add(saved);
                        System.Diagnostics.Debug.WriteLine($"Saved post image to: {saved}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save image: {r.FileName}");
                    }
                }

                // Update the preview layout to show selected images
                UpdatePreviewLayout();

                // Success message REMOVED - no alert shown
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PickImageButton_Clicked: {ex}");
                await DisplayAlert("Error", "Could not pick images: " + ex.Message, "OK");
            }
        }
        private void UpdatePreviewLayout()
        {
            if (ImagePreviewLayout == null)
                return;

            ImagePreviewLayout.Children.Clear();

            if (!_pickedImagePaths.Any())
            {
                ImagePreviewLayout.IsVisible = false;
                return;
            }

            ImagePreviewLayout.IsVisible = true;

            foreach (var path in _pickedImagePaths)
            {
                var container = new Grid
                {
                    WidthRequest = 100,
                    HeightRequest = 100,
                    Margin = new Thickness(0)
                };

                var frame = new Frame
                {
                    WidthRequest = 100,
                    HeightRequest = 100,
                    CornerRadius = 10,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BorderColor = Color.FromArgb("#2A2A2A"),
                    BackgroundColor = Colors.Transparent
                };

                var image = new Image
                {
                    Source = ImageSource.FromFile(path),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = 100,
                    HeightRequest = 100,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                frame.Content = image;
                container.Add(frame);

                var removeBadge = new Border
                {
                    WidthRequest = 24,
                    HeightRequest = 24,
                    BackgroundColor = Color.FromArgb("#008080"),
                    StrokeThickness = 1.5,
                    Stroke = Color.FromArgb("#121212"),
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 6, 6, 0),
                    ZIndex = 10,
                    InputTransparent = false
                };

                var removeLabel = new Label
                {
                    Text = "✕",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    InputTransparent = true
                };

                removeBadge.Content = removeLabel;

                var currentPath = path;
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => RemovePickedImage(currentPath);
                removeBadge.GestureRecognizers.Add(tap);

                container.Add(removeBadge);

                ImagePreviewLayout.Children.Add(container);
            }
        }

        private void RemovePickedImage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var idx = _pickedImagePaths.IndexOf(path);
                if (idx >= 0)
                    _pickedImagePaths.RemoveAt(idx);

                UpdatePreviewLayout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemovePickedImage error: {ex}");
            }
        }

        private async void SparkButton_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PostModel post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // ── REMOVE spark (optimistic, no confirmation dialog) ──────────
                if (post.IsSparkedByCurrentUser)
                {
                    post.ToggleSpark(currentUserPhone);

                    var removeMsg = new SparkUpdateMessage
                    {
                        PostId = post.Id,
                        IsSparked = false,
                        SparkCount = post.SparkCount,
                        UserPhone = currentUserPhone,
                        AuthorPhone = post.AuthorPhone,
                        Timestamp = DateTime.UtcNow
                    };

                    _ = Task.Run(async () =>
                    {
                        await SparkService.RemoveSparkAsync(currentUserPhone, post.Id);
                        await SignalRService.Instance.SendSparkUpdateAsync(removeMsg);
                        MessagingCenter.Send(this, "SparkToggled", removeMsg);
                    });

                    return;
                }

                // ── RATE LIMIT CHECK ────────────────────────────────────────────
                var (canSpark, remaining, waitMinutes) = await SparkService.CanSendSparkAsync(currentUserPhone);

                if (!canSpark)
                {
                    ShowRateLimitToast(waitMinutes);
                    return;
                }

                // ── OPTIMISTIC UPDATE — instant, before any network call ────────
                post.ToggleSpark(currentUserPhone);

                var sparkMsg = new SparkUpdateMessage
                {
                    PostId = post.Id,
                    IsSparked = true,
                    SparkCount = post.SparkCount,
                    UserPhone = currentUserPhone,
                    AuthorPhone = post.AuthorPhone,
                    Timestamp = DateTime.UtcNow
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

                    await SignalRService.Instance.SendSparkUpdateAsync(sparkMsg);

                    if (sparkSent)
                        await CreateSparkNotification(post, currentUserPhone);

                    MessagingCenter.Send(this, "SparkToggled", sparkMsg);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SparkButton_Tapped error: {ex}");
            }
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
                        StrokeShape = new RoundRectangle { CornerRadius = 30 },
                        Padding = new Thickness(20, 14),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.End,
                        Margin = new Thickness(0, 0, 0, 100),
                        Opacity = 0,
                        ZIndex = 9999,
                        Content = new HorizontalStackLayout
                        {
                            Spacing = 10,
                            Children =
                    {
                        new Label { Text = "⚡", FontSize = 20 },
                        new Label
                        {
                            Text = $"Spark limit reached. Wait {waitMinutes}m",
                            FontSize = 13,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#FF3B6F"),
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                        }
                    };

                    Grid.SetRowSpan(toast, mainGrid.RowDefinitions.Count > 0
                        ? mainGrid.RowDefinitions.Count : 3);
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
                        new Label
                        {
                            Text = "⚡",
                            FontSize = 13,
                            VerticalOptions = LayoutOptions.Center
                        },
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
        private async Task AnimateSparkButton(VisualElement button)
        {
            try
            {
                await button.ScaleTo(1.3, 100, Easing.SpringOut);
                await button.ScaleTo(1, 100, Easing.SpringOut);
                await button.RotateTo(15, 50);
                await button.RotateTo(-15, 50);
                await button.RotateTo(0, 50);
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
                const int durationMs = 580;

                Dispatcher.StartTimer(TimeSpan.FromMilliseconds(16), () =>
                {
                    double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

                    if (elapsed >= durationMs)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                            parentGrid.Children.Remove(canvas));
                        return false;
                    }

                    drawable.Elapsed = elapsed;
                    canvas.Invalidate();
                    return true;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowSparkAnimation error: {ex}");
            }
        }

        private sealed class SparkParticleDrawable : IDrawable
        {
            private readonly double _cx, _cy;

            // Pre-built particle descriptors
            private readonly (double angle, double speed, float size, int colorIndex, bool hasTrail, double delay, double duration)[] _dots;
            private readonly (double angle, double speed, float width, int colorIndex, double duration)[] _streaks;
            private readonly (double angle, double speed, float size, int colorIndex, double delay, double duration)[] _sparkles;
            private readonly (double maxR, float width, int colorIndex, double delay, double duration)[] _rings;

            // Palette
            private static readonly Microsoft.Maui.Graphics.Color[] Colors = {
        Microsoft.Maui.Graphics.Color.FromArgb("#FFD700"), // gold
        Microsoft.Maui.Graphics.Color.FromArgb("#FFA500"), // amber
        Microsoft.Maui.Graphics.Color.FromArgb("#FF8C00"), // orange
        Microsoft.Maui.Graphics.Color.FromArgb("#FFFDE8"), // white-warm
        Microsoft.Maui.Graphics.Color.FromArgb("#00B5B5"), // teal
    };

            public double Elapsed { get; set; }

            public SparkParticleDrawable(double cx, double cy)
            {
                _cx = cx;
                _cy = cy;
                var rng = new Random();

                // 12 main burst dots
                _dots = Enumerable.Range(0, 12).Select(i => (
                    angle: i * (Math.PI * 2 / 12) + (rng.NextDouble() - 0.5) * 0.3,
                    speed: 38.0 + rng.NextDouble() * 22,
                    size: (float)(3.2 + rng.NextDouble() * 2.5),
                    colorIndex: i % Colors.Length,
                    hasTrail: true,
                    delay: 0.0,
                    duration: 520.0
                )).ToArray();

                // 6 radial streaks
                _streaks = Enumerable.Range(0, 6).Select(i => (
                    angle: i * (Math.PI * 2 / 6) + Math.PI / 12,
                    speed: 46.0 + rng.NextDouble() * 16,
                    width: (float)(1.0 + rng.NextDouble() * 0.5),
                    colorIndex: i % 2 == 0 ? 0 : 3, // gold or warm-white
                    duration: 360.0
                )).ToArray();

                // 8 micro sparkles
                _sparkles = Enumerable.Range(0, 8).Select(i => (
                    angle: rng.NextDouble() * Math.PI * 2,
                    speed: 18.0 + rng.NextDouble() * 26,
                    size: (float)(1.4 + rng.NextDouble() * 1.4),
                    colorIndex: rng.Next(0, 2), // gold or amber
                    delay: 40.0 + rng.NextDouble() * 60,
                    duration: 400.0
                )).ToArray();

                // 3 expanding rings
                _rings = new[]
                {
            (maxR: 42.0, width: 2.0f, colorIndex: 0, delay: 0.0,   duration: 460.0), // gold
            (maxR: 30.0, width: 1.5f, colorIndex: 4, delay: 55.0,  duration: 390.0), // teal
            (maxR: 20.0, width: 1.0f, colorIndex: 3, delay: 110.0, duration: 300.0), // white
        };
            }

            private static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
            private static float EaseOutQuart(float t) => 1f - (1f - t) * (1f - t) * (1f - t) * (1f - t);
            private static float EaseInQuad(float t) => t * t;

            public void Draw(ICanvas canvas, RectF dirty)
            {
                float elapsed = (float)Elapsed;
                float cx = (float)_cx;
                float cy = (float)_cy;

                // ── Central core flash ──────────────────────────────────────
                const float coreDur = 200f;
                if (elapsed < coreDur)
                {
                    float cp = elapsed / coreDur;
                    float cr = 18f * EaseOutCubic(cp) * (1f - EaseInQuad(cp));
                    float alpha = (1f - cp) * 0.65f;
                    if (cr > 0)
                    {
                        var radial = new RadialGradientPaint
                        {
                            Center = new Point(0.5, 0.5),
                            Radius = 0.5,
                            GradientStops = new PaintGradientStop[]
                            {
                        new PaintGradientStop(0.0f,  Colors[3].WithAlpha(alpha)),
                        new PaintGradientStop(0.45f, Colors[0].WithAlpha(alpha * 0.75f)),
                        new PaintGradientStop(1.0f,  Colors[0].WithAlpha(0)),
                            }
                        };
                        canvas.SetFillPaint(radial, new RectF(cx - cr, cy - cr, cr * 2, cr * 2));
                        canvas.FillCircle(cx, cy, cr);
                    }
                }

                // ── Rings ───────────────────────────────────────────────────
                foreach (var ring in _rings)
                {
                    float t = elapsed - (float)ring.delay;
                    if (t < 0 || t >= (float)ring.duration) continue;
                    float p = t / (float)ring.duration;
                    float r = EaseOutCubic(p) * (float)ring.maxR;
                    float alpha = 1f - EaseInQuad(p);
                    canvas.StrokeColor = Colors[ring.colorIndex].WithAlpha(alpha * 0.82f);
                    canvas.StrokeSize = ring.width;
                    canvas.DrawCircle(cx, cy, r);
                }

                // ── Streaks ─────────────────────────────────────────────────
                foreach (var s in _streaks)
                {
                    float t = elapsed;
                    if (t >= (float)s.duration) continue;
                    float p = t / (float)s.duration;
                    float eased = EaseOutCubic(p);
                    float fadeIn = Math.Min(p / 0.2f, 1f);
                    float fadeOut = p > 0.4f ? Math.Max(0f, 1f - (p - 0.4f) / 0.6f) : 1f;
                    float alpha = fadeIn * fadeOut;

                    float endDist = eased * (float)s.speed;
                    float startDist = eased * (float)s.speed * 0.28f;
                    float ex = cx + (float)Math.Cos(s.angle) * endDist;
                    float ey = cy + (float)Math.Sin(s.angle) * endDist;
                    float sx2 = cx + (float)Math.Cos(s.angle) * startDist;
                    float sy2 = cy + (float)Math.Sin(s.angle) * startDist;

                    // Gradient line: transparent at base → solid at tip
                    var linePaint = new LinearGradientPaint
                    {
                        StartPoint = new Point(
                            (sx2 - (cx - 60)) / 120.0,
                            (sy2 - (cy - 60)) / 120.0),
                        EndPoint = new Point(
                            (ex - (cx - 60)) / 120.0,
                            (ey - (cy - 60)) / 120.0),
                        GradientStops = new PaintGradientStop[]
                        {
                    new PaintGradientStop(0f, Colors[s.colorIndex].WithAlpha(0f)),
                    new PaintGradientStop(1f, Colors[s.colorIndex].WithAlpha(alpha * 0.88f)),
                        }
                    };
                    canvas.StrokeSize = s.width;
                    canvas.StrokeColor = Colors[s.colorIndex].WithAlpha(alpha * 0.88f);
                    canvas.DrawLine(sx2, sy2, ex, ey);
                }

                // ── Main dots with glow + trail ─────────────────────────────
                foreach (var d in _dots)
                {
                    float t = elapsed - (float)d.delay;
                    if (t < 0 || t >= (float)d.duration) continue;
                    float p = t / (float)d.duration;
                    float eased = EaseOutCubic(p);
                    float fadeIn = Math.Min(p / 0.12f, 1f);
                    float fadeOut = p > 0.42f ? Math.Max(0f, 1f - (p - 0.42f) / 0.58f) : 1f;
                    float alpha = fadeIn * fadeOut;

                    float dist = eased * (float)d.speed;
                    float px = cx + (float)Math.Cos(d.angle) * dist;
                    float py = cy + (float)Math.Sin(d.angle) * dist;

                    // Trail
                    if (d.hasTrail && p > 0.06f)
                    {
                        float trailP = Math.Max(0f, p - 0.14f);
                        float trailE = EaseOutCubic(trailP);
                        float trailDist = trailE * (float)d.speed;
                        float tx = cx + (float)Math.Cos(d.angle) * trailDist;
                        float ty = cy + (float)Math.Sin(d.angle) * trailDist;
                        canvas.StrokeColor = Colors[d.colorIndex].WithAlpha(alpha * 0.28f);
                        canvas.StrokeSize = d.size * 0.55f;
                        canvas.DrawLine(tx, ty, px, py);
                    }

                    // Soft outer glow
                    float glowR = d.size * 2.6f;
                    var glow = new RadialGradientPaint
                    {
                        Center = new Point(0.5, 0.5),
                        Radius = 0.5,
                        GradientStops = new PaintGradientStop[]
                        {
                    new PaintGradientStop(0f,   Colors[d.colorIndex].WithAlpha(alpha * 0.55f)),
                    new PaintGradientStop(0.5f, Colors[d.colorIndex].WithAlpha(alpha * 0.18f)),
                    new PaintGradientStop(1f,   Colors[d.colorIndex].WithAlpha(0f)),
                        }
                    };
                    canvas.SetFillPaint(glow, new RectF(px - glowR, py - glowR, glowR * 2, glowR * 2));
                    canvas.FillCircle(px, py, glowR);

                    // Hard bright core
                    canvas.FillColor = Colors[3].WithAlpha(alpha * 0.92f); // warm white
                    canvas.FillCircle(px, py, d.size * 0.52f);
                }

                // ── Micro sparkles ──────────────────────────────────────────
                foreach (var s in _sparkles)
                {
                    float t = elapsed - (float)s.delay;
                    if (t < 0 || t >= (float)s.duration) continue;
                    float p = t / (float)s.duration;
                    float eased = EaseOutCubic(p);
                    float alpha = p < 0.35f
                        ? p / 0.35f
                        : Math.Max(0f, 1f - (p - 0.35f) / 0.65f);

                    float px = cx + (float)Math.Cos(s.angle) * eased * (float)s.speed;
                    float py = cy + (float)Math.Sin(s.angle) * eased * (float)s.speed;

                    canvas.FillColor = Colors[s.colorIndex].WithAlpha(alpha * 0.78f);
                    canvas.FillCircle(px, py, s.size);
                }
            }
        }


        private void ShowSparkRipple(Grid parentGrid, double cx, double cy,
                                     string colorHex, int delayMs)
        {
            const double size = 48;

            var ripple = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                StrokeThickness = 2.5,
                Stroke = Color.FromArgb(colorHex),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                BackgroundColor = Colors.Transparent,
                Opacity = 0,
                Scale = 0.3,
                ZIndex = 997,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0),
                TranslationX = cx - size / 2,
                TranslationY = cy - size / 2,
                InputTransparent = true
            };

            Grid.SetRow(ripple, 0);
            Grid.SetRowSpan(ripple, parentGrid.RowDefinitions.Count > 0
                ? parentGrid.RowDefinitions.Count : 3);
            parentGrid.Children.Add(ripple);

            Task.Run(async () =>
            {
                if (delayMs > 0) await Task.Delay(delayMs);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    new Animation(t =>
                    {
                        ripple.Scale = 0.3 + t * 3.5;
                        ripple.Opacity = t < 0.2 ? t * 5 : 1 - ((t - 0.2) / 0.8);
                    }, 0, 1, Easing.CubicOut)
                    .Commit(ripple, "SparkRipple", 16, 600,
                        finished: (v, c) => MainThread.BeginInvokeOnMainThread(() =>
                            parentGrid.Children.Remove(ripple)));
                });
            });
        }

        private void ShowGlowRing(Grid parentGrid, double cx, double cy)
        {
            const double size = 70;

            var glow = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                StrokeThickness = 8,
                Stroke = Color.FromArgb("#33FFD700"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                BackgroundColor = Color.FromArgb("#11FFD700"),
                Opacity = 0,
                Scale = 0.2,
                ZIndex = 996,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0),
                TranslationX = cx - size / 2,
                TranslationY = cy - size / 2,
                InputTransparent = true
            };

            Grid.SetRow(glow, 0);
            Grid.SetRowSpan(glow, parentGrid.RowDefinitions.Count > 0
                ? parentGrid.RowDefinitions.Count : 3);
            parentGrid.Children.Add(glow);

            var glowAnim = new Animation();
            glowAnim.Add(0, 0.4, new Animation(t =>
            {
                glow.Scale = 0.2 + t * 2.0;
                glow.Opacity = t * 2.5;
            }, 0, 1, Easing.SpringOut));
            glowAnim.Add(0.4, 1.0, new Animation(t =>
            {
                glow.Scale = 1.0 + t * 0.8;
                glow.Opacity = 1 - t;
            }, 0, 1, Easing.CubicIn));

            glowAnim.Commit(glow, "GlowRing", 16, 700,
                finished: (v, c) => MainThread.BeginInvokeOnMainThread(() =>
                    parentGrid.Children.Remove(glow)));
        }
        private async Task CreateSparkNotification(PostModel post, string sparkerPhone)
        {
            try
            {
                var sparkerName = await GetUserDisplayName(sparkerPhone);
                var sparkerProfileImage = await GetUserProfileImagePathAsync(sparkerPhone);

                // Get the first image from the post (if any)
                List<string> postImagePaths = post.ImagePathsList?.ToList() ?? new List<string>();

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
                    Timestamp = DateTime.UtcNow,
                    PostImagePathsList = postImagePaths  // ← Use PostImagePathsList instead of PostImagePath
                };

                const string key = "notifications_v2";
                var json = Preferences.Get(key, string.Empty);
                var list = string.IsNullOrEmpty(json)
                    ? new List<NotificationItem>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<NotificationItem>>(json) ?? new List<NotificationItem>();

                list.Insert(0, notif);
                if (list.Count > 200) list = list.Take(200).ToList();
                Preferences.Set(key, System.Text.Json.JsonSerializer.Serialize(list));

                MessagingCenter.Send<object, NotificationItem>(this, "NewNotificationStructured", notif);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating spark notification: {ex}");
            }
        }

        private async Task<string> GetUserProfileImagePathAsync(string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                    return string.Empty;

                var found = _allFeedPosts?
                    .FirstOrDefault(p => string.Equals(p.AuthorPhone?.Trim(), phone.Trim(), StringComparison.OrdinalIgnoreCase));
                if (found != null && !string.IsNullOrWhiteSpace(found.AuthorProfileImagePath) && File.Exists(found.AuthorProfileImagePath))
                    return found.AuthorProfileImagePath;

                try
                {
                    var prefKey = $"user_profile_image_{phone}";
                    var cached = Preferences.Get(prefKey, string.Empty);
                    if (!string.IsNullOrWhiteSpace(cached) && File.Exists(cached))
                        return cached;
                }
                catch { }

                // FIXED: Use Supabase instead of SQLite
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null && !string.IsNullOrWhiteSpace(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                {
                    try
                    {
                        var prefKey = $"user_profile_image_{phone}";
                        Preferences.Set(prefKey, user.ProfileImagePath);
                    }
                    catch { }

                    return user.ProfileImagePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserProfileImagePathAsync error: {ex}");
            }

            return string.Empty;
        }

        private async void OnAddToListTapped(object sender, EventArgs e)
        {
            try
            {
                var post = GetPostFromGesture(sender, e);
                if (post == null) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to save posts", "OK");
                    return;
                }

                bool isSaved = SavePostService.IsPostSaved(post.Id, currentUserPhone);

                if (isSaved)
                {
                    var action = await DisplayActionSheet(
                        "Post Options", "Cancel", null,
                        "Unsave", "Move to Category", "View Post");

                    if (action == "Unsave")
                    {
                        bool success = await SavePostService.UnsavePostAsync(post.Id, currentUserPhone);
                        if (success)
                        {
                            post.IsSavedByCurrentUser = false;
                            await DisplayAlert("Removed", "Post removed from your bookmarks.", "OK");
                            MessagingCenter.Send(this, "PostUnsaved", post.Id);
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
                    // ── Show existing folders + option to create new ──────────────
                    var allSaved = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                    var existingFolders = allSaved
                        .Select(s => string.IsNullOrEmpty(s.FolderName) ? "Saved" : s.FolderName)
                        .Distinct()
                        .OrderBy(f => f)
                        .ToList();

                    string chosenFolder;

                    if (existingFolders.Any())
                    {
                        // Build options: existing folders first, then "＋ New Category"
                        var options = existingFolders.Concat(new[] { "＋ New Category" }).ToArray();

                        var picked = await DisplayActionSheet(
                            "Save to Category", "Cancel", null, options);

                        if (picked == null || picked == "Cancel") return;

                        if (picked == "＋ New Category")
                        {
                            var newName = await DisplayPromptAsync(
                                "New Category",
                                "Enter category name:",
                                placeholder: "e.g. Inspiration",
                                maxLength: 30,
                                keyboard: Keyboard.Text);

                            if (newName == null) return;
                            chosenFolder = string.IsNullOrWhiteSpace(newName) ? "Saved" : newName.Trim();
                        }
                        else
                        {
                            chosenFolder = picked;
                        }
                    }
                    else
                    {
                        // No folders yet — go straight to prompt
                        var newName = await DisplayPromptAsync(
                            "Save to Category",
                            "Enter a category name:",
                            placeholder: "e.g. Inspiration",
                            maxLength: 30,
                            keyboard: Keyboard.Text);

                        if (newName == null) return;
                        chosenFolder = string.IsNullOrWhiteSpace(newName) ? "Saved" : newName.Trim();
                    }

                    bool success = await SavePostService.SavePostAsync(post.Id, currentUserPhone, chosenFolder);

                    if (success)
                    {
                        post.IsSavedByCurrentUser = true;
                        await DisplayAlert("Saved!", $"Post saved to '{chosenFolder}'.\nFind it in Hidden → Saved tab.", "OK");
                        MessagingCenter.Send(this, "PostSaved", post.Id);
                    }
                    else
                    {
                        await DisplayAlert("Already Saved", "This post is already in your bookmarks.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAddToListTapped error: {ex}");
                await DisplayAlert("Error", "Could not save post", "OK");
            }
        }
        private async Task MovePostToCategory(Lock.Models.Post post, string currentUserPhone)
        {
            try
            {
                // Get existing categories
                var savedItems = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                var existingCategories = savedItems
                    .Where(s => s.Post.Id != post.Id) // Exclude current post
                    .Select(s => s.FolderName)
                    .Distinct()
                    .ToList();

                var options = new List<string>();
                if (existingCategories.Any())
                {
                    options.AddRange(existingCategories);
                }
                options.Add("Create New Category");
                options.Add("Cancel");

                var selectedCategory = await DisplayActionSheet(
                    "Move to Category",
                    "Cancel",
                    null,
                    options.ToArray());

                if (string.IsNullOrEmpty(selectedCategory) || selectedCategory == "Cancel")
                    return;

                string finalCategory;

                if (selectedCategory == "Create New Category")
                {
                    finalCategory = await DisplayPromptAsync(
                        "New Category",
                        "Enter category name:",
                        maxLength: 30,
                        keyboard: Keyboard.Text);

                    if (string.IsNullOrWhiteSpace(finalCategory))
                        return;
                }
                else
                {
                    finalCategory = selectedCategory;
                }

                bool success = await SavePostService.MovePostToFolderAsync(post.Id, currentUserPhone, finalCategory);
                if (success)
                {
                    await DisplayAlert("Moved", $"Post moved to '{finalCategory}' category.", "OK");
                    MessagingCenter.Send(this, "PostMovedToFolder", new { PostId = post.Id, FolderName = finalCategory });
                }
                else
                {
                    await DisplayAlert("Error", "Could not move post.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MovePostToCategory error: {ex}");
                await DisplayAlert("Error", "Could not move post.", "OK");
            }
        }


        private async Task<List<FolderInfo>> GetUserFolders(string currentUserPhone)
        {
            try
            {
                var savedItems = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                var folders = savedItems
                    .GroupBy(s => s.FolderName)
                    .Select(g => new FolderInfo
                    {
                        Name = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                return folders;
            }
            catch
            {
                return new List<FolderInfo>();
            }
        }
        private async void SavePostButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var content = ContentEditor?.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(content) && !_pickedImagePaths.Any())
                {
                    await DisplayAlert("Validation", "Please add text or at least one image before posting.", "OK");
                    return;
                }

                var authorPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;
                if (string.IsNullOrEmpty(authorPhone))
                {
                    await DisplayAlert("Error", "You must be logged in to post", "OK");
                    return;
                }

                var authorName = await GetUserDisplayName(authorPhone);

                // Get category
                string category = string.Empty;
                if (CategoryPicker != null && CategoryPicker.SelectedIndex >= 0)
                {
                    var sel = CategoryPicker.Items[CategoryPicker.SelectedIndex];
                    if (string.Equals(sel, "None", StringComparison.OrdinalIgnoreCase))
                        category = string.Empty;
                    else if (string.Equals(sel, "Other", StringComparison.OrdinalIgnoreCase))
                        category = CategoryOtherEntry?.Text?.Trim() ?? string.Empty;
                    else
                        category = sel ?? string.Empty;
                }

                if (_editingPostId.HasValue)
                {
                    // EDIT EXISTING POST
                    var existing = await PostRepository.GetByIdAsync(_editingPostId.Value);
                    if (existing != null)
                    {
                        string oldContent = existing.Content;

                        existing.Content = content;
                        existing.ImagePathsList = _pickedImagePaths.ToArray();
                        existing.Category = category;
                        existing.Visibility = _selectedVisibility;

                        InvokeUpdateDisplayContent(existing, 200);
                        await PostRepository.UpdateAsync(existing);

                        // ========== TRACK POST EDIT ==========
                        await TrackPostEditAsync(existing, oldContent);

                        // Clear editing state
                        _editingPostId = null;

                        // SHOW Post button, HIDE Update and Cancel buttons
                        if (PostButton != null)
                            PostButton.IsVisible = true;
                        if (_updateGrid != null)
                            _updateGrid.IsVisible = false;
                        if (_cancelGrid != null)
                            _cancelGrid.IsVisible = false;

                        // Clear the form
                        if (ContentEditor != null)
                            ContentEditor.Text = string.Empty;
                        _pickedImagePaths.Clear();
                        UpdatePreviewLayout();

                        if (CategoryPicker != null)
                            CategoryPicker.SelectedIndex = 0;
                        if (CategoryOtherEntry != null)
                        {
                            CategoryOtherEntry.Text = string.Empty;
                            CategoryOtherEntry.IsVisible = false;
                        }

                        ResetVisibilityToDefault();

                        await DisplayAlert("Success", "Post updated successfully", "OK");

                        // FORCE a full refresh to show the updated post immediately
                        ClearPostCache();
                        await LoadPostsAsync(forceRefresh: true);
                    }
                }
                else
                {
                    // CREATE NEW POST
                    var post = new Lock.Models.Post
                    {
                        AuthorPhone = authorPhone,
                        AuthorDisplayName = authorName,
                        Content = content,
                        ImagePathsList = _pickedImagePaths.ToArray(),
                        CreatedAt = DateTime.UtcNow,
                        Category = category,
                        Visibility = _selectedVisibility,
                        LovedBy = new List<string>(),
                        LoveCount = 0,
                        CommentCount = 0
                    };

                    InvokeUpdateDisplayContent(post, 200);
                    await PostRepository.InsertAsync(post);

                    // ========== TRACK POST CREATION ==========
                    await TrackPostCreationAsync(post);

                    // Clear form
                    if (ContentEditor != null) ContentEditor.Text = string.Empty;
                    _pickedImagePaths.Clear();
                    UpdatePreviewLayout();

                    if (CategoryPicker != null) CategoryPicker.SelectedIndex = 0;
                    if (CategoryOtherEntry != null)
                    {
                        CategoryOtherEntry.Text = string.Empty;
                        CategoryOtherEntry.IsVisible = false;
                    }

                    ResetVisibilityToDefault();

                    await DisplayAlert("Success", "Post created successfully", "OK");

                    // Clear cache and force fresh load
                    ClearPostCache();
                    await LoadPostsAsync(forceRefresh: true);

                    // Scroll to top to show the new post
                    await MainScrollView?.ScrollToAsync(0, 0, true);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not save post: " + ex.Message, "OK");
                Debug.WriteLine($"Error in SavePostButton_Clicked: {ex}");
            }
        }


        private async Task RefreshPostInFeed(int postId)
        {
            try
            {
                var updatedPost = await PostRepository.GetByIdAsync(postId);
                if (updatedPost == null) return;

                var index = _allFeedPosts.FindIndex(p => p.Id == postId);
                if (index >= 0)
                {
                    // Update the existing post with new data
                    var existingPost = _allFeedPosts[index];
                    existingPost.Content = updatedPost.Content;
                    existingPost.ImagePathsList = updatedPost.ImagePathsList;
                    existingPost.Category = updatedPost.Category;
                    existingPost.Visibility = updatedPost.Visibility;
                    // existingPost.UpdatedAt = updatedPost.UpdatedAt; // REMOVE THIS LINE

                    InvokeUpdateDisplayContent(existingPost, 200);

                    // Refresh the CollectionView
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (PostsCollectionView != null)
                        {
                            var source = PostsCollectionView.ItemsSource;
                            PostsCollectionView.ItemsSource = null;
                            PostsCollectionView.ItemsSource = source;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshPostInFeed error: {ex}");
            }
        }

        private async Task UpdateBottomNavChatBadge()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    SetBottomNavChatBadgeVisibility(false);
                    return;
                }

                // FIXED: Use Supabase instead of SQLite
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(ParticipantA.eq.{Uri.EscapeDataString(currentUserPhone)},ParticipantB.eq.{Uri.EscapeDataString(currentUserPhone)})");

                int conversationsWithUnread = 0;

                foreach (var conv in conversations)
                {
                    try
                    {
                        var unreadMessages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                            $"ConversationId=eq.{conv.ConversationId}&RecipientPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&IsRead=eq.false");

                        if (unreadMessages.Count > 0)
                        {
                            conversationsWithUnread++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking conversation {conv.ConversationId}: {ex}");
                    }
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetBottomNavChatBadgeVisibility(conversationsWithUnread > 0);

                    if (ChatNavBadge != null && ChatNavBadgeLabel != null)
                    {
                        if (conversationsWithUnread > 0)
                        {
                            ChatNavBadge.IsVisible = true;
                            ChatNavBadgeLabel.Text = conversationsWithUnread > 99 ? "99+" : conversationsWithUnread.ToString();
                        }
                        else
                        {
                            ChatNavBadge.IsVisible = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating bottom nav chat badge: {ex}");
                SetBottomNavChatBadgeVisibility(false);
            }
        }

        private void SetBottomNavChatBadgeVisibility(bool isVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ChatNavBadge != null)
                    ChatNavBadge.IsVisible = isVisible;
            });
        }


        private async Task<string?> SavePickedFileAsync(FileResult result, string destFileName)
        {
            if (result == null) return null;

            try
            {
                var folder = FileSystem.AppDataDirectory;
                var destPath = System.IO.Path.Combine(folder, destFileName);

                System.Diagnostics.Debug.WriteLine($"Saving file to: {destPath}");

                using var sourceStream = await result.OpenReadAsync();
                using var destStream = File.Open(destPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destStream);

                System.Diagnostics.Debug.WriteLine($"File saved successfully: {destPath}");
                return destPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving file: {ex}");
                return null;
            }
        }
        private void CategoryPicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (CategoryPicker == null || CategoryOtherEntry == null) return;

            var selected = CategoryPicker.SelectedIndex >= 0
                ? CategoryPicker.Items[CategoryPicker.SelectedIndex]
                : null;

            bool isOther = string.Equals(selected, "Other", StringComparison.OrdinalIgnoreCase);

            var pickerContainer = this.FindByName<Grid>("CategoryPickerContainer");
            var otherContainer = this.FindByName<Grid>("CategoryOtherContainer");

            if (pickerContainer != null) pickerContainer.IsVisible = !isOther;
            if (otherContainer != null) otherContainer.IsVisible = isOther;
            CategoryOtherEntry.IsVisible = isOther;

            if (isOther)
            {
                CategoryOtherEntry.Text = string.Empty;
                CategoryOtherEntry.Focus();
            }
            else
            {
                CategoryOtherEntry.Text = string.Empty;
            }
        }

        private void CategoryOtherCancel_Tapped(object? sender, TappedEventArgs e)
        {
            try
            {
                // Reset picker back to "None"
                if (CategoryPicker != null)
                    CategoryPicker.SelectedIndex = 0;

                // Hide other container, show picker
                var pickerContainer = this.FindByName<Grid>("CategoryPickerContainer");
                var otherContainer = this.FindByName<Grid>("CategoryOtherContainer");

                if (pickerContainer != null) pickerContainer.IsVisible = true;
                if (otherContainer != null) otherContainer.IsVisible = false;

                if (CategoryOtherEntry != null)
                {
                    CategoryOtherEntry.IsVisible = false;
                    CategoryOtherEntry.Text = string.Empty;
                }

                // Dismiss keyboard
                ContentEditor?.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CategoryOtherCancel_Tapped error: {ex}");
            }
        }

        private Lock.Models.Post? GetPostFromGesture(object? sender, EventArgs e)
        {
            if (sender is TapGestureRecognizer tg)
            {
                if (tg.CommandParameter is Lock.Models.Post p1) return p1;
            }

            if (sender is VisualElement ve && ve.BindingContext is Lock.Models.Post p2)
            {
                return p2;
            }

            return null;
        }


        private async void LoveButton_Tapped(object sender, TappedEventArgs e)
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

                bool wasLoved = post.IsLovedByCurrentUser;

                // Toggle in database
                await PostRepository.ToggleLoveAsync(post.Id, currentUserPhone);

                // Update model only — no rebind, no reload
                post.ToggleLove(currentUserPhone);

                // Animation ONLY when loving — run without await so scroll isn't disturbed
                if (!wasLoved && post.IsLovedByCurrentUser)
                {
                    VisualElement loveElement = null;
                    if (sender is Border border) loveElement = border;
                    else if (sender is VisualElement ve) loveElement = ve;

                    // Fire and forget — do NOT await here; that's what causes the layout jump
                    _ = Task.Run(async () =>
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (loveElement != null)
                            {
                                var animTask = AnimateLoveButton(loveElement);
                                ShowMultipleHeartsAnimation(loveElement);
                                await animTask;
                            }
                            else
                            {
                                ShowMultipleHeartsAnimation(PostsCollectionView);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling love: {ex}");
            }
        }

        private void ShowMultipleHeartsAnimation(VisualElement targetElement)
        {
            try
            {
                var parentGrid = this.FindByName<Grid>("MainGrid");
                if (parentGrid == null) return;

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

                const double particleSize = 20;

                foreach (var (text, color, angleDeg) in particles)
                {
                    double angleRad = angleDeg * Math.PI / 180.0;
                    double burstDist = random.NextDouble() * 35 + 40;
                    double targetX = Math.Cos(angleRad) * burstDist;
                    double targetY = Math.Sin(angleRad) * burstDist;

                    var particle = new Label
                    {
                        Text = text,
                        FontSize = text == "❤" ? 12 : 16,
                        TextColor = Color.FromArgb(color),
                        Opacity = 1,
                        Scale = 0,
                        BackgroundColor = Colors.Transparent,
                        ZIndex = 999,
                        // Position at top-left, then translate to center
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(0),
                        WidthRequest = particleSize,
                        HeightRequest = particleSize,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        // Pre-translate to center so Margin never changes
                        TranslationX = cx - particleSize / 2,
                        TranslationY = cy - particleSize / 2,
                        InputTransparent = true
                    };

                    // Span all rows so it floats over everything
                    Grid.SetRow(particle, 0);
                    Grid.SetRowSpan(particle, parentGrid.RowDefinitions.Count > 0
                        ? parentGrid.RowDefinitions.Count : 3);

                    parentGrid.Children.Add(particle);

                    uint duration = (uint)random.Next(500, 750);
                    var combined = new Animation();

                    combined.Add(0, 1, new Animation(t =>
                    {
                        // Animate FROM center outward using TranslationX/Y only
                        particle.TranslationX = (cx - particleSize / 2) + targetX * t;
                        particle.TranslationY = (cy - particleSize / 2) + targetY * t;
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
                                parentGrid.Children.Remove(particle));
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
                    Margin = new Thickness(0),
                    TranslationX = cx - rippleSize / 2,
                    TranslationY = cy - rippleSize / 2,
                    InputTransparent = true
                };

                Grid.SetRow(ripple, 0);
                Grid.SetRowSpan(ripple, parentGrid.RowDefinitions.Count > 0
                    ? parentGrid.RowDefinitions.Count : 3);

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
                            parentGrid.Children.Remove(ripple));
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowRippleRing error: {ex}");
            }
        }
        // Add these animation methods to your PostPage class

        private async Task AnimateLoveButton(VisualElement button)
        {
            try
            {
                // Quick pop scale — same feel as Twitter's heart tap
                await button.ScaleTo(0.8, 80, Easing.CubicIn);
                await button.ScaleTo(1.25, 120, Easing.SpringOut);
                await button.ScaleTo(1.0, 100, Easing.CubicOut);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AnimateLoveButton error: {ex}");
            }
        }



        private void ShowLoveAnimation(VisualElement targetElement)
        {
            try
            {
                var parentGrid = this.FindByName<Grid>("MainGrid");
                if (parentGrid == null) return;

                // Get button position
                double centerX = 0;
                double centerY = 0;

                var buttonBounds = targetElement.Bounds;
                if (targetElement.Parent is VisualElement parent)
                {
                    centerX = buttonBounds.X + buttonBounds.Width / 2;
                    centerY = buttonBounds.Y + buttonBounds.Height / 2;
                }

                // Create floating heart
                var floatingHeart = new Label
                {
                    Text = "❤️",
                    FontSize = 32,
                    TextColor = Color.FromArgb("#FF3B6F"),
                    Opacity = 1,
                    TranslationX = 0,
                    TranslationY = 0,
                    BackgroundColor = Colors.Transparent,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                parentGrid.Children.Add(floatingHeart);

                // Position near the button
                AbsoluteLayout.SetLayoutBounds(floatingHeart, new Rect(centerX - 16, centerY - 16, 32, 32));
                AbsoluteLayout.SetLayoutFlags(floatingHeart, AbsoluteLayoutFlags.None);

                // Animate
                floatingHeart.ScaleTo(1.5, 300);
                floatingHeart.TranslateTo(0, -80, 500, Easing.CubicOut);
                floatingHeart.FadeTo(0, 500);

                // Remove after animation
                Task.Run(async () =>
                {
                    await Task.Delay(600);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        parentGrid.Children.Remove(floatingHeart);
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowLoveAnimation error: {ex}");
            }
        }

        // Add this message class inside your PostPage class (or outside)
        public class LoveChangedMessage
        {
            public int PostId { get; set; }
            public bool IsLoved { get; set; }
            public int LoveCount { get; set; }
            public string UserPhone { get; set; }
        }
        private async void OnSearchTapped(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SearchPage());
        }

        private async void CommentButton_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (_isNavigatingToComments) return;
                if (e.Parameter is not PostModel post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to comment", "OK");
                    return;
                }

                _isNavigatingToComments = true;

                var commentsPage = new CommentsPage(post.Id, currentUserPhone);
                await Navigation.PushAsync(commentsPage);

                // User has returned — re-read this post's count from DB
                try
                {
                    int freshCount = await Lock.Data.Post.CommentRepository
                                            .GetCommentCountForPostAsync(post.Id);
                    if (post.CommentCount != freshCount)
                    {
                        post.CommentCount = freshCount;
                        post.NotifyCommentCountChanged();
                        Debug.WriteLine($"CommentButton return: post {post.Id} count → {freshCount}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Comment count refresh error: {ex}");
                }

                await Task.Delay(500);
                _isNavigatingToComments = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening comments: {ex}");
                _isNavigatingToComments = false;
            }
        }

        // Add this method for tracking post creation
        private async Task TrackPostCreationAsync(PostModel post)
        {
            try
            {
                await UserTrackingService.Instance.TrackPostCreationAsync(
                    post.Id,
                    post.AuthorPhone,
                    post.Content,
                    post.Category ?? string.Empty);
                Debug.WriteLine($"[TRACKING] Post created: PostId={post.Id}, Author={post.AuthorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPostCreationAsync error: {ex}");
            }
        }

        // Add this method for tracking post edit
        private async Task TrackPostEditAsync(PostModel post, string oldContent)
        {
            try
            {
                await UserTrackingService.Instance.TrackPostEditAsync(
                    post.Id,
                    post.AuthorPhone,
                    oldContent,
                    post.Content,
                    post.Category ?? string.Empty);
                Debug.WriteLine($"[TRACKING] Post edited: PostId={post.Id}, Author={post.AuthorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPostEditAsync error: {ex}");
            }
        }

        // Add this method for tracking post deletion
        private async Task TrackPostDeletionAsync(PostModel post)
        {
            try
            {
                await UserTrackingService.Instance.TrackPostDeletionAsync(
                    post.Id,
                    post.AuthorPhone,
                    post.Content,
                    post.Category ?? string.Empty);
                Debug.WriteLine($"[TRACKING] Post deleted: PostId={post.Id}, Author={post.AuthorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPostDeletionAsync error: {ex}");
            }
        }

        private async Task RefreshSinglePostCommentCountAsync(PostModel post)
        {
            try
            {
                // Read the persisted count straight from the DB
                int freshCount = await Lock.Data.Post.CommentRepository
                                        .GetCommentCountForPostAsync(post.Id);

                if (post.CommentCount == freshCount) return; // nothing changed

                post.CommentCount = freshCount;
                post.NotifyCommentCountChanged();

                Debug.WriteLine($"RefreshSinglePostCommentCount: post {post.Id} → {freshCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshSinglePostCommentCountAsync error: {ex}");
            }
        }


        // ══════════════════════════════════════════════════════════════════════════════
        // 3.  ADD this new helper — refreshes ALL posts' comment counts cheaply.
        //     Call it from OnAppearing (see change 4 below) so counts are always
        //     correct when the feed re-appears without a full reload.
        // ══════════════════════════════════════════════════════════════════════════════
        private async Task RefreshAllCommentCountsAsync()
        {
            try
            {
                if (_allFeedPosts == null || !_allFeedPosts.Any()) return;

                bool anyChanged = false;

                foreach (var p in _allFeedPosts)
                {
                    int freshCount = await Lock.Data.Post.CommentRepository
                                            .GetCommentCountForPostAsync(p.Id);
                    if (p.CommentCount != freshCount)
                    {
                        p.CommentCount = freshCount;
                        p.NotifyCommentCountChanged();
                        anyChanged = true;
                    }
                }

                if (anyChanged)
                    Debug.WriteLine("RefreshAllCommentCountsAsync: counts updated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshAllCommentCountsAsync error: {ex}");
            }
        }



        private async void OnNotificationsTapped(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new NotificationPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Notifications navigation error: {ex}");
                try
                {
                    await DisplayAlert("Navigation Error", "Could not navigate to notifications", "OK");
                }
                catch { }
            }
        }

        public void UpdateNotificationBadge(int unreadCount)
        {
            if (NotificationBadge == null || NotificationBadgeLabel == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (unreadCount <= 0)
                {
                    NotificationBadge.IsVisible = false;
                    return;
                }

                NotificationBadge.IsVisible = true;

                if (unreadCount > 99)
                    NotificationBadgeLabel.Text = "99+";
                else
                    NotificationBadgeLabel.Text = unreadCount.ToString();
            });
        }

        private DataTemplate CreateCommentTemplateWithActions(string currentUserPhone)
        {
            return new DataTemplate(() =>
            {
                var rootGrid = new Grid
                {
                    Padding = new Thickness(12, 8),
                    RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
                };

                var profileFrame = new Frame
                {
                    HeightRequest = 40,
                    WidthRequest = 40,
                    CornerRadius = 20,
                    Padding = 0,
                    IsClippedToBounds = true,
                    BackgroundColor = Color.FromArgb("#333333"),
                    HasShadow = false
                };

                var profileImage = new Image
                {
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 40,
                    WidthRequest = 40
                };
                profileImage.SetBinding(Image.SourceProperty, "AuthorProfileImagePath");
                profileFrame.Content = profileImage;

                var contentStack = new VerticalStackLayout
                {
                    Margin = new Thickness(8, 0, 0, 0),
                    Spacing = 4
                };

                var headerStack = new HorizontalStackLayout { Spacing = 8 };

                var nameLabel = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F5F2E9"),
                    FontSize = 14
                };
                nameLabel.SetBinding(Label.TextProperty, "AuthorDisplayName");

                var timeLabel = new Label
                {
                    TextColor = Color.FromArgb("#888888"),
                    FontSize = 11
                };
                timeLabel.SetBinding(Label.TextProperty, "CreatedAtRelative");

                headerStack.Children.Add(nameLabel);
                headerStack.Children.Add(timeLabel);

                var commentLabel = new Label
                {
                    TextColor = Color.FromArgb("#DDDDDD"),
                    FontSize = 14
                };
                commentLabel.SetBinding(Label.TextProperty, "Content");

                contentStack.Children.Add(headerStack);
                contentStack.Children.Add(commentLabel);

                Grid.SetRow(profileFrame, 0);
                Grid.SetColumn(profileFrame, 0);
                rootGrid.Children.Add(profileFrame);

                Grid.SetRow(contentStack, 0);
                Grid.SetColumn(contentStack, 1);
                rootGrid.Children.Add(contentStack);

                var menuButton = new Button
                {
                    Text = "⋮",
                    FontSize = 18,
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#888888"),
                    WidthRequest = 40,
                    HeightRequest = 40,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center
                };

                menuButton.SetBinding(VisualElement.IsVisibleProperty, "IsOwnedByCurrentUser");

                Grid.SetRow(menuButton, 0);
                Grid.SetColumn(menuButton, 2);
                rootGrid.Children.Add(menuButton);

                var actionStack = new HorizontalStackLayout
                {
                    Spacing = 20,
                    Margin = new Thickness(48, 4, 0, 0),
                    HorizontalOptions = LayoutOptions.Start
                };

                var loveStack = new HorizontalStackLayout { Spacing = 4 };
                var loveTap = new TapGestureRecognizer();

                var loveIcon = new Label
                {
                    Text = "🤍",
                    FontSize = 16,
                    TextColor = Color.FromArgb("#888888")
                };
                loveIcon.SetBinding(Label.TextProperty, "LoveIcon");
                loveIcon.SetBinding(Label.TextColorProperty, "LoveIconColor");

                var loveCount = new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888888")
                };
                loveCount.SetBinding(Label.TextProperty, "LoveCountDisplay");

                loveStack.GestureRecognizers.Add(loveTap);
                loveStack.Children.Add(loveIcon);
                loveStack.Children.Add(loveCount);
                actionStack.Children.Add(loveStack);

                var replyStack = new HorizontalStackLayout { Spacing = 4 };
                var replyTap = new TapGestureRecognizer();

                replyStack.Children.Add(new Label
                {
                    Text = "💬",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#888888")
                });
                replyStack.Children.Add(new Label
                {
                    Text = "Reply",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888888")
                });

                replyStack.GestureRecognizers.Add(replyTap);
                actionStack.Children.Add(replyStack);

                Grid.SetRow(actionStack, 1);
                Grid.SetColumnSpan(actionStack, 3);
                rootGrid.Children.Add(actionStack);

                var menuFlyout = new StackLayout
                {
                    IsVisible = false,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    Padding = new Thickness(8, 4),
                    Spacing = 4,
                    WidthRequest = 100,
                    HorizontalOptions = LayoutOptions.End
                };

                var editButton = new Label
                {
                    Text = "Edit",
                    TextColor = Color.FromArgb("#F5F2E9"),
                    FontSize = 14,
                    Padding = new Thickness(8, 4)
                };
                editButton.GestureRecognizers.Add(new TapGestureRecognizer());

                var deleteButton = new Label
                {
                    Text = "Delete",
                    TextColor = Color.FromArgb("#008080"),
                    FontSize = 14,
                    Padding = new Thickness(8, 4)
                };
                deleteButton.GestureRecognizers.Add(new TapGestureRecognizer());

                menuFlyout.Children.Add(editButton);
                menuFlyout.Children.Add(deleteButton);

                Grid.SetRow(menuFlyout, 0);
                Grid.SetColumn(menuFlyout, 2);
                rootGrid.Children.Add(menuFlyout);

                menuButton.Clicked += (s, e) =>
                {
                    menuFlyout.IsVisible = !menuFlyout.IsVisible;
                };

                editButton.GestureRecognizers.OfType<TapGestureRecognizer>().First().Tapped += async (s, e) =>
                {
                    var comment = (s as VisualElement)?.BindingContext as Comment;
                    if (comment == null) return;

                    menuFlyout.IsVisible = false;

                    string newContent = await Application.Current.MainPage.DisplayPromptAsync(
                        "Edit Comment",
                        "Edit your comment:",
                        initialValue: comment.Content,
                        maxLength: 1000,
                        keyboard: Keyboard.Text
                    );

                    if (!string.IsNullOrWhiteSpace(newContent))
                    {
                        try
                        {
                            await CommentRepository.UpdateCommentAsync(comment.Id, newContent);

                            if (rootGrid.BindingContext is Comment updatedComment)
                            {
                                updatedComment.Content = newContent;
                                commentLabel.Text = newContent;
                            }
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.MainPage.DisplayAlert("Error", "Could not edit comment", "OK");
                            Debug.WriteLine($"Error editing comment: {ex}");
                        }
                    }
                };

                deleteButton.GestureRecognizers.OfType<TapGestureRecognizer>().First().Tapped += async (s, e) =>
                {
                    var comment = (s as VisualElement)?.BindingContext as Comment;
                    if (comment == null) return;

                    menuFlyout.IsVisible = false;

                    bool confirm = await Application.Current.MainPage.DisplayAlert(
                        "Delete Comment",
                        "Are you sure you want to delete this comment?",
                        "Yes", "No"
                    );

                    if (confirm)
                    {
                        try
                        {
                            await CommentRepository.DeleteCommentAsync(comment.Id);
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.MainPage.DisplayAlert("Error", "Could not delete comment", "OK");
                            Debug.WriteLine($"Error deleting comment: {ex}");
                        }
                    }
                };

                loveTap.Tapped += async (s, e) =>
                {
                    var comment = (s as VisualElement)?.BindingContext as Comment;
                    if (comment == null) return;

                    try
                    {
                        await CommentRepository.ToggleLoveAsync(comment.Id, currentUserPhone);
                        comment.ToggleLove(currentUserPhone);

                        loveIcon.Text = comment.LoveIcon;
                        loveIcon.TextColor = comment.LoveIconColor;
                        loveCount.Text = comment.LoveCountDisplay;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error toggling love: {ex}");
                    }
                };

                replyTap.Tapped += async (s, e) =>
                {
                    var comment = (s as VisualElement)?.BindingContext as Comment;
                    if (comment == null) return;

                    string replyContent = await Application.Current.MainPage.DisplayPromptAsync(
                        "Reply to Comment",
                        "Write your reply:",
                        maxLength: 1000,
                        keyboard: Keyboard.Text
                    );

                    if (!string.IsNullOrWhiteSpace(replyContent))
                    {
                        try
                        {
                            var postId = comment.PostId;
                            await CommentRepository.AddCommentAsync(
                                postId,
                                currentUserPhone,
                                replyContent,
                                comment.Id
                            );
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.MainPage.DisplayAlert("Error", "Could not post reply", "OK");
                            Debug.WriteLine($"Error posting reply: {ex}");
                        }
                    }
                };

                var separator = new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#333333"),
                    Margin = new Thickness(12, 0, 12, 0)
                };

                var finalStack = new VerticalStackLayout();
                finalStack.Children.Add(rootGrid);
                finalStack.Children.Add(separator);

                return finalStack;
            });
        }

        private DataTemplate CreateCommentTemplate()
        {
            return new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    Padding = new Thickness(12, 8),
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
                };

                var profileFrame = new Frame
                {
                    HeightRequest = 36,
                    WidthRequest = 36,
                    CornerRadius = 18,
                    Padding = 0,
                    IsClippedToBounds = true,
                    BackgroundColor = Color.FromArgb("#333"),
                    HasShadow = false
                };

                var profileImage = new Image
                {
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 36,
                    WidthRequest = 36
                };
                profileImage.SetBinding(Image.SourceProperty, "AuthorProfileImagePath");
                profileFrame.Content = profileImage;

                var contentStack = new VerticalStackLayout
                {
                    Margin = new Thickness(8, 0, 0, 0),
                    Spacing = 4
                };

                var headerStack = new HorizontalStackLayout { Spacing = 8 };

                var nameLabel = new Label { FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#F5F2E9") };
                nameLabel.SetBinding(Label.TextProperty, "AuthorDisplayName");

                var timeLabel = new Label { TextColor = Color.FromArgb("#888"), FontSize = 11 };
                timeLabel.SetBinding(Label.TextProperty, "CreatedAtRelative");

                headerStack.Children.Add(nameLabel);
                headerStack.Children.Add(timeLabel);

                var commentLabel = new Label { TextColor = Color.FromArgb("#DDD"), FontSize = 14 };
                commentLabel.SetBinding(Label.TextProperty, "Content");

                contentStack.Children.Add(headerStack);
                contentStack.Children.Add(commentLabel);

                Grid.SetColumn(profileFrame, 0);
                Grid.SetColumn(contentStack, 1);

                grid.Children.Add(profileFrame);
                grid.Children.Add(contentStack);

                var separator = new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#333"),
                    Margin = new Thickness(12, 0, 12, 0)
                };

                var rootStack = new VerticalStackLayout();
                rootStack.Children.Add(grid);
                rootStack.Children.Add(separator);

                return rootStack;
            });
        }

        private async void EditIcon_Tapped(object? sender, EventArgs e)
        {
            try
            {
                var post = GetPostFromGesture(sender, e);
                if (post == null) return;

                if (!IsCurrentUserPost(post))
                {
                    await DisplayAlert("Access Denied", "You can only edit your own posts", "OK");
                    return;
                }

                if (ContentEditor != null)
                    ContentEditor.Text = post.Content;

                _pickedImagePaths = post.ImagePathsList?.ToList() ?? new List<string>();
                UpdatePreviewLayout();

                // Set category picker
                if (CategoryPicker != null)
                {
                    if (!string.IsNullOrEmpty(post.Category))
                    {
                        var idx = CategoryPicker.Items.IndexOf(post.Category);
                        if (idx >= 0)
                        {
                            CategoryPicker.SelectedIndex = idx;
                        }
                        else
                        {
                            CategoryPicker.Items.Add(post.Category);
                            CategoryPicker.SelectedIndex = CategoryPicker.Items.IndexOf(post.Category);
                        }
                        if (CategoryOtherEntry != null)
                        {
                            CategoryOtherEntry.Text = string.Empty;
                            CategoryOtherEntry.IsVisible = false;
                        }
                    }
                    else
                    {
                        CategoryPicker.SelectedIndex = 0;
                        if (CategoryOtherEntry != null)
                        {
                            CategoryOtherEntry.Text = string.Empty;
                            CategoryOtherEntry.IsVisible = false;
                        }
                    }
                }

                // Set visibility for the post being edited
                _selectedVisibility = post.Visibility ?? "Everyone";
                UpdateVisibilityIconColor(_selectedVisibility == "By Mood" ? "#FF3B6F" : "#008080");

                _editingPostId = post.Id;

                // HIDE the Post button, SHOW Update and Cancel buttons
                if (PostButton != null)
                    PostButton.IsVisible = false;
                if (_updateGrid != null)
                    _updateGrid.IsVisible = true;
                if (_cancelGrid != null)
                    _cancelGrid.IsVisible = true;

                // Scroll to the create post section
                if (CreatePostSection != null && MainScrollView != null)
                {
                    await MainScrollView.ScrollToAsync(CreatePostSection, ScrollToPosition.Start, true);

                    // Highlight the section briefly
                    CreatePostSection.BackgroundColor = Color.FromArgb("#33008080");
                    await Task.Delay(500);
                    CreatePostSection.BackgroundColor = Colors.Transparent;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not start edit: " + ex.Message, "OK");
            }
        }
        private void CancelEditButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                // Clear the editor content
                if (ContentEditor != null)
                    ContentEditor.Text = string.Empty;

                // Clear picked images
                _pickedImagePaths.Clear();
                UpdatePreviewLayout();

                // Reset editing state
                _editingPostId = null;

                // Reset category picker
                if (CategoryPicker != null)
                    CategoryPicker.SelectedIndex = 0;
                if (CategoryOtherEntry != null)
                {
                    CategoryOtherEntry.Text = string.Empty;
                    CategoryOtherEntry.IsVisible = false;
                }

                // SHOW Post button, HIDE Update and Cancel buttons
                if (PostButton != null)
                    PostButton.IsVisible = true;
                if (_updateGrid != null)
                    _updateGrid.IsVisible = false;
                if (_cancelGrid != null)
                    _cancelGrid.IsVisible = false;

                // Reset visibility to default "Everyone" with GREEN color
                ResetVisibilityToDefault();

                // Clear any focus
                ContentEditor?.Unfocus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CancelEditButton_Clicked: {ex}");
            }
        }
        private void ResetVisibilityToDefault()
        {
            _selectedVisibility = "Everyone";
            UpdateVisibilityIconColor("#008080"); // Green for Everyone (default)
            Debug.WriteLine("Visibility reset to Everyone (default) with green icon");
        }


        private async void AuthorName_Tapped(object sender, EventArgs e)
        {
            if (sender is not VisualElement element) return;
            if (element.BindingContext is not Lock.Models.Post post) return;

            string phone = post.AuthorPhone ?? string.Empty;

            if (phone.Contains("·"))
            {
                var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                phone = parts.Length > 1 ? parts[1].Trim() : phone;
            }

            phone = phone.Trim();
            if (string.IsNullOrWhiteSpace(phone)) return;

            try
            {
                var navigationParams = new Dictionary<string, object>
                {
                    ["phone"] = phone,
                    ["viewOnly"] = "true"
                };

                await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(phone)}&viewOnly=true");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Navigation failed", ex.Message, "OK");
            }
        }

        private async void PostMenuButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not VisualElement ve) return;
            if (ve.BindingContext is not Lock.Models.Post post) return;

            try
            {
                var actionsPage = new PostActionsPage(
                    post,
                    onEdit: (p) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            var tg = new TapGestureRecognizer { CommandParameter = p };
                            EditIcon_Tapped(tg, EventArgs.Empty);
                        });
                    },
                    onDelete: async (p) =>
                    {
                        var confirm = await DisplayAlert("Delete Post",
                            "Are you sure you want to delete this post?",
                            "Yes", "No");

                        if (confirm)
                        {
                            var tg = new TapGestureRecognizer { CommandParameter = p };
                            DeleteIcon_Tapped(tg, EventArgs.Empty);
                        }
                    }
                );

                // Use PushModalAsync with animated false to avoid white flash
                await Navigation.PushModalAsync(actionsPage, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in PostMenuButton_Clicked: {ex}");
            }
        }

        public void UpdateChatBadge(int unreadCount)
        {
            if (ChatNavBadge == null || ChatNavBadgeLabel == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (unreadCount <= 0)
                {
                    ChatNavBadge.IsVisible = false;
                    return;
                }

                ChatNavBadge.IsVisible = true;

                if (unreadCount > 99)
                    ChatNavBadgeLabel.Text = "99+";
                else
                    ChatNavBadgeLabel.Text = unreadCount.ToString();
            });
        }
        private void OnHomeTapped(object sender, EventArgs e)
        {
            try
            {
                MainScrollView?.ScrollToAsync(0, 0, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scroll error: {ex}");
            }
        }

        private async void OnChatsTapped(object sender, EventArgs e)
        {
            try
            {
                bool success = await SafeNavigateToChatsAsync();
                if (!success)
                {
                    await DisplayAlert("Error", "Could not navigate to chats", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chats navigation error: {ex}");
            }
        }

        private async void OnProfileTapped(object sender, EventArgs e)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Error", "Please sign in to view your profile", "OK");
                    await Shell.Current.GoToAsync("///LoginPage");
                    return;
                }

                bool success = await SafeNavigateToProfileAsync(currentUserPhone);
                if (!success)
                {
                    await DisplayAlert("Error", "Could not navigate to profile", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Profile navigation error: {ex}");
            }
        }

        private async Task<bool> SafeNavigateToChatsAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("//conversations");
                return true;
            }
            catch
            {
                try
                {
                    await Shell.Current.GoToAsync("conversations");
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private async Task<bool> SafeNavigateToProfileAsync(string phone)
        {
            try
            {
                await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(phone)}&viewOnly=false");
                return true;
            }
            catch
            {
                try
                {
                    await Shell.Current.GoToAsync($"//profile?phone={Uri.EscapeDataString(phone)}&viewOnly=false");
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private bool IsCurrentUserPost(Lock.Models.Post post)
        {
            if (post == null) return false;

            var currentUserPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;
            var postAuthorPhone = post.AuthorPhone ?? string.Empty;

            if (postAuthorPhone.Contains("·"))
            {
                var parts = postAuthorPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                postAuthorPhone = parts.Length > 1 ? parts[1].Trim() : postAuthorPhone;
            }

            return string.Equals(postAuthorPhone.Trim(), currentUserPhone.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private async void PostImage_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e?.Parameter is not string imagePath || string.IsNullOrEmpty(imagePath))
                {
                    System.Diagnostics.Debug.WriteLine("PostImage_Tapped: No image path parameter");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"PostImage_Tapped: Image path = {imagePath}");

                // ── FIND POST BY IMAGE PATH from _allFeedPosts ──
                var post = _allFeedPosts?.FirstOrDefault(p =>
                    p.ImagePathsList != null &&
                    p.ImagePathsList.Any(img =>
                        string.Equals(img, imagePath, StringComparison.OrdinalIgnoreCase)));

                if (post == null)
                {
                    System.Diagnostics.Debug.WriteLine("PostImage_Tapped: Could not find post for this image");
                    await DisplayAlert("Error", "Could not identify image source", "OK");
                    return;
                }

                var postImages = post.ImagePathsList?.ToList() ?? new List<string>();
                if (!postImages.Any())
                {
                    System.Diagnostics.Debug.WriteLine("PostImage_Tapped: Post has no images");
                    return;
                }

                int startIndex = postImages.FindIndex(p =>
                    string.Equals(p, imagePath, StringComparison.OrdinalIgnoreCase));
                if (startIndex < 0) startIndex = 0;

                if (!File.Exists(imagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"PostImage_Tapped: File does not exist: {imagePath}");
                    await DisplayAlert("Error", "Image file not found", "OK");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"PostImage_Tapped: Opening {postImages.Count} images at index {startIndex}");

                var fullScreenPage = new Lock.Pages.Profile.FullScreenMediaPage(postImages, startIndex);
                await Navigation.PushModalAsync(fullScreenPage);

                System.Diagnostics.Debug.WriteLine("PostImage_Tapped: FullScreenMediaPage opened successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PostImage_Tapped: {ex}");
                await DisplayAlert("Error", $"Could not open image: {ex.Message}", "OK");
            }
        }
        private async Task<string> PickFolderAsync()
        {
            try
            {
#if ANDROID
                var defaultFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "MyApp");

                bool useDefault = await Application.Current.MainPage.DisplayAlert(
                    "Custom Folder",
                    $"Android doesn't support folder picking yet. Save to:\n{defaultFolder}?",
                    "Yes",
                    "No");

                return useDefault ? defaultFolder : null;

#elif WINDOWS
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        folderPicker.FileTypeFilter.Add("*");
        
        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
        
#else
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "MyApp");
#endif
            }
            catch
            {
                return null;
            }
        }

        private async void DeleteIcon_Tapped(object? sender, EventArgs e)
        {
            try
            {
                var post = GetPostFromGesture(sender, e);
                if (post == null) return;

                if (!IsCurrentUserPost(post))
                {
                    await DisplayAlert("Access Denied", "You can only delete your own posts", "OK");
                    return;
                }

                var confirm = await DisplayAlert("Delete Post", "Are you sure you want to delete this post?", "Yes", "No");
                if (!confirm) return;

                // ========== TRACK POST DELETION ==========
                await TrackPostDeletionAsync(post);

                await PostRepository.DeleteAsync(post.Id);
                await LoadPostsAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not delete post: " + ex.Message, "OK");
            }
        }
        private async void CloudDriveButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var action = await DisplayActionSheet(
                    "Select Post Visibility",
                    "Cancel",
                    null,
                    "Everyone",
                    "By Mood");

                if (action == "Cancel" || string.IsNullOrEmpty(action))
                    return;

                if (action == "Everyone")
                {
                    _selectedVisibility = "Everyone";
                    UpdateVisibilityIconColor("#008080"); // Green color for Everyone
                    await DisplayAlert("Visibility Set",
                        "Your post will be visible to everyone",
                        "OK");
                }
                else if (action == "By Mood")
                {
                    // Get current user's mood
                    var currentUserMood = await GetCurrentUserMoodAsync();
                    if (string.IsNullOrEmpty(currentUserMood))
                    {
                        var setMood = await DisplayAlert(
                            "No Mood Set",
                            "You need to set a mood before posting mood-restricted content. Would you like to set your mood now?",
                            "Yes", "No");

                        if (setMood)
                        {
                            await Shell.Current.GoToAsync("///profile");
                            return;
                        }
                        else
                        {
                            // Revert to default "Everyone" with GREEN color if user cancels
                            _selectedVisibility = "Everyone";
                            UpdateVisibilityIconColor("#008080"); // Green for Everyone
                            return;
                        }
                    }

                    _selectedVisibility = "By Mood";
                    UpdateVisibilityIconColor("#008080"); // Red/Pink color for By Mood
                    await DisplayAlert("Visibility Set",
                        $"Your post will only be visible to people with the mood: {currentUserMood}",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CloudDriveButton_Clicked: {ex}");
                await DisplayAlert("Error", "Could not set visibility", "OK");
            }
        }


        private void UpdateVisibilityIconColor(string colorHex)
        {
            try
            {
                var icon = this.FindByName<Path>("CloudDriveIcon");
                if (icon != null)
                {
                    var color = Color.FromArgb(colorHex);
                    icon.Fill = color;
                    icon.Stroke = color;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating visibility icon color: {ex}");
            }
        }


        private void RefreshCollectionView()
        {
            if (PostsCollectionView == null) return;

            if (_allFeedPosts != null)
            {
                foreach (var p in _allFeedPosts)
                    p.NotifyCommentCountChanged();
            }

            PostsCollectionView.ItemTemplate = null;
            PostsCollectionView.ItemTemplate = (DataTemplateSelector)Resources["PostTemplateSelector"];
        }
    }

    public class NearbyUserCardViewModel : INotifyPropertyChanged
    {
        private readonly User _user;
        private readonly string _location;
        private int _matchPercent;

        public NearbyUserCardViewModel(User user, string location)
        {
            _user = user;
            _location = location;
            ComputeAge();
            _ = LoadMatchPercentageAsync();
        }

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

                // FIXED: Use Supabase instead of SQLite
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

        public int? HeightCm => _user.HeightCm;
        public bool HasHeight => _user.HeightCm.HasValue && _user.HeightCm.Value > 0;
        public string HeightDisplay => HasHeight ? $"{_user.HeightCm.Value}" : null;

        public string BodyType => _user.BodyType;
        public bool HasBodyType => !string.IsNullOrEmpty(_user.BodyType);

        public string Ethnicity => _user.Ethnicity;
        public bool HasEthnicity => !string.IsNullOrEmpty(_user.Ethnicity);
        public string EthnicityDisplay => _user.Ethnicity;

        public string PersonalityType => _user.PersonalityType;
        public bool HasPersonalityType => !string.IsNullOrEmpty(_user.PersonalityType);
        public string PersonalityTypeShort => HasPersonalityType ? _user.PersonalityType.Split('-')[0].Trim() : null;

        public string LoveLanguage => _user.LoveLanguage;
        public bool HasLoveLanguage => !string.IsNullOrEmpty(_user.LoveLanguage);
        public string LoveLanguageShort => HasLoveLanguage && _user.LoveLanguage.Length > 15 ? _user.LoveLanguage.Substring(0, 12) + "…" : _user.LoveLanguage;

        public List<string> InterestsList => (_user.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).ToList();
        public bool HasInterests => InterestsList.Any();

        private void ComputeAge()
        {
            if (_user.DateOfBirth == DateTime.MinValue) return;
            var t = DateTime.Today;
            Age = t.Year - _user.DateOfBirth.Year;
            if (_user.DateOfBirth > t.AddYears(-Age.Value)) Age--;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // Live User Card ViewModel for PostPage
    public class PostPageLiveUserCard : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string _phoneNumber = string.Empty;
        private string _name = string.Empty;
        private string _profileImagePath = string.Empty;
        private string _mood = string.Empty;
        private string _message = string.Empty;
        private string _location = string.Empty;
        private bool _chatAvailable;
        private bool _voiceAvailable;
        private bool _videoAvailable;
        private DateTime _startedAt;
        private DateTime? _scheduledEndTime;
        private int _age;
        private string _bio = string.Empty;
        private string _interests = string.Empty;
        private string _gender = string.Empty;
        private string _lookingFor = string.Empty;
        private string _height = string.Empty;
        private string _bodyType = string.Empty;
        private string _ethnicity = string.Empty;
        private string _personalityType = string.Empty;
        private string _loveLanguage = string.Empty;
        private string _energyLevel = string.Empty;
        private bool _isVerified;
        private float _moodOpacity = 1.0f;
        private Timer _moodBlinkTimer;

        public string PhoneNumber { get => _phoneNumber; set { _phoneNumber = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string ProfileImagePath { get => _profileImagePath; set { _profileImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProfileImage)); } }
        public string Mood { get => _mood; set { _mood = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMood)); OnPropertyChanged(nameof(MoodBlinkColor)); } }
        public string Message { get => _message; set { _message = value; OnPropertyChanged(); } }
        public string Location { get => _location; set { _location = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLocation)); } }
        public bool ChatAvailable { get => _chatAvailable; set { _chatAvailable = value; OnPropertyChanged(); } }
        public bool VoiceAvailable { get => _voiceAvailable; set { _voiceAvailable = value; OnPropertyChanged(); } }
        public bool VideoAvailable { get => _videoAvailable; set { _videoAvailable = value; OnPropertyChanged(); } }
        public DateTime StartedAt { get => _startedAt; set { _startedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(LiveSince)); } }
        public DateTime? ScheduledEndTime { get => _scheduledEndTime; set { _scheduledEndTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeRemaining)); OnPropertyChanged(nameof(CountdownColor)); OnPropertyChanged(nameof(ShowCountdown)); } }
        public int Age { get => _age; set { _age = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAge)); OnPropertyChanged(nameof(AgeText)); } }
        public string Bio { get => _bio; set { _bio = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBio)); OnPropertyChanged(nameof(BioPreview)); } }
        public string Interests { get => _interests; set { _interests = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInterests)); OnPropertyChanged(nameof(InterestsDisplay)); } }
        public string Gender { get => _gender; set { _gender = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGender)); } }
        public string LookingFor { get => _lookingFor; set { _lookingFor = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLookingFor)); } }
        public string Height { get => _height; set { _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasHeight)); } }
        public string BodyType { get => _bodyType; set { _bodyType = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBodyType)); } }
        public string Ethnicity { get => _ethnicity; set { _ethnicity = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEthnicity)); } }
        public string PersonalityType { get => _personalityType; set { _personalityType = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPersonalityType)); OnPropertyChanged(nameof(PersonalityTypeShort)); } }
        public string LoveLanguage { get => _loveLanguage; set { _loveLanguage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLoveLanguage)); } }
        public string EnergyLevel { get => _energyLevel; set { _energyLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEnergyLevel)); } }
        public bool IsVerified { get => _isVerified; set { _isVerified = value; OnPropertyChanged(); } }
        public float MoodOpacity { get => _moodOpacity; set { _moodOpacity = value; OnPropertyChanged(); } }

        public bool HasLocation => !string.IsNullOrEmpty(Location);
        public bool HasAge => Age > 0;
        public bool HasBio => !string.IsNullOrEmpty(Bio);
        public bool HasInterests => !string.IsNullOrEmpty(Interests);
        public bool HasMood => !string.IsNullOrEmpty(Mood);
        public bool HasGender => !string.IsNullOrEmpty(Gender);
        public bool HasLookingFor => !string.IsNullOrEmpty(LookingFor);
        public bool HasHeight => !string.IsNullOrEmpty(Height);
        public bool HasBodyType => !string.IsNullOrEmpty(BodyType);
        public bool HasEthnicity => !string.IsNullOrEmpty(Ethnicity);
        public bool HasPersonalityType => !string.IsNullOrEmpty(PersonalityType);
        public bool HasLoveLanguage => !string.IsNullOrEmpty(LoveLanguage);
        public bool HasEnergyLevel => !string.IsNullOrEmpty(EnergyLevel);

        public string AgeText => HasAge ? $"{Age}" : string.Empty;
        public string BioPreview => string.IsNullOrEmpty(Bio) ? string.Empty : (Bio.Length > 60 ? Bio.Substring(0, 60) + "..." : Bio);
        private List<ImageSource> _preloadedSources = new List<ImageSource>();

        private List<string> _imageCarouselPaths = new List<string>();
        private int _currentImageIndex = 0;
        private Timer _carouselTimer;
        private ImageSource _currentCarouselImage;
        private ImageSource _nextCarouselImage;
        private float _currentImageOpacity = 1.0f;
        private float _nextImageOpacity = 0.0f;

        // REPLACE the ImageCarouselPaths setter:
        public List<string> ImageCarouselPaths
        {
            get => _imageCarouselPaths;
            set
            {
                _imageCarouselPaths = value ?? new List<string>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCarouselImages));

                // Pre-load ALL sources at assignment time
                _preloadedSources = _imageCarouselPaths
                    .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                    .Select(p => (ImageSource)ImageSource.FromFile(p))
                    .ToList();

                _currentCarouselImage = _preloadedSources.Count > 0
                    ? _preloadedSources[0]
                    : ProfileImage;

                OnPropertyChanged(nameof(CurrentCarouselImage));
                _currentImageIndex = 0;
            }
        }
        public bool HasCarouselImages => _imageCarouselPaths.Count > 1;

        public ImageSource CurrentCarouselImage
        {
            get => _currentCarouselImage ?? ProfileImage;
            private set
            {
                _currentCarouselImage = value;
                OnPropertyChanged();
            }
        }

        public ImageSource NextCarouselImage
        {
            get => _nextCarouselImage ?? ProfileImage;
            set { _nextCarouselImage = value; OnPropertyChanged(); }
        }

        public float CurrentImageOpacity
        {
            get => _currentImageOpacity;
            set { _currentImageOpacity = value; OnPropertyChanged(); }
        }

        public float NextImageOpacity
        {
            get => _nextImageOpacity;
            set { _nextImageOpacity = value; OnPropertyChanged(); }
        }

        private ImageSource GetCarouselImageSource(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return ImageSource.FromFile(path);
            return ProfileImage;
        }

        public void StartCarousel()
        {
            StopCarousel();
            if (_preloadedSources.Count <= 1) return;

            _currentImageIndex = 0;

            _carouselTimer = new Timer(_ =>
            {
                _currentImageIndex = (_currentImageIndex + 1) % _preloadedSources.Count;
                var next = _preloadedSources[_currentImageIndex];

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CurrentCarouselImage = next;
                });

            }, null, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4));
        }

        public void StopCarousel()
        {
            _carouselTimer?.Dispose();
            _carouselTimer = null;
        }
        public string InterestsDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Interests)) return string.Empty;
                var interestsList = Interests.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim()).Take(2);
                return string.Join(" • ", interestsList);
            }
        }

        public string PersonalityTypeShort
        {
            get
            {
                if (string.IsNullOrEmpty(PersonalityType)) return string.Empty;
                var parts = PersonalityType.Split('-');
                return parts[0].Trim();
            }
        }

        public Color MoodBlinkColor
        {
            get
            {
                if (string.IsNullOrEmpty(Mood)) return Color.FromArgb("#FF3B6F");
                var moodLower = Mood.ToLower();
                if (moodLower.Contains("horny")) return Color.FromArgb("#FF1493");
                if (moodLower.Contains("romantic")) return Color.FromArgb("#FF69B4");
                if (moodLower.Contains("chill")) return Color.FromArgb("#00B5B5");
                if (moodLower.Contains("playful")) return Color.FromArgb("#FFA500");
                if (moodLower.Contains("adventurous")) return Color.FromArgb("#FF4500");
                if (moodLower.Contains("talkative")) return Color.FromArgb("#4CAF50");
                if (moodLower.Contains("flirty")) return Color.FromArgb("#FF6B6B");
                return Color.FromArgb("#FF3B6F");
            }
        }

        public string LiveSince
        {
            get
            {
                var diff = DateTime.UtcNow - StartedAt;
                if (diff.TotalSeconds < 60) return "just went live";
                if (diff.TotalMinutes < 60) return $"live {(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"live {(int)diff.TotalHours}h ago";
                return "live today";
            }
        }

        public string TimeRemaining
        {
            get
            {
                if (!ScheduledEndTime.HasValue) return string.Empty;
                var timeRemaining = ScheduledEndTime.Value - DateTime.UtcNow;
                if (timeRemaining.TotalSeconds <= 0) return "Ending now";
                if (timeRemaining.TotalHours >= 1) return $"{timeRemaining:hh\\:mm\\:ss}";
                if (timeRemaining.TotalMinutes >= 1) return $"{timeRemaining:mm\\:ss}";
                return $"{timeRemaining:ss}s";
            }
        }

        public Color CountdownColor
        {
            get
            {
                if (!ScheduledEndTime.HasValue) return Color.FromArgb("#4CAF50");
                var timeRemaining = ScheduledEndTime.Value - DateTime.UtcNow;
                if (timeRemaining.TotalSeconds <= 30) return Color.FromArgb("#FF4444");
                if (timeRemaining.TotalSeconds <= 60) return Color.FromArgb("#FFA500");
                return Color.FromArgb("#4CAF50");
            }
        }

        public bool ShowCountdown => ScheduledEndTime.HasValue && ScheduledEndTime.Value > DateTime.UtcNow;

        public ImageSource ProfileImage =>
            (!string.IsNullOrEmpty(ProfileImagePath) && File.Exists(ProfileImagePath))
                ? ImageSource.FromFile(ProfileImagePath)
                : ImageSource.FromFile("default_avatar.png");

        public void StartMoodBlinking()
        {
            _moodBlinkTimer?.Dispose();
            bool fadingOut = true;
            _moodBlinkTimer = new Timer(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (fadingOut)
                    {
                        MoodOpacity = Math.Max(0.2f, MoodOpacity - 0.03f);
                        if (MoodOpacity <= 0.2f) fadingOut = false;
                    }
                    else
                    {
                        MoodOpacity = Math.Min(1.0f, MoodOpacity + 0.03f);
                        if (MoodOpacity >= 1.0f) fadingOut = true;
                    }
                });
            }, null, 0, 50);
        }

        public void StopMoodBlinking()
        {
            _moodBlinkTimer?.Dispose();
            _moodBlinkTimer = null;
            MoodOpacity = 1.0f;
            // REMOVE: StopCarousel() — carousel has its own lifecycle
        }
    }

    public class PostFilterInfo
    {
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool ScrollToLatest { get; set; }
    }
}