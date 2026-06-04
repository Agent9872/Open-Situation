using Lock.Models;
using Lock.Models.Chat;
using Microsoft.Maui.Storage;
using SQLite;
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var conversations = await db.Table<Conversation>()
                    .Where(c => (c.ParticipantA == userPhone && c.ParticipantB == otherPhone) ||
                               (c.ParticipantA == otherPhone && c.ParticipantB == userPhone))
                    .FirstOrDefaultAsync();

                if (conversations == null)
                    return false;

                var messageCount = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversations.ConversationId && m.IsMessageRequest == false)
                    .CountAsync();

                return messageCount > 0;
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(request);

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
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            phoneA = (phoneA ?? "").Trim();
            phoneB = (phoneB ?? "").Trim();

            var conv = await db.Table<Conversation>()
                .Where(c =>
                    (c.ParticipantA == phoneA && c.ParticipantB == phoneB) ||
                    (c.ParticipantA == phoneB && c.ParticipantB == phoneA))
                .FirstOrDefaultAsync();

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

            await db.InsertAsync(conv);
            return await db.GetAsync<Conversation>(conv.Id) ?? conv;
        }

        // Add a message (with message request handling)
        public static async Task AddMessageAsync(ChatMessage message, bool isMultiImageMessage = false)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(message.ConversationId))
                throw new InvalidOperationException("Message must have a ConversationId");

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

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
                    await db.InsertAsync(message);
                    return;
                }

                // Check if this is a message request (first message from non-contact)
                bool isMessageRequest = false;

                var existingMessages = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == message.ConversationId && !m.IsBlocked)
                    .CountAsync();

                if (existingMessages == 0)
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
                await db.InsertAsync(message);
                Debug.WriteLine($"Saved message ID: {message.Id}");

                await UpdateConversationPreviewAsync(db, message.ConversationId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddMessageAsync: {ex.Message}");
                throw;
            }
        }

        public static async Task<List<Conversation>> GetAllConversationsAsync(string userPhone)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            return await db.Table<Conversation>()
                .Where(c => c.ParticipantA == userPhone || c.ParticipantB == userPhone)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();
        }

        // Get messages for a conversation
        public static async Task<List<ChatMessage>> GetMessagesAsync(string conversationId, int max = 200)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            try
            {
                var messages = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId && !m.IsBlocked)
                    .OrderBy(m => m.SentAt)
                    .Take(max)
                    .ToListAsync();

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
                            await db.UpdateAsync(msg);
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

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            try
            {
                // Update MediaItemsJson before saving
                if (message.MediaItems != null && message.MediaItems.Count > 0)
                {
                    message.MediaItemsJson = JsonSerializer.Serialize(message.MediaItems);
                }

                await db.UpdateAsync(message);
                await UpdateConversationPreviewAsync(db, message.ConversationId);
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

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            try
            {
                await db.DeleteAsync(message);
                await UpdateConversationPreviewAsync(db, message.ConversationId);
                Debug.WriteLine($"Deleted message with ID: {message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteMessageAsync: {ex.Message}");
                throw;
            }
        }

        // Update conversation preview after message changes
        private static async Task UpdateConversationPreviewAsync(SQLiteAsyncConnection db, string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) return;

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

                await db.UpdateAsync(conversation);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversationId)
                    .FirstOrDefaultAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var existing = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversation.ConversationId)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    conversation.Id = existing.Id;
                    return await db.UpdateAsync(conversation);
                }
                else
                {
                    return await db.InsertAsync(conversation);
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
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            try
            {
                phone = (phone ?? "").Trim();

                return await db.Table<Conversation>()
                    .Where(c => c.ParticipantA == phone || c.ParticipantB == phone)
                    .OrderByDescending(c => c.LastMessageAt)
                    .ToListAsync();
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
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            try
            {
                var toMark = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId &&
                                m.RecipientPhone == readerPhone &&
                                !m.IsRead)
                    .ToListAsync();

                foreach (var m in toMark)
                {
                    m.IsRead = true;
                    await db.UpdateAsync(m);
                }

                Debug.WriteLine($"Marked {toMark.Count} messages as read");
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // 1. Update all messages to NOT be message requests
                var messages = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId)
                    .ToListAsync();

                foreach (var msg in messages)
                {
                    msg.IsMessageRequest = false;
                    await db.UpdateAsync(msg);
                }

                // 2. CRITICAL: Update the conversation itself
                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversationId)
                    .FirstOrDefaultAsync();

                if (conversation != null)
                {
                    // Make sure it's not archived
                    if (conversation.IsArchived)
                    {
                        conversation.IsArchived = false;
                        await db.UpdateAsync(conversation);
                    }
                }

                // 3. Remove from preferences
                var existingJson = Preferences.Get(MessageRequestsKey, "[]");
                var requests = JsonSerializer.Deserialize<List<string>>(existingJson) ?? new List<string>();
                requests.RemoveAll(r => r == conversationId);
                Preferences.Set(MessageRequestsKey, JsonSerializer.Serialize(requests));

                // 4. Update MessageRequest table
                var request = await db.Table<MessageRequest>()
                    .Where(r => r.ConversationId == conversationId)
                    .FirstOrDefaultAsync();

                if (request != null)
                {
                    request.IsAccepted = true;
                    request.AcceptedAt = DateTime.UtcNow;
                    await db.UpdateAsync(request);
                }

                Debug.WriteLine($"Accepted message request for conversation: {conversationId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accepting message request: {ex}");
                throw; // THROW the exception so the UI knows it failed
            }
        }
        // Decline a message request
        public static async Task DeclineMessageRequestAsync(string conversationId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var existingJson = Preferences.Get(MessageRequestsKey, "[]");
                var requests = JsonSerializer.Deserialize<List<string>>(existingJson) ?? new List<string>();
                requests.RemoveAll(r => r == conversationId);
                Preferences.Set(MessageRequestsKey, JsonSerializer.Serialize(requests));

                var request = await db.Table<MessageRequest>()
                    .Where(r => r.ConversationId == conversationId)
                    .FirstOrDefaultAsync();

                if (request != null)
                {
                    request.IsDeclined = true;
                    request.DeclinedAt = DateTime.UtcNow;
                    await db.UpdateAsync(request);
                }

                var messages = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId)
                    .ToListAsync();

                foreach (var msg in messages)
                {
                    msg.IsMessageRequest = false;
                    msg.IsDeclined = true;
                    await db.UpdateAsync(msg);
                }

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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<ChatMessage>()
                    .Where(m => m.RecipientPhone == userPhone && m.IsMessageRequest == true)
                    .CountAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var messages = await db.Table<ChatMessage>()
                    .Where(m => m.RecipientPhone == userPhone && m.IsMessageRequest == true)
                    .OrderByDescending(m => m.SentAt)
                    .ToListAsync();

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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<MessageRequest>()
                    .Where(r => r.RecipientPhone == userPhone &&
                               !r.IsAccepted &&
                               !r.IsDeclined)  // Explicitly check both flags
                    .OrderByDescending(r => r.RequestedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetPendingMessageRequestsAsync: {ex}");
                return new List<MessageRequest>();
            }
        }
        // Block a user
        public static async Task<bool> BlockUserAsync(string currentUserPhone, string userToBlockPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var existing = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == currentUserPhone && b.BlockedPhone == userToBlockPhone)
                    .FirstOrDefaultAsync();

                if (existing != null)
                    return true;

                var blockedUser = new BlockedUser
                {
                    UserPhone = currentUserPhone,
                    BlockedPhone = userToBlockPhone,
                    BlockedAt = DateTime.UtcNow
                };

                await db.InsertAsync(blockedUser);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var blocked = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == currentUserPhone && b.BlockedPhone == userToUnblockPhone)
                    .FirstOrDefaultAsync();

                if (blocked != null)
                {
                    await db.DeleteAsync(blocked);
                    Debug.WriteLine($"User {currentUserPhone} unblocked {userToUnblockPhone}");
                }

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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var blocked = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == currentUserPhone && b.BlockedPhone == otherUserPhone)
                    .FirstOrDefaultAsync();

                return blocked != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking blocked status: {ex}");
                return false;
            }
        }

        // ADD THIS MISSING METHOD
        public static async Task<bool> IsSenderBlockedByRecipientAsync(string senderPhone, string recipientPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var isBlocked = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == recipientPhone && b.BlockedPhone == senderPhone)
                    .FirstOrDefaultAsync();

                return isBlocked != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if sender is blocked by recipient: {ex}");
                return false;
            }
        }

        public static async Task<bool> IsRecipientBlockingSenderAsync(string senderPhone, string recipientPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var isBlocked = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == recipientPhone && b.BlockedPhone == senderPhone)
                    .FirstOrDefaultAsync();

                return isBlocked != null;
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var blocked = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == userPhone)
                    .ToListAsync();

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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var isBlocked = await db.Table<BlockedUser>()
                    .Where(b => b.UserPhone == recipientPhone && b.BlockedPhone == senderPhone)
                    .FirstOrDefaultAsync();

                return isBlocked == null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking send permission: {ex}");
                return false;
            }
        }

        // Add this method to ChatRepository.cs
        public static async Task AddMediaItemsJsonColumnAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if column exists by trying to query it
                try
                {
                    // This will throw if column doesn't exist
                    await db.QueryAsync<ChatMessage>("SELECT MediaItemsJson FROM ChatMessage LIMIT 1");
                    Debug.WriteLine("MediaItemsJson column already exists");
                    return;
                }
                catch
                {
                    // Column doesn't exist, add it
                    Debug.WriteLine("Adding MediaItemsJson column to ChatMessage table");
                    await db.ExecuteAsync("ALTER TABLE ChatMessage ADD COLUMN MediaItemsJson TEXT DEFAULT '[]'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding MediaItemsJson column: {ex}");
            }
        }

        // Add this method to ChatRepository.cs
        public static async Task ClearConversationMessagesAsync(string conversationId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var messages = await db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId)
                    .ToListAsync();

                foreach (var message in messages)
                {
                    await db.DeleteAsync(message);
                }

                // Update conversation preview
                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversationId)
                    .FirstOrDefaultAsync();

                if (conversation != null)
                {
                    conversation.LastMessageAt = DateTime.MinValue;
                    conversation.LastMessagePreview = string.Empty;
                    await db.UpdateAsync(conversation);
                }

                Debug.WriteLine($"Cleared {messages.Count} messages from conversation {conversationId}");
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
            if (conversation == null) throw new ArgumentNullException(nameof(conversation));

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var existing = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == conversation.ConversationId)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    conversation.Id = existing.Id;
                    return await db.UpdateAsync(conversation);
                }
                else
                {
                    return await db.InsertAsync(conversation);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SaveConversationSilentlyAsync: {ex}");
                throw;
            }
        }
    }
}