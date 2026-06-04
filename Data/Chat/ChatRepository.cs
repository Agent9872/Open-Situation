using Lock.Models;
using Lock.Models.Chat;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lock.Chat.Services
{
    public static class ChatRepository
    {
        private const string MessageRequestsKey = "message_requests";

        // Check if users have exchanged messages before (are contacts)
        private static async Task<bool> AreUsersContactsAsync(string userPhone, string otherPhone)
        {
            try
            {
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(and(ParticipantA.eq.{Uri.EscapeDataString(userPhone)},ParticipantB.eq.{Uri.EscapeDataString(otherPhone)})," +
                    $"and(ParticipantA.eq.{Uri.EscapeDataString(otherPhone)},ParticipantB.eq.{Uri.EscapeDataString(userPhone)}))&limit=1");

                var conversation = conversations.FirstOrDefault();
                if (conversation == null) return false;

                var messages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversation.ConversationId)}&IsMessageRequest=eq.false");

                return messages.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AreUsersContactsAsync error: {ex}");
                return false;
            }
        }

        // Save message request to database and preferences
        private static async Task SaveMessageRequestAsync(string conversationId, string senderPhone, string recipientPhone, string messagePreview)
        {
            try
            {
                var request = new MessageRequest
                {
                    ConversationId = conversationId,
                    SenderPhone = senderPhone,
                    RecipientPhone = recipientPhone,
                    RequestedAt = DateTime.UtcNow,
                    MessagePreview = messagePreview,
                    IsAccepted = false,
                    IsDeclined = false
                };

                await SupabaseService.InsertAsync("MessageRequests", request);

                var existingJson = Preferences.Get(MessageRequestsKey, "[]");
                var requests = JsonSerializer.Deserialize<List<string>>(existingJson) ?? new List<string>();
                if (!requests.Contains(conversationId))
                {
                    requests.Add(conversationId);
                    Preferences.Set(MessageRequestsKey, JsonSerializer.Serialize(requests));
                }

                Debug.WriteLine($"Saved message request for conversation: {conversationId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving message request: {ex}");
            }
        }

        // Get or create conversation
        public static async Task<Conversation> GetOrCreateConversationAsync(string phoneA, string phoneB)
        {
            phoneA = (phoneA ?? "").Trim();
            phoneB = (phoneB ?? "").Trim();

            var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                $"or(and(ParticipantA.eq.{Uri.EscapeDataString(phoneA)},ParticipantB.eq.{Uri.EscapeDataString(phoneB)})," +
                $"and(ParticipantA.eq.{Uri.EscapeDataString(phoneB)},ParticipantB.eq.{Uri.EscapeDataString(phoneA)}))&limit=1");

            var conv = conversations.FirstOrDefault();

            if (conv != null)
                return conv;

            conv = new Conversation
            {
                ConversationId = Guid.NewGuid().ToString(),
                ParticipantA = phoneA,
                ParticipantB = phoneB,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.MinValue,
                LastMessagePreview = string.Empty
            };

            var inserted = await SupabaseService.InsertAndReturnAsync<Conversation>("Conversations", conv);
            return inserted ?? conv;
        }

        // Add a message (with message request handling)
        public static async Task AddMessageAsync(ChatMessage message, bool isMultiImageMessage = false)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(message.ConversationId))
                throw new InvalidOperationException("Message must have a ConversationId");

            message.SentAt = DateTime.UtcNow;

            try
            {
                // Check if blocked (both directions)
                bool recipientBlockedSender = await IsRecipientBlockingSenderAsync(message.SenderPhone, message.RecipientPhone);
                bool senderBlockedRecipient = await IsUserBlockedAsync(message.SenderPhone, message.RecipientPhone);

                if (recipientBlockedSender || senderBlockedRecipient)
                {
                    message.IsBlocked = true;
                    message.IsDelivered = false;
                    message.IsRead = false;
                    await SupabaseService.InsertAsync("ChatMessages", message);
                    return;
                }

                // Check if this is a message request (first message from non-contact)
                bool isMessageRequest = false;

                var existingMessages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(message.ConversationId)}&IsBlocked=eq.false");

                if (existingMessages.Count == 0)
                {
                    var areContacts = await AreUsersContactsAsync(message.RecipientPhone, message.SenderPhone);
                    isMessageRequest = !areContacts;

                    if (isMessageRequest)
                    {
                        string preview = !string.IsNullOrEmpty(message.Content)
                            ? (message.Content.Length > 50 ? message.Content.Substring(0, 50) + "..." : message.Content)
                            : message.IsVoiceMessage ? "?? Voice message"
                            : message.MediaItems?.Count > 0 ? $"?? {message.MediaItems.Count} photos"
                            : "New message";

                        await SaveMessageRequestAsync(message.ConversationId, message.SenderPhone, message.RecipientPhone, preview);
                    }
                }

                message.IsMessageRequest = isMessageRequest;
                message.IsDelivered = true;
                message.IsRead = false;

                // Handle media items
                if (message.MediaItems?.Count > 0)
                {
                    foreach (var item in message.MediaItems)
                    {
                        if (string.IsNullOrEmpty(item.Type))
                            item.Type = "image";
                    }

                    // Set legacy fields from first item for backward compatibility
                    if (message.MediaItems.Count > 0)
                    {
                        message.MediaPath = message.MediaItems[0].Path;
                        message.MediaType = message.MediaItems[0].Type;

                        if (message.MediaItems[0].IsAudio)
                        {
                            message.IsVoiceMessage = true;
                            message.VoiceDurationSeconds = message.MediaItems[0].DurationSeconds ?? 5;
                            message.VoiceWaveformData = message.MediaItems[0].WaveformData;
                        }
                    }
                }

                // Serialize MediaItems to JSON
                if (message.MediaItems != null && message.MediaItems.Count > 0)
                {
                    message.MediaItemsJson = JsonSerializer.Serialize(message.MediaItems);
                }
                else
                {
                    message.MediaItemsJson = "[]";
                }

                // Insert the message
                await SupabaseService.InsertAsync("ChatMessages", message);
                Debug.WriteLine($"Saved message ID: {message.Id}");

                await UpdateConversationPreviewAsync(message.ConversationId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddMessageAsync: {ex.Message}");
                throw;
            }
        }

        public static async Task<List<Conversation>> GetAllConversationsAsync(string userPhone)
        {
            var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                $"or(ParticipantA.eq.{Uri.EscapeDataString(userPhone)},ParticipantB.eq.{Uri.EscapeDataString(userPhone)})&order=LastMessageAt.desc");
            return conversations;
        }

        // Get messages for a conversation
        public static async Task<List<ChatMessage>> GetMessagesAsync(string conversationId, int max = 200)
        {
            try
            {
                var messages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&IsBlocked=eq.false&order=SentAt.asc&limit={max}");

                // Deserialize MediaItems
                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.MediaItemsJson))
                    {
                        try
                        {
                            msg.MediaItems = JsonSerializer.Deserialize<List<ChatMediaItem>>(msg.MediaItemsJson)
                                ?? new List<ChatMediaItem>();
                        }
                        catch
                        {
                            msg.MediaItems = new List<ChatMediaItem>();
                        }
                    }
                    else
                    {
                        msg.MediaItems = new List<ChatMediaItem>();
                    }

                    // For backward compatibility: if we have legacy fields but no MediaItems, create them
                    if (msg.MediaItems.Count == 0 && !string.IsNullOrEmpty(msg.MediaPath))
                    {
                        if (msg.IsVoiceMessage)
                        {
                            msg.MediaItems = new List<ChatMediaItem>
                            {
                                ChatMediaItem.CreateAudio(
                                    msg.MediaPath,
                                    msg.VoiceDurationSeconds ?? 5,
                                    msg.VoiceWaveformData)
                            };
                        }
                        else if (msg.MediaType == "image")
                        {
                            msg.MediaItems = new List<ChatMediaItem>
                            {
                                ChatMediaItem.CreateImage(msg.MediaPath)
                            };
                        }

                        // Update the JSON for next time
                        if (msg.MediaItems.Count > 0)
                        {
                            msg.MediaItemsJson = JsonSerializer.Serialize(msg.MediaItems);
                            await SupabaseService.UpdateAsync("ChatMessages", $"Id=eq.{msg.Id}",
                                new { MediaItemsJson = msg.MediaItemsJson });
                        }
                    }
                }

                Debug.WriteLine($"Loaded {messages.Count} messages for conversation {conversationId}");
                return messages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetMessagesAsync: {ex.Message}");
                return new List<ChatMessage>();
            }
        }

        // Update an existing message
        public static async Task UpdateMessageAsync(ChatMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            try
            {
                // Update MediaItemsJson before saving
                if (message.MediaItems != null && message.MediaItems.Count > 0)
                {
                    message.MediaItemsJson = JsonSerializer.Serialize(message.MediaItems);
                }

                await SupabaseService.UpdateAsync("ChatMessages", $"Id=eq.{message.Id}", message);
                await UpdateConversationPreviewAsync(message.ConversationId);
                Debug.WriteLine($"Updated message with ID: {message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateMessageAsync: {ex.Message}");
                throw;
            }
        }

        // Delete a message
        public static async Task DeleteMessageAsync(ChatMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            try
            {
                await SupabaseService.DeleteAsync("ChatMessages", $"Id=eq.{message.Id}");
                await UpdateConversationPreviewAsync(message.ConversationId);
                Debug.WriteLine($"Deleted message with ID: {message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteMessageAsync: {ex.Message}");
                throw;
            }
        }

        // Update conversation preview after message changes
        private static async Task UpdateConversationPreviewAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) return;

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

                    // Create preview based on message type
                    if (lastMsg.IsVoiceMessage)
                    {
                        conversation.LastMessagePreview = "?? Voice message";
                    }
                    else if (lastMsg.MediaItems?.Count > 1)
                    {
                        conversation.LastMessagePreview = $"?? {lastMsg.MediaItems.Count} photos";
                    }
                    else if (!string.IsNullOrEmpty(lastMsg.MediaPath) && lastMsg.MediaType == "image")
                    {
                        conversation.LastMessagePreview = "?? Photo";
                    }
                    else if (lastMsg.MessageType == "gift")
                    {
                        var giftDef = GiftDefinition.FindById(lastMsg.Content ?? "");
                        string name = giftDef?.Name ?? "Gift";
                        conversation.LastMessagePreview = $"?? {name}";
                    }
                    else if (!string.IsNullOrWhiteSpace(lastMsg.Content))
                    {
                        conversation.LastMessagePreview = lastMsg.Content.Length > 120
                            ? lastMsg.Content.Substring(0, 120) + "…"
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
                Debug.WriteLine($"Error in UpdateConversationPreviewAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a single conversation by ID
        /// </summary>
        public static async Task<Conversation?> GetConversationAsync(string conversationId)
        {
            try
            {
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&limit=1");
                return conversations.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetConversationAsync: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Save a conversation (insert or update)
        /// </summary>
        public static async Task<int> SaveConversationAsync(Conversation conversation)
        {
            if (conversation == null) throw new ArgumentNullException(nameof(conversation));

            try
            {
                var existing = await GetConversationAsync(conversation.ConversationId);
                if (existing != null)
                {
                    conversation.Id = existing.Id;
                    var success = await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversation.ConversationId)}", conversation);
                    return success ? conversation.Id : 0;
                }
                else
                {
                    var inserted = await SupabaseService.InsertAndReturnAsync<Conversation>("Conversations", conversation);
                    return inserted?.Id ?? 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SaveConversationAsync: {ex}");
                throw;
            }
        }

        // Get conversations for a user
        public static async Task<List<Conversation>> GetConversationsForUserAsync(string phone)
        {
            try
            {
                phone = (phone ?? "").Trim();
                return await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(ParticipantA.eq.{Uri.EscapeDataString(phone)},ParticipantB.eq.{Uri.EscapeDataString(phone)})&order=LastMessageAt.desc");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetConversationsForUserAsync: {ex.Message}");
                return new List<Conversation>();
            }
        }

        // Mark messages as read
        public static async Task MarkMessagesReadAsync(string conversationId, string readerPhone)
        {
            try
            {
                await SupabaseService.UpdateAsync("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&RecipientPhone=eq.{Uri.EscapeDataString(readerPhone)}&IsRead=eq.false",
                    new { IsRead = true });
                Debug.WriteLine($"Marked messages as read for conversation {conversationId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MarkMessagesReadAsync: {ex.Message}");
            }
        }

        // Accept a message request
        public static async Task AcceptMessageRequestAsync(string conversationId)
        {
            try
            {
                // 1. Update all messages to NOT be message requests
                await SupabaseService.UpdateAsync("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                    new { IsMessageRequest = false });

                // 2. Update the conversation to ensure it's not archived
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&limit=1");
                var conversation = conversations.FirstOrDefault();
                if (conversation != null && conversation.IsArchived)
                {
                    await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                        new { IsArchived = false });
                }

                // 3. Remove from preferences
                var existingJson = Preferences.Get(MessageRequestsKey, "[]");
                var requests = JsonSerializer.Deserialize<List<string>>(existingJson) ?? new List<string>();
                requests.RemoveAll(r => r == conversationId);
                Preferences.Set(MessageRequestsKey, JsonSerializer.Serialize(requests));

                // 4. Update MessageRequest table
                await SupabaseService.UpdateAsync("MessageRequests",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                    new { IsAccepted = true, AcceptedAt = DateTime.UtcNow });

                Debug.WriteLine($"Accepted message request for conversation: {conversationId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accepting message request: {ex}");
                throw;
            }
        }

        // Decline a message request
        public static async Task DeclineMessageRequestAsync(string conversationId)
        {
            try
            {
                var existingJson = Preferences.Get(MessageRequestsKey, "[]");
                var requests = JsonSerializer.Deserialize<List<string>>(existingJson) ?? new List<string>();
                requests.RemoveAll(r => r == conversationId);
                Preferences.Set(MessageRequestsKey, JsonSerializer.Serialize(requests));

                await SupabaseService.UpdateAsync("MessageRequests",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                    new { IsDeclined = true, DeclinedAt = DateTime.UtcNow });

                await SupabaseService.UpdateAsync("ChatMessages",
                    $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                    new { IsMessageRequest = false, IsDeclined = true });

                Debug.WriteLine($"Declined message request for conversation: {conversationId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error declining message request: {ex}");
            }
        }

        // Get message request count for a user
        public static async Task<int> GetMessageRequestCountAsync(string userPhone)
        {
            try
            {
                var messages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    $"RecipientPhone=eq.{Uri.EscapeDataString(userPhone)}&IsMessageRequest=eq.true");
                return messages.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetMessageRequestCountAsync: {ex}");
                return 0;
            }
        }

        // Get all message requests for a user
        public static async Task<List<ChatMessage>> GetMessageRequestsAsync(string userPhone)
        {
            try
            {
                var messages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                    $"RecipientPhone=eq.{Uri.EscapeDataString(userPhone)}&IsMessageRequest=eq.true&order=SentAt.desc");

                // Deserialize MediaItems
                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.MediaItemsJson))
                    {
                        try
                        {
                            msg.MediaItems = JsonSerializer.Deserialize<List<ChatMediaItem>>(msg.MediaItemsJson)
                                ?? new List<ChatMediaItem>();
                        }
                        catch
                        {
                            msg.MediaItems = new List<ChatMediaItem>();
                        }
                    }
                }

                return messages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetMessageRequestsAsync: {ex}");
                return new List<ChatMessage>();
            }
        }

        // Get pending message requests (not accepted or declined)
        public static async Task<List<MessageRequest>> GetPendingMessageRequestsAsync(string userPhone)
        {
            try
            {
                return await SupabaseService.GetAsync<MessageRequest>("MessageRequests",
                    $"RecipientPhone=eq.{Uri.EscapeDataString(userPhone)}&IsAccepted=eq.false&IsDeclined=eq.false&order=RequestedAt.desc");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetPendingMessageRequestsAsync: {ex}");
                return new List<MessageRequest>();
            }
        }


        /// <summary>
        /// Check if the recipient has blocked the sender
        /// </summary>
        public static async Task<bool> IsSenderBlockedByRecipientAsync(string senderPhone, string recipientPhone)
        {
            try
            {
                var blocked = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(recipientPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(senderPhone)}&limit=1");
                return blocked.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IsSenderBlockedByRecipientAsync error: {ex}");
                return false;
            }
        }

        // Block a user
        public static async Task<bool> BlockUserAsync(string currentUserPhone, string userToBlockPhone)
        {
            try
            {
                var existing = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(userToBlockPhone)}&limit=1");

                if (existing.Any())
                    return true;

                var blockedUser = new BlockedUser
                {
                    UserPhone = currentUserPhone,
                    BlockedPhone = userToBlockPhone,
                    BlockedAt = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("BlockedUsers", blockedUser);
                Debug.WriteLine($"User {currentUserPhone} blocked {userToBlockPhone}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error blocking user: {ex}");
                return false;
            }
        }

        // Unblock a user
        public static async Task<bool> UnblockUserAsync(string currentUserPhone, string userToUnblockPhone)
        {
            try
            {
                await SupabaseService.DeleteAsync("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(userToUnblockPhone)}");
                Debug.WriteLine($"User {currentUserPhone} unblocked {userToUnblockPhone}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unblocking user: {ex}");
                return false;
            }
        }

        public static async Task<bool> IsUserBlockedAsync(string currentUserPhone, string otherUserPhone)
        {
            try
            {
                var blocked = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(otherUserPhone)}&limit=1");
                return blocked.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking blocked status: {ex}");
                return false;
            }
        }

        public static async Task<bool> IsRecipientBlockingSenderAsync(string senderPhone, string recipientPhone)
        {
            try
            {
                var isBlocked = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(recipientPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(senderPhone)}&limit=1");
                return isBlocked.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking recipient block status: {ex}");
                return false;
            }
        }

        // Get all blocked users for a user
        public static async Task<List<string>> GetBlockedUsersAsync(string userPhone)
        {
            try
            {
                var blocked = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}");
                return blocked.Select(b => b.BlockedPhone).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting blocked users: {ex}");
                return new List<string>();
            }
        }

        // Check if user can send message (not blocked)
        public static async Task<bool> CanSendMessageAsync(string senderPhone, string recipientPhone)
        {
            try
            {
                var isBlocked = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(recipientPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(senderPhone)}&limit=1");
                return !isBlocked.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking send permission: {ex}");
                return false;
            }
        }

        // Clear all messages in a conversation
        public static async Task ClearConversationMessagesAsync(string conversationId)
        {
            try
            {
                await SupabaseService.DeleteAsync("ChatMessages", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}");

                // Update conversation preview
                await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}",
                    new { LastMessageAt = DateTime.MinValue, LastMessagePreview = string.Empty });

                Debug.WriteLine($"Cleared messages from conversation {conversationId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing conversation messages: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Save a conversation without triggering any notifications
        /// </summary>
        public static async Task<int> SaveConversationSilentlyAsync(Conversation conversation)
        {
            return await SaveConversationAsync(conversation);
        }
    }
}