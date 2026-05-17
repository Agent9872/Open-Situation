using Lock.Chat.Services;
using Lock.Models.Chat;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lock.Services.Chat
{
    public static class DisappearingMessagesService
    {
        private static Timer? _cleanupTimer;
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // Run cleanup every 30 seconds
            _cleanupTimer = new Timer(async _ => await CleanupExpiredMessages(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            _isInitialized = true;

            Debug.WriteLine("DisappearingMessagesService initialized");
        }

        public static async Task CleanupExpiredMessages()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Find all messages that have expired
                var expiredMessages = await db.Table<ChatMessage>()
                    .Where(m => m.WillDisappear && m.ExpiresAt != null && m.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync();

                if (expiredMessages.Any())
                {
                    Debug.WriteLine($"Found {expiredMessages.Count} expired messages to delete");

                    foreach (var message in expiredMessages)
                    {
                        // Delete associated media files if any
                        if (!string.IsNullOrEmpty(message.MediaPath) && File.Exists(message.MediaPath))
                        {
                            try
                            {
                                File.Delete(message.MediaPath);
                                Debug.WriteLine($"Deleted media file: {message.MediaPath}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to delete media file: {ex.Message}");
                            }
                        }

                        // Delete from database
                        await db.DeleteAsync(message);
                        Debug.WriteLine($"Deleted expired message ID: {message.Id}");
                    }

                    // Update conversation previews for affected conversations
                    var conversationIds = expiredMessages.Select(m => m.ConversationId).Distinct();
                    foreach (var convId in conversationIds)
                    {
                        await UpdateConversationPreviewAsync(db, convId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CleanupExpiredMessages: {ex.Message}");
            }
        }

        private static async Task UpdateConversationPreviewAsync(SQLiteAsyncConnection db, string conversationId)
        {
            try
            {
                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversationId)
                    .FirstOrDefaultAsync();

                if (conversation == null) return;

                var lastMsg = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                if (lastMsg != null)
                {
                    conversation.LastMessageAt = lastMsg.SentAt;

                    if (lastMsg.IsVoiceMessage)
                    {
                        conversation.LastMessagePreview = "🎤 Voice message";
                    }
                    else if (!string.IsNullOrEmpty(lastMsg.MediaPath))
                    {
                        conversation.LastMessagePreview = "📷 Photo";
                    }
                    else if (!string.IsNullOrWhiteSpace(lastMsg.Content))
                    {
                        conversation.LastMessagePreview = lastMsg.Content.Length > 50
                            ? lastMsg.Content.Substring(0, 50) + "…"
                            : lastMsg.Content;
                    }
                    else
                    {
                        conversation.LastMessagePreview = "New message";
                    }
                }
                else
                {
                    conversation.LastMessageAt = DateTime.MinValue;
                    conversation.LastMessagePreview = string.Empty;
                }

                await db.UpdateAsync(conversation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating conversation preview: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
            _isInitialized = false;
        }
    }
}