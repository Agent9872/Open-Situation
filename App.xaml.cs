using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Lock.Helpers;
using Lock.Services;
using Lock.Models.Chat;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
using System.Linq;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;
using Lock.Models;

namespace Lock
{
    public partial class App : Application
    {
        private const string CurrentUserPhoneKey = "current_user_phone";
        private static bool _databaseInitialized = false;
        private static bool _moodMappingInitialized = false;
        private static readonly object _lock = new object();
        private static readonly object _moodMappingLock = new object();

        public static bool IsNavigationHandled { get; set; } = false;
        public static bool IsInForeground { get; set; } = true;

        private IMessagePollingService? _pollingService;
        private IMessageNotificationService? _notificationService;
        private ISystemNotificationService? _systemNotificationService;

        public App()
        {
            Debug.WriteLine("=== APP CONSTRUCTOR START ===");

            // ✅ FIX: Do NOT block the UI thread with .Wait() — this deadlocks on Android.
            // MoodMapping is now initialised fire-and-forget in the background.
            _ = Task.Run(() =>
            {
                try
                {
                    if (MoodMapping.EnsureInitialized())
                    {
                        _moodMappingInitialized = true;
                        Debug.WriteLine("MoodMapping initialised successfully (background)");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MoodMapping init error: {ex.Message}");
                }
            });

            try
            {
                InitializeComponent();
                Debug.WriteLine("InitializeComponent completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in InitializeComponent: {ex}");
            }

            try
            {
                MainPage = new AppShell();
                Debug.WriteLine("AppShell created successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR creating AppShell: {ex}");
            }

            // ✅ FIX: Don't resolve services in constructor — Handler is null here.
            // Services are resolved lazily in OnStart() after the app is fully running.

            Debug.WriteLine("=== APP CONSTRUCTOR END ===");
        }

        // ── Service initialisation ────────────────────────────────────────────
        // ✅ FIX: Called from OnStart() where Handler/MauiContext is available.

        private void InitializeNotificationServices()
        {
            try
            {
                var services = IPlatformApplication.Current?.Services;
                if (services != null)
                {
                    _pollingService          = services.GetService(typeof(IMessagePollingService))      as IMessagePollingService;
                    _notificationService     = services.GetService(typeof(IMessageNotificationService)) as IMessageNotificationService;
                    _systemNotificationService = services.GetService(typeof(ISystemNotificationService)) as ISystemNotificationService;

                    _systemNotificationService?.Initialize();
                    Debug.WriteLine("Notification services initialised successfully");
                }
                else
                {
                    Debug.WriteLine("IPlatformApplication.Current?.Services is null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initialising notification services: {ex}");
            }
        }

        // ── OnStart ───────────────────────────────────────────────────────────

        protected override async void OnStart()
        {
            Debug.WriteLine("=== APP ONSTART BEGIN ===");

            try
            {
                LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
                IsInForeground = true;
                IsNavigationHandled = false;

                if (_pollingService == null || _notificationService == null)
                    InitializeNotificationServices();

                // Non-blocking Supabase ping
                _ = Task.Run(async () => await InitializeSupabaseAsync());

                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                Debug.WriteLine($"[ONSTART] Saved phone: '{phone}'");

                if (!string.IsNullOrWhiteSpace(phone))
                {
                    try
                    {
                        Debug.WriteLine("[ONSTART] Fetching user from Supabase...");

                        // ✅ Timeout guard — Supabase hanging on mobile kills the app
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

                        var usersTask = SupabaseService.GetAsync<Lock.Models.User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");

                        var completedTask = await Task.WhenAny(usersTask, Task.Delay(8000, cts.Token));

                        if (completedTask != usersTask)
                        {
                            // Timed out — go to login, don't crash
                            Debug.WriteLine("[ONSTART] Supabase timed out — navigating to login");
                            Preferences.Remove(CurrentUserPhoneKey);
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                await Shell.Current.GoToAsync("//login", false));
                            base.OnStart();
                            return;
                        }

                        var users = await usersTask;
                        var user = users?.FirstOrDefault();
                        Debug.WriteLine($"[ONSTART] User found: {user != null}");

                        if (user != null)
                        {
                            // Fire-and-forget last active update — don't block startup
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}",
                                        new { LastActive = DateTime.UtcNow });
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[ONSTART] LastActive update failed: {ex.Message}");
                                }
                            });

                            StartMessagePolling(phone);

                            Debug.WriteLine("[ONSTART] Navigating to //post");
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                await Shell.Current.GoToAsync("//post", false));
                        }
                        else
                        {
                            Debug.WriteLine("[ONSTART] No user found — clearing phone and going to login");
                            Preferences.Remove(CurrentUserPhoneKey);
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                                await Shell.Current.GoToAsync("//login", false));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ONSTART] User fetch error: {ex.GetType().Name}: {ex.Message}");
                        Debug.WriteLine($"[ONSTART] StackTrace: {ex.StackTrace}");
                        // Don't crash — just go to login
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                            await Shell.Current.GoToAsync("//login", false));
                    }
                }
                else
                {
                    Debug.WriteLine("[ONSTART] No saved phone — navigating to login");
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await Shell.Current.GoToAsync("//login", false));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ONSTART] *** TOP LEVEL CRASH: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[ONSTART] *** StackTrace: {ex.StackTrace}");

                // Last resort — try to get to login without crashing
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                        await Shell.Current.GoToAsync("//login", false));
                }
                catch (Exception navEx)
                {
                    Debug.WriteLine($"[ONSTART] Even fallback navigation failed: {navEx.Message}");
                }
            }

            Debug.WriteLine("=== APP ONSTART END ===");
            base.OnStart();
        }

        // ── Supabase init ─────────────────────────────────────────────────────

        private async Task InitializeSupabaseAsync()
        {
            if (_databaseInitialized) return;

            lock (_lock)
            {
                if (_databaseInitialized) return;
                _databaseInitialized = true;
            }

            try
            {
                Debug.WriteLine("Starting Supabase connection test...");
                var test = await SupabaseService.GetAsync<User>("Users", "limit=1");
                Debug.WriteLine($"Supabase connected. Found {test.Count} users.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Supabase connection test failed: {ex.Message}");
                lock (_lock) { _databaseInitialized = false; }
            }
        }

        // ── Message polling ───────────────────────────────────────────────────

        private void StartMessagePolling(string userPhone)
        {
            try
            {
                if (_pollingService != null)
                {
                    _pollingService.MessageReceived -= OnMessageReceived;
                    _pollingService.MessageReceived += OnMessageReceived;
                    _pollingService.StartPolling(userPhone);
                    Debug.WriteLine($"Message polling started for: {userPhone}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting message polling: {ex}");
            }
        }

        private void StopMessagePolling()
        {
            try
            {
                if (_pollingService != null)
                {
                    _pollingService.MessageReceived -= OnMessageReceived;
                    _pollingService.StopPolling();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping message polling: {ex}");
            }
        }

        private async Task OnMessageReceived(ChatMessage message)
        {
            Debug.WriteLine($"Message received from {message.SenderPhone}");

            if (IsCurrentlyInChat(message.ConversationId))
            {
                Debug.WriteLine("User is in this chat — skipping notification");
                return;
            }

            var senderName   = await GetUserDisplayName(message.SenderPhone);
            var senderAvatar = await GetUserAvatarPath(message.SenderPhone);

            if (IsInForeground && _notificationService != null)
            {
                await _notificationService.ShowNewMessagePopupAsync(
                    message, senderName, senderAvatar,
                    () => NavigateToChat(message.ConversationId, message.SenderPhone));
            }

            MessagingCenter.Send(this, "MessagesUpdated");
        }

        private async Task<string> GetUserDisplayName(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                return users.FirstOrDefault()?.Name ?? phone;
            }
            catch { return phone; }
        }

        private async Task<string> GetUserAvatarPath(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                return users.FirstOrDefault()?.ProfileImagePath ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private bool IsCurrentlyInChat(string conversationId)
        {
            try
            {
                return GetCurrentPage() is Pages.Chat.ChatPage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking current chat: {ex}");
                return false;
            }
        }

        private Page? GetCurrentPage()
        {
            var mainPage = Application.Current?.MainPage;
            if (mainPage is NavigationPage nav) return nav.CurrentPage;
            if (mainPage is Shell shell)        return shell.CurrentPage;
            return mainPage;
        }

        private async void NavigateToChat(string conversationId, string otherPhone)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync(
                        $"//conversations/chat?conversationId={conversationId}&otherPhone={otherPhone}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error navigating to chat: {ex}");
                }
            });
        }

        // ── Notification handling ─────────────────────────────────────────────

        private void OnNotificationActionTapped(NotificationActionEventArgs e)
        {
            try
            {
                Debug.WriteLine($"Notification tapped: {e?.Request?.NotificationId}");
                if (e.IsDismissed) return;

                if (e.IsTapped)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            var returningData = e?.Request?.ReturningData;
                            if (!string.IsNullOrEmpty(returningData))
                            {
                                var data = System.Text.Json.JsonSerializer.Deserialize<NotificationData>(returningData);
                                if (data != null && !string.IsNullOrEmpty(data.ConversationId))
                                {
                                    await Shell.Current.GoToAsync(
                                        $"//conversations/chat?conversationId={data.ConversationId}&otherPhone={data.SenderPhone}");
                                    return;
                                }
                            }
                            await Shell.Current.GoToAsync("//conversations");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error navigating from notification: {ex}");
                            await Shell.Current.GoToAsync("//conversations");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling notification tap: {ex}");
            }
        }

        // ── Permissions ───────────────────────────────────────────────────────

        public static async Task RequestLocationPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

                if (DeviceInfo.Platform == DevicePlatform.Android && OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    var notifStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                    if (notifStatus != PermissionStatus.Granted)
                        await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Permission request error: {ex}");
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override async void OnSleep()
        {
            Debug.WriteLine("App OnSleep");

            LocalNotificationCenter.Current.NotificationActionTapped -= OnNotificationActionTapped;
            IsInForeground = false;

            try
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var user = users.FirstOrDefault();
                    if (user != null)
                    {
                        await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}",
                            new { LastActive = DateTime.UtcNow });
                        Debug.WriteLine($"Updated last active on sleep for {phone}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnSleep: {ex}");
            }

            base.OnSleep();
        }

        protected override async void OnResume()
        {
            Debug.WriteLine("App OnResume");

            LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
            IsInForeground = true;

            if (_pollingService == null || _notificationService == null)
                InitializeNotificationServices();

            try
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var user = users.FirstOrDefault();

                    if (user != null)
                    {
                        await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}",
                            new { LastActive = DateTime.UtcNow });

                        if (_pollingService != null)
                        {
                            _pollingService.StopPolling();
                            _pollingService.StartPolling(phone);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnResume: {ex}");
            }

            base.OnResume();
        }

        // ── Test helper ───────────────────────────────────────────────────────

        public async Task TestSystemNotification()
        {
            try
            {
                var testMessage = new ChatMessage
                {
                    Id             = Guid.NewGuid().ToString(),
                    ConversationId = "test-conversation-123",
                    SenderPhone    = "+1234567890",
                    Content        = "This is a test notification! 🎉",
                    SentAt         = DateTime.UtcNow,
                    IsRead         = false,
                    MessageType    = "text"
                };

                var svc = new SystemNotificationService();
                svc.Initialize();
                svc.ShowNewMessageNotification(testMessage, "Test User", 1);

                Debug.WriteLine("Test notification sent!");
                await Application.Current!.MainPage!.DisplayAlert("Test", "Test notification sent!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test notification failed: {ex}");
                await Application.Current!.MainPage!.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // ── Static helpers ────────────────────────────────────────────────────

        public static bool IsMoodMappingInitialized() => _moodMappingInitialized;
        public static bool IsDatabaseInitialized()    => _databaseInitialized;

        // ── Private types ─────────────────────────────────────────────────────

        private class NotificationData
        {
            public string ConversationId { get; set; } = string.Empty;
            public string SenderPhone    { get; set; } = string.Empty;
            public int    Id             { get; set; }
        }
    }
}