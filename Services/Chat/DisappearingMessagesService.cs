using Lock.Models.Chat;
using Lock.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                // Find all messages that have expired
                var allMessages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    "WillDisappear=eq.true&order=SentAt.asc");

                var expiredMessages = allMessages
                    .Where(m => m.ExpiresAt.HasValue && m.ExpiresAt.Value <= DateTime.UtcNow)
                    .ToList();

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
                        await SupabaseService.DeleteAsync("ChatMessages", $"Id=eq.{message.Id}");
                        Debug.WriteLine($"Deleted expired message ID: {message.Id}");
                    }

                    // Update conversation previews for affected conversations
                    var conversationIds = expiredMessages.Select(m => m.ConversationId).Distinct();
                    foreach (var convId in conversationIds)
                    {
                        await UpdateConversationPreviewAsync(convId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CleanupExpiredMessages: {ex.Message}");
            }
        }

        private static async Task UpdateConversationPreviewAsync(string conversationId)
        {
            try
            {
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&limit=1");
                var conversation = conversations.FirstOrDefault();
                if (conversation == null) return;

                var lastMsgList = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&order=SentAt.desc&limit=1");
                var lastMsg = lastMsgList.FirstOrDefault();

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

                await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                    new { LastMessageAt = conversation.LastMessageAt, LastMessagePreview = conversation.LastMessagePreview });
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