using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Lock.Chat.Services;
using Lock.Platforms.Android;
using Lock.Services;
using Plugin.LocalNotification.EventArgs;
using System.Text.Json;
using SQLite;
using Lock.Models.Chat;

namespace Lock
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              LaunchMode = LaunchMode.SingleTop,
              ConfigurationChanges = ConfigChanges.ScreenSize |
                                     ConfigChanges.Orientation |
                                     ConfigChanges.UiMode |
                                     ConfigChanges.ScreenLayout |
                                     ConfigChanges.SmallestScreenSize |
                                     ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int NOTIFICATION_PERMISSION_CODE = 100;
        private const int FOLDER_PICKER_REQUEST_CODE = 1001;
        private const int ACTION_REPLY = 100;
        private const int ACTION_MARK_READ = 101;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestNotificationPermission();
            HandleNotificationIntent(Intent);

            Plugin.LocalNotification.LocalNotificationCenter.Current
                .NotificationActionTapped += OnNotificationActionTapped;
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleNotificationIntent(intent);
        }

        private void OnNotificationActionTapped(NotificationActionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Notification action: ActionId={e.ActionId} IsTapped={e.IsTapped}");

            if (e.IsTapped)
            {
                NavigateToChat(e.Request?.ReturningData);
                return;
            }

            if (string.IsNullOrEmpty(e.Request?.ReturningData)) return;

            NotificationPayload? payload;
            try { payload = JsonSerializer.Deserialize<NotificationPayload>(e.Request.ReturningData); }
            catch { return; }
            if (payload == null) return;

            if (e.ActionId == ACTION_REPLY)
                HandleReply(payload);
            else if (e.ActionId == ACTION_MARK_READ)
                HandleMarkAsRead(payload);
        }

        private void HandleReply(NotificationPayload payload)
        {
            // v11 of Plugin.LocalNotification does not support RemoteInput inline reply.
            // Best we can do: open the chat page so the user can type there.
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(300);
                await Shell.Current.GoToAsync(
                    $"//conversations/chat?conversationId={payload.ConversationId}" +
                    $"&otherPhone={payload.SenderPhone}");
            });
        }

        private void HandleMarkAsRead(NotificationPayload payload)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();

                    var currentPhone = Microsoft.Maui.Storage.Preferences
                        .Get("current_user_phone", string.Empty);

                    var unread = await db.Table<Lock.Models.Chat.ChatMessage>()
                        .Where(m => m.ConversationId == payload.ConversationId
                                 && m.RecipientPhone == currentPhone
                                 && !m.IsRead)
                        .ToListAsync();

                    foreach (var msg in unread)
                    {
                        msg.IsRead = true;
                        await db.UpdateAsync(msg);
                    }

                    Plugin.LocalNotification.LocalNotificationCenter.Current
                        .Cancel(payload.MessageId);

                    System.Diagnostics.Debug.WriteLine($"✅ Marked {unread.Count} messages read in {payload.ConversationId}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Mark as read error: {ex.Message}");
                }
            });
        }

        private void NavigateToChat(string? returningData)
        {
            if (string.IsNullOrEmpty(returningData)) return;
            try
            {
                var payload = JsonSerializer.Deserialize<NotificationPayload>(returningData);
                if (payload == null) return;

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(500);
                    await Shell.Current.GoToAsync(
                        $"//conversations/chat?conversationId={payload.ConversationId}" +
                        $"&otherPhone={payload.SenderPhone}");
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}"); }
        }

        private void HandleNotificationIntent(Intent? intent)
        {
            if (intent == null) return;
            try
            {
                var data = intent.GetStringExtra(
                    Plugin.LocalNotification.LocalNotificationCenter.ReturnRequest);
                if (!string.IsNullOrEmpty(data))
                    NavigateToChat(data);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Intent error: {ex.Message}"); }
        }

        private void RequestNotificationPermission()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;
            const string perm = Android.Manifest.Permission.PostNotifications;
            if (ContextCompat.CheckSelfPermission(this, perm) != Permission.Granted)
                ActivityCompat.RequestPermissions(this, new[] { perm }, NOTIFICATION_PERMISSION_CODE);
        }

        public override void OnRequestPermissionsResult(
            int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == NOTIFICATION_PERMISSION_CODE)
                System.Diagnostics.Debug.WriteLine(grantResults.Length > 0 && grantResults[0] == Permission.Granted
                    ? "Notifications: granted ✅" : "Notifications: denied ❌");
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            Lock.Platforms.Android.ContactPickerService.OnActivityResult(requestCode, resultCode, data);
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                ((NotificationManager)GetSystemService(NotificationService)!).CancelAll();
        }
    }

    internal class NotificationPayload
    {
        public string ConversationId { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public int MessageId { get; set; }
    }
}