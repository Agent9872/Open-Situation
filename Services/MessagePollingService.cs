using Lock.Chat.Services;
using Lock.Models.Chat;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lock.Services
{
    public interface IMessagePollingService
    {
        void StartPolling(string userPhone);
        void StopPolling();
        event Func<ChatMessage, Task> MessageReceived;
    }

    public class MessagePollingService : IMessagePollingService, IAsyncDisposable
    {
        private HubConnection _hubConnection;
        private string _currentUserPhone;
        private DateTime _lastChecked = DateTime.UtcNow;
        private HashSet<int> _processedMessageIds = new HashSet<int>();
        private ISystemNotificationService _systemNotificationService;
        private bool _isConnected = false;
        private readonly HttpClient _httpClient;
        private System.Timers.Timer _pollingTimer;

        public event Func<ChatMessage, Task> MessageReceived;

        public MessagePollingService(ISystemNotificationService systemNotificationService)
        {
            _systemNotificationService = systemNotificationService;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.BaseAddress = new Uri(ApiConfig.BaseUrl);
        }

        public void StartPolling(string userPhone)
        {
            _currentUserPhone = userPhone;
            _lastChecked = DateTime.UtcNow;
            _processedMessageIds.Clear();

            // Start SignalR connection for real-time messages
            Task.Run(async () => await InitializeSignalRConnection(userPhone));

            // Also start polling as fallback (every 10 seconds)
            _pollingTimer = new System.Timers.Timer(10000);
            _pollingTimer.Elapsed += async (sender, e) => await CheckForNewMessages();
            _pollingTimer.AutoReset = true;
            _pollingTimer.Start();

            Debug.WriteLine($"Message polling started for user: {userPhone}");
            Debug.WriteLine($"API Base URL: {ApiConfig.BaseUrl}");
            Debug.WriteLine($"SignalR Hub URL: {ApiConfig.HubUrl}");
        }

        private async Task InitializeSignalRConnection(string userPhone)
        {
            try
            {
                Debug.WriteLine($"Connecting to SignalR hub at: {ApiConfig.HubUrl}");

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(ApiConfig.HubUrl, options =>
                    {
                        options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                        options.SkipNegotiation = true; // Force WebSockets only
                        options.HttpMessageHandlerFactory = handler =>
                        {
                            if (handler is HttpClientHandler clientHandler)
                            {
                                clientHandler.ServerCertificateCustomValidationCallback +=
                                    (sender, certificate, chain, sslPolicyErrors) => true;
                            }
                            return handler;
                        };
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                    .Build();

                // Handle connection events
                _hubConnection.Reconnecting += (error) =>
                {
                    Debug.WriteLine($"SignalR reconnecting: {error?.Message ?? "Unknown error"}");
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += (connectionId) =>
                {
                    Debug.WriteLine($"SignalR reconnected: {connectionId}");
                    _isConnected = true;
                    // Re-register user identifier after reconnect
                    Task.Run(async () => await _hubConnection.SendAsync("SetUserIdentifier", userPhone));
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += (error) =>
                {
                    Debug.WriteLine($"SignalR closed: {error?.Message ?? "Connection closed"}");
                    _isConnected = false;
                    // Attempt to reconnect after 5 seconds
                    Task.Delay(5000).ContinueWith(_ => InitializeSignalRConnection(userPhone));
                    return Task.CompletedTask;
                };

                // Register message handlers
                _hubConnection.On<object>("ReceiveMessage", async (messageData) =>
                {
                    await HandleReceivedMessage(messageData);
                });

                _hubConnection.On<object>("MessageSent", async (messageData) =>
                {
                    Debug.WriteLine($"Message sent confirmation received");
                });

                _hubConnection.On<string, string, bool>("UserTyping", async (conversationId, senderPhone, isTyping) =>
                {
                    Debug.WriteLine($"User {senderPhone} is {(isTyping ? "typing" : "stopped typing")} in conversation {conversationId}");
                    // You can raise an event here for UI updates
                    MessagingCenter.Send(this, "UserTyping", new { ConversationId = conversationId, UserPhone = senderPhone, IsTyping = isTyping });
                });

                _hubConnection.On<string>("UserOnline", async (userPhone) =>
                {
                    Debug.WriteLine($"User {userPhone} came online");
                    MessagingCenter.Send(this, "UserOnline", userPhone);
                });

                _hubConnection.On<string>("UserOffline", async (userPhone) =>
                {
                    Debug.WriteLine($"User {userPhone} went offline");
                    MessagingCenter.Send(this, "UserOffline", userPhone);
                });

                // Start the connection
                await _hubConnection.StartAsync();
                _isConnected = true;

                // Set user identifier for the connection
                await _hubConnection.SendAsync("SetUserIdentifier", userPhone);

                Debug.WriteLine($"✅ SignalR connected successfully for user: {userPhone}");
                Debug.WriteLine($"   Connection ID: {_hubConnection.ConnectionId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ SignalR connection failed: {ex.Message}");
                Debug.WriteLine($"   Stack trace: {ex.StackTrace}");
                _isConnected = false;
                // Retry connection after 10 seconds
                Task.Delay(10000).ContinueWith(_ => InitializeSignalRConnection(userPhone));
            }
        }

        private async Task HandleReceivedMessage(object messageData)
        {
            try
            {
                // Parse the message from SignalR
                var messageJson = System.Text.Json.JsonSerializer.Serialize(messageData);
                var message = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(messageJson);

                if (message == null) return;

                Debug.WriteLine($"📨 Real-time message received from {message.SenderPhone}");
                Debug.WriteLine($"   Content: {message.Content ?? "[Media message]"}");
                Debug.WriteLine($"   Conversation: {message.ConversationId}");

                // Process the message
                await ProcessNewMessage(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling real-time message: {ex}");
            }
        }

        private async Task ProcessNewMessage(ChatMessage message)
        {
            try
            {
                // Check if already processed
                if (_processedMessageIds.Contains(message.Id))
                {
                    Debug.WriteLine($"Message {message.Id} already processed, skipping");
                    return;
                }

                _processedMessageIds.Add(message.Id);

                // Save to local database
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var existingMessage = await db.Table<ChatMessage>()
                    .Where(m => m.Id == message.Id)
                    .FirstOrDefaultAsync();

                if (existingMessage == null)
                {
                    await db.InsertAsync(message);
                    Debug.WriteLine($"Message {message.Id} saved to local database");
                }
                else
                {
                    Debug.WriteLine($"Message {message.Id} already exists in database");
                }

                // Get sender name and avatar
                var senderName = await GetUserDisplayName(message.SenderPhone);
                var senderAvatar = await GetUserAvatarPath(message.SenderPhone);

                // Get unread count for badge
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var unreadCount = await db.Table<ChatMessage>()
                    .Where(m => m.RecipientPhone == currentUserPhone && !m.IsRead)
                    .CountAsync();

                Debug.WriteLine($"Unread count: {unreadCount}");

                // Show system notification (pass avatar path)
                _systemNotificationService?.ShowNewMessageNotification(message, senderName, unreadCount, senderAvatar);

                // Fire the in-app popup event
                if (MessageReceived != null)
                {
                    await MessageReceived.Invoke(message);
                }

                // Send read receipt if needed (optional)
                if (_isConnected && _hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                {
                    try
                    {
                        await _hubConnection.SendAsync("MarkMessageAsRead", message.Id, message.ConversationId);
                        Debug.WriteLine($"Sent read receipt for message {message.Id}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to send read receipt: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message: {ex}");
            }
        }

        public void StopPolling()
        {
            if (_pollingTimer != null)
            {
                _pollingTimer.Stop();
                _pollingTimer.Dispose();
                _pollingTimer = null;
            }

            if (_hubConnection != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _hubConnection.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disposing hub connection: {ex.Message}");
                    }
                });
                _hubConnection = null;
            }

            _isConnected = false;
            Debug.WriteLine("Message polling stopped");
        }

        private async Task CheckForNewMessages()
        {
            try
            {
                // If SignalR is connected, we don't need to poll
                if (_isConnected) return;

                Debug.WriteLine("Polling for new messages (fallback mode)");

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var unreadMessages = await db.Table<ChatMessage>()
                    .Where(m => m.RecipientPhone == currentUserPhone
                                && !m.IsRead
                                && m.SenderPhone != currentUserPhone)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                var newMessages = unreadMessages
                    .Where(m => m.SentAt > _lastChecked && !_processedMessageIds.Contains(m.Id))
                    .ToList();

                if (newMessages.Any())
                {
                    Debug.WriteLine($"🔍 Found {newMessages.Count} new message(s) via polling fallback");

                    foreach (var message in newMessages)
                    {
                        await ProcessNewMessage(message);
                    }

                    _lastChecked = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Polling error: {ex}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user display name: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user avatar path: {ex.Message}");
                return string.Empty;
            }
        }

        // Method to send a message via SignalR
        public async Task<bool> SendMessageViaSignalR(SendMessageRequest request)
        {
            try
            {
                if (_isConnected && _hubConnection?.State == HubConnectionState.Connected)
                {
                    await _hubConnection.SendAsync("SendMessage", request);
                    Debug.WriteLine($"Message sent via SignalR to {request.RecipientPhone}");
                    return true;
                }
                else
                {
                    Debug.WriteLine("SignalR not connected, using HTTP fallback");
                    // Fallback to HTTP API
                    var response = await _httpClient.PostAsJsonAsync(ApiConfig.Endpoints.SendMessage, request);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending message: {ex.Message}");
                return false;
            }
        }

        // Method to send typing indicator
        public async Task SendTypingIndicator(string conversationId, string recipientPhone, bool isTyping)
        {
            try
            {
                if (_isConnected && _hubConnection?.State == HubConnectionState.Connected)
                {
                    await _hubConnection.SendAsync("TypingIndicator", conversationId, recipientPhone, isTyping);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending typing indicator: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            StopPolling();
            _httpClient?.Dispose();
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }
    }

    // Add this class if you don't have it
    public class SendMessageRequest
    {
        public string ConversationId { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? MediaPath { get; set; }
        public string? MediaType { get; set; }
        public bool IsVoiceMessage { get; set; }
        public int? VoiceDurationSeconds { get; set; }
        public string? VoiceWaveformData { get; set; }
        public string? MediaItemsJson { get; set; }
        public string? MessageType { get; set; }
    }
}