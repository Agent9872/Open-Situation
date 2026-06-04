using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Lock.Models;
using Lock.Pages;
using Lock.Pages.Admin;
using Lock.Pages.Chat;
using Lock.Pages.Post;
using Lock.Pages.Profile;
using Lock.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.IO;
using Font = Microsoft.Maui.Font;

namespace Lock
{
    public partial class AppShell : Shell
    {
        private const string CurrentUserPhoneKey = "current_user_phone";
        private bool _autoLoginChecked;
        private static Dictionary<string, WeakReference<ContentPage>> _pageCache = new();
        private static readonly object _cacheLock = new();
        private bool _signalRInitialized = false;
        private bool _currentUserIsVerified = false;

        public AppShell()
        {
            InitializeComponent();

            FlyoutBehavior = FlyoutBehavior.Flyout;

            RegisterRoutes();

            // ✅ Repair roles for users registered before Role column was added
            _ = Task.Run(async () =>
            {
                await AuthService.MigrateExistingUserRolesAsync();

                var savedPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
                if (!string.IsNullOrEmpty(savedPhone))
                {
                    await InitializeSignalRAsync(savedPhone);
                    await LoadUserProfileAsync(savedPhone);
                }
            });

            // Forced logout (ban applied while user is active)
            MessagingCenter.Subscribe<object, string>(this, "UserForcedLogout", async (sender, note) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    AuthService.Logout();
                    await DisplayAlert("Account Action", note, "OK");
                    await GoToAsync("//LoginPage");
                });
            });

            // Warning issued while user is active (no logout)
            MessagingCenter.Subscribe<object, string>(this, "UserWarningIssued", async (sender, note) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Warning from Moderation Team", note, "I Understand");
                    var phone = AuthService.GetCurrentUserPhone();
                    if (!string.IsNullOrEmpty(phone))
                        await UserService.AcknowledgeWarningAsync(phone);
                });
            });

            // Login
            MessagingCenter.Subscribe<LoginPage, string>(this, "UserLoggedIn", async (sender, phone) =>
            {
                Debug.WriteLine($"UserLoggedIn message received for: {phone}");
                await InitializeSignalRAsync(phone);
                await LoadUserProfileAsync(phone);

                FlyoutBehavior = FlyoutBehavior.Flyout;
                Shell.Current.FlyoutIsPresented = false;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigateFastAsync("//post");
                    await Task.Delay(300);
                    await LoadUserProfileAsync(phone);
                });
            });

            // Register
            MessagingCenter.Subscribe<RegisterPage, string>(this, "UserRegistered", async (sender, phone) =>
            {
                Debug.WriteLine($"UserRegistered message received for: {phone}");
                await InitializeSignalRAsync(phone);
                await LoadUserProfileAsync(phone);

                FlyoutBehavior = FlyoutBehavior.Flyout;
                Shell.Current.FlyoutIsPresented = false;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await NavigateFastAsync("//post");
                    await Task.Delay(300);
                    await LoadUserProfileAsync(phone);
                });
            });

            // Profile updated
            MessagingCenter.Subscribe<ProfilePage>(this, "ProfileUpdated", async (sender) =>
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
                if (!string.IsNullOrEmpty(phone))
                {
                    Debug.WriteLine($"ProfileUpdated message received, reloading profile for: {phone}");
                    await LoadUserProfileAsync(phone);
                }
            });

            // Logout
            MessagingCenter.Subscribe<object>(this, "UserLoggedOut", async (sender) =>
            {
                Debug.WriteLine("UserLoggedOut message received");
                await StopSignalRAsync();
                ClearFlyoutProfile();
                ClearPageCache();

                FlyoutBehavior = FlyoutBehavior.Flyout;
                Shell.Current.FlyoutIsPresented = false;
            });

            LoadTheme();
        }

        // ─────────────────────────────────────────────
        // SignalR
        // ─────────────────────────────────────────────

        private async Task InitializeSignalRAsync(string userPhone)
        {
            try
            {
                if (string.IsNullOrEmpty(userPhone)) return;

                if (_signalRInitialized)
                {
                    if (!SignalRService.Instance.IsConnected)
                        await SignalRService.Instance.StartAsync(userPhone);
                    return;
                }

                await SignalRService.Instance.StartAsync(userPhone);
                _signalRInitialized = true;
                Debug.WriteLine($"SignalR initialized for user: {userPhone}");
                await SignalRService.Instance.UpdateUserStatusAsync(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeSignalRAsync error: {ex}");
            }
        }

        private async Task StopSignalRAsync()
        {
            try
            {
                await SignalRService.Instance.StopAsync();
                _signalRInitialized = false;
                Debug.WriteLine("SignalR stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopSignalRAsync error: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        // Navigation helpers
        // ─────────────────────────────────────────────

        private async Task NavigateFastAsync(string route)
        {
            try
            {
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigateFastAsync error: {ex}");
            }
        }

        private void ClearPageCache()
        {
            lock (_cacheLock) { _pageCache.Clear(); }
        }

        public static T GetOrCreatePage<T>(string key, Func<T> factory) where T : ContentPage
        {
            lock (_cacheLock)
            {
                if (_pageCache.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out var page))
                {
                    if (page is T typedPage) return typedPage;
                }
                var newPage = factory();
                _pageCache[key] = new WeakReference<ContentPage>(newPage);
                return newPage;
            }
        }

        // ─────────────────────────────────────────────
        // Route registration
        // ─────────────────────────────────────────────

        private void RegisterRoutes()
        {
            Debug.WriteLine("Registering routes...");

            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("//login", typeof(LoginPage));

            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute("register", typeof(RegisterPage));
            Routing.RegisterRoute("//register", typeof(RegisterPage));

            Routing.RegisterRoute(nameof(PostPage), typeof(PostPage));
            Routing.RegisterRoute("post", typeof(PostPage));
            Routing.RegisterRoute("//post", typeof(PostPage));
            Routing.RegisterRoute("home", typeof(PostPage));

            Routing.RegisterRoute(nameof(CommentsPage), typeof(CommentsPage));
            Routing.RegisterRoute("comments", typeof(CommentsPage));
            Routing.RegisterRoute("post/comments", typeof(CommentsPage));

            Routing.RegisterRoute(nameof(ConversationsPage), typeof(ConversationsPage));
            Routing.RegisterRoute("conversations", typeof(ConversationsPage));
            Routing.RegisterRoute("//conversations", typeof(ConversationsPage));
            Routing.RegisterRoute("chats", typeof(ConversationsPage));

            Routing.RegisterRoute(nameof(ChatSearchPage), typeof(ChatSearchPage));
            Routing.RegisterRoute("chatsearch", typeof(ChatSearchPage));
            Routing.RegisterRoute("//chatsearch", typeof(ChatSearchPage));

            Routing.RegisterRoute(nameof(ConversationSettingsPage), typeof(ConversationSettingsPage));
            Routing.RegisterRoute("conversationsettings", typeof(ConversationSettingsPage));

            Routing.RegisterRoute(nameof(CreateGroupPage), typeof(CreateGroupPage));
            Routing.RegisterRoute("creategroup", typeof(CreateGroupPage));

            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
            Routing.RegisterRoute("profile", typeof(ProfilePage));
            Routing.RegisterRoute("//profile", typeof(ProfilePage));
            Routing.RegisterRoute("profilepage", typeof(ProfilePage));

            Routing.RegisterRoute(nameof(MatchPage), typeof(MatchPage));
            Routing.RegisterRoute("match", typeof(MatchPage));
            Routing.RegisterRoute("//match", typeof(MatchPage));
            Routing.RegisterRoute("matchpage", typeof(MatchPage));

            Routing.RegisterRoute("admin/users", typeof(UsersListPage));
            Routing.RegisterRoute("//admin/users", typeof(UsersListPage));

            Routing.RegisterRoute("userdetail", typeof(UserDetailPage));
            Routing.RegisterRoute("//userdetail", typeof(UserDetailPage));

            Routing.RegisterRoute(nameof(ReportUserPage), typeof(ReportUserPage));

            Routing.RegisterRoute("admin/roles", typeof(AdminRolePage));
            Routing.RegisterRoute("//admin/roles", typeof(AdminRolePage));

            Debug.WriteLine("Routes registered successfully");
        }

        // ─────────────────────────────────────────────
        // Flyout menu item toggle
        // ─────────────────────────────────────────────

        private void UpdateFlyoutMenuItems(bool isLoggedIn)
        {
            try
            {
                var menuSignIn = this.FindByName<Grid>("MenuSignIn");
                var menuRegister = this.FindByName<Grid>("MenuRegister");
                var menuHome = this.FindByName<Grid>("MenuHome");
                var menuChat = this.FindByName<Grid>("MenuChat");

                if (menuSignIn != null) menuSignIn.IsVisible = !isLoggedIn;
                if (menuRegister != null) menuRegister.IsVisible = !isLoggedIn;
                if (menuHome != null) menuHome.IsVisible = isLoggedIn;
                if (menuChat != null) menuChat.IsVisible = isLoggedIn;

                Debug.WriteLine($"UpdateFlyoutMenuItems: isLoggedIn={isLoggedIn}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateFlyoutMenuItems error: {ex}");
            }
        }

        private async void OnHomeMenuTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);
                await NavigateFastAsync("//post");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnHomeMenuTapped error: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        // Load user profile into flyout using Supabase
        // ─────────────────────────────────────────────

        public async Task LoadUserProfileAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                Debug.WriteLine($"Loading profile for phone: {phone}, User found: {(user != null ? "Yes" : "No")}");

                if (user != null)
                    UpdateFlyoutVerificationBadge(user.IsVerified);
                else
                    UpdateFlyoutVerificationBadge(false);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    bool isLoggedIn = user != null;

                    // Labels
                    var nameLabel = this.FindByName<Label>("FlyoutUserName");
                    if (nameLabel != null) nameLabel.Text = user?.Name ?? "User";

                    var phoneLabel = this.FindByName<Label>("FlyoutUserPhone");
                    if (phoneLabel != null) phoneLabel.Text = user?.PhoneNumber ?? phone;

                    // Footer buttons
                    var signOutIcon = this.FindByName<Grid>("SignOutIcon");
                    if (signOutIcon != null) signOutIcon.IsVisible = isLoggedIn;

                    var signOutFooter = this.FindByName<Border>("SignOutFooterButton");
                    if (signOutFooter != null) signOutFooter.IsVisible = isLoggedIn;

                    var adminButton = this.FindByName<Border>("AdminButton");
                    if (adminButton != null)
                    {
                        bool isAdmin = isLoggedIn && (user?.Role == "Admin");
                        adminButton.IsVisible = isAdmin;

                        if (isLoggedIn && user != null)
                            Preferences.Set("current_user_role", user.Role);

                        Debug.WriteLine($"[FLYOUT] user.Role='{user?.Role}' isAdmin={isAdmin}");
                    }

                    // Menu items + force flyout rebuild
                    UpdateFlyoutMenuItems(isLoggedIn);

                    // Profile image
                    var profileImage = this.FindByName<Image>("FlyoutProfileImage");
                    var avatarPlaceholder = this.FindByName<Microsoft.Maui.Controls.Shapes.Path>("FlyoutAvatarPlaceholder");

                    if (profileImage != null)
                    {
                        bool imageLoaded = false;

                        if (isLoggedIn && !string.IsNullOrWhiteSpace(user!.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                        {
                            try
                            {
                                byte[] imageBytes = await File.ReadAllBytesAsync(user.ProfileImagePath);
                                if (imageBytes.Length > 0)
                                {
                                    var ms = new MemoryStream(imageBytes);
                                    profileImage.Source = null;
                                    profileImage.Source = ImageSource.FromStream(() => ms);
                                    profileImage.IsVisible = true;
                                    if (avatarPlaceholder != null) avatarPlaceholder.IsVisible = false;
                                    imageLoaded = true;
                                    Debug.WriteLine($"Flyout profile image loaded: {user.ProfileImagePath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to load flyout profile image: {ex.Message}");
                            }
                        }

                        if (!imageLoaded)
                        {
                            profileImage.Source = null;
                            profileImage.IsVisible = false;
                            if (avatarPlaceholder != null) avatarPlaceholder.IsVisible = true;
                            Debug.WriteLine("No profile image or file missing — showing placeholder");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadUserProfileAsync: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // Clear flyout on logout
        // ─────────────────────────────────────────────

        private void ClearFlyoutProfile()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var nameLabel = this.FindByName<Label>("FlyoutUserName");
                if (nameLabel != null) nameLabel.Text = "Guest";

                var phoneLabel = this.FindByName<Label>("FlyoutUserPhone");
                if (phoneLabel != null) phoneLabel.Text = "Not signed in";

                var profileImage = this.FindByName<Image>("FlyoutProfileImage");
                if (profileImage != null) { profileImage.Source = null; profileImage.IsVisible = false; }

                var avatarPlaceholder = this.FindByName<Microsoft.Maui.Controls.Shapes.Path>("FlyoutAvatarPlaceholder");
                if (avatarPlaceholder != null) avatarPlaceholder.IsVisible = true;

                var verificationBadge = this.FindByName<Border>("FlyoutVerificationBadge");
                if (verificationBadge != null) verificationBadge.IsVisible = false;

                var signOutIcon = this.FindByName<Grid>("SignOutIcon");
                if (signOutIcon != null) signOutIcon.IsVisible = false;

                var signOutFooter = this.FindByName<Border>("SignOutFooterButton");
                if (signOutFooter != null) signOutFooter.IsVisible = false;

                var adminButton = this.FindByName<Border>("AdminButton");
                if (adminButton != null) adminButton.IsVisible = false;

                UpdateFlyoutMenuItems(false);
            });
        }

        // ─────────────────────────────────────────────
        // Verification badge
        // ─────────────────────────────────────────────

        private void UpdateFlyoutVerificationBadge(bool isVerified)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var verificationBadge = this.FindByName<Border>("FlyoutVerificationBadge");
                if (verificationBadge != null)
                {
                    verificationBadge.IsVisible = isVerified;
                    _currentUserIsVerified = isVerified;
                    Debug.WriteLine($"Flyout verification badge: {isVerified}");
                }
            });
        }

        private async void OnVerificationBadgeTapped(object sender, EventArgs e)
        {
            try
            {
                var savedPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();

                if (string.IsNullOrEmpty(savedPhone))
                {
                    await DisplayAlert("Not Logged In", "Please log in to view verification status", "OK");
                    return;
                }

                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(savedPhone)}&limit=1");
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
                        var goToVerify = await DisplayAlert(
                            "Not Verified",
                            $"{user.Name} is not yet verified.\n\n" +
                            "Verification helps build trust in the community.\n\n" +
                            "Would you like to verify your identity now?",
                            "Verify Now", "Later");

                        if (goToVerify)
                            await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(savedPhone)}&viewOnly=false");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnVerificationBadgeTapped error: {ex}");
                await DisplayAlert("Error", "Could not load verification details", "OK");
            }
        }

        // ─────────────────────────────────────────────
        // Theme
        // ─────────────────────────────────────────────

        private void LoadTheme()
        {
            try
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
                var prefKey = string.IsNullOrEmpty(phone) ? "app_theme" : $"user_theme_{phone}";
                var saved = Preferences.Get(prefKey, "dark");

                if (saved == "light")
                {
                    Application.Current!.UserAppTheme = AppTheme.Light;
                    ThemeSegmentedControl.SelectedIndex = 0;
                }
                else
                {
                    Application.Current!.UserAppTheme = AppTheme.Dark;
                    ThemeSegmentedControl.SelectedIndex = 1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading theme: {ex}");
            }
        }

        private void SfSegmentedControl_SelectionChanged(object sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        {
            try
            {
                var selectedTheme = e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark;
                Application.Current!.UserAppTheme = selectedTheme;

                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
                var prefKey = string.IsNullOrEmpty(phone) ? "app_theme" : $"user_theme_{phone}";
                Preferences.Set(prefKey, selectedTheme == AppTheme.Light ? "light" : "dark");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing theme: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        // OnAppearing
        // ─────────────────────────────────────────────

        protected override void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                var savedPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
                if (!string.IsNullOrEmpty(savedPhone))
                    _ = Task.Run(async () => await LoadUserProfileAsync(savedPhone));
                else
                    MainThread.BeginInvokeOnMainThread(() => ClearFlyoutProfile());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearing: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        // Tapped handlers
        // ─────────────────────────────────────────────

        private async void OnSignOutTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);

                bool confirm = await DisplayAlert("Sign Out", "Are you sure you want to sign out?", "Yes", "No");
                if (!confirm) return;

                AuthService.Logout();
                ClearFlyoutProfile();
                ClearPageCache();
                MessagingCenter.Send<object>(this, "UserLoggedOut");
                await NavigateFastAsync("//login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Sign out error: {ex}");
                try
                {
                    AuthService.Logout();
                    ClearFlyoutProfile();
                    await NavigateFastAsync("//login");
                }
                catch { }
            }
        }

        private async void OnLoginMenuTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);
                await NavigateFastAsync("//login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnLoginMenuTapped error: {ex}");
                await NavigateFastAsync("//login");
            }
        }

        private async void OnRegisterMenuTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);
                await NavigateFastAsync("//register");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnRegisterMenuTapped error: {ex}");
                await NavigateFastAsync("//register");
            }
        }

        private async void OnConversationsTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);

                var savedPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim();
                if (string.IsNullOrEmpty(savedPhone))
                {
                    await NavigateFastAsync("//login");
                    return;
                }

                await NavigateFastAsync("//conversations");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnConversationsTapped error: {ex}");
                await NavigateFastAsync("//conversations");
            }
        }

        private async void OnProfileHeaderTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);

                var savedPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
                if (string.IsNullOrEmpty(savedPhone))
                {
                    await NavigateFastAsync("//login");
                    return;
                }

                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(savedPhone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null)
                    await NavigateFastAsync($"profilepage?phone={Uri.EscapeDataString(savedPhone)}&viewOnly=false");
                else
                {
                    Preferences.Remove(CurrentUserPhoneKey);
                    await NavigateFastAsync("//login");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to profile: {ex}");
                try { await NavigateFastAsync("//profile"); }
                catch { await NavigateFastAsync("//login"); }
            }
        }

        private async void OnAdminButtonClicked(object sender, EventArgs e)
        {
            if (!AuthService.IsCurrentUserAdmin())
            {
                await DisplayAlert("Access Denied", "You do not have admin privileges.", "OK");
                return;
            }

            Shell.Current.FlyoutIsPresented = false;
            await Task.Delay(100);
            await Shell.Current.GoToAsync("//admin/users");
        }

        // ─────────────────────────────────────────────
        // Public helpers
        // ─────────────────────────────────────────────

        public void HideMainNavigation()
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateFlyoutMenuItems(false));
        }

        public void ShowMainNavigation()
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateFlyoutMenuItems(true));
        }

        public void HideFlyoutAndTabs()
        {
            MainThread.BeginInvokeOnMainThread(() => FlyoutBehavior = FlyoutBehavior.Locked);
        }

        public void ShowFlyoutAndTabs()
        {
            MainThread.BeginInvokeOnMainThread(() => FlyoutBehavior = FlyoutBehavior.Flyout);
        }

        // ─────────────────────────────────────────────
        // Snackbar / Toast
        // ─────────────────────────────────────────────

        public static async Task DisplaySnackbarAsync(string message)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);
            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            if (OperatingSystem.IsWindows()) return;

            var toast = Toast.Make(message, textSize: 18);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }
    }
}