using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Lock.Chat.Services;
using Lock.Helpers;
using Lock.Services;
using Lock.Models.Chat;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
using System.Linq;
using Plugin.LocalNotification;
using Plugin.LocalNotification.EventArgs;


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

        private IMessagePollingService _pollingService;
        private IMessageNotificationService _notificationService;
        private ISystemNotificationService _systemNotificationService;

        public App()
        {
            Debug.WriteLine("=== APP CONSTRUCTOR START ===");

            InitializeMoodMappingWithTimeout(TimeSpan.FromSeconds(2));

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

            InitializeNotificationServices();
            Task.Run(async () => await InitializeDatabaseAsync());

            Debug.WriteLine("=== APP CONSTRUCTOR END ===");
        }

        private void InitializeNotificationServices()
        {
            try
            {
                var services = Handler?.MauiContext?.Services;
                if (services != null)
                {
                    _pollingService = services.GetService(typeof(IMessagePollingService)) as IMessagePollingService;
                    _notificationService = services.GetService(typeof(IMessageNotificationService)) as IMessageNotificationService;
                    _systemNotificationService = services.GetService(typeof(ISystemNotificationService)) as ISystemNotificationService;

                    _systemNotificationService?.Initialize();
                    Debug.WriteLine("Notification services initialized successfully");
                }
                else
                {
                    Debug.WriteLine("Could not get services from Handler.MauiContext");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing notification services: {ex}");
            }
        }

        private void InitializeMoodMappingWithTimeout(TimeSpan timeout)
        {
            if (_moodMappingInitialized)
                return;

            lock (_moodMappingLock)
            {
                if (_moodMappingInitialized)
                    return;

                try
                {
                    Debug.WriteLine("Starting MoodMapping initialization...");
                    var task = Task.Run(() => MoodMapping.EnsureInitialized());

                    if (task.Wait(timeout))
                    {
                        if (task.Result)
                        {
                            _moodMappingInitialized = true;
                            Debug.WriteLine("MoodMapping initialized successfully");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"MoodMapping initialization timed out");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error initializing MoodMapping: {ex}");
                }
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            if (!_databaseInitialized)
            {
                lock (_lock)
                {
                    if (!_databaseInitialized)
                    {
                        _databaseInitialized = true;
                    }
                    else
                    {
                        return;
                    }
                }

                try
                {
                    Debug.WriteLine("Starting database initialization...");
                    await DatabaseService.InitializeAsync();
                    await ChatRepository.AddMediaItemsJsonColumnAsync();
                    Preferences.Set("DatabaseMigrationComplete", true);
                    Debug.WriteLine("Database initialization completed successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Database initialization error: {ex}");
                    lock (_lock)
                    {
                        _databaseInitialized = false;
                    }
                }
            }
        }

        public static async Task RequestLocationPermissionAsync()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }

                if (DeviceInfo.Platform == DevicePlatform.Android && OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    var notificationStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                    if (notificationStatus != PermissionStatus.Granted)
                    {
                        await Permissions.RequestAsync<Permissions.PostNotifications>();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Permission request error: {ex}");
            }
        }

        protected override async void OnStart()
        {
            Debug.WriteLine("=== APP ONSTART ===");

            LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;

            IsInForeground = true;
            IsNavigationHandled = false;

            if (!_moodMappingInitialized)
            {
                InitializeMoodMappingWithTimeout(TimeSpan.FromSeconds(1));
            }

            if (_pollingService == null || _notificationService == null)
            {
                InitializeNotificationServices();
            }

            // NO DELAY - redirect immediately
            var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(phone))
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();

                if (user != null)
                {
                    user.LastActive = DateTime.UtcNow;
                    await db.UpdateAsync(user);
                    StartMessagePolling(phone);

                    // Navigate IMMEDIATELY to PostPage
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        // REMOVE THIS LINE - FlyoutBehavior doesn't exist here
                        // FlyoutBehavior = FlyoutBehavior.Flyout;

                        await Shell.Current.GoToAsync("//post", new Dictionary<string, object>
                        {
                            ["animated"] = false
                        });
                    });
                }
                else
                {
                    Preferences.Remove(CurrentUserPhoneKey);
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync("//login", new Dictionary<string, object>
                        {
                            ["animated"] = false
                        });
                    });
                }
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync("//login", new Dictionary<string, object>
                    {
                        ["animated"] = false
                    });
                });
            }

            base.OnStart();
        }


        private void StartMessagePolling(string userPhone)
        {
            try
            {
                if (_pollingService != null)
                {
                    _pollingService.MessageReceived -= OnMessageReceived;
                    _pollingService.MessageReceived += OnMessageReceived;
                    _pollingService.StartPolling(userPhone);
                    Debug.WriteLine($"Message polling started for user: {userPhone}");
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
            Debug.WriteLine($"📨 Message received from {message.SenderPhone}");

            if (IsCurrentlyInChat(message.ConversationId))
            {
                Debug.WriteLine("User is currently in this chat - skipping notification");
                return;
            }

            var senderName = await GetUserDisplayName(message.SenderPhone);
            var senderAvatar = await GetUserAvatarPath(message.SenderPhone);

            if (IsInForeground)
            {
                await _notificationService?.ShowNewMessagePopupAsync(
                    message, senderName, senderAvatar,
                    () => NavigateToChat(message.ConversationId, message.SenderPhone));
            }

            MessagingCenter.Send(this, "MessagesUpdated");
        }

        private async Task<int> GetUnreadMessageCount()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);

                if (string.IsNullOrEmpty(currentUserPhone)) return 0;

                return await db.Table<ChatMessage>()
                    .Where(m => m.RecipientPhone == currentUserPhone && !m.IsRead)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting unread count: {ex}");
                return 0;
            }
        }

        private async Task<string> GetUserDisplayName(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
                return user?.Name ?? phone;
            }
            catch
            {
                return phone;
            }
        }

        private async Task<string> GetUserAvatarPath(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
                return user?.ProfileImagePath ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool IsCurrentlyInChat(string conversationId)
        {
            try
            {
                var currentPage = GetCurrentPage();
                if (currentPage is Pages.Chat.ChatPage chatPage)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking current chat: {ex}");
            }
            return false;
        }

        private Page GetCurrentPage()
        {
            var mainPage = Application.Current?.MainPage;
            if (mainPage is NavigationPage navigationPage)
                return navigationPage.CurrentPage;
            if (mainPage is Shell shell)
                return shell.CurrentPage;
            return mainPage;
        }

        private async void NavigateToChat(string conversationId, string otherPhone)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync($"//conversations/chat?conversationId={conversationId}&otherPhone={otherPhone}");
                    Debug.WriteLine($"Navigated to chat: {conversationId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error navigating to chat: {ex}");
                }
            });
        }

        // Event handler using object parameter to avoid type resolution issues
        private void OnNotificationActionTapped(NotificationActionEventArgs e)
        {
            try
            {
                Debug.WriteLine($"🔔 Notification tapped: {e?.Request?.NotificationId}");

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

        // Add this method to your App.xaml.cs or any page
        public async Task TestSystemNotification()
        {
            try
            {
                var testMessage = new ChatMessage
                {
                    Id = 9999, // Unique ID
                    ConversationId = "test-conversation-123",
                    SenderPhone = "+1234567890",
                    Content = "This is a test notification message! 🎉",
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    MessageType = "text"
                };

                var testNotificationService = new SystemNotificationService();
                testNotificationService.Initialize();
                testNotificationService.ShowNewMessageNotification(testMessage, "Test User", 1);

                Debug.WriteLine("✅ Test notification sent!");

                // Show a popup confirmation
                await Application.Current.MainPage.DisplayAlert("Test", "Test notification sent! Check your notification center.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Test notification failed: {ex}");
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to send test notification: {ex.Message}", "OK");
            }
        }

        protected override void OnSleep()
        {
            Debug.WriteLine("App OnSleep");

            // CRITICAL: Use FULL namespace
            LocalNotificationCenter.Current.NotificationActionTapped -= OnNotificationActionTapped;

            IsInForeground = false;

            try
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await DatabaseService.InitializeAsync();
                            var db = DatabaseService.GetConnection();
                            var user = await db.Table<Lock.Models.User>()
                                .Where(u => u.PhoneNumber == phone)
                                .FirstOrDefaultAsync();

                            if (user != null)
                            {
                                user.LastActive = DateTime.UtcNow;
                                await db.UpdateAsync(user);
                                Debug.WriteLine($"Updated last active for {phone} on sleep");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating last active on sleep: {ex}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnSleep: {ex}");
            }

            base.OnSleep();
        }

        protected override void OnResume()
        {
            Debug.WriteLine("App OnResume");

            // CRITICAL: Use FULL namespace
            LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;

            IsInForeground = true;

            bool migrationComplete = Preferences.Get("DatabaseMigrationComplete", false);
            if (!migrationComplete)
            {
                Task.Run(async () => await InitializeDatabaseAsync());
            }

            if (!_moodMappingInitialized)
            {
                InitializeMoodMappingWithTimeout(TimeSpan.FromSeconds(1));
            }

            try
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await DatabaseService.InitializeAsync();
                            var db = DatabaseService.GetConnection();
                            var user = await db.Table<Lock.Models.User>()
                                .Where(u => u.PhoneNumber == phone)
                                .FirstOrDefaultAsync();

                            if (user != null)
                            {
                                user.LastActive = DateTime.UtcNow;
                                await db.UpdateAsync(user);
                                Debug.WriteLine($"Updated last active for {phone} on resume");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating last active on resume: {ex}");
                        }
                    });

                    if (_pollingService != null)
                    {
                        _pollingService.StopPolling();
                        _pollingService.StartPolling(phone);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnResume: {ex}");
            }

            base.OnResume();
        }

        public static bool IsMoodMappingInitialized() => _moodMappingInitialized;
        public static bool IsDatabaseInitialized() => _databaseInitialized;

        private class NotificationData
        {
            public string ConversationId { get; set; } = string.Empty;
            public string SenderPhone { get; set; } = string.Empty;
            public int Id { get; set; }
        }
    }
}