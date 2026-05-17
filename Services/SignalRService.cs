using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Services
{
    public class SignalRService : IDisposable
    {
        private HubConnection _hubConnection;
        private readonly string _baseUrl = "https://your-api-server.com"; // Replace with your server URL
        private bool _isConnected = false;
        private string _currentUserPhone;

        // Events for real-time updates
        public event Action<SparkUpdateMessage> SparkReceived;
        public event Action<LoveUpdateMessage> LoveReceived;
        public event Action<CommentUpdateMessage> CommentReceived;
        public event Action<PostUpdateMessage> PostReceived;
        public event Action<NotificationUpdateMessage> NotificationReceived;
        public event Action<ChatMessageUpdate> ChatMessageReceived;
        public event Action<UserStatusUpdate> UserStatusChanged;

        private static SignalRService _instance;
        public static SignalRService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SignalRService();
                return _instance;
            }
        }

        private SignalRService() { }

        public async Task StartAsync(string userPhone)
        {
            if (string.IsNullOrEmpty(userPhone))
                return;

            _currentUserPhone = userPhone;

            if (_hubConnection != null)
            {
                await StopAsync();
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/lockHub")
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            // Register event handlers
            RegisterEventHandlers();

            try
            {
                await _hubConnection.StartAsync();
                _isConnected = true;
                Debug.WriteLine($"SignalR connected for user: {userPhone}");

                // Register user with the hub
                await _hubConnection.InvokeAsync("RegisterUser", userPhone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignalR connection error: {ex.Message}");
                _isConnected = false;
            }
        }

        private void RegisterEventHandlers()
        {
            if (_hubConnection == null) return;

            // Spark events
            _hubConnection.On<SparkUpdateMessage>("SparkToggled", (message) =>
            {
                Debug.WriteLine($"SignalR: Spark received for post {message.PostId}");
                SparkReceived?.Invoke(message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(message, "SparkToggled");
                });
            });

            // Love events
            _hubConnection.On<LoveUpdateMessage>("LoveToggled", (message) =>
            {
                Debug.WriteLine($"SignalR: Love received for post {message.PostId}");
                LoveReceived?.Invoke(message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(message, "PostLoveChanged");
                });
            });

            // Comment events
            _hubConnection.On<CommentUpdateMessage>("CommentAdded", (message) =>
            {
                Debug.WriteLine($"SignalR: Comment added to post {message.PostId}");
                CommentReceived?.Invoke(message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(message, "CommentAdded");
                });
            });

            // Post events
            _hubConnection.On<PostUpdateMessage>("PostCreated", (message) =>
            {
                Debug.WriteLine($"SignalR: New post created by {message.AuthorPhone}");
                PostReceived?.Invoke(message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(message, "PostCreated");
                });
            });

            // Notification events
            _hubConnection.On<NotificationUpdateMessage>("NotificationReceived", (message) =>
            {
                Debug.WriteLine($"SignalR: Notification received");
                NotificationReceived?.Invoke(message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(message, "NewNotification");
                });
            });

            // Chat message events
            _hubConnection.On<ChatMessageUpdate>("NewChatMessage", (message) =>
            {
                Debug.WriteLine($"SignalR: New chat message from {message.SenderPhone}");
                ChatMessageReceived?.Invoke(message);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(message, "NewMessage");
                });
            });

            // User status events
            _hubConnection.On<UserStatusUpdate>("UserStatusChanged", (status) =>
            {
                Debug.WriteLine($"SignalR: User {status.UserPhone} status changed to {status.IsOnline}");
                UserStatusChanged?.Invoke(status);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagingCenter.Send(status, "UserStatusChanged");
                });
            });
        }

        // Send spark update
        public async Task SendSparkUpdateAsync(SparkUpdateMessage message)
        {
            if (!_isConnected || _hubConnection == null) return;
            try
            {
                await _hubConnection.InvokeAsync("ToggleSpark", message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendSparkUpdateAsync error: {ex.Message}");
            }
        }

        // Send love update
        public async Task SendLoveUpdateAsync(LoveUpdateMessage message)
        {
            if (!_isConnected || _hubConnection == null) return;
            try
            {
                await _hubConnection.InvokeAsync("ToggleLove", message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendLoveUpdateAsync error: {ex.Message}");
            }
        }

        // Send comment update
        public async Task SendCommentUpdateAsync(CommentUpdateMessage message)
        {
            if (!_isConnected || _hubConnection == null) return;
            try
            {
                await _hubConnection.InvokeAsync("AddComment", message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendCommentUpdateAsync error: {ex.Message}");
            }
        }

        // Update user status
        public async Task UpdateUserStatusAsync(bool isOnline)
        {
            if (!_isConnected || _hubConnection == null) return;
            try
            {
                await _hubConnection.InvokeAsync("UpdateUserStatus", _currentUserPhone, isOnline);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateUserStatusAsync error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (_hubConnection != null)
            {
                try
                {
                    if (_isConnected)
                    {
                        await UpdateUserStatusAsync(false);
                        await _hubConnection.InvokeAsync("UnregisterUser", _currentUserPhone);
                    }
                    await _hubConnection.StopAsync();
                    await _hubConnection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SignalR stop error: {ex.Message}");
                }
                finally
                {
                    _hubConnection = null;
                    _isConnected = false;
                }
            }
        }

        public bool IsConnected => _isConnected;

        public void Dispose()
        {
            _ = StopAsync();
        }
    }

    // Message models
    public class SparkUpdateMessage
    {
        public int PostId { get; set; }
        public bool IsSparked { get; set; }
        public int SparkCount { get; set; }
        public string UserPhone { get; set; }
        public string AuthorPhone { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class LoveUpdateMessage
    {
        public int PostId { get; set; }
        public bool IsLoved { get; set; }
        public int LoveCount { get; set; }
        public string UserPhone { get; set; }
        public string AuthorPhone { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class CommentUpdateMessage
    {
        public int PostId { get; set; }
        public int CommentCount { get; set; }
        public string UserPhone { get; set; }
        public string AuthorPhone { get; set; }
        public string CommentContent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class PostUpdateMessage
    {
        public int PostId { get; set; }
        public string AuthorPhone { get; set; }
        public string AuthorName { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class NotificationUpdateMessage
    {
        public string UserPhone { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public int? PostId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ChatMessageUpdate
    {
        public string ConversationId { get; set; }
        public string SenderPhone { get; set; }
        public string RecipientPhone { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }

    public class UserStatusUpdate
    {
        public string UserPhone { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }
}