using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Pages.Chat;
using Lock.Pages.Post;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using MauiSwitch = Microsoft.Maui.Controls.Switch;
using Plugin.Maui.Audio;
using Path = Microsoft.Maui.Controls.Shapes.Path;
using SystemPath = System.IO.Path;
using Lock.Pages.Controls;
using Lock.Services.Admin;

namespace Lock.Pages.Profile
{
    public class SparkChangedMessage
    {
        public int PostId { get; set; }
        public bool IsSparked { get; set; }
        public int SparkCount { get; set; }
        public string UserPhone { get; set; } = string.Empty;
        public string AuthorPhone { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }


    // QueryProperty attributes - MUST be inside the class after the properties
    [QueryProperty(nameof(ViewOnlyString), "viewOnly")]
    [QueryProperty(nameof(Phone), "phone")]
    public partial class ProfilePage : ContentPage, INotifyPropertyChanged
    {
        // Backing fields
        private string _viewOnlyString = string.Empty;
        private string _phone = string.Empty;
        private bool _viewOnly = false;

        // Properties
        public string ViewOnlyString
        {
            get => _viewOnlyString;
            set
            {
                _viewOnlyString = value ?? string.Empty;
                _viewOnly = bool.TryParse(value, out var b) && b;
                OnPropertyChanged(nameof(IsOwner));
                OnPropertyChanged(nameof(IsNotOwner));
                ApplyViewOnlyMode();
            }
        }

        public string Phone
        {
            get => _phone;
            set
            {
                _phone = value ?? string.Empty;
                OnPropertyChanged(nameof(IsOwner));
                OnPropertyChanged(nameof(IsNotOwner));
                if (!string.IsNullOrEmpty(_phone))
                    _ = LoadUserAsync(_phone);
            }
        }

       

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private const string CurrentUserPhoneKey = "current_user_phone";

        // Other fields
        private Dictionary<string, List<string>> _mediaByCategory = new();

        private int _currentUserId = 0;
        private List<UserPhoto> _userPhotos = new();
        private List<UserPrompt> _userPrompts = new();
        private List<DateIdea> _userDateIdeas = new();
        private List<UserEvent> _userEvents = new();
        private string _currentTab = "Profile";
        private User? _currentUser = null;

        public bool IsOwner => !_viewOnly &&
             !string.IsNullOrEmpty(_phone) &&
             string.Equals(_phone.Trim(), Preferences.Get("current_user_phone", string.Empty)?.Trim(), StringComparison.OrdinalIgnoreCase);

        public bool IsNotOwner => !IsOwner;

        private IAudioPlayer _voiceIntroPlayer;
        private bool _isVoiceIntroPlaying;

        private List<string> _allMutualInterests = new();
        private bool _mutualInterestsExpanded = false;
        private int _compatibilityScore;
        private List<UserEndorsement> _endorsements = new();
        private List<PendingEndorsement> _pendingEndorsements = new();
        private List<BlockedUserItem> _blockedUsers = new();

        public bool HidePhoneNumber { get; set; } = false;

        public class BlockedUserItem
        {
            public string Phone { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ProfileImage { get; set; } = string.Empty;
        }



        // Method to load blocked users
        private async Task LoadBlockedUsersAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                    return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Use correct property names: UserPhone and BlockedPhone
                var blockedRelations = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == currentUserPhone)
                    .ToListAsync();

                _blockedUsers.Clear();

                foreach (var blocked in blockedRelations)
                {
                    string blockedPhone = blocked.BlockedPhone;

                    var user = await db.Table<User>()
                        .Where(u => u.PhoneNumber == blockedPhone)
                        .FirstOrDefaultAsync();

                    var blockedUser = new BlockedUserItem
                    {
                        Phone = blockedPhone,
                        Name = user?.Name ?? blockedPhone,
                        ProfileImage = user?.ProfileImagePath ?? string.Empty
                    };

                    _blockedUsers.Add(blockedUser);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var countLabel = this.FindByName<Label>("BlockedUsersCountLabel");
                    if (countLabel != null)
                    {
                        countLabel.Text = _blockedUsers.Count == 1 ? "1 blocked user" : $"{_blockedUsers.Count} blocked users";
                    }

                    var collectionView = this.FindByName<CollectionView>("BlockedUsersCollectionView");
                    if (collectionView != null)
                    {
                        collectionView.ItemsSource = _blockedUsers;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadBlockedUsersAsync error: {ex}");
            }
        }


        private async void HidePhoneSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            try
            {
                if (_viewOnly) return;
                if (!EnsurePhoneFromPreferences()) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == _phone)
                    .FirstOrDefaultAsync();

                if (user == null) return;

                user.HidePhoneNumber = e.Value;
                await db.UpdateAsync(user);

                if (_currentUser != null)
                    _currentUser.HidePhoneNumber = e.Value;

                // Apply visibility immediately
                ApplyPhoneVisibility(user);

                Debug.WriteLine($"[PHONE] HidePhoneNumber instantly saved: {e.Value} for {_phone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HidePhoneSwitch_Toggled error: {ex.Message}");
            }
        }

        private void ApplyPhoneVisibility(User user)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var phoneRow = this.FindByName<HorizontalStackLayout>("PhoneRow");
                if (phoneRow == null) return;

                // Owner always sees their own phone row
                // Other users see it only if HidePhoneNumber is false
                phoneRow.IsVisible = IsOwner || !user.HidePhoneNumber;

                // Also load the switch state when owner views their own profile
                if (IsOwner)
                {
                    var hideSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("HidePhoneSwitch");
                    if (hideSwitch != null)
                        hideSwitch.IsToggled = user.HidePhoneNumber;
                }
            });
        }

        // Toggle blocked users list visibility
        private void ToggleBlockedUsersList(object sender, EventArgs e)
        {
            var list = this.FindByName<Border>("BlockedUsersList");
            if (list != null)
            {
                list.IsVisible = !list.IsVisible;

                // If expanding, load the list if not loaded
                if (list.IsVisible && _blockedUsers.Count == 0)
                {
                    _ = LoadBlockedUsersAsync();
                }
            }
        }

        // Unblock a user
        private async void UnblockUser_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is TapGestureRecognizer tap && tap.CommandParameter is BlockedUserItem user)
                {
                    bool confirm = await DisplayAlert(
                        "Unblock User",
                        $"Are you sure you want to unblock {user.Name}?\n\nYou will be able to send and receive messages again.",
                        "Unblock",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                        if (string.IsNullOrEmpty(currentUserPhone)) return;

                        bool success = await ChatRepository.UnblockUserAsync(currentUserPhone, user.Phone);

                        if (success)
                        {
                            // Remove from local list
                            _blockedUsers.Remove(user);

                            // Refresh UI
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                var countLabel = this.FindByName<Label>("BlockedUsersCountLabel");
                                if (countLabel != null)
                                {
                                    countLabel.Text = _blockedUsers.Count == 1 ? "1 blocked user" : $"{_blockedUsers.Count} blocked users";
                                }

                                var collectionView = this.FindByName<CollectionView>("BlockedUsersCollectionView");
                                if (collectionView != null)
                                {
                                    collectionView.ItemsSource = null;
                                    collectionView.ItemsSource = _blockedUsers;
                                }

                                // If no blocked users left, collapse the list
                                if (_blockedUsers.Count == 0)
                                {
                                    var list = this.FindByName<Border>("BlockedUsersList");
                                    if (list != null) list.IsVisible = false;
                                }
                            });

                            await DisplayAlert("Unblocked", $"{user.Name} has been unblocked.", "OK");

                            // Notify other pages to refresh
                            MessagingCenter.Send(this, "UserUnmuted", user.Phone);
                        }
                        else
                        {
                            await DisplayAlert("Error", "Failed to unblock user. Please try again.", "OK");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnblockUser_Clicked error: {ex}");
                await DisplayAlert("Error", $"Could not unblock user: {ex.Message}", "OK");
            }
        }

        public ProfilePage()
        {
            InitializeComponent();
            // Remove or comment out these lines:
            // NavigationPage.SetHasNavigationBar(this, false);
            // Shell.SetNavBarIsVisible(this, false);

            this.Appearing += async (s, e) =>
            {
                if (!await IsUserLoggedIn())
                {
                    await Shell.Current.GoToAsync("///LoginPage");
                }
            };
        }

        private void UpdateNavigationBarVisibility()
        {
            // Show navigation bar with back button only when viewing other users
            // Hide navigation bar only when viewing own profile (to use custom bottom nav)
            bool showNavBar = _viewOnly || !IsOwner;

            NavigationPage.SetHasNavigationBar(this, showNavBar);
            Shell.SetNavBarIsVisible(this, showNavBar);

            // If showing navigation bar, set the back button title
            if (showNavBar)
            {
                // This sets the back button text (optional)
                Shell.SetBackButtonBehavior(this, new BackButtonBehavior
                {
                    IsVisible = true,
                    IsEnabled = true
                });
            }
        }

        private async void SparkButton_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not Lock.Models.Post post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Error", "Please log in first", "OK");
                    return;
                }

                // Find the spark button for animation
                VisualElement sparkButton = sender as VisualElement;

                // ── REMOVE spark (optimistic, no confirmation dialog) ──────────
                if (post.IsSparkedByCurrentUser)
                {
                    post.ToggleSpark(currentUserPhone);

                    var removeMsg = new Lock.Services.SparkUpdateMessage
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

                    RefreshProfilePostsCollectionView();
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

                var sparkMsg = new Lock.Services.SparkUpdateMessage
                {
                    PostId = post.Id,
                    IsSparked = true,
                    SparkCount = post.SparkCount,
                    UserPhone = currentUserPhone,
                    AuthorPhone = post.AuthorPhone,
                    Timestamp = DateTime.UtcNow
                };

                // Animate immediately
                if (sparkButton != null)
                {
                    _ = AnimateSparkButton(sparkButton);
                    ShowSparkAnimation(sparkButton);
                }

                ShowTopRightSparkToast(remaining - 1);

                // All heavy work off UI thread
                _ = Task.Run(async () =>
                {
                    bool sparkSent = await SparkService.RecordSparkAsync(currentUserPhone, post.Id, post.AuthorPhone);
                    await SignalRService.Instance.SendSparkUpdateAsync(sparkMsg);
                    if (sparkSent)
                        await CreateSparkNotification(post, currentUserPhone);
                    MessagingCenter.Send(this, "SparkToggled", sparkMsg);
                });

                RefreshProfilePostsCollectionView();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Spark error in ProfilePage: {ex}");
                await DisplayAlert("Error", "Something went wrong while sparking.", "OK");
            }
        }
        // Helper method - Add this inside your ProfilePage class
        private void RefreshProfilePostsCollectionView()
        {
            var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView"); // make sure the name matches your XAML
            if (collectionView == null) return;

            // This is the most reliable way to force MAUI CollectionView to redraw
            var items = collectionView.ItemsSource;
            collectionView.ItemsSource = null;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                collectionView.ItemsSource = items;
            });
        }

        // Add these helper methods for animations
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

                var random = new Random();

                // ── 1. Central burst emoji ──────────────────────────────────
                var burstLabel = new Label
                {
                    Text = "⚡",
                    FontSize = 52,
                    Opacity = 0,
                    Scale = 0.3,
                    BackgroundColor = Colors.Transparent,
                    ZIndex = 1000,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0),
                    TranslationX = cx - 26,
                    TranslationY = cy - 26,
                    InputTransparent = true
                };
                Grid.SetRow(burstLabel, 0);
                Grid.SetRowSpan(burstLabel, parentGrid.RowDefinitions.Count > 0
                    ? parentGrid.RowDefinitions.Count : 3);
                parentGrid.Children.Add(burstLabel);

                var burstAnim = new Animation();
                burstAnim.Add(0, 0.3, new Animation(t =>
                {
                    burstLabel.Scale = 0.3 + t * 2.2;
                    burstLabel.Opacity = t * 3;
                }, 0, 1, Easing.SpringOut));
                burstAnim.Add(0.5, 1.0, new Animation(t =>
                {
                    burstLabel.Scale = 2.5 - t * 1.5;
                    burstLabel.Opacity = 1 - t;
                }, 0, 1, Easing.CubicIn));
                burstAnim.Commit(burstLabel, "BurstAnim", 16, 800,
                    finished: (v, c) => MainThread.BeginInvokeOnMainThread(() =>
                        parentGrid.Children.Remove(burstLabel)));

                // ── 2. Particles ────────────────────────────────────────────
                var particles = new (string text, string color, double angle, double speed)[]
                {
            ("⚡", "#FFD700", 0,   1.1),
            ("⚡", "#FFA500", 40,  0.9),
            ("⚡", "#FFD700", 80,  1.2),
            ("⚡", "#FF8C00", 130, 1.0),
            ("⚡", "#FFD700", 180, 0.95),
            ("⚡", "#FFA500", 230, 1.15),
            ("⚡", "#FFD700", 280, 1.0),
            ("⚡", "#FF8C00", 320, 0.9),
            ("•",  "#FF3B6F", 20,  1.3),
            ("•",  "#FF6B9D", 70,  1.1),
            ("•",  "#FF3B6F", 110, 0.85),
            ("•",  "#FFD700", 160, 1.2),
            ("•",  "#FF3B6F", 200, 1.0),
            ("•",  "#FF6B9D", 250, 1.15),
            ("•",  "#FF3B6F", 300, 0.9),
            ("•",  "#FFD700", 350, 1.05),
            ("❤",  "#FF3B6F", 50,  0.7),
            ("❤",  "#FF6B9D", 150, 0.65),
            ("❤",  "#FF3B6F", 260, 0.75),
            ("✨", "#FFD700", 90,  0.8),
            ("✨", "#FFA500", 210, 0.85),
            ("✨", "#FFD700", 330, 0.78),
                };

                int rowSpan = parentGrid.RowDefinitions.Count > 0
                    ? parentGrid.RowDefinitions.Count : 3;

                foreach (var (text, color, angleDeg, speed) in particles)
                {
                    double angleRad = angleDeg * Math.PI / 180.0;
                    double burstDist = (random.NextDouble() * 50 + 55) * speed;
                    double targetX = Math.Cos(angleRad) * burstDist;
                    double targetY = Math.Sin(angleRad) * burstDist;
                    double size = text == "•" ? 16 : text == "❤" ? 14 : 20;

                    var particle = new Label
                    {
                        Text = text,
                        FontSize = text == "•" ? 18 : text == "❤" ? 13 : 17,
                        TextColor = Color.FromArgb(color),
                        Opacity = 1,
                        Scale = 0,
                        BackgroundColor = Colors.Transparent,
                        ZIndex = 999,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(0),
                        TranslationX = cx - size / 2,
                        TranslationY = cy - size / 2,
                        WidthRequest = size,
                        HeightRequest = size,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        InputTransparent = true
                    };

                    Grid.SetRow(particle, 0);
                    Grid.SetRowSpan(particle, rowSpan);
                    parentGrid.Children.Add(particle);

                    uint duration = (uint)(random.Next(550, 850));

                    var anim = new Animation();
                    anim.Add(0, 1, new Animation(t =>
                    {
                        particle.TranslationX = (cx - size / 2) + targetX * t;
                        particle.TranslationY = (cy - size / 2) + targetY * t;
                        particle.Scale = t < 0.25
                            ? (t / 0.25) * 1.3
                            : 1.3 - ((t - 0.25) / 0.75) * 0.5;
                    }, 0, 1, Easing.CubicOut));

                    anim.Add(0.55, 1, new Animation(t =>
                    {
                        particle.Opacity = 1 - t;
                    }, 0, 1, Easing.CubicIn));

                    if (text == "⚡" || text == "✨")
                    {
                        double rotTarget = random.NextDouble() * 60 - 30;
                        anim.Add(0, 1, new Animation(t =>
                        {
                            particle.Rotation = rotTarget * t;
                        }, 0, 1, Easing.CubicOut));
                    }

                    anim.Commit(particle, $"SparkParticle_{Guid.NewGuid():N}", 16, duration,
                        finished: (v, c) => MainThread.BeginInvokeOnMainThread(() =>
                            parentGrid.Children.Remove(particle)));
                }

                // ── 3. Two ripple rings ─────────────────────────────────────
                ShowSparkRippleProfile(parentGrid, cx, cy, "#FFD700", 0, rowSpan);
                ShowSparkRippleProfile(parentGrid, cx, cy, "#FF3B6F", 150, rowSpan);

                // ── 4. Glow ring ────────────────────────────────────────────
                ShowGlowRingProfile(parentGrid, cx, cy, rowSpan);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowSparkAnimation error: {ex}");
            }
        }

        private void ShowSparkRippleProfile(Grid parentGrid, double cx, double cy,
                                            string colorHex, int delayMs, int rowSpan)
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
            Grid.SetRowSpan(ripple, rowSpan);
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

        private void ShowGlowRingProfile(Grid parentGrid, double cx, double cy, int rowSpan)
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
            Grid.SetRowSpan(glow, rowSpan);
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

        private void ShowTopRightSparkToast(int remainingSparks)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var mainGrid = this.FindByName<Grid>("MainGrid");
                    if (mainGrid == null) return;

                    int rowSpan = mainGrid.RowDefinitions.Count > 0
                        ? mainGrid.RowDefinitions.Count : 3;

                    var toast = new Border
                    {
                        BackgroundColor = Color.FromArgb("#1C1C25"),
                        StrokeThickness = 1,
                        Stroke = Color.FromArgb("#FFD700"),
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Padding = new Thickness(12, 6),
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(0),
                        // Position via translation — never via Margin changes
                        TranslationX = 40,
                        TranslationY = 52,
                        Opacity = 0,
                        ZIndex = 9999,
                        InputTransparent = true,
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

                    Grid.SetRow(toast, 0);
                    Grid.SetRowSpan(toast, rowSpan);
                    mainGrid.Children.Add(toast);

                    // Animate in using TranslationX — no Margin mutation
                    toast.FadeTo(1, 150);
                    toast.TranslateTo(0, 52, 180, Easing.CubicOut);

                    Task.Run(async () =>
                    {
                        await Task.Delay(1800);
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Task.WhenAll(
                                toast.FadeTo(0, 150),
                                toast.TranslateTo(40, 52, 150, Easing.CubicIn)
                            );
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

                    int rowSpan = mainGrid.RowDefinitions.Count > 0
                        ? mainGrid.RowDefinitions.Count : 3;

                    // Position at bottom center via TranslationY — no Margin
                    double bottomOffset = -(mainGrid.Height * 0.15 + 60);

                    var toast = new Border
                    {
                        BackgroundColor = Color.FromArgb("#2A1520"),
                        StrokeThickness = 1.5,
                        Stroke = Color.FromArgb("#FF3B6F"),
                        StrokeShape = new RoundRectangle { CornerRadius = 30 },
                        Padding = new Thickness(20, 14),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.End,
                        Margin = new Thickness(0),
                        TranslationY = 0,
                        Opacity = 0,
                        ZIndex = 9999,
                        InputTransparent = true,
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

                    Grid.SetRow(toast, 0);
                    Grid.SetRowSpan(toast, rowSpan);
                    mainGrid.Children.Add(toast);

                    // Slide up from bottom
                    toast.TranslationY = 80;
                    Task.WhenAll(
                        toast.FadeTo(1, 200),
                        toast.TranslateTo(0, bottomOffset, 220, Easing.CubicOut)
                    );

                    Task.Run(async () =>
                    {
                        await Task.Delay(2500);
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Task.WhenAll(
                                toast.FadeTo(0, 200),
                                toast.TranslateTo(0, 80, 200, Easing.CubicIn)
                            );
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

        // Add this helper method for spark notifications
        private async Task CreateSparkNotification(Lock.Models.Post post, string sparkerPhone)
        {
            try
            {
                var sparkerName = await GetUserDisplayName(sparkerPhone);
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
                    Timestamp = DateTime.UtcNow,
                    PostImagePathsList = post.ImagePathsList?.ToList() ?? new List<string>()
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
        private async void OnUpdateLookingForTapped(object sender, EventArgs e)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Error", "User not found", "OK");
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == currentUserPhone).FirstOrDefaultAsync();

                if (user == null)
                {
                    await DisplayAlert("Error", "User not found", "OK");
                    return;
                }

                // Use the same options as PostPage
                string[] lookingForOptions = {
            "Long-term relationship",
            "Short-term fun",
            "Hookup",
            "Open to exploring",
            "Friends first",
            "Casual dating",
            "Serious relationship",
            "Marriage minded",
            "Not sure yet"
        };

                // Show current selection as pre-selected
                string currentMood = user.Mood ?? "Long-term relationship";
                var selected = await DisplayActionSheet(
                    "What are you looking for?",
                    "Cancel",
                    null,
                    lookingForOptions);

                if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                    return;

                // Update the user's mood
                user.Mood = selected;
                user.MoodLastUpdated = DateTime.UtcNow;
                await db.UpdateAsync(user);
                _currentUser = user;

                // Update the UI property
                OnPropertyChanged(nameof(CurrentUserLookingFor));

                // Send notifications to update PostPage and other pages
                MessagingCenter.Send(this, "MoodUpdated");
                MessagingCenter.Send(this, "MoodSaved");

                // Also update any picker if it exists in the preferences tab
                var moodPicker = this.FindByName<Picker>("MoodPicker");
                if (moodPicker != null && moodPicker.Items.Contains(selected))
                {
                    moodPicker.SelectedItem = selected;
                }

                await DisplayAlert("Updated", $"Looking for: {selected}", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnUpdateLookingForTapped error: {ex}");
                await DisplayAlert("Error", "Could not update: " + ex.Message, "OK");
            }
        }

        private async Task<string> GetUserProfileImagePathAsync(string phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                    return string.Empty;

                // First try to get from the current user object if it's the same user
                if (_currentUser != null && _currentUser.PhoneNumber == phone && !string.IsNullOrEmpty(_currentUser.ProfileImagePath))
                {
                    if (File.Exists(_currentUser.ProfileImagePath))
                        return _currentUser.ProfileImagePath;
                }

                // Try to get from database
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();

                if (user != null && !string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                {
                    return user.ProfileImagePath;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserProfileImagePathAsync error: {ex}");
                return string.Empty;
            }
        }

        // Add this helper method to get user display name
        private async Task<string> GetUserDisplayName(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
                return user?.Name ?? phone;
            }
            catch
            {
                return phone;
            }
        }

        // Add this method to ProfilePage.xaml.cs
        private async void OnAuthorNameTapped(object sender, TappedEventArgs e)
        {
            try
            {
                Lock.Models.Post? post = null;

                if (e.Parameter is Lock.Models.Post paramPost)
                    post = paramPost;
                else if (sender is VisualElement ve && ve.BindingContext is Lock.Models.Post bindingPost)
                    post = bindingPost;

                if (post == null) return;

                string phone = post.AuthorPhone ?? string.Empty;

                if (phone.Contains("·"))
                {
                    var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                    phone = parts.Length > 1 ? parts[1].Trim() : phone;
                }

                phone = phone.Trim();
                if (string.IsNullOrWhiteSpace(phone)) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                bool isOwnProfile = string.Equals(phone, currentUserPhone, StringComparison.OrdinalIgnoreCase);

                if (isOwnProfile)
                {
                    // FIX: Use relative route, not absolute //
                    await Shell.Current.GoToAsync(
                        $"profilepage?phone={Uri.EscapeDataString(phone)}&viewOnly=false");
                }
                else
                {
                    var profilePage = new ProfilePage();
                    profilePage.Phone = phone;
                    var viewOnlyProperty = profilePage.GetType().GetProperty("ViewOnlyString");
                    if (viewOnlyProperty != null && viewOnlyProperty.CanWrite)
                        viewOnlyProperty.SetValue(profilePage, "true");

                    await Navigation.PushAsync(profilePage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAuthorNameTapped error: {ex}");
                await DisplayAlert("Error", "Could not navigate to profile", "OK");
            }
        }


        // Helper: populate _phone from Preferences if empty; returns true when phone available
        private bool EnsurePhoneFromPreferences()
        {
            if (!string.IsNullOrEmpty(_phone))
                return true;
            _phone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim() ?? string.Empty;
            return !string.IsNullOrEmpty(_phone);
        }

        // Add inside the ProfilePage class
        private void CoverOverlay_Tapped(object sender, EventArgs e)
        {
            // This will now only be called from the edit icon, not the image itself
            ChangeCoverButton_Clicked(sender, e);
        }

        private void ProfileOverlay_Tapped(object sender, EventArgs e)
        {
            // This will now only be called from the edit icon, not the image itself
            ChangeProfileButton_Clicked(sender, e);
        }

        private async Task LoadUserAsync(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == phone).FirstOrDefaultAsync();

                // Get current user phone for love state checking
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                if (user == null)
                {
                    // placeholders when user not found
                    var nl = this.FindByName<Label>("NameLabel");
                    var pl = this.FindByName<Label>("PhoneLabel");
                    var gl = this.FindByName<Label>("GenderLabel");
                    var il = this.FindByName<Label>("InterestLabel");
                    var dl = this.FindByName<Label>("DobLabel");
                    var al = this.FindByName<Label>("AgeLabel");
                    var heightLbl = this.FindByName<Label>("HeightLabel");
                    var bodyTypeLbl = this.FindByName<Label>("BodyTypeLabel");
                    var ethnicityLbl = this.FindByName<Label>("EthnicityLabel");
                    var familyLbl = this.FindByName<Label>("FamilyLabel");
                    var dietLbl = this.FindByName<Label>("DietLabel");
                    var exerciseLbl = this.FindByName<Label>("ExerciseLabel");
                    var voiceStatus = this.FindByName<Label>("VoiceIntroStatus");

                    if (nl != null) nl.Text = "—";
                    if (pl != null) pl.Text = phone;
                    if (gl != null) gl.Text = "—";
                    if (il != null) il.Text = "—";
                    if (dl != null) dl.Text = "—";
                    if (al != null) al.Text = "Age: —";
                    if (heightLbl != null) heightLbl.Text = "—";
                    if (bodyTypeLbl != null) bodyTypeLbl.Text = "—";
                    if (ethnicityLbl != null) ethnicityLbl.Text = "—";
                    if (familyLbl != null) familyLbl.Text = "—";
                    if (dietLbl != null) dietLbl.Text = "—";
                    if (exerciseLbl != null) exerciseLbl.Text = "—";
                    if (voiceStatus != null) voiceStatus.IsVisible = false;

                    // clear images
                    var cover = this.FindByName<Image>("CoverImageOverlay");
                    var overlay = this.FindByName<Image>("ProfileImageOverlay");
                    if (cover != null) cover.Source = null;
                    if (overlay != null) overlay.Source = null;

                    // hide conditional UI
                    this.FindByName<HorizontalStackLayout>("TopInterestOtherLayout")?.SetValue(VisualElement.IsVisibleProperty, false);
                    this.FindByName<HorizontalStackLayout>("FavoriteMusicGenreOtherLayout")?.SetValue(VisualElement.IsVisibleProperty, false);

                    // clear posts
                    var clearCv = this.FindByName<CollectionView>("UserPostsCollectionView");
                    if (clearCv != null) clearCv.ItemsSource = null;

                    // clear media thumbnails
                    var photosLayoutClear = this.FindByName<HorizontalStackLayout>("PhotosLayout");
                    if (photosLayoutClear != null) photosLayoutClear.Children.Clear();
                    var mediaPicker = this.FindByName<Picker>("MediaCategoryPicker");
                    if (mediaPicker != null) mediaPicker.Items.Clear();

                    // update edit icons visibility
                    UpdateCoverEditIconVisibility();
                    UpdateProfileEditIconVisibility();

                    // Hide verification badge
                    IsVerified = false;

                    // HIDE compatibility UI when user not found
                    HideCompatibilityUI();
                    return;
                }

                _currentUser = user;
                _currentUserId = user.Id;

                // Set verification status
                IsVerified = user.IsVerified;

                UpdateVoiceIntroOptionsButtonVisibility();

                // Populate basic info labels
                var nameLabel = this.FindByName<Label>("NameLabel");
                var phoneLabel = this.FindByName<Label>("PhoneLabel");
                var genderLabel = this.FindByName<Label>("GenderLabel");
                var interestLabel = this.FindByName<Label>("InterestLabel");
                var dobLabel = this.FindByName<Label>("DobLabel");
                var ageLabel = this.FindByName<Label>("AgeLabel");

                if (nameLabel != null) nameLabel.Text = user.Name ?? "—";
                if (phoneLabel != null) phoneLabel.Text = user.PhoneNumber ?? "—";
                if (phoneLabel != null) phoneLabel.Text = user.PhoneNumber ?? "—";
                if (genderLabel != null) genderLabel.Text = user.Gender ?? "—";
                if (interestLabel != null) interestLabel.Text = string.IsNullOrEmpty(user.Interest) ? "—" : user.Interest;
                if (dobLabel != null) dobLabel.Text = user.DateOfBirth.ToString("yyyy-MM-dd");

                var today = DateTime.Today;
                var age = today.Year - user.DateOfBirth.Year;
                if (user.DateOfBirth > today.AddYears(-age)) age--;
                if (ageLabel != null) ageLabel.Text = $"Age: {age}";

                // Load physical attributes
                var heightLabel = this.FindByName<Label>("HeightLabel");
                var bodyTypeLabel = this.FindByName<Label>("BodyTypeLabel");
                var ethnicityLabel = this.FindByName<Label>("EthnicityLabel");
                var familyLabel = this.FindByName<Label>("FamilyLabel");
                var dietLabel = this.FindByName<Label>("DietLabel");
                var exerciseLabel = this.FindByName<Label>("ExerciseLabel");
                var voiceStatusLabel = this.FindByName<Label>("VoiceIntroStatus");

                // ========== Load compatibility and mutual interests ==========
                bool isViewingOwnProfile = string.Equals(phone?.Trim(), currentUserPhone?.Trim(), StringComparison.OrdinalIgnoreCase);

                if (!isViewingOwnProfile)
                {
                    Debug.WriteLine($"Loading compatibility for viewing user: {phone}");
                    await LoadCompatibilityAndMutualInterestsAsync(user);

                    // Show compatibility UI elements for other users
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ShowCompatibilityUI();
                    });
                }
                else
                {
                    Debug.WriteLine($"Skipping compatibility - viewing own profile");

                    // HIDE compatibility UI elements when viewing own profile
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        HideCompatibilityUI();
                    });
                }

                if (heightLabel != null)
                {
                    if (user.HeightCm.HasValue && user.HeightCm.Value > 0)
                    {
                        int feet = (int)(user.HeightCm.Value / 30.48);
                        int inches = (int)((user.HeightCm.Value % 30.48) / 2.54);
                        heightLabel.Text = $"{feet}'{inches}\" ({user.HeightCm.Value}cm)";
                    }
                    else
                    {
                        heightLabel.Text = "—";
                    }
                }

                if (bodyTypeLabel != null)
                {
                    bodyTypeLabel.Text = string.IsNullOrEmpty(user.BodyType) ? "—" : user.BodyType;
                }

                if (ethnicityLabel != null)
                {
                    if (!string.IsNullOrEmpty(user.Ethnicity) && !string.IsNullOrEmpty(user.Tribe))
                    {
                        ethnicityLabel.Text = $"{user.Ethnicity} · {user.Tribe}";
                    }
                    else if (!string.IsNullOrEmpty(user.Ethnicity))
                    {
                        ethnicityLabel.Text = user.Ethnicity;
                    }
                    else if (!string.IsNullOrEmpty(user.Tribe))
                    {
                        ethnicityLabel.Text = user.Tribe;
                    }
                    else
                    {
                        ethnicityLabel.Text = "—";
                    }
                }

                // Family / Kids Label
                if (familyLabel != null)
                {
                    string familyText = string.Empty;
                    if (!string.IsNullOrEmpty(user.KidsPreference) && !string.IsNullOrEmpty(user.HasChildren))
                    {
                        familyText = $"{user.KidsPreference} · {user.HasChildren}";
                    }
                    else if (!string.IsNullOrEmpty(user.KidsPreference))
                    {
                        familyText = user.KidsPreference;
                    }
                    else if (!string.IsNullOrEmpty(user.HasChildren))
                    {
                        familyText = user.HasChildren;
                    }
                    else
                    {
                        familyText = "—";
                    }
                    familyLabel.Text = familyText;
                }

                // Load Personality Type
                var personalityTypeLabel = this.FindByName<Label>("PersonalityTypeLabel");
                if (personalityTypeLabel != null)
                {
                    personalityTypeLabel.Text = string.IsNullOrEmpty(user.PersonalityType) ? "—" : user.PersonalityType;
                }

                // Load Love Language
                var loveLanguageLabel = this.FindByName<Label>("LoveLanguageLabel");
                if (loveLanguageLabel != null)
                {
                    loveLanguageLabel.Text = string.IsNullOrEmpty(user.LoveLanguage) ? "—" : user.LoveLanguage;
                }

                // Diet Label
                if (dietLabel != null)
                {
                    dietLabel.Text = string.IsNullOrEmpty(user.DietaryPreference) ? "—" : user.DietaryPreference;
                }

                // Exercise Label
                if (exerciseLabel != null)
                {
                    exerciseLabel.Text = string.IsNullOrEmpty(user.ExerciseFrequency) ? "—" : user.ExerciseFrequency;
                }

                if (voiceStatusLabel != null)
                {
                    if (string.IsNullOrEmpty(user.VoiceIntroPath) || !File.Exists(user.VoiceIntroPath))
                    {
                        voiceStatusLabel.Text = IsOwner ? "Tap to record" : "No voice intro";
                        voiceStatusLabel.IsVisible = true;
                    }
                    else
                    {
                        voiceStatusLabel.IsVisible = false;
                    }
                }

                // images (cover + profile)
                var coverImage = this.FindByName<Image>("CoverImageOverlay");
                var profileOverlay = this.FindByName<Image>("ProfileImageOverlay");

                if (coverImage != null)
                {
                    if (!string.IsNullOrEmpty(user.CoverImagePath) && File.Exists(user.CoverImagePath))
                        coverImage.Source = ImageSource.FromFile(user.CoverImagePath);
                    else
                        coverImage.Source = null;
                }
                if (profileOverlay != null)
                {
                    if (!string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                        profileOverlay.Source = ImageSource.FromFile(user.ProfileImagePath);
                    else
                        profileOverlay.Source = null;
                }
                UpdateCoverEditIconVisibility();
                UpdateProfileEditIconVisibility();

                // populate preferences controls
                var moodPicker = this.FindByName<Picker>("MoodPicker");
                var energyPicker = this.FindByName<Picker>("EnergyPicker");
                var countryEntry = this.FindByName<Entry>("CountryEntry");
                var stateEntry = this.FindByName<Entry>("StateEntry");
                var bioEditorInfo = this.FindByName<Editor>("BioEditorInfo");
                var drinksPicker = this.FindByName<Picker>("DrinksPicker");
                var smokesSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("SmokesSwitch");
                var petsSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("PetsSwitch");
                var religionEntry = this.FindByName<Entry>("ReligionEntry");
                var politicalEntry = this.FindByName<Entry>("PoliticalEntry");

                if (moodPicker != null && !string.IsNullOrEmpty(user.Mood))
                {
                    if (moodPicker.Items.Contains(user.Mood))
                        moodPicker.SelectedItem = user.Mood;
                }
                if (energyPicker != null && !string.IsNullOrEmpty(user.EnergyLevel))
                {
                    if (energyPicker.Items.Contains(user.EnergyLevel))
                        energyPicker.SelectedItem = user.EnergyLevel;
                }
                if (countryEntry != null) countryEntry.Text = user.Country ?? "";
                if (stateEntry != null) stateEntry.Text = user.State ?? "";
                if (bioEditorInfo != null) bioEditorInfo.Text = user.Bio ?? "";
                if (drinksPicker != null && !string.IsNullOrEmpty(user.Drinks))
                {
                    if (drinksPicker.Items.Contains(user.Drinks))
                        drinksPicker.SelectedItem = user.Drinks;
                }
                if (smokesSwitch != null) smokesSwitch.IsToggled = user.Smokes;
                if (petsSwitch != null) petsSwitch.IsToggled = user.HasPets;
                if (religionEntry != null) religionEntry.Text = user.Religion ?? "";
                if (politicalEntry != null) politicalEntry.Text = user.PoliticalViews ?? "";

                // Load "Allow Mood Search" toggle
                var moodSearchSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("MoodSearchSwitch");
                if (moodSearchSwitch != null)
                {
                    moodSearchSwitch.IsToggled = user.AllowMoodSearch;
                }

                // Load Ghost Mode + Mood Shield toggle
                var ghostSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("GhostModeMoodShieldSwitch");
                if (ghostSwitch != null)
                {
                    ghostSwitch.IsToggled = user.GhostModeMoodShield;
                }

                // ─────────────────────────────────────────────────────────────────
                // FIX: Load Phone Number Visibility toggle
                // ─────────────────────────────────────────────────────────────────
                var hidePhoneSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("HidePhoneSwitch");
                if (hidePhoneSwitch != null)
                {
                    hidePhoneSwitch.IsToggled = user.HidePhoneNumber;
                }

                // Apply phone row visibility based on saved setting
                ApplyPhoneVisibility(user);
                // ─────────────────────────────────────────────────────────────────

                // Initialize Height Picker
                var heightPicker = this.FindByName<Picker>("HeightPicker");
                if (heightPicker != null)
                {
                    heightPicker.Items.Clear();
                    for (int h = 140; h <= 210; h++)
                    {
                        heightPicker.Items.Add($"{h} cm");
                    }
                    if (user.HeightCm.HasValue && user.HeightCm.Value > 0)
                    {
                        string heightText = $"{user.HeightCm.Value} cm";
                        if (heightPicker.Items.Contains(heightText))
                            heightPicker.SelectedItem = heightText;
                    }
                }

                // Initialize Body Type Picker
                var bodyTypePicker = this.FindByName<Picker>("BodyTypePicker");
                if (bodyTypePicker != null)
                {
                    if (bodyTypePicker.Items.Count == 0)
                    {
                        bodyTypePicker.Items.Add("Slim");
                        bodyTypePicker.Items.Add("Athletic");
                        bodyTypePicker.Items.Add("Average");
                        bodyTypePicker.Items.Add("Curvy");
                        bodyTypePicker.Items.Add("Full-figured");
                        bodyTypePicker.Items.Add("Muscular");
                        bodyTypePicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.BodyType) && bodyTypePicker.Items.Contains(user.BodyType))
                        bodyTypePicker.SelectedItem = user.BodyType;
                }

                // Initialize Ethnicity Picker
                var ethnicityPicker = this.FindByName<Picker>("EthnicityPicker");
                if (ethnicityPicker != null)
                {
                    if (ethnicityPicker.Items.Count == 0)
                    {
                        ethnicityPicker.Items.Add("African");
                        ethnicityPicker.Items.Add("African American");
                        ethnicityPicker.Items.Add("Caucasian");
                        ethnicityPicker.Items.Add("Asian");
                        ethnicityPicker.Items.Add("Hispanic/Latino");
                        ethnicityPicker.Items.Add("Middle Eastern");
                        ethnicityPicker.Items.Add("Mixed");
                        ethnicityPicker.Items.Add("Other");
                        ethnicityPicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.Ethnicity) && ethnicityPicker.Items.Contains(user.Ethnicity))
                        ethnicityPicker.SelectedItem = user.Ethnicity;
                }

                // Initialize Tribe Picker
                var tribePicker = this.FindByName<Picker>("TribePicker");
                if (tribePicker != null)
                {
                    if (tribePicker.Items.Count == 0)
                    {
                        tribePicker.Items.Add("Yoruba");
                        tribePicker.Items.Add("Igbo");
                        tribePicker.Items.Add("Hausa");
                        tribePicker.Items.Add("Fulani");
                        tribePicker.Items.Add("Ijaw");
                        tribePicker.Items.Add("Kanuri");
                        tribePicker.Items.Add("Tiv");
                        tribePicker.Items.Add("Edo");
                        tribePicker.Items.Add("Nupe");
                        tribePicker.Items.Add("Other");
                        tribePicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.Tribe) && tribePicker.Items.Contains(user.Tribe))
                        tribePicker.SelectedItem = user.Tribe;
                }

                // Initialize Kids/Family Pickers
                var kidsPreferencePicker = this.FindByName<Picker>("KidsPreferencePicker");
                if (kidsPreferencePicker != null)
                {
                    if (kidsPreferencePicker.Items.Count == 0)
                    {
                        kidsPreferencePicker.Items.Add("Want children");
                        kidsPreferencePicker.Items.Add("Open to children");
                        kidsPreferencePicker.Items.Add("Don't want children");
                        kidsPreferencePicker.Items.Add("Not sure");
                        kidsPreferencePicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.KidsPreference) && kidsPreferencePicker.Items.Contains(user.KidsPreference))
                        kidsPreferencePicker.SelectedItem = user.KidsPreference;
                }

                var hasChildrenPicker = this.FindByName<Picker>("HasChildrenPicker");
                if (hasChildrenPicker != null)
                {
                    if (hasChildrenPicker.Items.Count == 0)
                    {
                        hasChildrenPicker.Items.Add("Have children");
                        hasChildrenPicker.Items.Add("Don't have children");
                        hasChildrenPicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.HasChildren) && hasChildrenPicker.Items.Contains(user.HasChildren))
                        hasChildrenPicker.SelectedItem = user.HasChildren;
                }

                // Initialize Dietary Preference Picker
                var dietaryPreferencePicker = this.FindByName<Picker>("DietaryPreferencePicker");
                if (dietaryPreferencePicker != null)
                {
                    if (dietaryPreferencePicker.Items.Count == 0)
                    {
                        dietaryPreferencePicker.Items.Add("Omnivore");
                        dietaryPreferencePicker.Items.Add("Vegetarian");
                        dietaryPreferencePicker.Items.Add("Vegan");
                        dietaryPreferencePicker.Items.Add("Pescatarian");
                        dietaryPreferencePicker.Items.Add("Halal");
                        dietaryPreferencePicker.Items.Add("Kosher");
                        dietaryPreferencePicker.Items.Add("Gluten-free");
                        dietaryPreferencePicker.Items.Add("Dairy-free");
                        dietaryPreferencePicker.Items.Add("No restrictions");
                        dietaryPreferencePicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.DietaryPreference) && dietaryPreferencePicker.Items.Contains(user.DietaryPreference))
                        dietaryPreferencePicker.SelectedItem = user.DietaryPreference;
                }

                // Initialize Exercise Frequency Picker
                var exerciseFrequencyPicker = this.FindByName<Picker>("ExerciseFrequencyPicker");
                if (exerciseFrequencyPicker != null)
                {
                    if (exerciseFrequencyPicker.Items.Count == 0)
                    {
                        exerciseFrequencyPicker.Items.Add("Daily");
                        exerciseFrequencyPicker.Items.Add("Several times a week");
                        exerciseFrequencyPicker.Items.Add("Once a week");
                        exerciseFrequencyPicker.Items.Add("Few times a month");
                        exerciseFrequencyPicker.Items.Add("Rarely");
                        exerciseFrequencyPicker.Items.Add("Never");
                        exerciseFrequencyPicker.Items.Add("Prefer not to say");
                    }
                    if (!string.IsNullOrEmpty(user.ExerciseFrequency) && exerciseFrequencyPicker.Items.Contains(user.ExerciseFrequency))
                        exerciseFrequencyPicker.SelectedItem = user.ExerciseFrequency;
                }

                // restore interest tags visual state
                var tags = (user.Interests ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var tagNames = new[] { "Travel", "Fitness", "Tech", "Music", "Coffee lover", "Gym", "Entrepreneur" };
                foreach (var tag in tagNames)
                {
                    var btnName = tag.Replace(" ", string.Empty);
                    var btn = this.FindByName<Button>($"Tag{btnName}");
                    if (btn != null)
                    {
                        bool isSelected = tags.Contains(tag);
                        btn.BackgroundColor = isSelected ? Color.FromArgb("#3B82F6") : Color.FromArgb("#EEE");
                        btn.TextColor = isSelected ? Colors.White : Colors.Black;
                        btn.BindingContext = isSelected;
                    }
                }

                // populate Interests panel: render chips from user.Interests
                var interestsLayout = this.FindByName<HorizontalStackLayout>("InterestsChipsLayout");
                if (interestsLayout != null)
                {
                    interestsLayout.Children.Clear();
                    foreach (var t in tags)
                    {
                        var chip = new Border
                        {
                            Padding = new Thickness(8, 4),
                            BackgroundColor = Color.FromArgb("#EEE"),
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                            Content = new Label { Text = t, FontSize = 12, TextColor = Colors.Black }
                        };
                        interestsLayout.Children.Add(chip);
                    }
                }

                // populate music genres and favorite artists
                var musicLayout = this.FindByName<HorizontalStackLayout>("MusicGenresLayout");
                var favArtistsLabel = this.FindByName<Label>("FavoriteArtistsLabel");
                if (musicLayout != null)
                {
                    musicLayout.Children.Clear();
                    var genres = (user.MusicGenres ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var g in genres)
                    {
                        var chip = new Border
                        {
                            Padding = new Thickness(8, 4),
                            BackgroundColor = Color.FromArgb("#EEE"),
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                            Content = new Label { Text = g, FontSize = 12, TextColor = Colors.Black }
                        };
                        musicLayout.Children.Add(chip);
                    }
                }
                if (favArtistsLabel != null) favArtistsLabel.Text = string.IsNullOrEmpty(user.FavoriteArtists) ? "—" : user.FavoriteArtists;

                // populate editable Interests fields
                var musicEntry = this.FindByName<Entry>("MusicGenresEntry");
                var favArtistsEntry = this.FindByName<Entry>("FavoriteArtistsEntry");
                var favMoviesEntry = this.FindByName<Entry>("FavoriteMoviesEntry");
                var favBooksEntry = this.FindByName<Entry>("FavoriteBooksEntry");
                var languagesEntry = this.FindByName<Entry>("LanguagesEntry");
                var occupationEntry = this.FindByName<Entry>("OccupationEntry");
                var educationEntry = this.FindByName<Entry>("EducationEntry");
                var promptsEditor = this.FindByName<Editor>("PromptsEditor");
                var dealbreakersEntry = this.FindByName<Entry>("DealbreakersEntry");

                if (musicEntry != null) musicEntry.Text = user.MusicGenres ?? "";
                if (favArtistsEntry != null) favArtistsEntry.Text = user.FavoriteArtists ?? "";
                if (favMoviesEntry != null) favMoviesEntry.Text = user.FavoriteMovies ?? "";
                if (favBooksEntry != null) favBooksEntry.Text = user.FavoriteBooks ?? "";
                if (languagesEntry != null) languagesEntry.Text = user.Languages ?? "";
                if (occupationEntry != null) occupationEntry.Text = user.Occupation ?? "";
                if (educationEntry != null) educationEntry.Text = user.Education ?? "";
                if (promptsEditor != null) promptsEditor.Text = user.Prompts ?? "";
                if (dealbreakersEntry != null) dealbreakersEntry.Text = user.Dealbreakers ?? "";

                // populate top selections controls
                var topInterestPicker = this.FindByName<Picker>("TopInterestPicker");
                var topArtistEntry = this.FindByName<Entry>("TopArtistEntry");
                var topMovieEntry = this.FindByName<Entry>("TopMovieEntry");
                var sexualPicker = this.FindByName<Picker>("SexualOrientationPicker");
                var topInterestOtherLayout = this.FindByName<HorizontalStackLayout>("TopInterestOtherLayout");

                if (topInterestPicker != null && !string.IsNullOrEmpty(user.TopInterest))
                {
                    if (topInterestPicker.Items.Contains(user.TopInterest))
                        topInterestPicker.SelectedItem = user.TopInterest;
                    else
                    {
                        topInterestPicker.Items.Add(user.TopInterest);
                        topInterestPicker.SelectedItem = user.TopInterest;
                    }
                }
                if (topInterestOtherLayout != null)
                {
                    topInterestOtherLayout.IsVisible = false;
                }
                if (topArtistEntry != null) topArtistEntry.Text = user.TopArtist ?? "";
                if (topMovieEntry != null) topMovieEntry.Text = user.TopMovie ?? "";
                if (sexualPicker != null && !string.IsNullOrEmpty(user.SexualOrientation))
                {
                    if (sexualPicker.Items.Contains(user.SexualOrientation))
                        sexualPicker.SelectedItem = user.SexualOrientation;
                }

                // FavoriteMusicGenre picker
                var favoriteMusicGenrePicker = this.FindByName<Picker>("FavoriteMusicGenrePicker");
                if (favoriteMusicGenrePicker != null && !string.IsNullOrEmpty(user.FavoriteMusicGenre))
                {
                    if (favoriteMusicGenrePicker.Items.Contains(user.FavoriteMusicGenre))
                        favoriteMusicGenrePicker.SelectedItem = user.FavoriteMusicGenre;
                    else
                    {
                        favoriteMusicGenrePicker.Items.Add(user.FavoriteMusicGenre);
                        favoriteMusicGenrePicker.SelectedItem = user.FavoriteMusicGenre;
                    }
                }
                var favoriteMusicGenreOtherLayout = this.FindByName<HorizontalStackLayout>("FavoriteMusicGenreOtherLayout");
                if (favoriteMusicGenreOtherLayout != null) favoriteMusicGenreOtherLayout.IsVisible = false;
                var bestMusicEntry = this.FindByName<Entry>("BestMusicEntry");
                if (bestMusicEntry != null) bestMusicEntry.Text = user.BestMusic ?? "";

                // --- load posts authored by this user and bind to UserPostsCollectionView ---
                try
                {
                    var allPosts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                    // Exclude status-only posts
                    var userPosts = allPosts
                        .Where(p => string.Equals(p.AuthorPhone ?? string.Empty, phone ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                            && string.IsNullOrEmpty(p.StatusImagePath))
                        .OrderByDescending(p => p.CreatedAt)
                        .ToList();

                    // Initialize UI-specific fields AND love state
                    foreach (var p in userPosts)
                    {
                        p.IsExpanded = false;
                        p.UpdateDisplayContent(200);

                        // 🔥 CRITICAL: Set author verification status from the user object
                        p.IsAuthorVerified = user.IsVerified;

                        // 🔥 CRITICAL FIX: Set the author profile image path from the user object
                        p.AuthorProfileImagePath = user.ProfileImagePath;

                        // 🔥 Also set author name properly for display
                        p.AuthorDisplayName = user.Name ?? user.PhoneNumber;

                        // 🔥 Set author phone for navigation
                        p.AuthorPhone = $"{user.Name} · {user.PhoneNumber}";

                        if (!string.IsNullOrEmpty(currentUserPhone))
                        {
                            p.IsLovedByCurrentUser = p.LovedBy.Contains(currentUserPhone);
                            p.IsSavedByCurrentUser = SavePostService.IsPostSaved(p.Id, currentUserPhone);
                            p.IsSparkedByCurrentUser = p.SparkedBy.Contains(currentUserPhone);
                        }
                    }

                    var postsCv = this.FindByName<CollectionView>("UserPostsCollectionView");
                    if (postsCv != null)
                    {
                        postsCv.ItemsSource = null;
                        postsCv.ItemsSource = userPosts;
                    }

                    PopulateMediaTab(user, userPosts);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading posts: {ex.Message}");
                    PopulateMediaTab(user, null);
                }
                // ========== LOAD ALL DATA FOR BOTH OWNER AND VIEW-ONLY USERS ==========
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await LoadUserPhotosAsync(user.Id);
                        await LoadUserPromptsAsync(user.Id);
                        await LoadUserDateIdeasAsync(user.Id);
                        await LoadUserEventsAsync(user.Id);
                        await LoadProfileStatsAsync(user.Id);

                        // Load endorsements
                        await LoadEndorsementsAsync(user.Id);

                        // Load pending endorsements
                        await LoadPendingEndorsementsAsync();

                        System.Diagnostics.Debug.WriteLine($"Loaded all data for user {user.Name} (View-only: {_viewOnly})");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading user data: {ex.Message}");
                    }
                });

                // Load new profile fields
                await LoadNewProfileFields(user);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Failed to load profile: " + ex.Message, "OK");
            }
        }
        private void ShowCompatibilityUI()
        {
            var compatibilityCard = this.FindByName<Border>("CompatibilityCard");
            var mutualInterestsCard = this.FindByName<Border>("MutualInterestsCard");

            if (compatibilityCard != null) compatibilityCard.IsVisible = true;
            if (mutualInterestsCard != null) mutualInterestsCard.IsVisible = true;
        }

        private void HideCompatibilityUI()
        {
            var compatibilityCard = this.FindByName<Border>("CompatibilityCard");
            var mutualInterestsCard = this.FindByName<Border>("MutualInterestsCard");

            if (compatibilityCard != null) compatibilityCard.IsVisible = false;
            if (mutualInterestsCard != null) mutualInterestsCard.IsVisible = false;
        }

        // New methods for loading data
        private async Task LoadUserPhotosAsync(int userId)
        {
            _userPhotos = await ProfileDataService.GetUserPhotosAsync(userId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdatePhotosDisplay();

                if (_currentUser != null)
                {
                    var postsCv = this.FindByName<CollectionView>("UserPostsCollectionView");
                    var allPosts = postsCv?.ItemsSource as IEnumerable<Lock.Models.Post>;
                    PopulateMediaTab(_currentUser, allPosts);
                }
            });
        }


        private async Task LoadUserPromptsAsync(int userId)
        {
            _userPrompts = await ProfileDataService.GetUserPromptsAsync(userId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var promptsCv = this.FindByName<CollectionView>("PromptsCollectionView");
                if (promptsCv != null)
                {
                    promptsCv.ItemsSource = _userPrompts;
                    // Dynamically size based on item count so all items are visible inside ScrollView
                    promptsCv.HeightRequest = Math.Max(200, _userPrompts.Count * 120);
                }
            });
        }

        private async Task LoadUserDateIdeasAsync(int userId)
        {
            _userDateIdeas = await ProfileDataService.GetUserDateIdeasAsync(userId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var datesCv = this.FindByName<CollectionView>("DateIdeasCollectionView");
                if (datesCv != null)
                {
                    datesCv.ItemsSource = _userDateIdeas;
                    // Dynamically size based on item count
                    datesCv.HeightRequest = Math.Max(200, _userDateIdeas.Count * 140);
                }
            });
        }


        // Add this to LoadUserAsync (after loading user data)
        private async Task LoadCompatibilityAndMutualInterestsAsync(User targetUser)
        {
            try
            {
                Debug.WriteLine($"=== LoadCompatibilityAndMutualInterestsAsync START ===");

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                Debug.WriteLine($"Current user phone: {currentUserPhone}");
                Debug.WriteLine($"Target user phone: {targetUser?.PhoneNumber}");

                // Check if viewing own profile by comparing phone numbers directly
                bool isOwnProfile = string.Equals(targetUser?.PhoneNumber?.Trim(), currentUserPhone?.Trim(), StringComparison.OrdinalIgnoreCase);

                if (isOwnProfile)
                {
                    Debug.WriteLine("Viewing own profile, skipping compatibility load");
                    return;
                }

                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    Debug.WriteLine("Current user phone is empty, skipping");
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var currentUser = await db.Table<User>().Where(u => u.PhoneNumber == currentUserPhone).FirstOrDefaultAsync();

                Debug.WriteLine($"Current user found: {currentUser?.Name ?? "null"}");
                Debug.WriteLine($"Target user: {targetUser?.Name ?? "null"}");
                Debug.WriteLine($"Target user interests: {targetUser?.Interests ?? "null"}");

                if (currentUser != null && targetUser != null)
                {
                    // Calculate compatibility score
                    _compatibilityScore = await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, targetUser);
                    Debug.WriteLine($"Compatibility score calculated: {_compatibilityScore}%");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var scoreLabel = this.FindByName<Label>("CompatibilityScoreLabel");
                        if (scoreLabel != null)
                        {
                            scoreLabel.Text = $"{_compatibilityScore}%";
                            Debug.WriteLine($"Set score label to: {_compatibilityScore}%");
                        }
                        else
                        {
                            Debug.WriteLine("CompatibilityScoreLabel not found!");
                        }

                        var matchLabel = this.FindByName<Label>("CompatibilityMatchLabel");
                        var colorCard = this.FindByName<Border>("CompatibilityCard");

                        if (matchLabel != null && colorCard != null)
                        {
                            // Score levels with appropriate messages and colors
                            if (_compatibilityScore >= 90)
                            {
                                matchLabel.Text = "Perfect Match ??";
                                colorCard.BackgroundColor = Color.FromArgb("#9B59B6"); // Purple
                            }
                            else if (_compatibilityScore >= 80)
                            {
                                matchLabel.Text = "Excellent Match ??";
                                colorCard.BackgroundColor = Color.FromArgb("#10B981"); // Green
                            }
                            else if (_compatibilityScore >= 70)
                            {
                                matchLabel.Text = "Great Match ??";
                                colorCard.BackgroundColor = Color.FromArgb("#3B82F6"); // Blue
                            }
                            else if (_compatibilityScore >= 60)
                            {
                                matchLabel.Text = "Good Match ??";
                                colorCard.BackgroundColor = Color.FromArgb("#008080"); // Teal
                            }
                            else if (_compatibilityScore >= 50)
                            {
                                matchLabel.Text = "Decent Match ??";
                                colorCard.BackgroundColor = Color.FromArgb("#F59E0B"); // Orange
                            }
                            else if (_compatibilityScore >= 40)
                            {
                                matchLabel.Text = "Potential Match ?";
                                colorCard.BackgroundColor = Color.FromArgb("#FF3B6F"); // Red
                            }
                            else if (_compatibilityScore >= 30)
                            {
                                matchLabel.Text = "Getting There ??";
                                colorCard.BackgroundColor = Color.FromArgb("#8B5CF6"); // Violet
                            }
                            else if (_compatibilityScore >= 20)
                            {
                                matchLabel.Text = "Early Days ??";
                                colorCard.BackgroundColor = Color.FromArgb("#6B7280"); // Gray
                            }
                            else if (_compatibilityScore >= 10)
                            {
                                matchLabel.Text = "Room to Grow ??";
                                colorCard.BackgroundColor = Color.FromArgb("#78716C"); // Warm Gray
                            }
                            else
                            {
                                matchLabel.Text = "New Connection ??";
                                colorCard.BackgroundColor = Color.FromArgb("#9CA3AF"); // Light Gray
                            }
                            Debug.WriteLine($"Set match label to: {matchLabel.Text}");
                        }
                        else
                        {
                            Debug.WriteLine($"matchLabel null? {matchLabel == null}, colorCard null? {colorCard == null}");
                        }
                    });

                    // Load mutual interests
                    await LoadMutualInterestsAsync(currentUser, targetUser);

                    // Record profile view
                    await ProfileViewService.RecordProfileViewAsync(
                        currentUser.Id, currentUserPhone,
                        targetUser.Id, targetUser.PhoneNumber);
                }
                else
                {
                    Debug.WriteLine($"currentUser null? {currentUser == null}, targetUser null? {targetUser == null}");
                }

                Debug.WriteLine($"=== LoadCompatibilityAndMutualInterestsAsync END ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadCompatibilityAndMutualInterestsAsync error: {ex}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        private async Task LoadMutualInterestsAsync(User currentUser, User targetUser)
        {
            try
            {
                Debug.WriteLine($"=== LoadMutualInterestsAsync START ===");

                var currentInterests = (currentUser.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim()).ToList();
                var targetInterests = (targetUser.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim()).ToList();

                Debug.WriteLine($"Current user interests: {string.Join(", ", currentInterests)}");
                Debug.WriteLine($"Target user interests: {string.Join(", ", targetInterests)}");

                _allMutualInterests = currentInterests.Intersect(targetInterests).ToList();
                int mutualCount = _allMutualInterests.Count;

                Debug.WriteLine($"Mutual interests found: {mutualCount}");
                Debug.WriteLine($"Mutual interests list: {string.Join(", ", _allMutualInterests)}");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Update count label
                    var countLabel = this.FindByName<Label>("MutualInterestsCountLabel");
                    if (countLabel != null)
                    {
                        countLabel.Text = $"({mutualCount})";
                        Debug.WriteLine($"Set mutual count label to: ({mutualCount})");
                    }
                    else
                    {
                        Debug.WriteLine("MutualInterestsCountLabel not found!");
                    }

                    // Update collapsed layout (first 3 interests as chips)
                    var collapsedLayout = this.FindByName<FlexLayout>("MutualInterestsCollapsedLayout");
                    if (collapsedLayout != null)
                    {
                        collapsedLayout.Children.Clear();

                        if (mutualCount == 0)
                        {
                            var emptyLabel = new Label
                            {
                                Text = "No mutual interests yet",
                                FontSize = 11,
                                TextColor = Color.FromArgb("#AAAAAA"),
                                VerticalOptions = LayoutOptions.Center
                            };
                            collapsedLayout.Children.Add(emptyLabel);
                            Debug.WriteLine("Added 'No mutual interests yet' message");

                            // Hide expand button if no interests
                            var expandButton = this.FindByName<Border>("ExpandMutualInterestsButton");
                            if (expandButton != null) expandButton.IsVisible = false;
                        }
                        else
                        {
                            // Show first 3 interests as chips
                            var firstThree = _allMutualInterests.Take(3).ToList();
                            foreach (var interest in firstThree)
                            {
                                var chip = CreateInterestChip(interest);
                                collapsedLayout.Children.Add(chip);
                                Debug.WriteLine($"Added interest chip: {interest}");
                            }

                            // Show expand button only if more than 3 interests
                            var expandButton = this.FindByName<Border>("ExpandMutualInterestsButton");
                            if (expandButton != null) expandButton.IsVisible = mutualCount > 3;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MutualInterestsCollapsedLayout not found!");
                    }

                    // Update expanded layout (ALL interests with icons and descriptions)
                    var expandedLayout = this.FindByName<VerticalStackLayout>("MutualInterestsExpandedLayout");
                    if (expandedLayout != null)
                    {
                        expandedLayout.Children.Clear();

                        if (mutualCount > 0)
                        {
                            foreach (var interest in _allMutualInterests)
                            {
                                var interestItem = CreateInterestListItem(interest);
                                expandedLayout.Children.Add(interestItem);
                                Debug.WriteLine($"Added expanded interest item: {interest}");
                            }
                        }
                        else
                        {
                            var emptyItem = new Label
                            {
                                Text = "No mutual interests found",
                                FontSize = 12,
                                TextColor = Color.FromArgb("#AAAAAA"),
                                HorizontalOptions = LayoutOptions.Center,
                                Margin = new Thickness(0, 20, 0, 20)
                            };
                            expandedLayout.Children.Add(emptyItem);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("MutualInterestsExpandedLayout not found!");
                    }
                });

                Debug.WriteLine($"=== LoadMutualInterestsAsync END ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadMutualInterestsAsync error: {ex}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        private Border CreateInterestChip(string interest)
        {
            return new Border
            {
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                BackgroundColor = Color.FromArgb("#00808020"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = new Label
                {
                    Text = interest,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#008080")
                }
            };
        }

        private Border CreateInterestListItem(string interest)
        {
            // Get icon and description based on interest
            var (icon, description) = GetInterestDetails(interest);

            // Create the icon label
            var iconLabel = new Label
            {
                Text = icon,
                FontSize = 18,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(iconLabel, 0);

            // Create the content stack
            var contentStack = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
        {
            new Label
            {
                Text = interest,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#008080")
            },
            new Label
            {
                Text = description,
                FontSize = 11,
                TextColor = Color.FromArgb("#888880"),
                IsVisible = !string.IsNullOrEmpty(description)
            }
        }
            };
            Grid.SetColumn(contentStack, 1);

            var item = new Border
            {
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 0, 0, 4),
                BackgroundColor = Color.FromArgb("#00808008"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
                    Children = { iconLabel, contentStack }
                }
            };

            return item;
        }

        private (string icon, string description) GetInterestDetails(string interest)
        {
            var details = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Travel", ("??", "Love exploring new places and cultures") },
                { "Fitness", ("??", "Staying active and healthy") },
                { "Tech", ("??", "Passionate about technology and innovation") },
                { "Music", ("??", "Live for the rhythm and melodies") },
                { "Coffee lover", ("?", "Coffee dates and cozy cafes") },
                { "Gym", ("???", "Sweat, smile, repeat") },
                { "Entrepreneur", ("??", "Building dreams and businesses") },
                { "Reading", ("??", "Lost in good books") },
                { "Cooking", ("??", "Creating delicious moments") },
                { "Art", ("??", "Expressing through creativity") },
                { "Dancing", ("??", "Moving to the beat") },
                { "Photography", ("??", "Capturing beautiful moments") },
                { "Hiking", ("??", "Nature and adventure seeker") },
                { "Meditation", ("??", "Finding inner peace") },
                { "Gaming", ("??", "Leveling up together") },
                { "Movies", ("??", "Film enthusiast") },
                { "Food", ("??", "Foodie adventures") },
                { "Beach", ("???", "Sun, sand, and sea") },
                { "Pets", ("??", "Animal lover") }
            };

            if (details.TryGetValue(interest, out var value))
            {
                return value;
            }

            // Default for unknown interests
            return ("??", "Something we both enjoy");
        }
        private void PopulateMediaTab(Lock.Models.User user, IEnumerable<Lock.Models.Post>? userPosts)
        {
            var photosLayout = this.FindByName<HorizontalStackLayout>("PhotosLayout");
            var mediaPicker = this.FindByName<Picker>("MediaCategoryPicker");
            var noMediaLabel = this.FindByName<Label>("NoMediaLabel");

            if (photosLayout == null || mediaPicker == null) return;

            // Clear and rebuild
            photosLayout.Children.Clear();
            mediaPicker.Items.Clear();
            _mediaByCategory.Clear();

            _mediaByCategory["All"] = new List<string>();

            // FIXED: This is a regular method, NOT a collection initializer
            void Add(string category, string path)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                if (!_mediaByCategory.ContainsKey(category))
                    _mediaByCategory[category] = new List<string>();

                if (!_mediaByCategory[category].Contains(path))
                    _mediaByCategory[category].Add(path);

                if (!_mediaByCategory["All"].Contains(path))
                    _mediaByCategory["All"].Add(path);
            }

            // Add profile and cover photos from UserPhotos table
            foreach (var photo in _userPhotos)
            {
                if (File.Exists(photo.ImagePath))
                {
                    Add(photo.Category, photo.ImagePath);
                }
            }

            // Add post images (existing logic)
            if (userPosts != null)
            {
                foreach (var post in userPosts)
                {
                    if (!string.IsNullOrEmpty(post.StatusImagePath))
                        continue;

                    var cat = string.IsNullOrWhiteSpace(post.Category) ? "Uncategorized" : post.Category.Trim();
                    var imgs = post.ImagePathsList ?? Array.Empty<string>();
                    foreach (var p in imgs)
                    {
                        Add(cat, p);
                    }
                }
            }

            // Populate picker
            mediaPicker.Items.Add("All");
            var otherCats = _mediaByCategory.Keys.Where(k => k != "All").OrderBy(k => k);
            foreach (var c in otherCats)
                mediaPicker.Items.Add(c);

            if (mediaPicker.Items.Count > 0)
            {
                mediaPicker.SelectedIndex = 0;
                ShowMediaForCategory("All");  // ← already there but ensure it's called AFTER layout

                // Force layout pass then show
                MainThread.BeginInvokeOnMainThread(() => ShowMediaForCategory("All"));
            }
        }



        private void ToggleMutualInterestsExpand(object sender, EventArgs e)
        {
            _mutualInterestsExpanded = !_mutualInterestsExpanded;

            var expandedContainer = this.FindByName<Border>("MutualInterestsExpandedContainer");
            var expandIcon = this.FindByName<Label>("ExpandMutualInterestsIcon");

            if (expandedContainer != null)
            {
                expandedContainer.IsVisible = _mutualInterestsExpanded;
            }

            if (expandIcon != null)
            {
                expandIcon.Text = _mutualInterestsExpanded ? "?" : "?";
            }
        }

        private async Task LoadProfileViewsAsync(int userId)
        {
            try
            {
                var totalViews = await ProfileViewService.GetProfileViewCountAsync(userId);
                var recentViews = await ProfileViewService.GetRecentProfileViewsAsync(userId, 6);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var totalLabel = this.FindByName<Label>("TotalProfileViewsLabel");
                    if (totalLabel != null) totalLabel.Text = totalViews.ToString();

                    var viewersLayout = this.FindByName<HorizontalStackLayout>("RecentViewersLayout");
                    if (viewersLayout == null) return;

                    viewersLayout.Children.Clear();

                    foreach (var view in recentViews)
                    {
                        var viewerBorder = new Border
                        {
                            WidthRequest = 40,
                            HeightRequest = 40,
                            BackgroundColor = Color.FromArgb("#F0EDE8"),
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = 20 },
                            Content = new Label
                            {
                                Text = view.ViewerUserPhone.Length > 0 ? view.ViewerUserPhone.Substring(0, 1).ToUpper() : "?",
                                FontSize = 16,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#008080"),
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            }
                        };
                        viewersLayout.Children.Add(viewerBorder);
                    }

                    if (recentViews.Count == 0)
                    {
                        var emptyLabel = new Label
                        {
                            Text = "No recent viewers",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#AAAAAA"),
                            VerticalOptions = LayoutOptions.Center
                        };
                        viewersLayout.Children.Add(emptyLabel);
                    }
                });

                // Mark views as seen
                await ProfileViewService.MarkViewsAsSeenAsync(userId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfileViewsAsync error: {ex}");
            }
        }

        private async Task LoadEndorsementsAsync(int userId)
        {
            try
            {
                _endorsements = await EndorsementService.GetEndorsementsForUserAsync(userId, 10);

                // Remove any duplicate endorsements from the same friend (keep only the most recent)
                var uniqueEndorsements = _endorsements
                    .GroupBy(e => e.EndorserUserPhone)  // Changed from EndorserPhone to EndorserUserPhone
                    .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
                    .ToList();

                if (uniqueEndorsements.Count != _endorsements.Count)
                {
                    _endorsements = uniqueEndorsements;
                    // Optionally clean up duplicates in database
                    await CleanupDuplicateEndorsementsAsync(userId);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var endorsementsCv = this.FindByName<CollectionView>("EndorsementsCollectionView");
                    if (endorsementsCv != null)
                    {
                        endorsementsCv.ItemsSource = _endorsements;
                    }

                    var acceptedCountLabel = this.FindByName<Label>("AcceptedEndorsementsCountLabel");
                    if (acceptedCountLabel != null)
                    {
                        acceptedCountLabel.Text = $"({_endorsements.Count})";
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadEndorsementsAsync error: {ex}");
            }
        }

        private async Task CleanupDuplicateEndorsementsAsync(int userId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get all endorsements for this user
                var allEndorsements = await db.Table<UserEndorsement>()
                    .Where(e => e.TargetUserId == userId)  // Changed from UserId to TargetUserId
                    .ToListAsync();

                // Group by endorser phone and keep only the most recent
                var toDelete = allEndorsements
                    .GroupBy(e => e.EndorserUserPhone)  // Changed from EndorserPhone to EndorserUserPhone
                    .SelectMany(g => g.OrderByDescending(e => e.CreatedAt).Skip(1))
                    .ToList();

                foreach (var endorsement in toDelete)
                {
                    await db.DeleteAsync(endorsement);
                    Debug.WriteLine($"Deleted duplicate endorsement ID: {endorsement.Id}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupDuplicateEndorsementsAsync error: {ex}");
            }
        }


        // When accepting an endorsement request
        private async Task<bool> CanAcceptEndorsementAsync(string endorserPhone, int targetUserId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if this endorser has already endorsed this user
                var existingEndorsement = await db.Table<UserEndorsement>()
                    .Where(e => e.EndorserUserPhone == endorserPhone && e.TargetUserId == targetUserId)
                    .FirstOrDefaultAsync();

                if (existingEndorsement != null)
                {
                    Debug.WriteLine($"User {endorserPhone} has already endorsed user {targetUserId}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CanAcceptEndorsementAsync error: {ex}");
                return false;
            }
        }
        private async void AddEndorsementButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // First check if user has a registered phone number
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Error", "Please register your phone number first", "OK");
                    return;
                }

                // Get current user details
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var currentUser = await db.Table<User>().Where(u => u.PhoneNumber == currentUserPhone).FirstOrDefaultAsync();
                if (currentUser == null)
                {
                    await DisplayAlert("Error", "User not found. Please complete your profile.", "OK");
                    return;
                }

                // Create the input page
                var inputPage = new ContentPage
                {
                    Title = "Ask for Endorsement",
                    BackgroundColor = Color.FromArgb("#1E1E1E")
                };

                // Phone number entry
                var phoneEntry = new Entry
                {
                    Placeholder = "Enter friend's phone number",
                    FontSize = 14,
                    Keyboard = Keyboard.Telephone,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    TextColor = Colors.White,
                    PlaceholderColor = Color.FromArgb("#888880")
                };

                // Friend info card with profile image
                var friendCard = new Border
                {
                    Padding = 12,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    IsVisible = false,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                // Profile image frame
                var friendProfileFrame = new Frame
                {
                    HeightRequest = 50,
                    WidthRequest = 50,
                    CornerRadius = 25,
                    Padding = 0,
                    IsClippedToBounds = true,
                    BackgroundColor = Color.FromArgb("#1E1C1A"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var friendProfileImage = new Image
                {
                    HeightRequest = 50,
                    WidthRequest = 50,
                    Aspect = Aspect.AspectFill
                };

                var profilePlaceholder = new Label
                {
                    Text = "",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#008080"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var profileContainer = new Grid();
                profileContainer.Children.Add(friendProfileImage);
                profileContainer.Children.Add(profilePlaceholder);
                friendProfileFrame.Content = profileContainer;

                var friendNameLabel = new Label
                {
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    VerticalOptions = LayoutOptions.Center
                };

                var friendPhoneLabel = new Label
                {
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888880"),
                    VerticalOptions = LayoutOptions.Center
                };

                var friendGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
                    ColumnSpacing = 12
                };
                friendGrid.Children.Add(friendProfileFrame);

                var friendTextStack = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { friendNameLabel, friendPhoneLabel }
                };
                friendGrid.Children.Add(friendTextStack);
                Grid.SetColumn(friendTextStack, 1);

                friendCard.Content = friendGrid;

                // Friend info label (for errors)
                var friendInfoLabel = new Label
                {
                    Text = "",
                    FontSize = 14,
                    IsVisible = false,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                // Loading indicator
                var loadingIndicator = new ActivityIndicator
                {
                    IsVisible = false,
                    Color = Color.FromArgb("#008080")
                };

                // Testimonial templates - plain text, no emojis
                var templatesPicker = new Picker
                {
                    Title = "Select a template",
                    FontSize = 13,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    TextColor = Colors.White,
                    TitleColor = Color.FromArgb("#888880"),
                    IsEnabled = false
                };

                var testimonialTemplates = new List<string>
        {
            "Write my own",
            "One of the most genuine and kind-hearted people I know. Always there when you need them.",
            "Such an amazing friend. Great sense of humor, loyal, and always brings positive energy.",
            "Incredibly thoughtful, reliable, and fun to be around. Anyone would be lucky to have them.",
            "A true gem. Honest, caring, and has the biggest heart. Makes everyone feel welcome.",
            "Such a vibe. Smart, ambitious, and knows how to have a good time. 10 out of 10 would recommend.",
            "Most trustworthy person I know. Always keeps their word and stands by their friends.",
            "Driven, passionate, and has such a warm spirit. Truly one of the best people I have met.",
            "Strong, resilient, and always lifts others up. An absolute blessing to have as a friend."
        };

                foreach (var template in testimonialTemplates)
                {
                    templatesPicker.Items.Add(template);
                }
                templatesPicker.SelectedIndex = 0;

                var testimonialEditor = new Editor
                {
                    Placeholder = "What would you say about this person?",
                    HeightRequest = 100,
                    FontSize = 14,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    TextColor = Colors.White,
                    PlaceholderColor = Color.FromArgb("#888880"),
                    IsEnabled = false
                };

                templatesPicker.SelectedIndexChanged += (s, args) =>
                {
                    if (templatesPicker.SelectedIndex > 0)
                    {
                        testimonialEditor.Text = testimonialTemplates[templatesPicker.SelectedIndex];
                    }
                    else
                    {
                        testimonialEditor.Text = "";
                    }
                };

                // Rating picker - clean Unicode stars only
                var ratingPicker = new Picker
                {
                    Title = "Rating (1-5 stars)",
                    FontSize = 14,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    TextColor = Colors.White,
                    TitleColor = Color.FromArgb("#888880"),
                    IsEnabled = false
                };

                for (int i = 1; i <= 5; i++)
                {
                    string filled = new string('\u2605', i);
                    string empty = new string('\u2606', 5 - i);
                    ratingPicker.Items.Add($"{filled}{empty} - {i} out of 5");
                }
                ratingPicker.SelectedIndex = 4;

                User foundFriend = null;
                CancellationTokenSource searchCts = null;

                // Real-time search as user types
                phoneEntry.TextChanged += async (s, args) =>
                {
                    searchCts?.Cancel();
                    searchCts = new CancellationTokenSource();
                    var token = searchCts.Token;
                    var phoneNumber = phoneEntry.Text?.Trim();

                    if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 10)
                    {
                        friendCard.IsVisible = false;
                        friendInfoLabel.IsVisible = false;
                        templatesPicker.IsEnabled = false;
                        testimonialEditor.IsEnabled = false;
                        ratingPicker.IsEnabled = false;
                        foundFriend = null;
                        return;
                    }

                    loadingIndicator.IsVisible = true;
                    loadingIndicator.IsRunning = true;
                    friendCard.IsVisible = false;
                    friendInfoLabel.IsVisible = false;

                    try
                    {
                        await Task.Delay(500, token);
                        if (token.IsCancellationRequested) return;

                        await DatabaseService.InitializeAsync();
                        var dbConnection = DatabaseService.GetConnection();

                        foundFriend = await dbConnection.Table<User>()
                            .Where(u => u.PhoneNumber == phoneNumber)
                            .FirstOrDefaultAsync();

                        if (token.IsCancellationRequested) return;

                        if (foundFriend != null)
                        {
                            friendNameLabel.Text = foundFriend.Name;
                            friendPhoneLabel.Text = foundFriend.PhoneNumber;

                            if (!string.IsNullOrEmpty(foundFriend.ProfileImagePath) && File.Exists(foundFriend.ProfileImagePath))
                            {
                                friendProfileImage.Source = ImageSource.FromFile(foundFriend.ProfileImagePath);
                                friendProfileImage.IsVisible = true;
                                profilePlaceholder.IsVisible = false;
                            }
                            else
                            {
                                profilePlaceholder.Text = foundFriend.Name?.Length > 0
                                    ? foundFriend.Name.Substring(0, 1).ToUpper()
                                    : "";
                                friendProfileImage.IsVisible = false;
                                profilePlaceholder.IsVisible = true;
                            }

                            friendCard.IsVisible = true;
                            friendInfoLabel.IsVisible = false;
                            templatesPicker.IsEnabled = true;
                            testimonialEditor.IsEnabled = true;
                            ratingPicker.IsEnabled = true;
                        }
                        else
                        {
                            friendCard.IsVisible = false;
                            friendInfoLabel.Text = "Phone number not registered on Lock";
                            friendInfoLabel.TextColor = Color.FromArgb("#FF4444");
                            friendInfoLabel.IsVisible = true;
                            templatesPicker.IsEnabled = false;
                            testimonialEditor.IsEnabled = false;
                            ratingPicker.IsEnabled = false;
                            foundFriend = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Search error: {ex}");
                        if (!token.IsCancellationRequested)
                        {
                            friendInfoLabel.Text = "Error searching for user";
                            friendInfoLabel.TextColor = Color.FromArgb("#FF4444");
                            friendInfoLabel.IsVisible = true;
                        }
                    }
                    finally
                    {
                        if (!token.IsCancellationRequested)
                        {
                            loadingIndicator.IsVisible = false;
                            loadingIndicator.IsRunning = false;
                        }
                    }
                };

                // Current user avatar
                var currentUserFrame = new Frame
                {
                    HeightRequest = 44,
                    WidthRequest = 44,
                    CornerRadius = 22,
                    Padding = 0,
                    IsClippedToBounds = true,
                    BackgroundColor = Color.FromArgb("#2A2A2A"),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center
                };

                if (!string.IsNullOrEmpty(currentUser.ProfileImagePath) && File.Exists(currentUser.ProfileImagePath))
                {
                    currentUserFrame.Content = new Image
                    {
                        Source = ImageSource.FromFile(currentUser.ProfileImagePath),
                        HeightRequest = 44,
                        WidthRequest = 44,
                        Aspect = Aspect.AspectFill
                    };
                }
                else
                {
                    currentUserFrame.Content = new Label
                    {
                        Text = currentUser.Name?.Length > 0 ? currentUser.Name.Substring(0, 1).ToUpper() : "",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#008080"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };
                }

                var currentUserRow = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
                    ColumnSpacing = 10,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                currentUserRow.Children.Add(currentUserFrame);

                var currentUserNameLabel = new Label
                {
                    Text = currentUser.Name,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    VerticalOptions = LayoutOptions.Center
                };
                currentUserRow.Children.Add(currentUserNameLabel);
                Grid.SetColumn(currentUserNameLabel, 1);

                var layout = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 12,
                    Children =
            {
                new Label
                {
                    Text = "Get a testimonial from a friend",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White
                },
                new Label
                {
                    Text = "Enter your friend's phone number - their profile will appear if registered",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888880")
                },
                new BoxView { HeightRequest = 1, Color = Color.FromArgb("#333333") },
                new Label
                {
                    Text = "Sending as",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888880")
                },
                currentUserRow,
                new BoxView { HeightRequest = 1, Color = Color.FromArgb("#333333") },
                new Label
                {
                    Text = "Friend's Phone Number",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#008080")
                },
                phoneEntry,
                loadingIndicator,
                friendInfoLabel,
                friendCard,
                new Label
                {
                    Text = "Choose a template (or write your own):",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#888880")
                },
                templatesPicker,
                new Label
                {
                    Text = "Your Testimonial",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#008080")
                },
                testimonialEditor,
                new Label
                {
                    Text = "Rating",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#008080")
                },
                ratingPicker
            }
                };

                var sendButton = new Button
                {
                    Text = "Send Request",
                    BackgroundColor = Color.FromArgb("#008080"),
                    TextColor = Colors.White,
                    FontSize = 14,
                    HeightRequest = 44,
                    CornerRadius = 22,
                    Margin = new Thickness(0, 20, 0, 10)
                };

                sendButton.Clicked += async (s, args) =>
                {
                    if (foundFriend == null)
                    {
                        await inputPage.DisplayAlert("Error", "Please enter a registered phone number first", "OK");
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(testimonialEditor.Text))
                    {
                        await inputPage.DisplayAlert("Error", "Please write a testimonial or select a template", "OK");
                        return;
                    }

                    // Check if this friend has already endorsed this user
                    bool alreadyEndorsed = await HasUserAlreadyEndorsedAsync(foundFriend.PhoneNumber, _currentUserId);

                    if (alreadyEndorsed)
                    {
                        await inputPage.DisplayAlert(
                            "Already Endorsed",
                            $"{foundFriend.Name} has already written an endorsement for you.\n\nThey cannot endorse you multiple times.",
                            "OK"
                        );
                        return;
                    }

                    // Also check if there's already a pending request from this friend
                    var requestsJson = Preferences.Get("endorsement_requests", "[]");
                    var pendingList = System.Text.Json.JsonSerializer.Deserialize<List<PendingEndorsement>>(requestsJson) ?? new List<PendingEndorsement>();

                    var existingPending = pendingList.FirstOrDefault(r =>
                        r.FriendPhone == foundFriend.PhoneNumber &&
                        r.Status == "pending");

                    if (existingPending != null)
                    {
                        await inputPage.DisplayAlert(
                            "Pending Request",
                            $"You already have a pending endorsement request to {foundFriend.Name}.\n\nPlease wait for them to respond before sending another.",
                            "OK"
                        );
                        return;
                    }

                    sendButton.IsEnabled = false;
                    sendButton.Text = "Sending...";

                    try
                    {
                        string requestId = Guid.NewGuid().ToString();
                        string conversationId = await GetOrCreateConversationAsync(currentUserPhone, foundFriend.PhoneNumber, foundFriend.Name);

                        var endorsementMessage = new ChatMessage
                        {
                            ConversationId = conversationId,
                            SenderPhone = currentUserPhone,
                            RecipientPhone = foundFriend.PhoneNumber,
                            MessageType = "endorsement_request",
                            Content = testimonialEditor.Text.Trim(),
                            EndorsementRequestId = requestId,
                            EndorsementRequestorId = currentUser.Id.ToString(),
                            EndorsementRequestorName = currentUser.Name,
                            EndorsementTestimonial = testimonialEditor.Text.Trim(),
                            EndorsementRating = ratingPicker.SelectedItem?.ToString(),
                            EndorsementStatus = "pending",
                            SentAt = DateTime.UtcNow,
                            IsDelivered = true,
                            IsRead = false,
                            IsLocalOutgoing = true
                        };

                        await ChatRepository.AddMessageAsync(endorsementMessage);

                        var endorsementRequest = new PendingEndorsement
                        {
                            RequestId = requestId,
                            FriendPhone = foundFriend.PhoneNumber,
                            FriendName = foundFriend.Name,
                            FriendProfileImage = foundFriend.ProfileImagePath,
                            Testimonial = testimonialEditor.Text.Trim(),
                            Rating = ratingPicker.SelectedItem?.ToString(),
                            CreatedAt = DateTime.UtcNow,
                            Status = "pending"
                        };

                        var requestsJson2 = Preferences.Get("endorsement_requests", "[]");
                        var requests = System.Text.Json.JsonSerializer.Deserialize<List<PendingEndorsement>>(requestsJson2) ?? new List<PendingEndorsement>();
                        requests.Add(endorsementRequest);
                        Preferences.Set("endorsement_requests", System.Text.Json.JsonSerializer.Serialize(requests));

                        await inputPage.DisplayAlert(
                            "Request Sent",
                            $"Your endorsement request has been sent to {foundFriend.Name}.\n\nThey will see it in their chat and can accept or decline.",
                            "OK"
                        );

                        await inputPage.Navigation.PopModalAsync();
                        MessagingCenter.Send(this, "ConversationsUpdated");
                        await LoadPendingEndorsementsAsync(); // Refresh the pending list
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Send endorsement error: {ex}");
                        await inputPage.DisplayAlert("Error", $"Failed to send request: {ex.Message}", "OK");
                        sendButton.IsEnabled = true;
                        sendButton.Text = "Send Request";
                    }
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    BackgroundColor = Colors.Gray,
                    TextColor = Colors.White,
                    FontSize = 14,
                    HeightRequest = 44,
                    CornerRadius = 22
                };

                cancelButton.Clicked += async (s, args) => await inputPage.Navigation.PopModalAsync();

                layout.Children.Add(sendButton);
                layout.Children.Add(cancelButton);

                var scrollView = new ScrollView { Content = layout };
                inputPage.Content = scrollView;

                await Navigation.PushModalAsync(new NavigationPage(inputPage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddEndorsementButton_Clicked error: {ex}");
                await DisplayAlert("Error", "Could not create endorsement request: " + ex.Message, "OK");
            }
        }
        // Helper method to get or create a conversation
        private async Task<string> GetOrCreateConversationAsync(string userPhone, string contactPhone, string contactName)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if conversation already exists
                var existingConversation = await db.Table<Conversation>()
                    .Where(c => (c.ParticipantA == userPhone && c.ParticipantB == contactPhone) ||
                               (c.ParticipantA == contactPhone && c.ParticipantB == userPhone))
                    .FirstOrDefaultAsync();

                if (existingConversation != null)
                    return existingConversation.ConversationId;

                // Create new conversation
                string conversationId = Guid.NewGuid().ToString();
                var conversation = new Conversation
                {
                    ConversationId = conversationId,
                    ParticipantA = userPhone,
                    ParticipantB = contactPhone,
                    LastMessageAt = DateTime.UtcNow,
                    LastMessagePreview = $"?? Endorsement request sent to {contactName}",
                    CreatedAt = DateTime.UtcNow
                };

                await db.InsertAsync(conversation);
                return conversationId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrCreateConversationAsync error: {ex}");
                throw;
            }
        }
        private async void DeleteEndorsementButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                UserEndorsement endorsement = null;

                // Try to get endorsement from different sources
                if (sender is TapGestureRecognizer tap)
                {
                    endorsement = tap.CommandParameter as UserEndorsement;
                }
                else if (sender is Border border && border.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer borderTap)
                {
                    endorsement = borderTap.CommandParameter as UserEndorsement;
                }
                else if (sender is VisualElement ve && ve.BindingContext is UserEndorsement veEndorsement)
                {
                    endorsement = veEndorsement;
                }
                else if (sender is Button button && button.CommandParameter is UserEndorsement buttonEndorsement)
                {
                    endorsement = buttonEndorsement;
                }

                if (endorsement == null)
                {
                    Debug.WriteLine("DeleteEndorsementButton_Clicked: endorsement is null");
                    await DisplayAlert("Error", "Could not identify which endorsement to delete", "OK");
                    return;
                }

                Debug.WriteLine($"DeleteEndorsementButton_Clicked: Trying to delete endorsement Id={endorsement.Id}, EndorserName={endorsement.EndorserName}");

                var confirm = await DisplayAlert(
                    "Delete Endorsement",
                    $"Remove {endorsement.EndorserName}'s testimonial?\n\n\"{endorsement.Testimonial}\"",
                    "Delete",
                    "Cancel"
                );

                if (confirm)
                {
                    var success = await EndorsementService.DeleteEndorsementAsync(endorsement.Id, _currentUserId);

                    if (success)
                    {
                        // Remove from local list
                        _endorsements.Remove(endorsement);

                        // Refresh the CollectionView
                        var endorsementsCv = this.FindByName<CollectionView>("EndorsementsCollectionView");
                        if (endorsementsCv != null)
                        {
                            endorsementsCv.ItemsSource = null;
                            endorsementsCv.ItemsSource = _endorsements;
                        }

                        // Update the count label
                        var acceptedCountLabel = this.FindByName<Label>("AcceptedEndorsementsCountLabel");
                        if (acceptedCountLabel != null)
                        {
                            acceptedCountLabel.Text = $"({_endorsements.Count})";
                        }

                        await DisplayAlert("Deleted", "Endorsement removed successfully", "OK");

                        // Notify that endorsements were updated
                        MessagingCenter.Send(this, "EndorsementsUpdated", _currentUserId);
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to delete endorsement. Please try again.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteEndorsementButton_Clicked error: {ex}");
                await DisplayAlert("Error", $"Could not delete endorsement: {ex.Message}", "OK");
            }
        }


        private async Task<bool> HasUserAlreadyEndorsedAsync(string endorserPhone, int targetUserId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if there's already an endorsement from this user to the target user
                var existingEndorsement = await db.Table<UserEndorsement>()
                    .Where(e => e.EndorserUserPhone == endorserPhone && e.TargetUserId == targetUserId)
                    .FirstOrDefaultAsync();

                return existingEndorsement != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HasUserAlreadyEndorsedAsync error: {ex}");
                return false;
            }
        }
        private async Task LoadUserEventsAsync(int userId, string filter = "Upcoming")
        {
            _userEvents = await ProfileDataService.GetUserEventsAsync(userId, filter);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var eventsCv = this.FindByName<CollectionView>("EventsCollectionView");
                if (eventsCv != null)
                {
                    eventsCv.ItemsSource = _userEvents;
                    // Dynamically size based on item count
                    eventsCv.HeightRequest = Math.Max(200, _userEvents.Count * 160);
                }
            });
        }

        // Update this method (around line 800-810)
        private void UpdateVerificationBadgeVisibility()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Find the header verification badge
                var headerBadge = this.FindByName<Border>("VerificationBadge");
                if (headerBadge != null)
                {
                    headerBadge.IsVisible = _isVerified;

                    // Update styling based on verification status
                    if (_isVerified)
                    {
                        headerBadge.BackgroundColor = Color.FromArgb("#00B5B520");
                        headerBadge.Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5"));
                        headerBadge.StrokeThickness = 1;
                    }
                    else
                    {
                        headerBadge.BackgroundColor = Color.FromArgb("#66666620");
                        headerBadge.Stroke = new SolidColorBrush(Color.FromArgb("#666666"));
                        headerBadge.StrokeThickness = 0.5;
                    }

                    // Clear existing gestures
                    headerBadge.GestureRecognizers.Clear();

                    // Only add tap gesture for OWNER when verified
                    if (_isVerified && IsOwner)
                    {
                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += OnVerificationBadgeTapped;
                        headerBadge.GestureRecognizers.Add(tapGesture);
                    }
                    // If not owner, badge is just visual - no tap gesture

                    headerBadge.InvalidateMeasure();
                }

                // Also update the safety tab badge
                UpdateSafetyTabVerificationStatus();
            });
        }

        // Update this method to handle verification visibility correctly
        private void UpdateSafetyTabVerificationStatus()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var safetyBadge = this.FindByName<Border>("VerifiedBadge");
                var verifyButton = this.FindByName<Border>("VerifyButton");
                var verificationIcon = this.FindByName<Border>("VerificationIcon");
                var verificationStatusLabel = this.FindByName<Label>("VerificationStatusLabel");

                if (safetyBadge != null)
                {
                    // Show verified badge to EVERYONE if user is verified
                    safetyBadge.IsVisible = _isVerified;

                    // Clear existing gestures
                    safetyBadge.GestureRecognizers.Clear();

                    // Only add tap gesture for OWNER when verified
                    if (_isVerified && IsOwner)
                    {
                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += OnVerifyButtonTapped;
                        safetyBadge.GestureRecognizers.Add(tapGesture);
                    }
                }

                if (verifyButton != null)
                {
                    // Only show verify button to OWNER when NOT verified
                    verifyButton.IsVisible = !_isVerified && IsOwner;

                    // Ensure tap gesture is attached
                    if (verifyButton.IsVisible && verifyButton.GestureRecognizers.Count == 0)
                    {
                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += OnVerifyButtonTapped;
                        verifyButton.GestureRecognizers.Add(tapGesture);
                    }
                }

                if (verificationIcon != null)
                {
                    verificationIcon.IsVisible = _isVerified;
                }

                if (verificationStatusLabel != null)
                {
                    verificationStatusLabel.Text = _isVerified ? "Verified" : "Not Verified";
                    verificationStatusLabel.TextColor = _isVerified ? Color.FromArgb("#00B5B5") : Color.FromArgb("#FF6B6B");
                }
            });
        }
        // Update LoadSafetyInfo method
        private async void LoadSafetyInfo()
        {
            try
            {
                if (string.IsNullOrEmpty(_phone)) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user == null) return;

                // Update verification status
                _isVerified = user.IsVerified;

                // Update UI
                UpdateSafetyTabVerificationStatus();
                UpdateVerificationBadgeVisibility(); // This updates the header badge
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSafetyInfo error: {ex.Message}");
            }
        }

        // Update ApplyViewOnlyMode to NOT hide verification
        private void ApplyViewOnlyMode()
        {
            if (!_viewOnly)
                return;

            var disableNames = new[]
            {
                "BioEditorInfo", "MoodPicker", "EnergyPicker", "CountryEntry", "StateEntry",
                "DrinksPicker", "SmokesSwitch", "PetsSwitch", "ReligionEntry", "PoliticalEntry",
                "MusicGenresEntry", "FavoriteArtistsEntry", "FavoriteMoviesEntry", "FavoriteBooksEntry",
                "LanguagesEntry", "OccupationEntry", "EducationEntry", "PromptsEditor", "DealbreakersEntry",
                "TopInterestPicker", "TopArtistEntry", "TopMovieEntry", "SexualOrientationPicker",
                "FavoriteMusicGenrePicker", "BestMusicEntry", "KidsPreferencePicker", "HasChildrenPicker",
                "DietaryPreferencePicker", "ExerciseFrequencyPicker", "HeightPicker", "BodyTypePicker",
                "EthnicityPicker", "TribePicker", "PersonalityTypePicker", "LoveLanguagePicker",
                "HidePhoneSwitch"  // ← disable the toggle in view-only mode too
            };

            foreach (var name in disableNames)
            {
                var ve = this.FindByName<VisualElement>(name);
                if (ve != null)
                {
                    ve.IsEnabled = false;
                    ve.Opacity = 0.6;
                }
            }

            var hideNames = new[]
            {
                "ProfileEditIconOverlay", "CoverEditIcon", "EditImagesButton",
                "SaveInfoButton", "SaveProfileButton", "SaveInterestsButton",
                "AddPromptButton", "AddDateIdeaButton", "CreateEventButton",
                "BlockedUsersButton", "EmergencyContactsButton", "SafetyTipsButton",
                "MoodSearchSwitchContainer", "GhostModeMoodShieldSwitchContainer",
                "AddEndorsementButton", "HidePhoneSwitch"  // ← hide the toggle entirely
            };

            foreach (var name in hideNames)
            {
                var v = this.FindByName<VisualElement>(name);
                if (v != null)
                    v.IsVisible = false;
            }

            UpdateNavigationBarVisibility();

            Debug.WriteLine("View-only mode: Edit controls disabled, verification remains visible");
        }

        private async Task LoadUserPreferencesAsync(int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            var user = await db.Table<User>().Where(u => u.Id == userId).FirstOrDefaultAsync();

            if (user == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var moodPicker = this.FindByName<Picker>("MoodPicker");
                if (moodPicker != null && !string.IsNullOrEmpty(user.Mood) && moodPicker.Items.Contains(user.Mood))
                    moodPicker.SelectedItem = user.Mood;

                var energyPicker = this.FindByName<Picker>("EnergyPicker");
                if (energyPicker != null && !string.IsNullOrEmpty(user.EnergyLevel) && energyPicker.Items.Contains(user.EnergyLevel))
                    energyPicker.SelectedItem = user.EnergyLevel;

                var countryEntry = this.FindByName<Entry>("CountryEntry");
                if (countryEntry != null) countryEntry.Text = user.Country ?? "";

                var stateEntry = this.FindByName<Entry>("StateEntry");
                if (stateEntry != null) stateEntry.Text = user.State ?? "";

                var drinksPicker = this.FindByName<Picker>("DrinksPicker");
                if (drinksPicker != null && !string.IsNullOrEmpty(user.Drinks) && drinksPicker.Items.Contains(user.Drinks))
                    drinksPicker.SelectedItem = user.Drinks;

                var smokesSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("SmokesSwitch");
                if (smokesSwitch != null) smokesSwitch.IsToggled = user.Smokes;

                var petsSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("PetsSwitch");
                if (petsSwitch != null) petsSwitch.IsToggled = user.HasPets;

                var religionEntry = this.FindByName<Entry>("ReligionEntry");
                if (religionEntry != null) religionEntry.Text = user.Religion ?? "";

                var politicalEntry = this.FindByName<Entry>("PoliticalEntry");
                if (politicalEntry != null) politicalEntry.Text = user.PoliticalViews ?? "";

                var moodSearchSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("MoodSearchSwitch");
                if (moodSearchSwitch != null) moodSearchSwitch.IsToggled = user.AllowMoodSearch;

                var ghostSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("GhostModeMoodShieldSwitch");
                if (ghostSwitch != null) ghostSwitch.IsToggled = user.GhostModeMoodShield;

                // ── NEW: Phone Number Visibility toggle ──────────────────────
                var hidePhoneSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("HidePhoneSwitch");
                if (hidePhoneSwitch != null) hidePhoneSwitch.IsToggled = user.HidePhoneNumber;

                var kidsPreferencePicker = this.FindByName<Picker>("KidsPreferencePicker");
                if (kidsPreferencePicker != null && !string.IsNullOrEmpty(user.KidsPreference) && kidsPreferencePicker.Items.Contains(user.KidsPreference))
                    kidsPreferencePicker.SelectedItem = user.KidsPreference;

                var hasChildrenPicker = this.FindByName<Picker>("HasChildrenPicker");
                if (hasChildrenPicker != null && !string.IsNullOrEmpty(user.HasChildren) && hasChildrenPicker.Items.Contains(user.HasChildren))
                    hasChildrenPicker.SelectedItem = user.HasChildren;

                var dietaryPreferencePicker = this.FindByName<Picker>("DietaryPreferencePicker");
                if (dietaryPreferencePicker != null && !string.IsNullOrEmpty(user.DietaryPreference) && dietaryPreferencePicker.Items.Contains(user.DietaryPreference))
                    dietaryPreferencePicker.SelectedItem = user.DietaryPreference;

                var exerciseFrequencyPicker = this.FindByName<Picker>("ExerciseFrequencyPicker");
                if (exerciseFrequencyPicker != null && !string.IsNullOrEmpty(user.ExerciseFrequency) && exerciseFrequencyPicker.Items.Contains(user.ExerciseFrequency))
                    exerciseFrequencyPicker.SelectedItem = user.ExerciseFrequency;

                var heightPicker = this.FindByName<Picker>("HeightPicker");
                if (heightPicker != null && user.HeightCm.HasValue && user.HeightCm.Value > 0)
                {
                    string heightText = $"{user.HeightCm.Value} cm";
                    if (heightPicker.Items.Contains(heightText))
                        heightPicker.SelectedItem = heightText;
                }

                var bodyTypePicker = this.FindByName<Picker>("BodyTypePicker");
                if (bodyTypePicker != null && !string.IsNullOrEmpty(user.BodyType) && bodyTypePicker.Items.Contains(user.BodyType))
                    bodyTypePicker.SelectedItem = user.BodyType;

                var ethnicityPicker = this.FindByName<Picker>("EthnicityPicker");
                if (ethnicityPicker != null && !string.IsNullOrEmpty(user.Ethnicity) && ethnicityPicker.Items.Contains(user.Ethnicity))
                    ethnicityPicker.SelectedItem = user.Ethnicity;

                var tribePicker = this.FindByName<Picker>("TribePicker");
                if (tribePicker != null && !string.IsNullOrEmpty(user.Tribe) && tribePicker.Items.Contains(user.Tribe))
                    tribePicker.SelectedItem = user.Tribe;

                var personalityTypePicker = this.FindByName<Picker>("PersonalityTypePicker");
                if (personalityTypePicker != null && !string.IsNullOrEmpty(user.PersonalityType) && personalityTypePicker.Items.Contains(user.PersonalityType))
                    personalityTypePicker.SelectedItem = user.PersonalityType;

                var loveLanguagePicker = this.FindByName<Picker>("LoveLanguagePicker");
                if (loveLanguagePicker != null && !string.IsNullOrEmpty(user.LoveLanguage) && loveLanguagePicker.Items.Contains(user.LoveLanguage))
                    loveLanguagePicker.SelectedItem = user.LoveLanguage;
            });
        }

        // Add this property to your Post model (or create a computed property)
        // In your Post model class, add:
        // public bool IsAuthorVerified { get; set; }

        private bool _isAuthorVerified;
        public bool IsAuthorVerified
        {
            get => _isAuthorVerified;
            set
            {
                if (_isAuthorVerified != value)
                {
                    _isAuthorVerified = value;
                    OnPropertyChanged(nameof(IsAuthorVerified));  // Make sure property name is passed
                }
            }
        }

        // Add this method to handle tapping the verification badge on a post
        private async void OnPostAuthorVerificationTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not Lock.Models.Post post) return;

                string phone = post.AuthorPhone ?? string.Empty;

                // Clean the phone number
                if (phone.Contains("·"))
                {
                    var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                    phone = parts.Length > 1 ? parts[1].Trim() : phone;
                }

                phone = phone.Trim();
                if (string.IsNullOrWhiteSpace(phone)) return;

                // Get user verification status
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == phone).FirstOrDefaultAsync();

                if (user != null)
                {
                    if (user.IsVerified)
                    {
                        await DisplayAlert(
                            "Verified Account",
                            $"✓ {user.Name} is a verified user.\n\n" +
                            $"Verified on: {user.VerifiedAt:MMMM dd, yyyy}\n" +
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

        private bool _isVerified = false;
        public bool IsVerified
        {
            get => _isVerified;
            set
            {
                if (_isVerified == value) return;
                _isVerified = value;
                OnPropertyChanged(nameof(IsVerified));

                // FORCE main thread execution
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateVerificationBadgeVisibility();
                });
            }
        }
        private async void OnVerificationBadgeTapped(object sender, EventArgs e)
        {
            try
            {
                // Only allow owners to tap the verification badge
                if (!IsOwner)
                {
                    // Non-owners cannot tap - just return silently
                    return;
                }

                // Get the user's phone number
                string phone = _phone;
                if (string.IsNullOrEmpty(phone))
                {
                    phone = Preferences.Get("current_user_phone", string.Empty);
                }

                if (string.IsNullOrEmpty(phone))
                {
                    await DisplayAlert("Error", "User not found", "OK");
                    return;
                }

                // Check if user is verified
                if (_currentUser != null && _currentUser.IsVerified)
                {
                    // Show verification details
                    await DisplayAlert(
                        "Verified Account",
                        $"✓ {_currentUser.Name} is a verified user.\n\n" +
                        $"Verified on: {_currentUser.VerifiedAt:MMMM dd, yyyy}\n" +
                        $"Verification Score: {_currentUser.VerificationScore:F1}%\n\n" +
                        "Verified users have completed ID verification for added trust and safety.",
                        "OK");
                }
                else
                {
                    // Navigate to verification page
                    var verificationPage = new VerificationPage();
                    var phoneProperty = verificationPage.GetType().GetProperty("UserPhone");
                    if (phoneProperty != null && phoneProperty.CanWrite)
                    {
                        phoneProperty.SetValue(verificationPage, phone);
                    }
                    await Navigation.PushAsync(verificationPage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnVerificationBadgeTapped error: {ex}");
                await DisplayAlert("Error", "Could not open verification details", "OK");
            }
        }
        private async Task LoadProfileStatsAsync(int userId)
        {
            var views = await ProfileDataService.GetProfileViewsCountAsync(userId);
            var matches = await ProfileDataService.GetMatchesCountAsync(userId);
            var responseRate = await ProfileDataService.GetResponseRateAsync(userId);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var viewsLabel = this.FindByName<Label>("ProfileViewsCount");
                if (viewsLabel != null) viewsLabel.Text = views.ToString();

                var matchesLabel = this.FindByName<Label>("MatchesCount");
                if (matchesLabel != null) matchesLabel.Text = matches.ToString();

                var responseLabel = this.FindByName<Label>("ResponseRateLabel");
                if (responseLabel != null) responseLabel.Text = $"{responseRate}%";

                // Fix: Get user from database instead of using _currentUser
                var joinDateLabel = this.FindByName<Label>("JoinDateLabel");
                if (joinDateLabel != null)
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var user = await db.Table<User>().Where(u => u.Id == userId).FirstOrDefaultAsync();
                    if (user != null)
                        joinDateLabel.Text = user.JoinDate.ToString("yyyy");
                }
            });
        }


        private void UpdateProfileEditIconVisibility()
        {
            var profileImage = this.FindByName<Image>("ProfileImageOverlay");
            var profileEditIcon = this.FindByName<Border>("ProfileEditIconOverlay");
            if (profileImage == null || profileEditIcon == null) return;
            // Show edit icon ONLY when there is NO profile image loaded
            bool hasImage = profileImage.Source != null;
            // Also hide in view-only mode
            profileEditIcon.IsVisible = !hasImage && !_viewOnly;
        }

       

        private void SetupImageTapGestures()
        {
            var coverImage = this.FindByName<Image>("CoverImageOverlay");
            var profileImage = this.FindByName<Image>("ProfileImageOverlay");

            if (coverImage == null || profileImage == null) return;

            // Clear any existing gestures
            coverImage.GestureRecognizers.Clear();
            profileImage.GestureRecognizers.Clear();

            // Get all image paths for full-screen viewing
            var allImagePaths = new List<string>();

            // Add profile image if exists
            if (profileImage.Source != null && _currentUser != null && !string.IsNullOrEmpty(_currentUser.ProfileImagePath))
            {
                allImagePaths.Add(_currentUser.ProfileImagePath);
            }

            // Add cover image if exists
            if (coverImage.Source != null && _currentUser != null && !string.IsNullOrEmpty(_currentUser.CoverImagePath))
            {
                allImagePaths.Add(_currentUser.CoverImagePath);
            }

            // Add all user photos from gallery
            foreach (var photo in _userPhotos.Where(p => File.Exists(p.ImagePath)))
            {
                if (!allImagePaths.Contains(photo.ImagePath))
                {
                    allImagePaths.Add(photo.ImagePath);
                }
            }

            if (allImagePaths.Count == 0) return;

            // Add cover image tap gesture - opens full-screen viewer page
            var coverTapGesture = new TapGestureRecognizer();
            coverTapGesture.Tapped += async (s, e) =>
            {
                int startIndex = 0;
                if (_currentUser != null && !string.IsNullOrEmpty(_currentUser.CoverImagePath))
                {
                    startIndex = allImagePaths.IndexOf(_currentUser.CoverImagePath);
                    if (startIndex < 0) startIndex = 0;
                }

                var fullScreenPage = new FullScreenMediaPage(allImagePaths, startIndex);
                await Navigation.PushModalAsync(fullScreenPage);
            };
            coverImage.GestureRecognizers.Add(coverTapGesture);

            // Add profile image tap gesture - opens full-screen viewer page
            var profileTapGesture = new TapGestureRecognizer();
            profileTapGesture.Tapped += async (s, e) =>
            {
                int startIndex = 0;
                if (_currentUser != null && !string.IsNullOrEmpty(_currentUser.ProfileImagePath))
                {
                    startIndex = allImagePaths.IndexOf(_currentUser.ProfileImagePath);
                    if (startIndex < 0) startIndex = 0;
                }

                var fullScreenPage = new FullScreenMediaPage(allImagePaths, startIndex);
                await Navigation.PushModalAsync(fullScreenPage);
            };
            profileImage.GestureRecognizers.Add(profileTapGesture);
        }

        
        // Add these methods inside the ProfilePage class (near other tap handlers / OnAppearing).
        // They integrate with existing SavePickedFileAsync and DB code that's already present.
        private async void EditImagesButton_Tapped(object sender, EventArgs e)
        {
            // If page is view-only, don't allow editing
            if (_viewOnly)
            {
                await DisplayAlert("Read only", "This profile is view-only.", "OK");
                return;
            }
            if (!EnsurePhoneFromPreferences())
            {
                await DisplayAlert("Error", "User not found.", "OK");
                return;
            }
            var action = await DisplayActionSheet("Edit images", "Cancel", null, "Change profile image", "Change cover image", "Change both");
            switch (action)
            {
                case "Change profile image":
                    // reuse existing flow
                    ChangeProfileButton_Clicked(sender, e);
                    break;
                case "Change cover image":
                    ChangeCoverButton_Clicked(sender, e);
                    break;
                case "Change both":
                    await ChangeBothImagesAsync();
                    break;
                default:
                    // cancelled or unknown
                    break;
            }
        }


        // Add this helper to update photos display
        private void UpdatePhotosDisplay()
        {
            var photosLayout = this.FindByName<HorizontalStackLayout>("PhotosLayout");
            if (photosLayout == null) return;

            photosLayout.Children.Clear();

            foreach (var photo in _userPhotos.Where(p => File.Exists(p.ImagePath)).OrderBy(p => p.Order))
            {
                var border = new Border
                {
                    Padding = 0,
                    BackgroundColor = Colors.Transparent,
                    StrokeThickness = 0,
                    HeightRequest = 120,
                    WidthRequest = 96,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 }
                };

                var image = new Image
                {
                    Source = ImageSource.FromFile(photo.ImagePath),
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 120,
                    WidthRequest = 96,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                // Add primary badge if this is primary photo
                if (photo.IsPrimary)
                {
                    var grid = new Grid();
                    grid.Children.Add(image);

                    var badge = new Border
                    {
                        BackgroundColor = Color.FromArgb("#008080"),
                        Padding = new Thickness(4, 2),
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                        StrokeShape = new RoundRectangle { CornerRadius = 4 },
                        Content = new Label
                        {
                            Text = "?",
                            TextColor = Colors.White,
                            FontSize = 10
                        }
                    };
                    grid.Children.Add(badge);
                    border.Content = grid;
                }
                else
                {
                    border.Content = image;
                }

                // Add tap gesture for full-screen view - USING THE NEW PAGE
                var tapGesture = new TapGestureRecognizer();
                var paths = _userPhotos.Select(p => p.ImagePath).ToList();
                var index = paths.IndexOf(photo.ImagePath);

                tapGesture.Tapped += async (s, e) =>
                {
                    // Use the new FullScreenMediaPage instead of the old method
                    var fullScreenPage = new FullScreenMediaPage(paths, index);
                    await Navigation.PushModalAsync(fullScreenPage);
                };

                border.GestureRecognizers.Add(tapGesture);

                photosLayout.Children.Add(border);
            }
        }
        private async void AddPromptButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var questions = new[]
            {
        "My simple pleasures",
        "I'm weirdly attracted to",
        "A perfect Sunday morning",
        "Best travel story",
        "Two truths and a lie",
        "I'm looking for",
        "My biggest risk",
        "I get myself into trouble when",
        "Favorite quality in a person",
        "We'll get along if",
        "The way to my heart is",
        "Most spontaneous thing I've done"
    };

            var selectedQuestion = await DisplayActionSheet("Choose a prompt", "Cancel", null, questions);

            if (selectedQuestion == null || selectedQuestion == "Cancel")
                return;

            // Show answer dialog
            var answer = await DisplayPromptAsync(selectedQuestion, "Your answer:", "Save", "Cancel", maxLength: 300);

            if (string.IsNullOrWhiteSpace(answer))
                return;

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user != null)
                {
                    var prompt = await ProfileDataService.AddUserPromptAsync(user.Id, selectedQuestion, answer);
                    _userPrompts.Add(prompt);

                    var promptsCv = this.FindByName<CollectionView>("PromptsCollectionView");
                    if (promptsCv != null)
                        promptsCv.ItemsSource = null; // Refresh
                    promptsCv.ItemsSource = _userPrompts;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to add prompt: {ex.Message}", "OK");
            }
        }

        // Add these methods to your ProfilePage.xaml.cs file

        // Love Language Picker - Show custom entry when "Other" is selected
        private void LoveLanguagePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            var customLayout = this.FindByName<HorizontalStackLayout>("CustomLoveLanguageLayout");

            if (picker == null || customLayout == null) return;

            var selected = picker.SelectedIndex >= 0 ? picker.Items[picker.SelectedIndex] : null;
            customLayout.IsVisible = string.Equals(selected, "Other (Add your own)", StringComparison.OrdinalIgnoreCase);
        }

        // Add custom love language to picker
        private void AddCustomLoveLanguage_Clicked(object sender, EventArgs e)
        {
            var entry = this.FindByName<Entry>("CustomLoveLanguageEntry");
            var picker = this.FindByName<Picker>("LoveLanguagePicker");
            var customLayout = this.FindByName<HorizontalStackLayout>("CustomLoveLanguageLayout");

            if (entry == null || picker == null || customLayout == null) return;

            var customValue = (entry.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(customValue)) return;

            // Check if already exists
            if (!picker.Items.Contains(customValue))
            {
                // Insert before "Other" option
                int insertIndex = picker.Items.Count - 1;
                picker.Items.Insert(insertIndex, customValue);
            }

            picker.SelectedItem = customValue;
            entry.Text = string.Empty;
            customLayout.IsVisible = false;
        }

        // Mood Picker (Looking For) - Show custom entry when "Other" is selected
        private void MoodPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            var customLayout = this.FindByName<HorizontalStackLayout>("CustomLookingForLayout");

            if (picker == null || customLayout == null) return;

            var selected = picker.SelectedIndex >= 0 ? picker.Items[picker.SelectedIndex] : null;
            customLayout.IsVisible = string.Equals(selected, "Other (Add your own)", StringComparison.OrdinalIgnoreCase);
        }

        // Add custom looking for option to picker
        private void AddCustomLookingFor_Clicked(object sender, EventArgs e)
        {
            var entry = this.FindByName<Entry>("CustomLookingForEntry");
            var picker = this.FindByName<Picker>("MoodPicker");
            var customLayout = this.FindByName<HorizontalStackLayout>("CustomLookingForLayout");

            if (entry == null || picker == null || customLayout == null) return;

            var customValue = (entry.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(customValue)) return;

            // Check if already exists
            if (!picker.Items.Contains(customValue))
            {
                // Insert before "Other" option
                int insertIndex = picker.Items.Count - 1;
                picker.Items.Insert(insertIndex, customValue);
            }

            picker.SelectedItem = customValue;
            entry.Text = string.Empty;
            customLayout.IsVisible = false;
        }
        private void RestoreCurrentTab()
        {
            // Find all panels
            var info = this.FindByName<VisualElement>("ProfileInfoPanel");
            var prefs = this.FindByName<VisualElement>("ProfilePreferencesPanel");
            var interests = this.FindByName<VisualElement>("ProfileInterestsPanel");
            var media = this.FindByName<VisualElement>("ProfileMediaPanel");
            var prompts = this.FindByName<VisualElement>("ProfilePromptsPanel");
            var dates = this.FindByName<VisualElement>("ProfileDateIdeasPanel");
            var events = this.FindByName<VisualElement>("ProfileEventsPanel");
            var safety = this.FindByName<VisualElement>("ProfileSafetyPanel");
            var stats = this.FindByName<VisualElement>("ProfileStatsPanel");
            var endorsements = this.FindByName<VisualElement>("ProfileEndorsementsPanel");

            // All tab labels
            var infoLabel = this.FindByName<Label>("ProfileInfoButton");
            var prefsLabel = this.FindByName<Label>("ProfilePrefsButton");
            var interestsLabel = this.FindByName<Label>("ProfileInterestsButton");
            var mediaLabel = this.FindByName<Label>("ProfileMediaButton");
            var promptsLabel = this.FindByName<Label>("ProfilePromptsButton");
            var datesLabel = this.FindByName<Label>("ProfileDatesButton");
            var eventsLabel = this.FindByName<Label>("ProfileEventsButton");
            var safetyLabel = this.FindByName<Label>("ProfileSafetyButton");
            var endorsementsLabel = this.FindByName<Label>("ProfileEndorsementsButton");

            // All indicators
            var infoIndicator = this.FindByName<BoxView>("ProfileTabIndicator");
            var prefsIndicator = this.FindByName<BoxView>("ProfilePrefsIndicator");
            var interestsIndicator = this.FindByName<BoxView>("ProfileInterestsIndicator");
            var mediaIndicator = this.FindByName<BoxView>("ProfileMediaIndicator");
            var promptsIndicator = this.FindByName<BoxView>("ProfilePromptsIndicator");
            var datesIndicator = this.FindByName<BoxView>("ProfileDatesIndicator");
            var eventsIndicator = this.FindByName<BoxView>("ProfileEventsIndicator");
            var safetyIndicator = this.FindByName<BoxView>("ProfileSafetyIndicator");
            var endorsementsIndicator = this.FindByName<BoxView>("ProfileEndorsementsIndicator");

            // Reset all labels to default color
            void ResetLabel(Label lbl)
            {
                if (lbl != null)
                    lbl.TextColor = Color.FromArgb("#666666");
            }

            ResetLabel(infoLabel);
            ResetLabel(prefsLabel);
            ResetLabel(interestsLabel);
            ResetLabel(mediaLabel);
            ResetLabel(promptsLabel);
            ResetLabel(datesLabel);
            ResetLabel(eventsLabel);
            ResetLabel(safetyLabel);
            ResetLabel(endorsementsLabel);

            // Hide all indicators
            void HideIndicator(BoxView indicator)
            {
                if (indicator != null)
                    indicator.WidthRequest = 0;
            }

            HideIndicator(infoIndicator);
            HideIndicator(prefsIndicator);
            HideIndicator(interestsIndicator);
            HideIndicator(mediaIndicator);
            HideIndicator(promptsIndicator);
            HideIndicator(datesIndicator);
            HideIndicator(eventsIndicator);
            HideIndicator(safetyIndicator);
            HideIndicator(endorsementsIndicator);

            // Hide all panels
            if (info != null) info.IsVisible = false;
            if (prefs != null) prefs.IsVisible = false;
            if (interests != null) interests.IsVisible = false;
            if (media != null) media.IsVisible = false;
            if (prompts != null) prompts.IsVisible = false;
            if (dates != null) dates.IsVisible = false;
            if (events != null) events.IsVisible = false;
            if (safety != null) safety.IsVisible = false;
            if (stats != null) stats.IsVisible = false;
            if (endorsements != null) endorsements.IsVisible = false;

            // Restore the current tab based on _currentTab
            switch (_currentTab)
            {
                case "Profile":
                    if (info != null) info.IsVisible = true;
                    if (infoLabel != null) infoLabel.TextColor = Color.FromArgb("#008080");
                    if (infoIndicator != null) infoIndicator.WidthRequest = 24;
                    break;
                case "Preferences":
                    if (prefs != null) prefs.IsVisible = true;
                    if (prefsLabel != null) prefsLabel.TextColor = Color.FromArgb("#008080");
                    if (prefsIndicator != null) prefsIndicator.WidthRequest = 24;
                    break;
                case "Hobbies":
                    if (interests != null) interests.IsVisible = true;
                    if (interestsLabel != null) interestsLabel.TextColor = Color.FromArgb("#008080");
                    if (interestsIndicator != null) interestsIndicator.WidthRequest = 24;
                    break;
                case "Gallery":
                    if (media != null) media.IsVisible = true;
                    if (mediaLabel != null) mediaLabel.TextColor = Color.FromArgb("#008080");
                    if (mediaIndicator != null) mediaIndicator.WidthRequest = 24;
                    break;
                case "Prompts":
                    if (prompts != null) prompts.IsVisible = true;
                    if (promptsLabel != null) promptsLabel.TextColor = Color.FromArgb("#008080");
                    if (promptsIndicator != null) promptsIndicator.WidthRequest = 24;
                    break;
                case "DateIdeas":
                    if (dates != null) dates.IsVisible = true;
                    if (datesLabel != null) datesLabel.TextColor = Color.FromArgb("#008080");
                    if (datesIndicator != null) datesIndicator.WidthRequest = 24;
                    break;
                case "Events":
                    if (events != null) events.IsVisible = true;
                    if (eventsLabel != null) eventsLabel.TextColor = Color.FromArgb("#008080");
                    if (eventsIndicator != null) eventsIndicator.WidthRequest = 24;
                    break;
                case "Safety":
                    if (safety != null) safety.IsVisible = true;
                    if (safetyLabel != null) safetyLabel.TextColor = Color.FromArgb("#008080");
                    if (safetyIndicator != null) safetyIndicator.WidthRequest = 24;
                    break;
                case "Endorsements":
                    if (endorsements != null) endorsements.IsVisible = true;
                    if (endorsementsLabel != null) endorsementsLabel.TextColor = Color.FromArgb("#008080");
                    if (endorsementsIndicator != null) endorsementsIndicator.WidthRequest = 24;
                    break;
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
        private async Task ReloadCurrentTabDataAsync()
        {
            if (_currentUserId == 0) return;

            switch (_currentTab)
            {
                case "Profile":
                    await LoadUserProfileDataAsync(_currentUserId);
                    break;
                case "Preferences":
                    await LoadUserPreferencesAsync(_currentUserId);
                    break;
                case "Hobbies":
                    await LoadUserHobbiesAsync(_currentUserId);
                    break;
                case "Gallery":
                    await LoadUserPhotosAsync(_currentUserId);
                    break;
                case "Prompts":
                    await LoadUserPromptsAsync(_currentUserId);
                    break;
                case "DateIdeas":
                    await LoadUserDateIdeasAsync(_currentUserId);
                    break;
                case "Events":
                    await LoadUserEventsAsync(_currentUserId);
                    break;
                    // Safety tab doesn't need reload as it's mostly static
            }
        }
        private async Task LoadUserProfileDataAsync(int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            var user = await db.Table<User>().Where(u => u.Id == userId).FirstOrDefaultAsync();

            if (user == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var bioEditorInfo = this.FindByName<Editor>("BioEditorInfo");
                if (bioEditorInfo != null) bioEditorInfo.Text = user.Bio ?? "";

                // Load posts
                _ = LoadUserPostsAsync(user.PhoneNumber);
            });
        }

        private async Task LoadUserHobbiesAsync(int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            var user = await db.Table<User>().Where(u => u.Id == userId).FirstOrDefaultAsync();

            if (user == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var topInterestPicker = this.FindByName<Picker>("TopInterestPicker");
                if (topInterestPicker != null && !string.IsNullOrEmpty(user.TopInterest))
                {
                    if (topInterestPicker.Items.Contains(user.TopInterest))
                        topInterestPicker.SelectedItem = user.TopInterest;
                }

                var topArtistEntry = this.FindByName<Entry>("TopArtistEntry");
                if (topArtistEntry != null) topArtistEntry.Text = user.TopArtist ?? "";

                var topMovieEntry = this.FindByName<Entry>("TopMovieEntry");
                if (topMovieEntry != null) topMovieEntry.Text = user.TopMovie ?? "";

                var sexualPicker = this.FindByName<Picker>("SexualOrientationPicker");
                if (sexualPicker != null && !string.IsNullOrEmpty(user.SexualOrientation) && sexualPicker.Items.Contains(user.SexualOrientation))
                    sexualPicker.SelectedItem = user.SexualOrientation;

                var musicEntry = this.FindByName<Entry>("MusicGenresEntry");
                if (musicEntry != null) musicEntry.Text = user.MusicGenres ?? "";

                var favoriteMusicGenrePicker = this.FindByName<Picker>("FavoriteMusicGenrePicker");
                if (favoriteMusicGenrePicker != null && !string.IsNullOrEmpty(user.FavoriteMusicGenre))
                {
                    if (favoriteMusicGenrePicker.Items.Contains(user.FavoriteMusicGenre))
                        favoriteMusicGenrePicker.SelectedItem = user.FavoriteMusicGenre;
                }

                var bestMusicEntry = this.FindByName<Entry>("BestMusicEntry");
                if (bestMusicEntry != null) bestMusicEntry.Text = user.BestMusic ?? "";

                var favMoviesEntry = this.FindByName<Entry>("FavoriteMoviesEntry");
                if (favMoviesEntry != null) favMoviesEntry.Text = user.FavoriteMovies ?? "";

                var favBooksEntry = this.FindByName<Entry>("FavoriteBooksEntry");
                if (favBooksEntry != null) favBooksEntry.Text = user.FavoriteBooks ?? "";

                var languagesEntry = this.FindByName<Entry>("LanguagesEntry");
                if (languagesEntry != null) languagesEntry.Text = user.Languages ?? "";

                var occupationEntry = this.FindByName<Entry>("OccupationEntry");
                if (occupationEntry != null) occupationEntry.Text = user.Occupation ?? "";

                var educationEntry = this.FindByName<Entry>("EducationEntry");
                if (educationEntry != null) educationEntry.Text = user.Education ?? "";

                var promptsEditor = this.FindByName<Editor>("PromptsEditor");
                if (promptsEditor != null) promptsEditor.Text = user.Prompts ?? "";

                var dealbreakersEntry = this.FindByName<Entry>("DealbreakersEntry");
                if (dealbreakersEntry != null) dealbreakersEntry.Text = user.Dealbreakers ?? "";
            });
        }

        private async Task LoadUserPostsAsync(string phone)
        {
            try
            {
                var allPosts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                var userPosts = allPosts
                    .Where(p => string.Equals(p.AuthorPhone ?? string.Empty, phone ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                && string.IsNullOrEmpty(p.StatusImagePath))
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                foreach (var p in userPosts)
                {
                    p.IsExpanded = false;
                    p.UpdateDisplayContent(200);

                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var postsCv = this.FindByName<CollectionView>("UserPostsCollectionView");
                    if (postsCv != null)
                        postsCv.ItemsSource = userPosts;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadUserPostsAsync error: {ex.Message}");
            }
        }

        private void ShowMediaForCategory(string category)
        {
            var photosLayout = this.FindByName<HorizontalStackLayout>("PhotosLayout");
            var noMediaLabel = this.FindByName<Label>("NoMediaLabel");
            if (photosLayout == null) return;

            photosLayout.Children.Clear();

            if (string.IsNullOrEmpty(category)) category = "All";

            if (!_mediaByCategory.TryGetValue(category, out var list) || list == null || list.Count == 0)
            {
                if (noMediaLabel != null) noMediaLabel.IsVisible = true;
                return;
            }

            if (noMediaLabel != null) noMediaLabel.IsVisible = false;

            // Store the current list of paths for this category
            var currentCategoryPaths = list.ToList();

            foreach (var path in list)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                // Create the image first
                var image = new Image
                {
                    Source = ImageSource.FromFile(path),
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 120,
                    WidthRequest = 96,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                // Create the tap gesture recognizer
                var tapGesture = new TapGestureRecognizer();
                var capturedPath = path;
                var capturedCategoryPaths = currentCategoryPaths;
                var startIndex = capturedCategoryPaths.IndexOf(capturedPath);

                // Use the FullScreenMediaPage
                tapGesture.Tapped += async (s, e) =>
                {
                    var fullScreenPage = new FullScreenMediaPage(capturedCategoryPaths, startIndex);
                    await Navigation.PushModalAsync(fullScreenPage);
                };

                image.GestureRecognizers.Add(tapGesture);

                // Create the border and set its content
                var border = new Border
                {
                    Padding = 0,
                    BackgroundColor = Colors.Transparent,
                    StrokeThickness = 0,
                    HeightRequest = 120,
                    WidthRequest = 96,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Content = image  // Set the content here, not in a collection initializer
                };

                // Add to the layout
                photosLayout.Children.Add(border);
            }
        }
        
        // Media category picker changed
        private void MediaCategoryPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker == null) return;
            if (picker.SelectedIndex < 0) return;
            var selected = picker.Items[picker.SelectedIndex];
            ShowMediaForCategory(selected);
        }

        // Show/hide inline entry when "Other" selected for favorite-genre picker
        private void FavoriteMusicGenrePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            var favGenreOtherLayout = this.FindByName<HorizontalStackLayout>("FavoriteMusicGenreOtherLayout");
            if (picker == null || favGenreOtherLayout == null) return;
            var selected = picker.SelectedIndex >= 0 ? picker.Items[picker.SelectedIndex] : null;
            favGenreOtherLayout.IsVisible = string.Equals(selected, "Other", StringComparison.OrdinalIgnoreCase);
        }

        // Add custom genre into favorite-genre picker
        private void FavoriteMusicGenreAddButton_Clicked(object sender, EventArgs e)
        {
            var entry = this.FindByName<Entry>("FavoriteMusicGenreOtherEntry");
            var picker = this.FindByName<Picker>("FavoriteMusicGenrePicker");
            var favGenreOtherLayout = this.FindByName<HorizontalStackLayout>("FavoriteMusicGenreOtherLayout");
            if (entry == null || picker == null || favGenreOtherLayout == null) return;
            var val = (entry.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(val)) return;
            if (!picker.Items.Contains(val))
                picker.Items.Add(val);
            picker.SelectedItem = val;
            entry.Text = string.Empty;
            favGenreOtherLayout.IsVisible = false;
        }

        private async Task<string?> SavePickedFileAsync(FileResult result, string destFileName)
        {
            if (result == null) return null;
            try
            {
                var folder = FileSystem.AppDataDirectory;
                var destPath = System.IO.Path.Combine(folder, destFileName);
                using var sourceStream = await result.OpenReadAsync();
                using var destStream = File.Open(destPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destStream);
                return destPath;
            }
            catch
            {
                return null;
            }
        }

        private async void ChangeProfileButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (!EnsurePhoneFromPreferences())
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                var oldImagePath = _currentUser?.ProfileImagePath ?? string.Empty;

                var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Select profile image" });
                if (result == null) return;

                var phoneSafe = string.IsNullOrEmpty(_phone) ? Guid.NewGuid().ToString() : _phone.Replace("+", "").Replace(" ", "");
                var destFileName = $"profile_{phoneSafe}_{DateTime.UtcNow:yyyyMMddHHmmss}{System.IO.Path.GetExtension(result.FileName)}";
                var savedPath = await SavePickedFileAsync(result, destFileName);

                if (string.IsNullOrEmpty(savedPath))
                {
                    await DisplayAlert("Error", "Failed to save profile image.", "OK");
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user != null)
                {
                    user.ProfileImagePath = savedPath;
                    await db.UpdateAsync(user);

                    // ========== TRACK PROFILE PICTURE CHANGE ==========
                    await TrackProfilePictureChangeAsync(_phone, "Profile", oldImagePath, savedPath);
                }

                var overlay = this.FindByName<Image>("ProfileImageOverlay");
                if (overlay != null) overlay.Source = ImageSource.FromFile(savedPath);

                // Update edit icon visibility
                UpdateProfileEditIconVisibility();

                // Send notification that profile was updated (for AppShell flyout)
                MessagingCenter.Send(this, "ProfileUpdated");

                // refresh media tab & full profile
                await LoadUserAsync(_phone);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not pick profile image: " + ex.Message, "OK");
            }
        }
        private async void ChangeCoverButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (!EnsurePhoneFromPreferences())
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                var oldImagePath = _currentUser?.CoverImagePath ?? string.Empty;

                var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Select cover image" });
                if (result == null) return;

                var phoneSafe = string.IsNullOrEmpty(_phone) ? Guid.NewGuid().ToString() : _phone.Replace("+", "").Replace(" ", "");
                var destFileName = $"cover_{phoneSafe}_{DateTime.UtcNow:yyyyMMddHHmmss}{System.IO.Path.GetExtension(result.FileName)}";
                var savedPath = await SavePickedFileAsync(result, destFileName);

                if (string.IsNullOrEmpty(savedPath))
                {
                    await DisplayAlert("Error", "Failed to save cover image.", "OK");
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user != null)
                {
                    user.CoverImagePath = savedPath;
                    await db.UpdateAsync(user);

                    // ========== TRACK COVER PICTURE CHANGE ==========
                    await TrackProfilePictureChangeAsync(_phone, "Cover", oldImagePath, savedPath);
                }

                var cover = this.FindByName<Image>("CoverImageOverlay");
                if (cover != null) cover.Source = ImageSource.FromFile(savedPath);

                // Update edit icon visibility
                UpdateCoverEditIconVisibility();

                // Send notification that profile was updated (for AppShell flyout)
                MessagingCenter.Send(this, "ProfileUpdated");

                // refresh media tab
                await LoadUserAsync(_phone);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not pick cover image: " + ex.Message, "OK");
            }
        }


        private async Task ChangeBothImagesAsync()
        {
            try
            {
                if (!EnsurePhoneFromPreferences())
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                var oldProfilePath = _currentUser?.ProfileImagePath ?? string.Empty;
                var oldCoverPath = _currentUser?.CoverImagePath ?? string.Empty;

                // Pick profile image first
                var profileResult = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Select profile image" });
                string? profileSaved = null;
                if (profileResult != null)
                {
                    profileSaved = await SavePickedFileAsync(profileResult, $"profile_{_phone}_{DateTime.UtcNow:yyyyMMddHHmmss}{System.IO.Path.GetExtension(profileResult.FileName)}");
                }

                // Pick cover image next
                var coverResult = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Select cover image" });
                string? coverSaved = null;
                if (coverResult != null)
                {
                    coverSaved = await SavePickedFileAsync(coverResult, $"cover_{_phone}_{DateTime.UtcNow:yyyyMMddHHmmss}{System.IO.Path.GetExtension(coverResult.FileName)}");
                }

                if (profileSaved == null && coverSaved == null)
                {
                    // user cancelled both
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user == null)
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                bool profileUpdated = false;
                bool coverUpdated = false;

                if (!string.IsNullOrEmpty(profileSaved))
                {
                    user.ProfileImagePath = profileSaved;
                    profileUpdated = true;
                }

                if (!string.IsNullOrEmpty(coverSaved))
                {
                    user.CoverImagePath = coverSaved;
                    coverUpdated = true;
                }

                await db.UpdateAsync(user);

                // ========== TRACK PROFILE PICTURE CHANGES ==========
                if (profileUpdated)
                {
                    await TrackProfilePictureChangeAsync(_phone, "Profile", oldProfilePath, profileSaved);
                }
                if (coverUpdated)
                {
                    await TrackProfilePictureChangeAsync(_phone, "Cover", oldCoverPath, coverSaved);
                }

                // Refresh UI
                if (profileUpdated)
                {
                    var overlay = this.FindByName<Image>("ProfileImageOverlay");
                    if (overlay != null) overlay.Source = ImageSource.FromFile(profileSaved);
                    UpdateProfileEditIconVisibility();
                }

                if (coverUpdated)
                {
                    var cover = this.FindByName<Image>("CoverImageOverlay");
                    if (cover != null) cover.Source = ImageSource.FromFile(coverSaved);
                    UpdateCoverEditIconVisibility();
                }

                // Send notification that profile was updated (for AppShell flyout)
                // Only send if at least one image was updated
                if (profileUpdated || coverUpdated)
                {
                    MessagingCenter.Send(this, "ProfileUpdated");
                }

                // Rebuild media & posts
                await LoadUserAsync(_phone);
                await DisplayAlert("Saved", "Images updated.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChangeBothImagesAsync error: " + ex);
                await DisplayAlert("Error", "Failed to update images: " + ex.Message, "OK");
            }
        }


        private async void SignOutButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Clear stored login so app won't auto-sign-in next launch
                Preferences.Remove(CurrentUserPhoneKey);
                _phone = string.Empty;

                // Send message that user logged out to clear flyout profile
                MessagingCenter.Send(this, "UserLoggedOut");

                // Navigate to the login page (root)
                await Shell.Current.GoToAsync("///LoginPage");
            }
            catch
            {
                // ignore storage errors and continue to navigate to login
                await Shell.Current.GoToAsync("///LoginPage");
            }
        }


        // Tab buttons handler to switch panels (supports 8 tabs)
        // Tab buttons handler to switch panels (supports all tabs with green underline indicators)
        private void ProfileTabButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Get the label that was tapped
                Label tappedLabel = sender as Label;
                if (tappedLabel == null) return;

                // Existing panels
                var info = this.FindByName<VisualElement>("ProfileInfoPanel");
                var prefs = this.FindByName<VisualElement>("ProfilePreferencesPanel");
                var interests = this.FindByName<VisualElement>("ProfileInterestsPanel");
                var media = this.FindByName<VisualElement>("ProfileMediaPanel");
                var prompts = this.FindByName<VisualElement>("ProfilePromptsPanel");
                var dates = this.FindByName<VisualElement>("ProfileDateIdeasPanel");
                var events = this.FindByName<VisualElement>("ProfileEventsPanel");
                var safety = this.FindByName<VisualElement>("ProfileSafetyPanel");
                var stats = this.FindByName<VisualElement>("ProfileStatsPanel");
                var endorsements = this.FindByName<VisualElement>("ProfileEndorsementsPanel");

                // All tab labels and their indicators
                var infoLabel = this.FindByName<Label>("ProfileInfoButton");
                var prefsLabel = this.FindByName<Label>("ProfilePrefsButton");
                var interestsLabel = this.FindByName<Label>("ProfileInterestsButton");
                var mediaLabel = this.FindByName<Label>("ProfileMediaButton");
                var promptsLabel = this.FindByName<Label>("ProfilePromptsButton");
                var datesLabel = this.FindByName<Label>("ProfileDatesButton");
                var eventsLabel = this.FindByName<Label>("ProfileEventsButton");
                var safetyLabel = this.FindByName<Label>("ProfileSafetyButton");
                var endorsementsLabel = this.FindByName<Label>("ProfileEndorsementsButton");

                // All indicator boxes (the green underline)
                var infoIndicator = this.FindByName<BoxView>("ProfileTabIndicator");
                var prefsIndicator = this.FindByName<BoxView>("ProfilePrefsIndicator");
                var interestsIndicator = this.FindByName<BoxView>("ProfileInterestsIndicator");
                var mediaIndicator = this.FindByName<BoxView>("ProfileMediaIndicator");
                var promptsIndicator = this.FindByName<BoxView>("ProfilePromptsIndicator");
                var datesIndicator = this.FindByName<BoxView>("ProfileDatesIndicator");
                var eventsIndicator = this.FindByName<BoxView>("ProfileEventsIndicator");
                var safetyIndicator = this.FindByName<BoxView>("ProfileSafetyIndicator");
                var endorsementsIndicator = this.FindByName<BoxView>("ProfileEndorsementsIndicator");

                // Reset all panels visibility
                if (info != null) info.IsVisible = false;
                if (prefs != null) prefs.IsVisible = false;
                if (interests != null) interests.IsVisible = false;
                if (media != null) media.IsVisible = false;
                if (prompts != null) prompts.IsVisible = false;
                if (dates != null) dates.IsVisible = false;
                if (events != null) events.IsVisible = false;
                if (safety != null) safety.IsVisible = false;
                if (stats != null) stats.IsVisible = false;
                if (endorsements != null) endorsements.IsVisible = false;

                // Reset all text colors to default (dark gray)
                void ResetLabel(Label lbl)
                {
                    if (lbl != null)
                        lbl.TextColor = Color.FromArgb("#666666");
                }

                ResetLabel(infoLabel);
                ResetLabel(prefsLabel);
                ResetLabel(interestsLabel);
                ResetLabel(mediaLabel);
                ResetLabel(promptsLabel);
                ResetLabel(datesLabel);
                ResetLabel(eventsLabel);
                ResetLabel(safetyLabel);
                ResetLabel(endorsementsLabel);

                // Hide all indicators (set width to 0)
                void HideIndicator(BoxView indicator)
                {
                    if (indicator != null)
                        indicator.WidthRequest = 0;
                }

                HideIndicator(infoIndicator);
                HideIndicator(prefsIndicator);
                HideIndicator(interestsIndicator);
                HideIndicator(mediaIndicator);
                HideIndicator(promptsIndicator);
                HideIndicator(datesIndicator);
                HideIndicator(eventsIndicator);
                HideIndicator(safetyIndicator);
                HideIndicator(endorsementsIndicator);

                // Show selected panel and highlight the label
                if (tappedLabel == infoLabel)
                {
                    if (info != null) info.IsVisible = true;
                    if (infoLabel != null) infoLabel.TextColor = Color.FromArgb("#008080");
                    if (infoIndicator != null) infoIndicator.WidthRequest = 24;
                    _currentTab = "Profile";
                }
                else if (tappedLabel == prefsLabel)
                {
                    if (prefs != null) prefs.IsVisible = true;
                    if (prefsLabel != null) prefsLabel.TextColor = Color.FromArgb("#008080");
                    if (prefsIndicator != null) prefsIndicator.WidthRequest = 24;
                    _currentTab = "Preferences";
                }
                else if (tappedLabel == interestsLabel)
                {
                    if (interests != null) interests.IsVisible = true;
                    if (interestsLabel != null) interestsLabel.TextColor = Color.FromArgb("#008080");
                    if (interestsIndicator != null) interestsIndicator.WidthRequest = 24;
                    _currentTab = "Hobbies";
                }
                else if (tappedLabel == mediaLabel)
                {
                    if (media != null) media.IsVisible = true;
                    if (mediaLabel != null) mediaLabel.TextColor = Color.FromArgb("#008080");
                    if (mediaIndicator != null) mediaIndicator.WidthRequest = 24;
                    _currentTab = "Gallery";
                }
                else if (tappedLabel == promptsLabel && prompts != null)
                {
                    prompts.IsVisible = true;
                    if (promptsLabel != null) promptsLabel.TextColor = Color.FromArgb("#008080");
                    if (promptsIndicator != null) promptsIndicator.WidthRequest = 24;
                    _currentTab = "Prompts";
                    if (_currentUserId > 0)
                        _ = LoadUserPromptsAsync(_currentUserId);
                }
                else if (tappedLabel == datesLabel && dates != null)
                {
                    dates.IsVisible = true;
                    if (datesLabel != null) datesLabel.TextColor = Color.FromArgb("#008080");
                    if (datesIndicator != null) datesIndicator.WidthRequest = 24;
                    _currentTab = "DateIdeas";
                    if (_currentUserId > 0)
                        _ = LoadUserDateIdeasAsync(_currentUserId);
                }
                else if (tappedLabel == eventsLabel && events != null)
                {
                    events.IsVisible = true;
                    if (eventsLabel != null) eventsLabel.TextColor = Color.FromArgb("#008080");
                    if (eventsIndicator != null) eventsIndicator.WidthRequest = 24;
                    _currentTab = "Events";
                    if (_currentUserId > 0)
                        _ = LoadUserEventsAsync(_currentUserId);
                }
                else if (tappedLabel == safetyLabel && safety != null)
                {
                    safety.IsVisible = true;
                    if (safetyLabel != null) safetyLabel.TextColor = Color.FromArgb("#008080");
                    if (safetyIndicator != null) safetyIndicator.WidthRequest = 24;
                    _currentTab = "Safety";
                    LoadSafetyInfo();
                }
                else if (tappedLabel == endorsementsLabel && endorsements != null)
                {
                    endorsements.IsVisible = true;
                    if (endorsementsLabel != null) endorsementsLabel.TextColor = Color.FromArgb("#008080");
                    if (endorsementsIndicator != null) endorsementsIndicator.WidthRequest = 24;
                    _currentTab = "Endorsements";
                    if (_currentUserId > 0)
                        _ = LoadEndorsementsAsync(_currentUserId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tab switch error: {ex.Message}");
            }
        }
        // Load user prompts
        private async void LoadPrompts()
        {
            try
            {
                if (string.IsNullOrEmpty(_phone)) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get user ID from phone number
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();
                if (user == null) return;

                var prompts = await db.Table<UserPrompt>().Where(p => p.UserId == user.Id).OrderBy(p => p.Order).ToListAsync();

                var promptsCv = this.FindByName<CollectionView>("PromptsCollectionView");
                if (promptsCv != null)
                    promptsCv.ItemsSource = prompts;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPrompts error: {ex.Message}");
            }
        }

        // Load date ideas
        private async void LoadDateIdeas()
        {
            try
            {
                if (string.IsNullOrEmpty(_phone)) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();
                if (user == null) return;

                var dateIdeas = await db.Table<DateIdea>().Where(d => d.UserId == user.Id).OrderByDescending(d => d.CreatedAt).ToListAsync();

                var datesCv = this.FindByName<CollectionView>("DateIdeasCollectionView");
                if (datesCv != null)
                    datesCv.ItemsSource = dateIdeas;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDateIdeas error: {ex.Message}");
            }
        }

        // Add this method for the post menu button
        private async void OnPostMenuButtonClicked(object sender, EventArgs e)
        {
            try
            {
                Lock.Models.Post? post = null;

                // Try to get post from different sources
                if (sender is Button button && button.CommandParameter is Lock.Models.Post buttonPost)
                {
                    post = buttonPost;
                }
                else if (sender is TapGestureRecognizer tap && tap.CommandParameter is Lock.Models.Post tapPost)
                {
                    post = tapPost;
                }
                else if (sender is Grid grid && grid.BindingContext is Lock.Models.Post gridPost)
                {
                    post = gridPost;
                }
                else if (sender is VisualElement ve && ve.BindingContext is Lock.Models.Post vePost)
                {
                    post = vePost;
                }

                if (post == null)
                {
                    Debug.WriteLine("OnPostMenuButtonClicked: Could not find post");
                    await DisplayAlert("Error", "Could not identify which post to edit", "OK");
                    return;
                }

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                bool isOwner = string.Equals(post.AuthorPhone?.Trim(), currentUserPhone?.Trim(), StringComparison.OrdinalIgnoreCase);

                Debug.WriteLine($"Post menu opened - Post ID: {post.Id}, IsOwner: {isOwner}");

                var actionsPage = new Lock.Pages.Post.PostActionsPage(
                    post,
                    onEdit: async (p) =>
                    {
                        Debug.WriteLine($"Edit post clicked for post {p.Id}");
                        await HandleEditPost(p);
                    },
                    onDelete: async (p) =>
                    {
                        Debug.WriteLine($"Delete post clicked for post {p.Id}");
                        var confirm = await DisplayAlert("Delete Post",
                            "Are you sure you want to delete this post?",
                            "Yes", "No");

                        if (confirm)
                        {
                            await HandleDeletePost(p);
                        }
                    }
                );

                await Navigation.PushModalAsync(actionsPage, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnPostMenuButtonClicked: {ex}");
                await DisplayAlert("Error", "Could not open post menu: " + ex.Message, "OK");
            }
        }

        // Add these helper methods
        private async Task HandleEditPost(Lock.Models.Post post)
        {
            try
            {
                // Navigate to PostPage with edit mode
                // You can pass the post ID to edit
                var navigationParams = new Dictionary<string, object>
                {
                    ["editPostId"] = post.Id
                };

                // Send a message to PostPage to enter edit mode
                MessagingCenter.Send(this, "EditPost", post.Id);

                // Navigate to PostPage
                await Shell.Current.GoToAsync("//post");

                // Close the actions page
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HandleEditPost error: {ex}");
                await DisplayAlert("Error", "Could not edit post: " + ex.Message, "OK");
            }
        }

        private async Task HandleDeletePost(Lock.Models.Post post)
        {
            try
            {
                await PostRepository.DeleteAsync(post.Id);

                // Remove from local list
                var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView");
                if (collectionView?.ItemsSource is System.Collections.IEnumerable currentSource)
                {
                    var postsList = currentSource.Cast<Lock.Models.Post>().ToList();
                    postsList.RemoveAll(p => p.Id == post.Id);
                    collectionView.ItemsSource = null;
                    collectionView.ItemsSource = postsList;
                }

                // Refresh the entire user data
                if (!string.IsNullOrEmpty(_phone))
                {
                    await LoadUserAsync(_phone);
                }

                // Notify other pages
                MessagingCenter.Send(this, "PostDeleted", post.Id);

                await DisplayAlert("Deleted", "Post deleted successfully", "OK");

                // Close the actions page if still open
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HandleDeletePost error: {ex}");
                await DisplayAlert("Error", "Could not delete post: " + ex.Message, "OK");
            }
        }
        // Add this method for saving/unsaving posts
        private async void OnSavePostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not Lock.Models.Post post) return;

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

                            // Refresh the UI - Force refresh the collection view
                            var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView");
                            if (collectionView?.ItemsSource is IEnumerable<Lock.Models.Post> posts)
                            {
                                // Force a refresh by resetting ItemsSource
                                var currentList = posts.ToList();
                                collectionView.ItemsSource = null;
                                collectionView.ItemsSource = currentList;
                            }
                        }
                    }
                    else if (action == "Move to Category")
                    {
                        await MovePostToCategory(post, currentUserPhone);
                    }
                    else if (action == "View Post")
                    {
                        await NavigateToCommentsPage(post);
                    }
                }
                else
                {
                    // Show existing folders + option to create new
                    var allSaved = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                    var existingFolders = allSaved
                        .Select(s => string.IsNullOrEmpty(s.FolderName) ? "Saved" : s.FolderName)
                        .Distinct()
                        .OrderBy(f => f)
                        .ToList();

                    string chosenFolder;

                    if (existingFolders.Any())
                    {
                        var options = existingFolders.Concat(new[] { "? New Category" }).ToArray();
                        var picked = await DisplayActionSheet("Save to Category", "Cancel", null, options);

                        if (picked == null || picked == "Cancel") return;

                        if (picked == "? New Category")
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
                        await DisplayAlert("Saved!", $"Post saved to '{chosenFolder}'.\nFind it in Hidden ? Saved tab.", "OK");
                        MessagingCenter.Send(this, "PostSaved", post.Id);

                        // Refresh the UI - Force refresh the collection view
                        var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView");
                        if (collectionView?.ItemsSource is IEnumerable<Lock.Models.Post> posts)
                        {
                            // Force a refresh by resetting ItemsSource
                            var currentList = posts.ToList();
                            collectionView.ItemsSource = null;
                            collectionView.ItemsSource = currentList;
                        }
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
        // Add this helper method to navigate to CommentsPage
        private async Task NavigateToCommentsPage(Lock.Models.Post post)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var commentsPage = new Lock.Pages.Post.CommentsPage(post.Id, currentUserPhone);
                await Navigation.PushAsync(commentsPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigateToCommentsPage error: {ex}");
                await DisplayAlert("Error", "Could not open post", "OK");
            }
        }

        // Add this method for moving post to category
        private async Task MovePostToCategory(Lock.Models.Post post, string currentUserPhone)
        {
            try
            {
                var savedItems = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);
                var existingCategories = savedItems
                    .Where(s => s.Post.Id != post.Id)
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

        // Load events
        private async void LoadEvents(string filter = "Upcoming")
        {
            try
            {
                if (string.IsNullOrEmpty(_phone)) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();
                if (user == null) return;

                List<UserEvent> events = new List<UserEvent>();
                var now = DateTime.UtcNow;

                if (filter == "Upcoming")
                {
                    events = await db.Table<UserEvent>()
                        .Where(e => e.UserId == user.Id && e.EventDate > now)
                        .OrderBy(e => e.EventDate)
                        .ToListAsync();
                }
                else if (filter == "Past")
                {
                    events = await db.Table<UserEvent>()
                        .Where(e => e.UserId == user.Id && e.EventDate <= now)
                        .OrderByDescending(e => e.EventDate)
                        .ToListAsync();
                }
                else if (filter == "Hosting")
                {
                    events = await db.Table<UserEvent>()
                        .Where(e => e.UserId == user.Id)
                        .OrderByDescending(e => e.EventDate)
                        .ToListAsync();
                }

                var eventsCv = this.FindByName<CollectionView>("EventsCollectionView");
                if (eventsCv != null)
                    eventsCv.ItemsSource = events;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadEvents error: {ex.Message}");
            }
        }

        // Events filter changed
        private void EventsFilterPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var picker = sender as Picker;
            if (picker?.SelectedIndex < 0) return;

            var filter = picker.Items[picker.SelectedIndex];
            LoadEvents(filter);
        }

        // Load profile stats
        private async void LoadProfileStats()
        {
            try
            {
                if (string.IsNullOrEmpty(_phone)) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();
                if (user == null) return;

                // Get join date
                var joinDateLabel = this.FindByName<Label>("JoinDateLabel");
                if (joinDateLabel != null)
                    joinDateLabel.Text = user.JoinDate.ToString("yyyy");

                // Get mood history
                var moodHistoryLabel = this.FindByName<Label>("MoodHistoryLabel");
                if (moodHistoryLabel != null)
                {
                    // This would ideally track mood changes in a separate table
                    // For now, just show when mood was last updated
                    moodHistoryLabel.Text = $"Last mood update: {user.GetMoodLastUpdatedRelative()}";
                }

                // For demo purposes, set some sample stats
                // In production, these would come from actual tracking tables
                var viewsLabel = this.FindByName<Label>("ProfileViewsCount");
                if (viewsLabel != null) viewsLabel.Text = "24";

                var matchesLabel = this.FindByName<Label>("MatchesCount");
                if (matchesLabel != null) matchesLabel.Text = "8";

                var responseLabel = this.FindByName<Label>("ResponseRateLabel");
                if (responseLabel != null) responseLabel.Text = "75%";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProfileStats error: {ex.Message}");
            }
        }

        private async void AddDateIdeaButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            // Create a simple page for date idea input with ScrollView
            var titleEntry = new Entry
            {
                Placeholder = "Title (e.g., Cozy coffee shop)",
                FontSize = 14
            };

            var descEntry = new Entry
            {
                Placeholder = "Description",
                FontSize = 14
            };

            var locationEntry = new Entry
            {
                Placeholder = "Location",
                FontSize = 14
            };

            var categoryLabel = new Label
            {
                Text = "Category",
                FontSize = 12,
                TextColor = Color.FromArgb("#666666")
            };

            var categoryPicker = new Picker
            {
                Title = "Select a category",
                FontSize = 14
            };

            // Add items to the picker
            categoryPicker.Items.Add("Coffee");
            categoryPicker.Items.Add("Dinner");
            categoryPicker.Items.Add("Outdoor");
            categoryPicker.Items.Add("Drinks");
            categoryPicker.Items.Add("Activity");
            categoryPicker.Items.Add("Cultural");
            categoryPicker.Items.Add("Movie");
            categoryPicker.Items.Add("Concert");
            categoryPicker.Items.Add("Other");

            categoryPicker.SelectedIndex = 0;

            // Create a ScrollView to contain all content
            var scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new StackLayout
                {
                    Padding = 20,
                    Spacing = 12,
                    Children =
            {
                titleEntry,
                descEntry,
                locationEntry,
                categoryLabel,
                categoryPicker
            }
                }
            };

            var page = new ContentPage
            {
                Title = "Add Date Idea",
                Content = scrollView
            };

            var saveButton = new Button
            {
                Text = "Save",
                BackgroundColor = Color.FromArgb("#008080"),
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 10)
            };

            saveButton.Clicked += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(titleEntry.Text) || categoryPicker.SelectedIndex == -1)
                {
                    await page.DisplayAlert("Error", "Please fill all fields and select a category", "OK");
                    return;
                }

                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                    if (user != null)
                    {
                        var idea = await ProfileDataService.AddDateIdeaAsync(
                            user.Id,
                            titleEntry.Text,
                            descEntry.Text ?? "",
                            locationEntry.Text ?? "",
                            categoryPicker.Items[categoryPicker.SelectedIndex]
                        );

                        _userDateIdeas.Add(idea);

                        var datesCv = this.FindByName<CollectionView>("DateIdeasCollectionView");
                        if (datesCv != null)
                        {
                            datesCv.ItemsSource = null;
                            datesCv.ItemsSource = _userDateIdeas;
                        }
                    }

                    await Navigation.PopModalAsync();
                }
                catch (Exception ex)
                {
                    await page.DisplayAlert("Error", ex.Message, "OK");
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Colors.Gray,
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 20)
            };

            cancelButton.Clicked += async (s, args) => await Navigation.PopModalAsync();

            // Create a footer layout for buttons
            var buttonLayout = new StackLayout
            {
                Spacing = 10,
                Padding = 0,
                Children = { saveButton, cancelButton }
            };

            // Add buttons to the main content
            ((StackLayout)scrollView.Content).Children.Add(buttonLayout);

            await Navigation.PushModalAsync(new NavigationPage(page));
        }
        
        // Create event button
        private async void CreateEventButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            // Create event input page with ScrollView
            var nameEntry = new Entry
            {
                Placeholder = "Event name",
                FontSize = 14
            };

            var descEntry = new Editor
            {
                Placeholder = "Description",
                HeightRequest = 80,
                FontSize = 14
            };

            var locationEntry = new Entry
            {
                Placeholder = "Location",
                FontSize = 14
            };

            var dateLabel = new Label
            {
                Text = "Date",
                FontSize = 12,
                TextColor = Color.FromArgb("#666666")
            };

            var datePicker = new DatePicker
            {
                MinimumDate = DateTime.Today,
                FontSize = 14
            };

            var timeLabel = new Label
            {
                Text = "Time",
                FontSize = 12,
                TextColor = Color.FromArgb("#666666")
            };

            var timePicker = new TimePicker
            {
                Time = TimeSpan.FromHours(19), // 7 PM default
                FontSize = 14
            };

            // Create category picker with dropdown
            var categoryLabel = new Label
            {
                Text = "Category",
                FontSize = 12,
                TextColor = Color.FromArgb("#666666")
            };

            var categoryPicker = new Picker
            {
                Title = "Select a category",
                FontSize = 14
            };

            // Add categories to picker
            categoryPicker.Items.Add("Coffee");
            categoryPicker.Items.Add("Drinks");
            categoryPicker.Items.Add("Dinner");
            categoryPicker.Items.Add("Outdoor");
            categoryPicker.Items.Add("Music");
            categoryPicker.Items.Add("Sports");
            categoryPicker.Items.Add("Game Night");
            categoryPicker.Items.Add("Movie");
            categoryPicker.Items.Add("Art");
            categoryPicker.Items.Add("Food");
            categoryPicker.Items.Add("Other");

            // Set default selection
            categoryPicker.SelectedIndex = 0;

            var maxEntry = new Entry
            {
                Placeholder = "Max attendees (0 for unlimited)",
                Keyboard = Keyboard.Numeric,
                FontSize = 14
            };

            // Create a ScrollView to contain all content
            var scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new StackLayout
                {
                    Padding = 20,
                    Spacing = 12,
                    Children =
            {
                nameEntry,
                descEntry,
                locationEntry,
                dateLabel,
                datePicker,
                timeLabel,
                timePicker,
                categoryLabel,
                categoryPicker,
                maxEntry
            }
                }
            };

            var page = new ContentPage
            {
                Title = "Create Event",
                Content = scrollView
            };

            var createButton = new Button
            {
                Text = "Create Event",
                BackgroundColor = Color.FromArgb("#008080"),
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 10)
            };

            createButton.Clicked += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameEntry.Text) || categoryPicker.SelectedIndex == -1)
                {
                    await page.DisplayAlert("Error", "Please fill required fields and select a category", "OK");
                    return;
                }

                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                    if (user != null)
                    {
                        var eventDateTime = datePicker.Date.Add(timePicker.Time);

                        int maxAttendees = 0;
                        int.TryParse(maxEntry.Text, out maxAttendees);

                        var newEvent = await ProfileDataService.CreateEventAsync(
                            user.Id,
                            nameEntry.Text,
                            descEntry.Text ?? "",
                            locationEntry.Text ?? "",
                            eventDateTime,
                            categoryPicker.Items[categoryPicker.SelectedIndex],
                            maxAttendees
                        );

                        _userEvents.Add(newEvent);

                        // Refresh events display
                        var eventsCv = this.FindByName<CollectionView>("EventsCollectionView");
                        if (eventsCv != null)
                        {
                            eventsCv.ItemsSource = null;
                            eventsCv.ItemsSource = _userEvents;
                        }
                    }

                    await Navigation.PopModalAsync();
                    await DisplayAlert("Success", "Event created!", "OK");
                }
                catch (Exception ex)
                {
                    await page.DisplayAlert("Error", ex.Message, "OK");
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Colors.Gray,
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 20)
            };

            cancelButton.Clicked += async (s, args) => await Navigation.PopModalAsync();

            // Create a footer layout for buttons
            var buttonLayout = new StackLayout
            {
                Spacing = 10,
                Padding = 0,
                Children = { createButton, cancelButton }
            };

            // Add buttons to the main content
            ((StackLayout)scrollView.Content).Children.Add(buttonLayout);

            await Navigation.PushModalAsync(new NavigationPage(page));
        }
        // Verify button
        private async void VerifyButton_Clicked(object sender, EventArgs e)
        {
            await DisplayAlert("Verify", "Verification process will start", "OK");
        }

        // Blocked users button
        private async void BlockedUsersButton_Clicked(object sender, EventArgs e)
        {
            await DisplayAlert("Blocked Users", "List of blocked users", "OK");
        }

        // Emergency contacts button - Now navigates to the actual contacts page
        private async void EmergencyContactsButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_phone))
                {
                    await DisplayAlert("Error", "User not found", "OK");
                    return;
                }

                var contactsPage = new EmergencyContactsPage(_phone);
                await Navigation.PushAsync(contactsPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EmergencyContactsButton_Clicked error: {ex}");
                await DisplayAlert("Error", "Could not open emergency contacts", "OK");
            }
        }

       
        // Safety tips button
        private async void SafetyTipsButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var safetyTipsPage = new SafetyTipsPage();
                await Navigation.PushAsync(safetyTipsPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafetyTipsButton_Clicked error: {ex}");
                await DisplayAlert("Error", "Could not open safety tips", "OK");
            }
        }

        private void UpdateCoverEditIconVisibility()
        {
            var coverImage = this.FindByName<Image>("CoverImageOverlay");
            var coverEditIcon = this.FindByName<Border>("CoverEditIcon");
            if (coverImage == null || coverEditIcon == null) return;
            // Show edit icon ONLY when there is NO image loaded
            bool hasImage = coverImage.Source != null;
            coverEditIcon.IsVisible = !hasImage && !_viewOnly; // also respect view-only mode
        }

        // Tag buttons toggle selection
        private void TagButton_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                var selected = btn.BindingContext as bool? ?? false;
                selected = !selected;
                btn.BindingContext = selected;
                if (selected)
                {
                    btn.BackgroundColor = Color.FromArgb("#3B82F6");
                    btn.TextColor = Colors.White;
                }
                else
                {
                    btn.BackgroundColor = Color.FromArgb("#EEE");
                    btn.TextColor = Colors.Black;
                }
            }
        }

        // add this method to the ProfilePage class (small copy of the toggle logic)
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

            // FIX: Check for null properly using 'is null' pattern
            if (post is null)
                return;

            // FIX: Use null-forgiving operator (!) since we've already checked post is not null
            post!.IsExpanded = !post.IsExpanded;
            post!.UpdateDisplayContent(200);

            try
            {
                var cv = this.FindByName<CollectionView>("UserPostsCollectionView");
                if (cv != null)
                {
                    cv.ScrollTo(post, position: ScrollToPosition.Start);
                }
            }
            catch
            {
                // ignore scroll errors
            }
        }

        // Top interest 'Other' handler
        private void TopInterestPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var picker = sender as Picker;
                var topInterestOtherLayout = this.FindByName<HorizontalStackLayout>("TopInterestOtherLayout");
                if (picker == null || topInterestOtherLayout == null) return;
                var selected = picker.SelectedIndex >= 0 ? picker.Items[picker.SelectedIndex] : null;
                if (string.Equals(selected, "Other", StringComparison.OrdinalIgnoreCase))
                {
                    topInterestOtherLayout.IsVisible = true;
                }
                else
                {
                    topInterestOtherLayout.IsVisible = false;
                }
            }
            catch { }
        }

        private void TopInterestAddButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var entry = this.FindByName<Entry>("TopInterestOtherEntry");
                var picker = this.FindByName<Picker>("TopInterestPicker");
                var topInterestOtherLayout = this.FindByName<HorizontalStackLayout>("TopInterestOtherLayout");
                if (entry == null || picker == null || topInterestOtherLayout == null) return;
                var val = (entry.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(val)) return;
                if (!picker.Items.Contains(val))
                    picker.Items.Add(val);
                picker.SelectedItem = val;
                entry.Text = string.Empty;
                topInterestOtherLayout.IsVisible = false;
            }
            catch { }
        }

        private async Task LoadPendingEndorsementsAsync()
        {
            try
            {
                // Only load pending endorsements if this is the owner
                if (!IsOwner)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var pendingSection = this.FindByName<VerticalStackLayout>("PendingEndorsementsSection");
                        if (pendingSection != null) pendingSection.IsVisible = false;

                        var divider = this.FindByName<BoxView>("EndorsementDivider");
                        if (divider != null) divider.IsVisible = false;
                    });
                    return;
                }

                var requestsJson = Preferences.Get("endorsement_requests", "[]");
                var allRequests = System.Text.Json.JsonSerializer.Deserialize<List<PendingEndorsement>>(requestsJson) ?? new List<PendingEndorsement>();

                // Get only pending requests for the current user (where this user is the requestor)
                _pendingEndorsements = allRequests
                    .Where(r => r.Status == "pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var pendingSection = this.FindByName<VerticalStackLayout>("PendingEndorsementsSection");
                    var pendingCv = this.FindByName<CollectionView>("PendingEndorsementsCollectionView");
                    var divider = this.FindByName<BoxView>("EndorsementDivider");

                    if (_pendingEndorsements.Any())
                    {
                        if (pendingSection != null) pendingSection.IsVisible = true;
                        if (divider != null) divider.IsVisible = true;
                        if (pendingCv != null) pendingCv.ItemsSource = _pendingEndorsements;
                    }
                    else
                    {
                        if (pendingSection != null) pendingSection.IsVisible = false;
                        if (divider != null) divider.IsVisible = false;
                    }

                    // Update accepted count
                    var acceptedCountLabel = this.FindByName<Label>("AcceptedEndorsementsCountLabel");
                    if (acceptedCountLabel != null)
                    {
                        acceptedCountLabel.Text = $"({_endorsements.Count})";
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPendingEndorsementsAsync error: {ex}");
            }
        }

        private async void ResendEndorsementRequest_Clicked(object sender, EventArgs e)
        {
            try
            {
                PendingEndorsement pending = null;

                if (sender is TapGestureRecognizer tap)
                {
                    pending = tap.CommandParameter as PendingEndorsement;
                }
                else if (sender is Border border && border.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer borderTap)
                {
                    pending = borderTap.CommandParameter as PendingEndorsement;
                }
                else if (sender is VisualElement ve && ve.BindingContext is PendingEndorsement vePending)
                {
                    pending = vePending;
                }

                if (pending == null)
                {
                    Debug.WriteLine("ResendEndorsementRequest_Clicked: pending is null");
                    await DisplayAlert("Error", "Could not identify which request to resend", "OK");
                    return;
                }

                bool confirm = await DisplayAlert(
                    "Resend Request",
                    $"Resend endorsement request to {pending.FriendName}?",
                    "Resend",
                    "Cancel"
                );

                if (confirm)
                {
                    // Get current user
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var currentUser = await db.Table<User>().Where(u => u.PhoneNumber == currentUserPhone).FirstOrDefaultAsync();

                    if (currentUser != null)
                    {
                        // Get or create conversation
                        string conversationId = await GetOrCreateConversationAsync(currentUserPhone, pending.FriendPhone, pending.FriendName);

                        // Create new request message
                        var endorsementMessage = new ChatMessage
                        {
                            ConversationId = conversationId,
                            SenderPhone = currentUserPhone,
                            RecipientPhone = pending.FriendPhone,
                            MessageType = "endorsement_request",
                            Content = pending.Testimonial,
                            EndorsementRequestId = pending.RequestId,
                            EndorsementRequestorId = currentUser.Id.ToString(),
                            EndorsementRequestorName = currentUser.Name,
                            EndorsementTestimonial = pending.Testimonial,
                            EndorsementRating = pending.Rating,
                            EndorsementStatus = "pending",
                            SentAt = DateTime.UtcNow,
                            IsDelivered = true,
                            IsRead = false,
                            IsLocalOutgoing = true
                        };

                        await ChatRepository.AddMessageAsync(endorsementMessage);

                        // Update the pending request timestamp
                        pending.CreatedAt = DateTime.UtcNow;

                        // Update in local storage
                        var requestsJson = Preferences.Get("endorsement_requests", "[]");
                        var allRequests = System.Text.Json.JsonSerializer.Deserialize<List<PendingEndorsement>>(requestsJson) ?? new List<PendingEndorsement>();
                        var existing = allRequests.FirstOrDefault(r => r.RequestId == pending.RequestId);
                        if (existing != null)
                        {
                            existing.CreatedAt = DateTime.UtcNow;
                            Preferences.Set("endorsement_requests", System.Text.Json.JsonSerializer.Serialize(allRequests));
                        }

                        // Refresh the list
                        await LoadPendingEndorsementsAsync();

                        await DisplayAlert("Resent", $"Request resent to {pending.FriendName}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResendEndorsementRequest_Clicked error: {ex}");
                await DisplayAlert("Error", "Could not resend request", "OK");
            }
        }
        private async void CancelEndorsementRequest_Clicked(object sender, EventArgs e)
        {
            try
            {
                PendingEndorsement pending = null;

                if (sender is TapGestureRecognizer tap)
                {
                    pending = tap.CommandParameter as PendingEndorsement;
                }
                else if (sender is Border border && border.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer borderTap)
                {
                    pending = borderTap.CommandParameter as PendingEndorsement;
                }
                else if (sender is VisualElement ve && ve.BindingContext is PendingEndorsement vePending)
                {
                    pending = vePending;
                }

                if (pending == null)
                {
                    Debug.WriteLine("CancelEndorsementRequest_Clicked: pending is null");
                    await DisplayAlert("Error", "Could not identify which request to cancel", "OK");
                    return;
                }

                bool confirm = await DisplayAlert(
                    "Cancel Request",
                    $"Cancel endorsement request to {pending.FriendName}?",
                    "Cancel Request",
                    "Keep"
                );

                if (confirm)
                {
                    // Remove from local storage
                    var requestsJson = Preferences.Get("endorsement_requests", "[]");
                    var allRequests = System.Text.Json.JsonSerializer.Deserialize<List<PendingEndorsement>>(requestsJson) ?? new List<PendingEndorsement>();

                    allRequests.RemoveAll(r => r.RequestId == pending.RequestId);
                    Preferences.Set("endorsement_requests", System.Text.Json.JsonSerializer.Serialize(allRequests));

                    // Refresh the list
                    await LoadPendingEndorsementsAsync();

                    await DisplayAlert("Cancelled", $"Endorsement request to {pending.FriendName} cancelled", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CancelEndorsementRequest_Clicked error: {ex}");
                await DisplayAlert("Error", "Could not cancel request", "OK");
            }
        }

        // Add this method to track profile picture changes
        private async Task TrackProfilePictureChangeAsync(string userPhone, string pictureType, string oldPath, string newPath)
        {
            try
            {
                await UserTrackingService.Instance.TrackProfileChangeAsync(
                    userPhone,
                    $"{pictureType}Image",
                    string.IsNullOrEmpty(oldPath) ? "None" : "Has image",
                    string.IsNullOrEmpty(newPath) ? "None" : "Has image",
                    _currentUser?.Name ?? userPhone);
                Debug.WriteLine($"[TRACKING] {pictureType} image changed for {userPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackProfilePictureChangeAsync error: {ex}");
            }
        }


        // Save preferences back to DB
        private async void SaveProfileButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (!EnsurePhoneFromPreferences())
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // ========== GET OLD USER DATA BEFORE UPDATE ==========
                var oldUser = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user == null)
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                // Track if mood changed
                string oldMood = user.Mood;

                // Load controls
                var moodPicker = this.FindByName<Picker>("MoodPicker");
                var energyPicker = this.FindByName<Picker>("EnergyPicker");
                var countryEntry = this.FindByName<Entry>("CountryEntry");
                var stateEntry = this.FindByName<Entry>("StateEntry");
                var bioEditorInfo = this.FindByName<Editor>("BioEditorInfo");
                var drinksPicker = this.FindByName<Picker>("DrinksPicker");
                var smokesSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("SmokesSwitch");
                var petsSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("PetsSwitch");
                var religionEntry = this.FindByName<Entry>("ReligionEntry");
                var politicalEntry = this.FindByName<Entry>("PoliticalEntry");

                // Load physical attributes pickers
                var heightPicker = this.FindByName<Picker>("HeightPicker");
                var bodyTypePicker = this.FindByName<Picker>("BodyTypePicker");
                var ethnicityPicker = this.FindByName<Picker>("EthnicityPicker");
                var tribePicker = this.FindByName<Picker>("TribePicker");

                // Load Kids/Family Pickers
                var kidsPreferencePicker = this.FindByName<Picker>("KidsPreferencePicker");
                var hasChildrenPicker = this.FindByName<Picker>("HasChildrenPicker");

                // Load Dietary Preference Picker
                var dietaryPreferencePicker = this.FindByName<Picker>("DietaryPreferencePicker");

                // Load Exercise Frequency Picker
                var exerciseFrequencyPicker = this.FindByName<Picker>("ExerciseFrequencyPicker");

                // Load Personality Type Picker
                var personalityTypePicker = this.FindByName<Picker>("PersonalityTypePicker");

                // Load Love Language Picker
                var loveLanguagePicker = this.FindByName<Picker>("LoveLanguagePicker");

                // Get new mood value
                string newMood = moodPicker?.SelectedItem as string ?? string.Empty;

                // Store old location for comparison
                string oldCountry = user.Country ?? string.Empty;
                string oldState = user.State ?? string.Empty;

                // Save existing fields
                user.Mood = newMood;
                user.EnergyLevel = energyPicker?.SelectedItem as string ?? string.Empty;
                user.Country = countryEntry?.Text ?? string.Empty;
                user.State = stateEntry?.Text ?? string.Empty;
                user.Bio = bioEditorInfo?.Text ?? string.Empty;
                user.Drinks = drinksPicker?.SelectedItem as string ?? string.Empty;
                user.Smokes = smokesSwitch?.IsToggled ?? false;
                user.HasPets = petsSwitch?.IsToggled ?? false;
                user.Religion = religionEntry?.Text ?? string.Empty;
                user.PoliticalViews = politicalEntry?.Text ?? string.Empty;

                // Save Height
                if (heightPicker != null && heightPicker.SelectedItem != null)
                {
                    string heightText = heightPicker.SelectedItem.ToString();
                    string heightNumber = heightText.Replace(" cm", "").Trim();
                    if (int.TryParse(heightNumber, out int heightCm))
                    {
                        user.HeightCm = heightCm;
                    }
                }

                // Save Body Type
                if (bodyTypePicker != null && bodyTypePicker.SelectedItem != null)
                {
                    user.BodyType = bodyTypePicker.SelectedItem.ToString();
                }

                // Save Ethnicity
                if (ethnicityPicker != null && ethnicityPicker.SelectedItem != null)
                {
                    user.Ethnicity = ethnicityPicker.SelectedItem.ToString();
                }

                // Save Tribe
                if (tribePicker != null && tribePicker.SelectedItem != null)
                {
                    user.Tribe = tribePicker.SelectedItem.ToString();
                }

                // Save Kids/Family Preferences
                if (kidsPreferencePicker != null && kidsPreferencePicker.SelectedItem != null)
                {
                    user.KidsPreference = kidsPreferencePicker.SelectedItem.ToString();
                }

                if (hasChildrenPicker != null && hasChildrenPicker.SelectedItem != null)
                {
                    user.HasChildren = hasChildrenPicker.SelectedItem.ToString();
                }

                // Save Dietary Preference
                if (dietaryPreferencePicker != null && dietaryPreferencePicker.SelectedItem != null)
                {
                    user.DietaryPreference = dietaryPreferencePicker.SelectedItem.ToString();
                }

                // Save Exercise Frequency
                if (exerciseFrequencyPicker != null && exerciseFrequencyPicker.SelectedItem != null)
                {
                    user.ExerciseFrequency = exerciseFrequencyPicker.SelectedItem.ToString();
                }

                // Save Personality Type
                if (personalityTypePicker != null && personalityTypePicker.SelectedItem != null)
                {
                    string selectedPersonality = personalityTypePicker.SelectedItem.ToString();
                    if (selectedPersonality != "Prefer not to say")
                    {
                        user.PersonalityType = selectedPersonality;
                    }
                    else
                    {
                        user.PersonalityType = null;
                    }
                }

                // Save Love Language
                if (loveLanguagePicker != null && loveLanguagePicker.SelectedItem != null)
                {
                    string selectedLoveLanguage = loveLanguagePicker.SelectedItem.ToString();
                    if (selectedLoveLanguage != "Prefer not to say")
                    {
                        user.LoveLanguage = selectedLoveLanguage;
                    }
                    else
                    {
                        user.LoveLanguage = null;
                    }
                }

                // Check if mood changed - update timestamp and track
                if (oldMood != newMood)
                {
                    user.MoodLastUpdated = DateTime.UtcNow;

                    // ========== TRACK MOOD CHANGE ==========
                    await UserTrackingService.Instance.TrackMoodChangeAsync(_phone, oldMood, newMood, "profile");
                    Debug.WriteLine($"[TRACKING] Mood change tracked: '{oldMood}' -> '{newMood}'");

                    MessagingCenter.Send(this, "MoodUpdated");
                }

                // Collect selected tags
                var tagNames = new[] { "Travel", "Fitness", "Tech", "Music", "Coffee lover", "Gym", "Entrepreneur" };
                var selectedTags = tagNames.Where(t =>
                {
                    var btnName = t.Replace(" ", string.Empty);
                    var btn = this.FindByName<Button>($"Tag{btnName}");
                    return (btn?.BindingContext as bool?) == true;
                }).ToArray();
                user.Interests = string.Join(",", selectedTags);

                // Save "Allow Mood Search" toggle
                var moodSearchSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("MoodSearchSwitch");
                if (moodSearchSwitch != null)
                {
                    user.AllowMoodSearch = moodSearchSwitch.IsToggled;
                }

                // Save Ghost Mode + Mood Shield toggle
                var ghostSwitch = this.FindByName<Microsoft.Maui.Controls.Switch>("GhostModeMoodShieldSwitch");
                if (ghostSwitch != null)
                {
                    user.GhostModeMoodShield = ghostSwitch.IsToggled;
                }

                // ─────────────────────────────────────────────────────────────────
                // FIX: Save Phone Number Visibility toggle
                // ─────────────────────────────────────────────────────────────────
                var hidePhoneSwitchSave = this.FindByName<Microsoft.Maui.Controls.Switch>("HidePhoneSwitch");
                if (hidePhoneSwitchSave != null)
                {
                    user.HidePhoneNumber = hidePhoneSwitchSave.IsToggled;
                }
                // ─────────────────────────────────────────────────────────────────

                // Save to database
                await db.UpdateAsync(user);

                // ========== TRACK ALL PROFILE CHANGES ==========
                if (oldUser != null)
                {
                    await UserTrackingService.Instance.TrackAllProfileChangesAsync(oldUser, user);
                    Debug.WriteLine($"[TRACKING] All profile changes tracked for: {_phone}");
                }

                Debug.WriteLine("User preferences saved to database");

                // Update the UI labels with new values
                var heightLabel = this.FindByName<Label>("HeightLabel");
                if (heightLabel != null && user.HeightCm.HasValue && user.HeightCm.Value > 0)
                {
                    int feet = (int)(user.HeightCm.Value / 30.48);
                    int inches = (int)((user.HeightCm.Value % 30.48) / 2.54);
                    heightLabel.Text = $"{feet}'{inches}\" ({user.HeightCm.Value}cm)";
                }

                var bodyTypeLabel = this.FindByName<Label>("BodyTypeLabel");
                if (bodyTypeLabel != null)
                {
                    bodyTypeLabel.Text = string.IsNullOrEmpty(user.BodyType) ? "—" : user.BodyType;
                }

                var ethnicityLabel = this.FindByName<Label>("EthnicityLabel");
                if (ethnicityLabel != null)
                {
                    if (!string.IsNullOrEmpty(user.Ethnicity) && !string.IsNullOrEmpty(user.Tribe))
                    {
                        ethnicityLabel.Text = $"{user.Ethnicity} · {user.Tribe}";
                    }
                    else if (!string.IsNullOrEmpty(user.Ethnicity))
                    {
                        ethnicityLabel.Text = user.Ethnicity;
                    }
                    else if (!string.IsNullOrEmpty(user.Tribe))
                    {
                        ethnicityLabel.Text = user.Tribe;
                    }
                    else
                    {
                        ethnicityLabel.Text = "—";
                    }
                }

                // Update Family Label
                var familyLabel = this.FindByName<Label>("FamilyLabel");
                if (familyLabel != null)
                {
                    string familyText = string.Empty;
                    if (!string.IsNullOrEmpty(user.KidsPreference) && !string.IsNullOrEmpty(user.HasChildren))
                    {
                        familyText = $"{user.KidsPreference} · {user.HasChildren}";
                    }
                    else if (!string.IsNullOrEmpty(user.KidsPreference))
                    {
                        familyText = user.KidsPreference;
                    }
                    else if (!string.IsNullOrEmpty(user.HasChildren))
                    {
                        familyText = user.HasChildren;
                    }
                    else
                    {
                        familyText = "—";
                    }
                    familyLabel.Text = familyText;
                }

                // Update Diet Label
                var dietLabel = this.FindByName<Label>("DietLabel");
                if (dietLabel != null)
                {
                    dietLabel.Text = string.IsNullOrEmpty(user.DietaryPreference) ? "—" : user.DietaryPreference;
                }

                // Update Exercise Label
                var exerciseLabel = this.FindByName<Label>("ExerciseLabel");
                if (exerciseLabel != null)
                {
                    exerciseLabel.Text = string.IsNullOrEmpty(user.ExerciseFrequency) ? "—" : user.ExerciseFrequency;
                }

                // Update Personality Type Label
                var personalityTypeLabel = this.FindByName<Label>("PersonalityTypeLabel");
                if (personalityTypeLabel != null)
                {
                    personalityTypeLabel.Text = string.IsNullOrEmpty(user.PersonalityType) ? "—" : user.PersonalityType;
                }

                // Update Love Language Label
                var loveLanguageLabel = this.FindByName<Label>("LoveLanguageLabel");
                if (loveLanguageLabel != null)
                {
                    loveLanguageLabel.Text = string.IsNullOrEmpty(user.LoveLanguage) ? "—" : user.LoveLanguage;
                }

                // Update global locations list if location changed
                string newLocation = string.Empty;
                string oldLocation = string.Empty;

                if (!string.IsNullOrEmpty(user.Country) && !string.IsNullOrEmpty(user.State))
                {
                    newLocation = $"{user.Country}, {user.State}";
                }
                else if (!string.IsNullOrEmpty(user.Country))
                {
                    newLocation = user.Country;
                }
                else if (!string.IsNullOrEmpty(user.State))
                {
                    newLocation = user.State;
                }

                if (!string.IsNullOrEmpty(oldCountry) && !string.IsNullOrEmpty(oldState))
                {
                    oldLocation = $"{oldCountry}, {oldState}";
                }
                else if (!string.IsNullOrEmpty(oldCountry))
                {
                    oldLocation = oldCountry;
                }
                else if (!string.IsNullOrEmpty(oldState))
                {
                    oldLocation = oldState;
                }

                if (!string.IsNullOrEmpty(newLocation) && newLocation != oldLocation)
                {
                    var existingLocations = Preferences.Get("global_locations", string.Empty);
                    var locations = string.IsNullOrEmpty(existingLocations)
                        ? new List<string>()
                        : existingLocations.Split('|').ToList();

                    if (!locations.Contains(newLocation))
                    {
                        locations.Add(newLocation);
                        locations = locations.OrderBy(l => l).ToList();
                        Preferences.Set("global_locations", string.Join("|", locations));
                        NotifyLocationUpdated(user.Country ?? "", user.State ?? "");
                    }
                }

                // Show success message
                await DisplayAlert("Saved", "Preferences updated.", "OK");

                // Send notifications
                MessagingCenter.Send(this, "MoodSaved");
                MessagingCenter.Send(this, "ProfileUpdated");

                // Update current user reference
                _currentUser = user;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveProfileButton_Clicked error: {ex}");
                await DisplayAlert("Error", "Could not save preferences: " + ex.Message, "OK");
            }
        }
        // Add this method to your ProfilePage class
        // Verify button - Navigate to VerificationPage
        private async void OnVerifyButtonTapped(object sender, EventArgs e)
        {
            try
            {
                // Get the current user's phone number
                string phone = _phone;
                if (string.IsNullOrEmpty(phone))
                {
                    phone = Preferences.Get("current_user_phone", string.Empty);
                }

                if (string.IsNullOrEmpty(phone))
                {
                    await DisplayAlert("Error", "User not found. Please log in again.", "OK");
                    return;
                }

                // Check current verification status
                if (_currentUser != null)
                {
                    if (_currentUser.IsVerified)
                    {
                        // Even if verified, allow them to view verification status
                        var confirm = await DisplayAlert("Verification Status",
                            $"Your account is already verified!\n\nVerified on: {_currentUser.VerifiedAt:MMMM dd, yyyy}\n\n" +
                            $"Verification Score: {_currentUser.VerificationScore:F1}%\n\n" +
                            "Would you like to view your verification details?",
                            "View Details", "Cancel");

                        if (confirm)
                        {
                            // Navigate to verification page to view details
                            var verificationPage = new VerificationPage();
                            var phoneProperty = verificationPage.GetType().GetProperty("UserPhone");
                            if (phoneProperty != null && phoneProperty.CanWrite)
                            {
                                phoneProperty.SetValue(verificationPage, phone);
                            }
                            await Navigation.PushAsync(verificationPage);
                        }
                        return;
                    }

                    if (_currentUser.VerificationStatus == "pending")
                    {
                        await DisplayAlert("Pending Review",
                            "Your verification is already pending review.\n\nYou will be notified once verified.",
                            "OK");
                        return;
                    }
                }

                // Not verified - proceed to verification page
                var verificationPageNew = new VerificationPage();
                var phonePropertyNew = verificationPageNew.GetType().GetProperty("UserPhone");
                if (phonePropertyNew != null && phonePropertyNew.CanWrite)
                {
                    phonePropertyNew.SetValue(verificationPageNew, phone);
                }
                await Navigation.PushAsync(verificationPageNew);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnVerifyButtonTapped error: {ex}");
                await DisplayAlert("Error", $"Could not open verification page: {ex.Message}", "OK");
            }
        }
        
        // Add this method to your ProfilePage class to notify NewChatPage when location is updated
        private async void NotifyLocationUpdated(string country, string state)
        {
            try
            {
                // Format the location
                string location = string.Empty;
                if (!string.IsNullOrEmpty(country) && !string.IsNullOrEmpty(state))
                {
                    location = $"{country}, {state}";
                }
                else if (!string.IsNullOrEmpty(country))
                {
                    location = country;
                }
                else if (!string.IsNullOrEmpty(state))
                {
                    location = state;
                }

                if (!string.IsNullOrEmpty(location))
                {
                    // Store in global preferences
                    var existingLocations = Preferences.Get("global_locations", string.Empty);
                    var locations = string.IsNullOrEmpty(existingLocations)
                        ? new List<string>()
                        : existingLocations.Split('|').ToList();

                    if (!locations.Contains(location))
                    {
                        locations.Add(location);
                        locations = locations.OrderBy(l => l).ToList();
                        Preferences.Set("global_locations", string.Join("|", locations));

                        // Send a message to refresh location picker in NewChatPage
                        MessagingCenter.Send(this, "LocationListUpdated", location);
                        System.Diagnostics.Debug.WriteLine($"Location list updated with: {location}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NotifyLocationUpdated error: {ex.Message}");
            }
        }

        // Add this method to show the voice intro options menu
        private async void OnVoiceIntroOptionsTapped(object sender, EventArgs e)
        {
            try
            {
                string[] options = { "Delete voice intro", "Re-record", "Cancel" };

                var action = await DisplayActionSheet(
                    "Voice Intro Options",
                    "Cancel",
                    null,
                    options);

                switch (action)
                {
                    case "Delete voice intro":
                        await DeleteVoiceIntroAsync();
                        break;
                    case "Re-record":
                        await ShowVoiceIntroModal();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnVoiceIntroOptionsTapped error: {ex}");
            }
        }

        // Add this method to delete the voice intro
        private async Task DeleteVoiceIntroAsync()
        {
            try
            {
                var confirm = await DisplayAlert(
                    "Delete Voice Intro",
                    "Are you sure you want to delete your voice intro? Others won't be able to hear it.",
                    "Delete",
                    "Cancel");

                if (!confirm) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user != null)
                {
                    // Delete the file if it exists
                    if (!string.IsNullOrEmpty(user.VoiceIntroPath) && File.Exists(user.VoiceIntroPath))
                    {
                        File.Delete(user.VoiceIntroPath);
                    }

                    user.VoiceIntroPath = null;
                    user.VoiceIntroLastUpdated = null;
                    await db.UpdateAsync(user);
                    _currentUser = user;

                    // Update UI
                    var voiceStatusLabel = this.FindByName<Label>("VoiceIntroStatus");
                    if (voiceStatusLabel != null)
                    {
                        voiceStatusLabel.Text = IsOwner ? "Tap to record" : "No voice intro";
                        voiceStatusLabel.IsVisible = true;
                    }

                    // Update the options button visibility (hide it since voice intro is deleted)
                    UpdateVoiceIntroOptionsButtonVisibility();

                    // Update voice icon to play mode
                    UpdateVoiceIntroIcon(false);

                    await DisplayAlert("Deleted", "Your voice intro has been deleted.", "OK");

                    MessagingCenter.Send(this, "ProfileUpdated");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteVoiceIntroAsync error: {ex}");
                await DisplayAlert("Error", "Could not delete voice intro: " + ex.Message, "OK");
            }
        }
        private async void GhostModeMoodShieldSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            try
            {
                // Only save for the owner's own profile, never for view-only
                if (_viewOnly) return;
                if (!EnsurePhoneFromPreferences()) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == _phone)
                    .FirstOrDefaultAsync();

                if (user == null) return;

                user.GhostModeMoodShield = e.Value;
                await db.UpdateAsync(user);

                System.Diagnostics.Debug.WriteLine(
                    $"[GHOST] GhostModeMoodShield instantly saved: {e.Value} for {_phone}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"GhostModeMoodShieldSwitch_Toggled error: {ex.Message}");
            }
        }

        private async void MoodSearchSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            try
            {
                // Only save for the owner's own profile, never for view-only
                if (_viewOnly) return;
                if (!EnsurePhoneFromPreferences()) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == _phone)
                    .FirstOrDefaultAsync();

                if (user == null) return;

                user.AllowMoodSearch = e.Value;
                await db.UpdateAsync(user);

                System.Diagnostics.Debug.WriteLine(
                    $"[MOOD] AllowMoodSearch instantly saved: {e.Value} for {_phone}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MoodSearchSwitch_Toggled error: {ex.Message}");
            }
        }

        // Save info (bio etc.) to DB
        private async void SaveInfoButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (!EnsurePhoneFromPreferences())
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();
                if (user == null)
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }
                var bioEditorInfo = this.FindByName<Editor>("BioEditorInfo");
                user.Bio = bioEditorInfo?.Text ?? string.Empty;
                await db.UpdateAsync(user);
                await DisplayAlert("Saved", "Profile updated.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not save info: " + ex.Message, "OK");
            }
        }


        private async void OnHomeTapped(object sender, EventArgs e)
        {
            try
            {
                // First try the registered route
                await Shell.Current.GoToAsync("//post");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Home navigation error (//post): {ex.Message}");

                try
                {
                    // Try alternative route
                    await Shell.Current.GoToAsync("post");
                }
                catch
                {
                    try
                    {
                        // Try full type name as fallback
                        await Shell.Current.GoToAsync(nameof(PostPage));
                    }
                    catch
                    {
                        // If all navigation fails, at least don't crash the app
                        System.Diagnostics.Debug.WriteLine("All home navigation attempts failed");
                        await DisplayAlert("Navigation Error", "Could not navigate to home page. Please restart the app.", "OK");
                    }
                }
            }
        }


        private async void OnChatsTapped(object sender, EventArgs e)
        {
            try
            {
                // Navigate to Conversations page
                bool success = await SafeNavigateToChatsAsync();
                if (!success)
                {
                    await DisplayAlert("Error", "Could not navigate to chats", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Chats navigation error: {ex}");
                await DisplayAlert("Error", "Could not navigate to chats", "OK");
            }
        }

        // Add this helper method
        private async Task<bool> SafeNavigateToChatsAsync()
        {
            try
            {
                // Try different route variations
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
                    try
                    {
                        await Shell.Current.GoToAsync(nameof(ConversationsPage));
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Check if user is logged in
            if (!await IsUserLoggedIn())
            {
                await Shell.Current.GoToAsync("///LoginPage");
                return;
            }

            // Load user data if phone is available
            if (string.IsNullOrEmpty(_phone))
            {
                if (EnsurePhoneFromPreferences())
                {
                    await LoadUserAsync(_phone);
                }
            }
            else
            {
                await LoadUserAsync(_phone);
            }

            // Control edit affordances for owner view only
            var coverEdit = this.FindByName<VisualElement>("CoverEditIcon");
            var profileEdit = this.FindByName<VisualElement>("ProfileEditIconOverlay");
            var editBoth = this.FindByName<VisualElement>("EditImagesButton");
            var canEdit = IsOwner;

            if (coverEdit != null) coverEdit.IsVisible = canEdit;
            if (profileEdit != null) profileEdit.IsVisible = canEdit;
            if (editBoth != null) editBoth.IsVisible = canEdit;

            // Setup image tap gestures based on ownership
            SetupImageTapGestures();

            // DON'T reset to Home tab - restore the last active tab instead
            RestoreCurrentTab();

            // Re-evaluate icon visibility (in case LoadUserAsync completed fast or page re-appears)
            UpdateCoverEditIconVisibility();
            UpdateProfileEditIconVisibility();
            UpdateVoiceIntroOptionsButtonVisibility();

            // Update navigation bar visibility based on view mode
            UpdateNavigationBarVisibility();

            // Refresh pending endorsements
            if (_currentUserId > 0)
            {
                await LoadPendingEndorsementsAsync();
                await LoadEndorsementsAsync(_currentUserId);
            }

            // ========== UPDATE CHAT BADGE ==========
            await UpdateChatBadgeCount();

            // ========== SUBSCRIBE TO CHAT EVENTS ==========
            MessagingCenter.Subscribe<object>(this, "MessagesUpdated", async (sender) =>
            {
                System.Diagnostics.Debug.WriteLine("MessagesUpdated received in ProfilePage");
                await UpdateChatBadgeCount();
            });

            MessagingCenter.Subscribe<object>(this, "ConversationsUpdated", async (sender) =>
            {
                System.Diagnostics.Debug.WriteLine("ConversationsUpdated received in ProfilePage");
                await UpdateChatBadgeCount();
            });

            MessagingCenter.Subscribe<object>(this, "UpdateChatBadge", async (sender) =>
            {
                System.Diagnostics.Debug.WriteLine("UpdateChatBadge received in ProfilePage");
                await UpdateChatBadgeCount();
            });

            // Update notification badge count
            await UpdateNotificationBadgeCount();

            // Subscribe to notification updates
            MessagingCenter.Subscribe<object, NotificationItem>(this, "NewUnreadNotification", (s, n) =>
            {
                UpdateNotificationBadgeCount();
            });

            MessagingCenter.Subscribe<object>(this, "NotificationRead", (s) =>
            {
                UpdateNotificationBadgeCount();
            });

            MessagingCenter.Subscribe<object>(this, "AllNotificationsRead", (s) =>
            {
                UpdateNotificationBadgeCount();
            });

            // Subscribe to location updates
            MessagingCenter.Subscribe<object, string>(this, "LocationListUpdated", (sender, location) =>
            {
                System.Diagnostics.Debug.WriteLine($"Location list updated in ProfilePage: {location}");
            });

            // ========== SUBSCRIBE TO LOVE CHANGE MESSAGES ==========
            MessagingCenter.Subscribe<object, LoveChangedMessage>(this, "PostLoveChanged", (sender, message) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView");
                        if (collectionView?.ItemsSource is IEnumerable<Lock.Models.Post> posts)
                        {
                            var targetPost = posts.FirstOrDefault(p => p.Id == message.PostId);
                            if (targetPost != null)
                            {
                                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                                if (message.IsLoved)
                                {
                                    if (!targetPost.LovedBy.Contains(message.UserPhone))
                                        targetPost.LovedBy.Add(message.UserPhone);
                                }
                                else
                                {
                                    targetPost.LovedBy.Remove(message.UserPhone);
                                }

                                targetPost.LoveCount = message.LoveCount;

                                if (message.UserPhone == currentUserPhone)
                                {
                                    targetPost.IsLovedByCurrentUser = message.IsLoved;
                                }

                                targetPost.RefreshLoveState();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error handling love message: {ex}");
                    }
                });
            });

            // ========== SUBSCRIBE TO SPARK CHANGE MESSAGES ==========
            MessagingCenter.Subscribe<SparkChangedMessage>(this, "SparkToggled", (msg) =>
            {
                Debug.WriteLine($"[ProfilePage] SparkToggled received: PostId={msg.PostId}, IsSparked={msg.IsSparked}, Count={msg.SparkCount}");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView");
                        if (collectionView?.ItemsSource is IEnumerable<Lock.Models.Post> posts)
                        {
                            var targetPost = posts.FirstOrDefault(p => p.Id == msg.PostId);
                            if (targetPost != null)
                            {
                                targetPost.IsSparkedByCurrentUser = msg.IsSparked;
                                targetPost.SparkCount = msg.SparkCount;
                                targetPost.RefreshSparkState();

                                // Refresh the collection view to update UI
                                collectionView.ItemsSource = null;
                                collectionView.ItemsSource = posts.ToList();

                                Debug.WriteLine($"[ProfilePage] ✅ Post {msg.PostId} spark UI updated");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ProfilePage] SparkToggled error: {ex}");
                    }
                });
            });

            // ========== SUBSCRIBE TO PROFILE UPDATE MESSAGES ==========
            MessagingCenter.Subscribe<object>(this, "ProfileUpdated", async (sender) =>
            {
                if (!string.IsNullOrEmpty(_phone))
                {
                    await LoadUserAsync(_phone);
                }
            });

            // ========== SUBSCRIBE TO VERIFICATION UPDATES ==========
            MessagingCenter.Subscribe<object>(this, "UserVerified", async (sender) =>
            {
                Debug.WriteLine("UserVerified received in ProfilePage");
                await RefreshUserDataAsync();
                if (_currentUser != null)
                {
                    IsVerified = _currentUser.IsVerified;
                }
            });

            // ========== SUBSCRIBE TO MOOD UPDATES (Looking For) ==========
            MessagingCenter.Subscribe<object>(this, "MoodUpdated", async (sender) =>
            {
                Debug.WriteLine("MoodUpdated received in ProfilePage");
                await RefreshUserDataAsync();
            });

            MessagingCenter.Subscribe<object>(this, "MoodSaved", async (sender) =>
            {
                Debug.WriteLine("MoodSaved received in ProfilePage");
                await RefreshUserDataAsync();
            });

            // ========== SUBSCRIBE TO ENDORSEMENT EVENTS ==========
            MessagingCenter.Subscribe<object, string>(this, "EndorsementAdded", async (sender, userId) =>
            {
                if (userId == _currentUserId.ToString())
                {
                    await LoadPendingEndorsementsAsync();
                    await LoadEndorsementsAsync(_currentUserId);
                }
            });

            MessagingCenter.Subscribe<object, string>(this, "EndorsementRequestUpdated", async (sender, requestId) =>
            {
                await LoadPendingEndorsementsAsync();
            });

            // Refresh verification badge visibility
            UpdateVerificationBadgeVisibility();
        }

        private async Task RefreshUserDataAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_phone))
                {
                    // Reload user data from database
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var updatedUser = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                    if (updatedUser != null)
                    {
                        _currentUser = updatedUser;

                        // Update verification status
                        IsVerified = updatedUser.IsVerified;

                        // Update the Looking For property
                        OnPropertyChanged(nameof(CurrentUserLookingFor));

                        // If this is the owner's profile, also update the UI elements
                        if (IsOwner)
                        {
                            // Update any pickers or displays that show the mood
                            var moodPicker = this.FindByName<Picker>("MoodPicker");
                            if (moodPicker != null && !string.IsNullOrEmpty(updatedUser.Mood))
                            {
                                if (moodPicker.Items.Contains(updatedUser.Mood))
                                    moodPicker.SelectedItem = updatedUser.Mood;
                            }

                            // Also update any labels that display the mood
                            var lookingForLabel = this.FindByName<Label>("LookingForDisplayLabel");
                            if (lookingForLabel != null)
                            {
                                lookingForLabel.Text = updatedUser.Mood ?? "Long-term relationship";
                            }
                        }

                        Debug.WriteLine($"User data refreshed: Looking For = {updatedUser.Mood}, IsVerified = {updatedUser.IsVerified}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshUserDataAsync error: {ex}");
            }
        }


        public string CurrentUserLookingFor
        {
            get => _currentUser?.Mood ?? "Long-term relationship";
            set
            {
                OnPropertyChanged();
            }
        }


        // ========== PROMPT EDIT/DELETE METHODS ==========

        private async void EditPromptButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var prompt = (sender as VisualElement)?.BindingContext as UserPrompt
                         ?? (e as TappedEventArgs)?.Parameter as UserPrompt;
            if (prompt == null) return;

            var answer = await DisplayPromptAsync("Edit Answer", prompt.Question, "Save", "Cancel",
                                                  initialValue: prompt.Answer, maxLength: 300);
            if (string.IsNullOrWhiteSpace(answer)) return;

            try
            {
                await ProfileDataService.UpdateUserPromptAsync(prompt.Id, answer);
                prompt.Answer = answer;

                var promptsCv = this.FindByName<CollectionView>("PromptsCollectionView");
                if (promptsCv != null)
                {
                    promptsCv.ItemsSource = null;
                    promptsCv.ItemsSource = _userPrompts;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to edit prompt: {ex.Message}", "OK");
            }
        }
        private async void DeletePromptButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var prompt = (sender as VisualElement)?.BindingContext as UserPrompt
                         ?? (e as TappedEventArgs)?.Parameter as UserPrompt;
            if (prompt == null) return;

            var confirm = await DisplayAlert("Confirm Delete", "Are you sure you want to delete this prompt?", "Yes", "No");
            if (!confirm) return;

            try
            {
                await ProfileDataService.DeleteUserPromptAsync(prompt.Id);
                _userPrompts.Remove(prompt);

                var promptsCv = this.FindByName<CollectionView>("PromptsCollectionView");
                if (promptsCv != null)
                {
                    promptsCv.ItemsSource = null;
                    promptsCv.ItemsSource = _userPrompts;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete prompt: {ex.Message}", "OK");
            }
        }
        // ========== DATE IDEA EDIT/DELETE METHODS ==========

        private async void EditDateIdeaButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var idea = (sender as VisualElement)?.BindingContext as DateIdea
                       ?? (e as TappedEventArgs)?.Parameter as DateIdea;
            if (idea == null) return;

            var titleEntry = new Entry { Text = idea.Title, FontSize = 14 };
            var descEntry = new Entry { Text = idea.Description, FontSize = 14 };
            var locationEntry = new Entry { Text = idea.Location, FontSize = 14 };

            var categoryLabel = new Label
            {
                Text = "Category",
                FontSize = 12,
                TextColor = Color.FromArgb("#666666")
            };

            var categoryPicker = new Picker { Title = "Category", FontSize = 14 };
            categoryPicker.Items.Add("Coffee");
            categoryPicker.Items.Add("Dinner");
            categoryPicker.Items.Add("Outdoor");
            categoryPicker.Items.Add("Drinks");
            categoryPicker.Items.Add("Activity");
            categoryPicker.Items.Add("Cultural");
            categoryPicker.Items.Add("Movie");
            categoryPicker.Items.Add("Concert");
            categoryPicker.Items.Add("Other");

            for (int i = 0; i < categoryPicker.Items.Count; i++)
            {
                if (categoryPicker.Items[i] == idea.Category)
                {
                    categoryPicker.SelectedIndex = i;
                    break;
                }
            }

            var scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new StackLayout
                {
                    Padding = 20,
                    Spacing = 12,
                    Children = { titleEntry, descEntry, locationEntry, categoryLabel, categoryPicker }
                }
            };

            var page = new ContentPage
            {
                Title = "Edit Date Idea",
                Content = scrollView
            };

            var saveButton = new Button
            {
                Text = "Save",
                BackgroundColor = Color.FromArgb("#008080"),
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 10)
            };

            saveButton.Clicked += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(titleEntry.Text) || categoryPicker.SelectedIndex == -1)
                {
                    await page.DisplayAlert("Error", "Please fill all fields and select a category", "OK");
                    return;
                }

                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();

                    idea.Title = titleEntry.Text;
                    idea.Description = descEntry.Text ?? "";
                    idea.Location = locationEntry.Text ?? "";
                    idea.Category = categoryPicker.Items[categoryPicker.SelectedIndex];

                    await db.UpdateAsync(idea);

                    var datesCv = this.FindByName<CollectionView>("DateIdeasCollectionView");
                    if (datesCv != null)
                    {
                        datesCv.ItemsSource = null;
                        datesCv.ItemsSource = _userDateIdeas;
                    }

                    await Navigation.PopModalAsync();
                }
                catch (Exception ex)
                {
                    await page.DisplayAlert("Error", ex.Message, "OK");
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Colors.Gray,
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 20)
            };

            cancelButton.Clicked += async (s, args) => await Navigation.PopModalAsync();

            var buttonLayout = new StackLayout
            {
                Spacing = 10,
                Padding = 0,
                Children = { saveButton, cancelButton }
            };

            ((StackLayout)scrollView.Content).Children.Add(buttonLayout);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
        private async void OnPostImageTapped(object sender, EventArgs e)
        {
            try
            {
                var frame = sender as Frame;
                if (frame?.BindingContext is string imagePath)
                {
                    // Find the parent post that contains this image
                    var parentElement = frame.Parent;
                    while (parentElement != null && parentElement.BindingContext is not Lock.Models.Post)
                    {
                        parentElement = parentElement.Parent;
                    }

                    if (parentElement?.BindingContext is Lock.Models.Post post && post.ImagePathsList != null)
                    {
                        // Get all images from this post
                        var imagePaths = post.ImagePathsList.ToList();
                        var startIndex = imagePaths.IndexOf(imagePath);

                        if (startIndex >= 0)
                        {
                            var fullScreenPage = new FullScreenMediaPage(imagePaths, startIndex);
                            await Navigation.PushModalAsync(fullScreenPage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnPostImageTapped: {ex.Message}");
            }
        }

        private async void DeleteDateIdeaButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var idea = (sender as VisualElement)?.BindingContext as DateIdea
                       ?? (e as TappedEventArgs)?.Parameter as DateIdea;
            if (idea == null) return;

            var confirm = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete '{idea.Title}'?", "Yes", "No");
            if (!confirm) return;

            try
            {
                await ProfileDataService.DeleteDateIdeaAsync(idea.Id);
                _userDateIdeas.Remove(idea);

                var datesCv = this.FindByName<CollectionView>("DateIdeasCollectionView");
                if (datesCv != null)
                {
                    datesCv.ItemsSource = null;
                    datesCv.ItemsSource = _userDateIdeas;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete date idea: {ex.Message}", "OK");
            }
        }

        private async void OnMessageIconTapped(object sender, EventArgs e)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                var targetUserPhone = _phone?.Trim();

                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to send messages", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(targetUserPhone))
                {
                    await DisplayAlert("Error", "Could not determine user to message", "OK");
                    return;
                }

                if (string.Equals(currentUserPhone, targetUserPhone, StringComparison.OrdinalIgnoreCase))
                {
                    await DisplayAlert("Info", "You cannot message yourself", "OK");
                    return;
                }

                // Get or create conversation
                var conv = await ChatRepository.GetOrCreateConversationAsync(currentUserPhone, targetUserPhone);

                if (conv == null)
                {
                    await DisplayAlert("Error", "Could not create conversation", "OK");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Opening chat: convId={conv.ConversationId}, other={targetUserPhone}");

                // Navigate to ConversationsPage first (it's the Shell root for chat)
                // then push ChatPage directly — this is the most reliable approach
                // since ChatPage is a registered route under conversations, not a root Shell item
                try
                {
                    // First navigate to conversations (the Shell root)
                    await Shell.Current.GoToAsync("//conversations");

                    // Small delay to let the page load
                    await Task.Delay(150);

                    // Then push ChatPage on top using the registered route
                    await Shell.Current.GoToAsync(
                        $"chat?conversationId={Uri.EscapeDataString(conv.ConversationId)}&other={Uri.EscapeDataString(targetUserPhone)}");

                    return;
                }
                catch (Exception ex1)
                {
                    System.Diagnostics.Debug.WriteLine($"Shell navigation failed: {ex1.Message}");
                }

                // Fallback: push ChatPage directly onto the navigation stack
                try
                {
                    var chatPage = new Lock.Pages.Chat.ChatPage(conv.ConversationId, targetUserPhone);
                    await Navigation.PushAsync(chatPage);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"Direct push failed: {ex2.Message}");
                    await DisplayAlert("Error", "Could not open chat. Please use the Chat tab instead.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnMessageIconTapped error: {ex.Message}\n{ex.StackTrace}");
                await DisplayAlert("Error", "Could not open chat: " + ex.Message, "OK");
            }
        }



        // Add these methods to ProfilePage class

        private async void OnVoiceIntroTapped(object sender, EventArgs e)
        {
            try
            {
                if (_isVoiceIntroPlaying)
                {
                    _voiceIntroPlayer?.Stop();
                    _isVoiceIntroPlaying = false;
                    UpdateVoiceIntroIcon(false);
                    return;
                }

                if (_currentUser == null || string.IsNullOrEmpty(_currentUser.VoiceIntroPath))
                {
                    if (IsOwner)
                    {
                        await ShowVoiceIntroModal();
                    }
                    else
                    {
                        await DisplayAlert("No Voice Intro", "This user hasn't added a voice intro yet.", "OK");
                    }
                    return;
                }

                if (File.Exists(_currentUser.VoiceIntroPath))
                {
                    _voiceIntroPlayer?.Dispose();

                    // Use a stream for playback (matching ChatPage pattern)
                    var stream = File.OpenRead(_currentUser.VoiceIntroPath);
                    _voiceIntroPlayer = AudioManager.Current.CreatePlayer(stream);
                    _voiceIntroPlayer.Play();
                    _isVoiceIntroPlaying = true;
                    UpdateVoiceIntroIcon(true);

                    _voiceIntroPlayer.PlaybackEnded += (s, args) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _isVoiceIntroPlaying = false;
                            UpdateVoiceIntroIcon(false);
                            stream?.Dispose();
                        });
                    };
                }
                else if (IsOwner)
                {
                    await ShowVoiceIntroModal();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnVoiceIntroTapped error: {ex}");
                await DisplayAlert("Error", "Could not play voice intro: " + ex.Message, "OK");
            }
        }
        private void UpdateVoiceIntroIcon(bool isPlaying)
        {
            var playIcon = this.FindByName<Microsoft.Maui.Controls.Shapes.Path>("VoiceIconPlay");
            var recordingIcon = this.FindByName<Microsoft.Maui.Controls.Shapes.Path>("VoiceIconRecording");
            var loading = this.FindByName<ActivityIndicator>("VoiceIntroLoading");

            if (playIcon != null) playIcon.IsVisible = !isPlaying;
            if (recordingIcon != null) recordingIcon.IsVisible = isPlaying;
            if (loading != null) loading.IsVisible = false;
        }

        private async Task ShowVoiceIntroModal()
        {
            var modal = new VoiceIntroModal(_phone, _currentUser?.VoiceIntroPath, async (newPath) =>
            {
                await SaveVoiceIntroPath(newPath);
            });
            await Navigation.PushModalAsync(modal);
        }

        private async Task SaveVoiceIntroPath(string audioPath)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user != null)
                {
                    if (!string.IsNullOrEmpty(user.VoiceIntroPath) && File.Exists(user.VoiceIntroPath) && audioPath != user.VoiceIntroPath)
                    {
                        File.Delete(user.VoiceIntroPath);
                    }

                    user.VoiceIntroPath = audioPath;
                    user.VoiceIntroLastUpdated = DateTime.UtcNow;
                    await db.UpdateAsync(user);
                    _currentUser = user;

                    var voiceStatusLabel = this.FindByName<Label>("VoiceIntroStatus");
                    if (voiceStatusLabel != null)
                    {
                        if (string.IsNullOrEmpty(audioPath))
                        {
                            voiceStatusLabel.Text = "Tap to record";
                            voiceStatusLabel.IsVisible = true;
                        }
                        else
                        {
                            voiceStatusLabel.IsVisible = false;
                        }
                    }

                    // Update the options button visibility
                    UpdateVoiceIntroOptionsButtonVisibility();

                    MessagingCenter.Send(this, "ProfileUpdated");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveVoiceIntroPath error: {ex}");
                await DisplayAlert("Error", "Could not save voice intro: " + ex.Message, "OK");
            }
        }

        private void UpdateVoiceIntroOptionsButtonVisibility()
        {
            var optionsButton = this.FindByName<Border>("VoiceIntroOptionsButton");
            if (optionsButton != null)
            {
                // Show the three-dot menu only when there IS a voice intro and user is the owner
                bool hasVoiceIntro = _currentUser != null &&
                                     !string.IsNullOrEmpty(_currentUser.VoiceIntroPath) &&
                                     File.Exists(_currentUser.VoiceIntroPath);
                optionsButton.IsVisible = hasVoiceIntro && IsOwner;

                Debug.WriteLine($"VoiceIntroOptionsButton visibility: {optionsButton.IsVisible} (hasVoiceIntro={hasVoiceIntro}, IsOwner={IsOwner})");
            }
        }

        // Add this to LoadUserAsync method (inside where you populate user fields)
        private async Task LoadNewProfileFields(User user)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Height
            var heightLabel = this.FindByName<Label>("HeightLabel");
            if (heightLabel != null)
            {
                if (user.HeightCm.HasValue && user.HeightCm.Value > 0)
                {
                    int feet = (int)(user.HeightCm.Value / 30.48);
                    int inches = (int)((user.HeightCm.Value % 30.48) / 2.54);
                    heightLabel.Text = $"{feet}'{inches}\" ({user.HeightCm.Value}cm)";
                }
                else
                {
                    heightLabel.Text = "—";
                }
            }

            // Body Type
            var bodyTypeLabel = this.FindByName<Label>("BodyTypeLabel");
            if (bodyTypeLabel != null)
            {
                bodyTypeLabel.Text = string.IsNullOrEmpty(user.BodyType) ? "—" : user.BodyType;
            }

            // Ethnicity / Tribe
            var ethnicityLabel = this.FindByName<Label>("EthnicityLabel");
            if (ethnicityLabel != null)
            {
                if (!string.IsNullOrEmpty(user.Ethnicity) && !string.IsNullOrEmpty(user.Tribe))
                {
                    ethnicityLabel.Text = $"{user.Ethnicity} · {user.Tribe}";
                }
                else if (!string.IsNullOrEmpty(user.Ethnicity))
                {
                    ethnicityLabel.Text = user.Ethnicity;
                }
                else if (!string.IsNullOrEmpty(user.Tribe))
                {
                    ethnicityLabel.Text = user.Tribe;
                }
                else
                {
                    ethnicityLabel.Text = "—";
                }
            }



            // Voice Intro Status
            var voiceStatus = this.FindByName<Label>("VoiceIntroStatus");
            if (voiceStatus != null)
            {
                if (string.IsNullOrEmpty(user.VoiceIntroPath) || !File.Exists(user.VoiceIntroPath))
                {
                    voiceStatus.Text = IsOwner ? "Tap to record" : "No voice intro";
                    voiceStatus.IsVisible = true;
                }
                else
                {
                    voiceStatus.IsVisible = false;
                }
            }
        });
    }

    // Call this method from LoadUserAsync after setting _currentUser

    // ========== EVENT EDIT/DELETE METHODS ==========
    private async void EditEventButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var evt = (sender as VisualElement)?.BindingContext as UserEvent
                      ?? (e as TappedEventArgs)?.Parameter as UserEvent;
            if (evt == null) return;

            var nameEntry = new Entry { Text = evt.EventName, FontSize = 14 };
            var descEntry = new Editor { Text = evt.Description, HeightRequest = 80, FontSize = 14 };
            var locationEntry = new Entry { Text = evt.Location, FontSize = 14 };

            var dateLabel = new Label { Text = "Date", FontSize = 12, TextColor = Color.FromArgb("#666666") };
            var datePicker = new DatePicker { Date = evt.EventDate.Date, MinimumDate = DateTime.Today, FontSize = 14 };

            var timeLabel = new Label { Text = "Time", FontSize = 12, TextColor = Color.FromArgb("#666666") };
            var timePicker = new TimePicker { Time = evt.EventDate.TimeOfDay, FontSize = 14 };

            var categoryLabel = new Label { Text = "Category", FontSize = 12, TextColor = Color.FromArgb("#666666") };
            var categoryPicker = new Picker { Title = "Category", FontSize = 14 };
            categoryPicker.Items.Add("Coffee");
            categoryPicker.Items.Add("Drinks");
            categoryPicker.Items.Add("Dinner");
            categoryPicker.Items.Add("Outdoor");
            categoryPicker.Items.Add("Music");
            categoryPicker.Items.Add("Sports");
            categoryPicker.Items.Add("Game Night");
            categoryPicker.Items.Add("Movie");
            categoryPicker.Items.Add("Art");
            categoryPicker.Items.Add("Food");
            categoryPicker.Items.Add("Other");

            for (int i = 0; i < categoryPicker.Items.Count; i++)
            {
                if (categoryPicker.Items[i] == evt.Category)
                {
                    categoryPicker.SelectedIndex = i;
                    break;
                }
            }

            var maxEntry = new Entry
            {
                Text = evt.MaxAttendees.ToString(),
                Placeholder = "Max attendees (0 for unlimited)",
                Keyboard = Keyboard.Numeric,
                FontSize = 14
            };

            var scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Vertical,
                Content = new StackLayout
                {
                    Padding = 20,
                    Spacing = 12,
                    Children =
            {
                nameEntry, descEntry, locationEntry,
                dateLabel, datePicker,
                timeLabel, timePicker,
                categoryLabel, categoryPicker,
                maxEntry
            }
                }
            };

            var page = new ContentPage
            {
                Title = "Edit Event",
                Content = scrollView
            };

            var saveButton = new Button
            {
                Text = "Save",
                BackgroundColor = Color.FromArgb("#008080"),
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 10)
            };

            saveButton.Clicked += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameEntry.Text) || categoryPicker.SelectedIndex == -1)
                {
                    await page.DisplayAlert("Error", "Please fill required fields and select a category", "OK");
                    return;
                }

                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();

                    evt.EventName = nameEntry.Text;
                    evt.Description = descEntry.Text ?? "";
                    evt.Location = locationEntry.Text ?? "";
                    evt.EventDate = datePicker.Date.Add(timePicker.Time);
                    evt.Category = categoryPicker.Items[categoryPicker.SelectedIndex];

                    int maxAttendees = 0;
                    int.TryParse(maxEntry.Text, out maxAttendees);
                    evt.MaxAttendees = maxAttendees;

                    await db.UpdateAsync(evt);

                    var eventsCv = this.FindByName<CollectionView>("EventsCollectionView");
                    if (eventsCv != null)
                    {
                        eventsCv.ItemsSource = null;
                        eventsCv.ItemsSource = _userEvents;
                    }

                    await Navigation.PopModalAsync();
                }
                catch (Exception ex)
                {
                    await page.DisplayAlert("Error", ex.Message, "OK");
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Colors.Gray,
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 20)
            };

            cancelButton.Clicked += async (s, args) => await Navigation.PopModalAsync();

            var buttonLayout = new StackLayout
            {
                Spacing = 10,
                Padding = 0,
                Children = { saveButton, cancelButton }
            };

            ((StackLayout)scrollView.Content).Children.Add(buttonLayout);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
        private async void DeleteEventButton_Clicked(object sender, EventArgs e)
        {
            if (_viewOnly)
            {
                await DisplayAlert("Read Only", "Cannot edit this profile", "OK");
                return;
            }

            var evt = (sender as VisualElement)?.BindingContext as UserEvent
                      ?? (e as TappedEventArgs)?.Parameter as UserEvent;
            if (evt == null) return;

            var confirm = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete '{evt.EventName}'?", "Yes", "No");
            if (!confirm) return;

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                await db.DeleteAsync(evt);

                var attendances = await db.Table<EventAttendance>().Where(a => a.EventId == evt.Id).ToListAsync();
                foreach (var attendance in attendances)
                {
                    await db.DeleteAsync(attendance);
                }

                _userEvents.Remove(evt);

                var eventsCv = this.FindByName<CollectionView>("EventsCollectionView");
                if (eventsCv != null)
                {
                    eventsCv.ItemsSource = null;
                    eventsCv.ItemsSource = _userEvents;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete event: {ex.Message}", "OK");
            }
        }


        // Add this helper method
        private async Task<bool> IsUserLoggedIn()
        {
            var savedPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();

            if (string.IsNullOrEmpty(savedPhone))
                return false;

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == savedPhone)
                    .FirstOrDefaultAsync();

                return user != null;
            }
            catch
            {
                return false;
            }
        }

        // Add this helper method to update the chat badge
        private async Task UpdateChatBadgeCount()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                System.Diagnostics.Debug.WriteLine($"Updating chat badge for user: {currentUserPhone}");

                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    SetChatBadgeVisibility(false);
                    return;
                }

                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();

                // Get all conversations for user
                var conversations = await db.Table<Conversation>()
                    .Where(c => c.ParticipantA == currentUserPhone || c.ParticipantB == currentUserPhone)
                    .ToListAsync();

                int conversationsWithUnread = 0;

                foreach (var conv in conversations)
                {
                    // Skip archived conversations for badge count
                    if (conv.IsArchived)
                    {
                        continue;
                    }

                    int unreadCount = await db.Table<ChatMessage>()
                        .Where(m => m.ConversationId == conv.ConversationId &&
                                   m.RecipientPhone == currentUserPhone &&
                                   m.IsRead == false &&
                                   m.IsMessageRequest == false)
                        .CountAsync();

                    if (unreadCount > 0)
                    {
                        conversationsWithUnread++;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Conversations with unread messages: {conversationsWithUnread}");

                // Update badge on UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var chatBadge = this.FindByName<Border>("ChatBadge");
                    var chatBadgeLabel = this.FindByName<Label>("ChatBadgeLabel");

                    if (chatBadge != null && chatBadgeLabel != null)
                    {
                        if (conversationsWithUnread > 0)
                        {
                            chatBadge.IsVisible = true;
                            chatBadgeLabel.Text = conversationsWithUnread > 99 ? "99+" : conversationsWithUnread.ToString();
                        }
                        else
                        {
                            chatBadge.IsVisible = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating chat badge: {ex.Message}");
                SetChatBadgeVisibility(false);
            }
        }

        private void SetChatBadgeVisibility(bool isVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var chatBadge = this.FindByName<Border>("ChatBadge");
                if (chatBadge != null)
                {
                    chatBadge.IsVisible = isVisible;
                }
            });
        }
        // ========== NOTIFICATION BADGE METHODS ==========

        private async Task UpdateNotificationBadgeCount()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Updating notification badge in ProfilePage");

                // Get unread notification count from Preferences
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

                System.Diagnostics.Debug.WriteLine($"Notification badge count: {unreadCount}");

                // Update UI on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var notificationBadge = this.FindByName<Border>("NotificationBadge");
                    var notificationBadgeLabel = this.FindByName<Label>("NotificationBadgeLabel");

                    if (notificationBadge != null && notificationBadgeLabel != null)
                    {
                        if (unreadCount > 0)
                        {
                            notificationBadgeLabel.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
                            notificationBadge.IsVisible = true;
                            System.Diagnostics.Debug.WriteLine($"Notification badge shown: {notificationBadgeLabel.Text}");
                        }
                        else
                        {
                            notificationBadge.IsVisible = false;
                            System.Diagnostics.Debug.WriteLine("Notification badge hidden (0 unread)");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"NotificationBadge controls not found! Badge: {notificationBadge != null}, Label: {notificationBadgeLabel != null}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating notification badge: {ex.Message}");
                SetNotificationBadgeVisibility(false);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _voiceIntroPlayer?.Dispose();

            // Unsubscribe from chat events
            MessagingCenter.Unsubscribe<object>(this, "MessagesUpdated");
            MessagingCenter.Unsubscribe<object>(this, "ConversationsUpdated");
            MessagingCenter.Unsubscribe<object>(this, "UpdateChatBadge");

            // Unsubscribe from notification events
            MessagingCenter.Unsubscribe<object, NotificationItem>(this, "NewUnreadNotification");
            MessagingCenter.Unsubscribe<object>(this, "NotificationRead");
            MessagingCenter.Unsubscribe<object>(this, "AllNotificationsRead");
            MessagingCenter.Unsubscribe<object, string>(this, "LocationListUpdated");
            MessagingCenter.Unsubscribe<object, LoveChangedMessage>(this, "PostLoveChanged");
            MessagingCenter.Unsubscribe<object>(this, "ProfileUpdated");

            // Unsubscribe from endorsement events
            MessagingCenter.Unsubscribe<object, string>(this, "EndorsementAdded");
            MessagingCenter.Unsubscribe<object, string>(this, "EndorsementRequestUpdated");
        }


        private void SetNotificationBadgeVisibility(bool isVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var notificationBadge = this.FindByName<Border>("NotificationBadge");
                if (notificationBadge != null)
                {
                    notificationBadge.IsVisible = isVisible;
                    System.Diagnostics.Debug.WriteLine($"Set notification badge visibility to {isVisible}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("NotificationBadge not found in SetNotificationBadgeVisibility");
                }
            });
        }

        private async void OnNotificationsTapped(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Notification icon tapped in ProfilePage");

                // Navigate to NotificationPage
                await Navigation.PushAsync(new Lock.Pages.Post.NotificationPage());

                // Badge will be updated when returning to this page via OnAppearing
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notifications navigation error: {ex}");
                await DisplayAlert("Error", "Could not navigate to notifications", "OK");
            }
        }



        private async void OnCommentButtonTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not Lock.Models.Post post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to comment", "OK");
                    return;
                }

                var commentsPage = new Lock.Pages.Post.CommentsPage(post.Id, currentUserPhone);
                await Navigation.PushAsync(commentsPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening comments: {ex}");
            }
        }
        private async void OnLoveButtonTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not Lock.Models.Post post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to love posts", "OK");
                    return;
                }

                // Store the previous state to detect change
                bool wasLoved = post.IsLovedByCurrentUser;
                int previousLoveCount = post.LoveCount;

                // Toggle love in database
                await PostRepository.ToggleLoveAsync(post.Id, currentUserPhone);

                // Update the post object's love state
                post.ToggleLove(currentUserPhone);

                // Verify the love state changed (should be opposite of before)
                bool isNowLoved = post.IsLovedByCurrentUser;

                System.Diagnostics.Debug.WriteLine($"Love toggled for post {post.Id}: wasLoved={wasLoved}, isNowLoved={isNowLoved}, loveCount={post.LoveCount}");

                // Force refresh the CollectionView to update UI
                var collectionView = this.FindByName<CollectionView>("UserPostsCollectionView");
                if (collectionView != null)
                {
                    // Method 1: Refresh by resetting ItemsSource (most reliable)
                    var currentSource = collectionView.ItemsSource;
                    collectionView.ItemsSource = null;
                    collectionView.ItemsSource = currentSource;

                    // Method 2: Use the public refresh method instead of direct OnPropertyChanged calls
                    post.RefreshLoveState();
                }

                // Send a message that love state changed so other pages can update
                MessagingCenter.Send(this, "PostLoveChanged", new LoveChangedMessage
                {
                    PostId = post.Id,
                    IsLoved = isNowLoved,
                    LoveCount = post.LoveCount,
                    UserPhone = currentUserPhone
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling love: {ex}");
            }
        }
        // Add this message class
        public class LoveChangedMessage
        {
            public int PostId { get; set; }
            public bool IsLoved { get; set; }
            public int LoveCount { get; set; }
            public string UserPhone { get; set; }
        }
        private async void OnShareButtonTapped(object sender, TappedEventArgs e)
        {
            try
            {
                // CHANGE THIS LINE
                if (e.Parameter is not Lock.Models.Post post) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var sharePopup = new Lock.Pages.Post.PostSharePopup(post, currentUserPhone);
                await this.ShowPopupAsync(sharePopup);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sharing post: {ex}");
            }
        }

        // Save interests/public fields to DB (now includes TopInterest/TopArtist/TopMovie and SexualOrientation)
        private async void SaveInterestsButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (!EnsurePhoneFromPreferences())
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == _phone).FirstOrDefaultAsync();

                if (user == null)
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                // Track if mood changed (if mood picker exists in this tab)
                string oldMood = user.Mood;

                var topInterestPicker = this.FindByName<Picker>("TopInterestPicker");
                var topArtistEntry = this.FindByName<Entry>("TopArtistEntry");
                var topMovieEntry = this.FindByName<Entry>("TopMovieEntry");
                var sexualPicker = this.FindByName<Picker>("SexualOrientationPicker");
                var musicEntry = this.FindByName<Entry>("MusicGenresEntry");
                var favArtistsEntry = this.FindByName<Entry>("FavoriteArtistsEntry");
                var favMoviesEntry = this.FindByName<Entry>("FavoriteMoviesEntry");
                var favBooksEntry = this.FindByName<Entry>("FavoriteBooksEntry");
                var languagesEntry = this.FindByName<Entry>("LanguagesEntry");
                var occupationEntry = this.FindByName<Entry>("OccupationEntry");
                var educationEntry = this.FindByName<Entry>("EducationEntry");
                var promptsEditor = this.FindByName<Editor>("PromptsEditor");
                var dealbreakersEntry = this.FindByName<Entry>("DealbreakersEntry");
                var favoriteMusicGenrePicker = this.FindByName<Picker>("FavoriteMusicGenrePicker");
                var bestMusicEntry = this.FindByName<Entry>("BestMusicEntry");

                // Check if there's a mood picker in this tab (optional)
                var moodPicker = this.FindByName<Picker>("MoodPicker");
                if (moodPicker != null)
                {
                    string newMood = moodPicker.SelectedItem as string ?? string.Empty;
                    if (oldMood != newMood)
                    {
                        user.Mood = newMood;
                        user.MoodLastUpdated = DateTime.UtcNow;
                        MessagingCenter.Send(this, "MoodUpdated");
                    }
                }

                // Save favorite-genre (picker) into new property
                user.FavoriteMusicGenre = favoriteMusicGenrePicker?.SelectedItem as string ?? string.Empty;

                // BestMusic is free-text (song/or artist)
                user.BestMusic = bestMusicEntry?.Text ?? string.Empty;
                user.TopInterest = topInterestPicker?.SelectedItem as string ?? string.Empty;
                user.TopArtist = topArtistEntry?.Text ?? string.Empty;
                user.TopMovie = topMovieEntry?.Text ?? string.Empty;
                user.SexualOrientation = sexualPicker?.SelectedItem as string ?? string.Empty;
                user.MusicGenres = musicEntry?.Text ?? string.Empty;
                user.FavoriteArtists = favArtistsEntry?.Text ?? string.Empty;
                user.FavoriteMovies = favMoviesEntry?.Text ?? string.Empty;
                user.FavoriteBooks = favBooksEntry?.Text ?? string.Empty;
                user.Languages = languagesEntry?.Text ?? string.Empty;
                user.Occupation = occupationEntry?.Text ?? string.Empty;
                user.Education = educationEntry?.Text ?? string.Empty;
                user.Prompts = promptsEditor?.Text ?? string.Empty;
                user.Dealbreakers = dealbreakersEntry?.Text ?? string.Empty;

                await db.UpdateAsync(user);

                await DisplayAlert("Saved", "Interests updated.", "OK");

                // Send notification in case mood was updated
                MessagingCenter.Send(this, "MoodSaved");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not save interests: " + ex.Message, "OK");
            }
        }
    }

    public sealed class SparkParticleDrawable : IDrawable
    {
        private readonly double _cx, _cy;

        private readonly (double angle, double speed, float size, int colorIndex, bool hasTrail, double delay, double duration)[] _dots;
        private readonly (double angle, double speed, float width, int colorIndex, double duration)[] _streaks;
        private readonly (double angle, double speed, float size, int colorIndex, double delay, double duration)[] _sparkles;
        private readonly (double maxR, float width, int colorIndex, double delay, double duration)[] _rings;

        private static readonly Microsoft.Maui.Graphics.Color[] Colors = {
        Microsoft.Maui.Graphics.Color.FromArgb("#FFD700"),
        Microsoft.Maui.Graphics.Color.FromArgb("#FFA500"),
        Microsoft.Maui.Graphics.Color.FromArgb("#FF8C00"),
        Microsoft.Maui.Graphics.Color.FromArgb("#FFFDE8"),
        Microsoft.Maui.Graphics.Color.FromArgb("#00B5B5"),
    };

        public double Elapsed { get; set; }

        public SparkParticleDrawable(double cx, double cy)
        {
            _cx = cx;
            _cy = cy;
            var rng = new Random();

            _dots = Enumerable.Range(0, 12).Select(i => (
                angle: i * (Math.PI * 2 / 12) + (rng.NextDouble() - 0.5) * 0.3,
                speed: 38.0 + rng.NextDouble() * 22,
                size: (float)(3.2 + rng.NextDouble() * 2.5),
                colorIndex: i % Colors.Length,
                hasTrail: true,
                delay: 0.0,
                duration: 520.0
            )).ToArray();

            _streaks = Enumerable.Range(0, 6).Select(i => (
                angle: i * (Math.PI * 2 / 6) + Math.PI / 12,
                speed: 46.0 + rng.NextDouble() * 16,
                width: (float)(1.0 + rng.NextDouble() * 0.5),
                colorIndex: i % 2 == 0 ? 0 : 3,
                duration: 360.0
            )).ToArray();

            _sparkles = Enumerable.Range(0, 8).Select(i => (
                angle: rng.NextDouble() * Math.PI * 2,
                speed: 18.0 + rng.NextDouble() * 26,
                size: (float)(1.4 + rng.NextDouble() * 1.4),
                colorIndex: rng.Next(0, 2),
                delay: 40.0 + rng.NextDouble() * 60,
                duration: 400.0
            )).ToArray();

            _rings = new[]
            {
            (maxR: 42.0, width: 2.0f, colorIndex: 0, delay: 0.0,   duration: 460.0),
            (maxR: 30.0, width: 1.5f, colorIndex: 4, delay: 55.0,  duration: 390.0),
            (maxR: 20.0, width: 1.0f, colorIndex: 3, delay: 110.0, duration: 300.0),
        };
        }

        private static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;

        public void Draw(ICanvas canvas, RectF dirty)
        {
            float elapsed = (float)Elapsed;
            float cx = (float)_cx;
            float cy = (float)_cy;

            // Central core flash
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

            // Draw rings
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

            // Draw streaks
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

                canvas.StrokeSize = s.width;
                canvas.StrokeColor = Colors[s.colorIndex].WithAlpha(alpha * 0.88f);
                canvas.DrawLine(sx2, sy2, ex, ey);
            }

            // Draw main dots
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

                canvas.FillColor = Colors[3].WithAlpha(alpha * 0.92f);
                canvas.FillCircle(px, py, d.size * 0.52f);
            }

            // Draw micro sparkles
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
}
