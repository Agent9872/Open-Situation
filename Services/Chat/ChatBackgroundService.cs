// Services/ChatBackgroundService.cs
using System.Diagnostics;

namespace Lock.Services
{
    public static class ChatBackgroundService
    {
        public const string BackgroundChangedMessage = "BackgroundChanged";
        public const string UpdateChatBadgeMessage = "UpdateChatBadge";

        public static void NotifyBackgroundChanged(string userPhone)
        {
            try
            {
                // Broadcast to all subscribers that background has changed
                MessagingCenter.Send<object, string>(new object(), BackgroundChangedMessage, userPhone);
                Debug.WriteLine($"Background change notification sent for user: {userPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotifyBackgroundChanged error: {ex}");
            }
        }

        public static void NotifyChatBadgeUpdate(int unreadCount)
        {
            try
            {
                // Broadcast badge update to all pages
                MessagingCenter.Send<object, int>(new object(), UpdateChatBadgeMessage, unreadCount);
                Debug.WriteLine($"Chat badge update notification sent: {unreadCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotifyChatBadgeUpdate error: {ex}");
            }
        }
    }
}