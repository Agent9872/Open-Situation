using Lock.Models.Chat;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using System;
using System.Diagnostics;

namespace Lock.Services
{
    public interface ISystemNotificationService
    {
        void ShowNewMessageNotification(ChatMessage message, string senderName, int unreadCount, string senderAvatarPath = null);
        void Initialize();
    }

    public class SystemNotificationService : ISystemNotificationService
    {
        private bool _initialized = false;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Debug.WriteLine("System notification service initialized");
        }

        public void ShowNewMessageNotification(
            ChatMessage message,
            string senderName,
            int unreadCount,
            string senderAvatarPath = null)
        {
            try
            {
                Debug.WriteLine($"=== SENDING NOTIFICATION === Sender:{senderName} MsgId:{message.Id}");

                string preview = GetMessagePreview(message);
                string description = preview.Length > 100 ? preview[..100] + "..." : preview;

                string returningData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    message.ConversationId,
                    message.SenderPhone,
                    MessageId = message.Id
                });

                var notification = new NotificationRequest
                {
                    NotificationId = message.Id,
                    Title = senderName,
                    Description = description,
                    BadgeNumber = unreadCount,
                    ReturningData = returningData,

                    // Use Status — Message does NOT exist in v11
                    // This links to the Reply + Mark as Read actions
                    // registered in MauiProgram.cs
                    CategoryType = NotificationCategoryType.Status,

                    Schedule = new NotificationRequestSchedule
                    {
                        NotifyTime = DateTime.Now
                    },

                    Android = new AndroidOptions
                    {
                        ChannelId = "lock_chat_channel",
                        Priority = AndroidPriority.Max,
                        AutoCancel = true,
                        VisibilityType = AndroidVisibilityType.Public
                    }
                };

                Plugin.LocalNotification.LocalNotificationCenter.Current.Show(notification);
                Debug.WriteLine($"✅ Notification sent for message {message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Notification error: {ex.Message}");
                Debug.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }

        private string GetMessagePreview(ChatMessage message)
        {
            if (message.IsVoiceMessage) return "🎤 Voice message";
            if (message.MessageType == "post") return "📝 Shared a post";
            if (!string.IsNullOrEmpty(message.Content))
            {
                var content = message.Content.Replace("\n", " ").Trim();
                return content.Length > 60 ? content[..60] + "..." : content;
            }
            return "New message";
        }
    }
}