using Microsoft.AspNetCore.SignalR.Client;
using Lock.Models.Chat;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Services
{
    public interface IMessageHubService
    {
        Task StartAsync(string userPhone);
        Task StopAsync();
        Task SendMessageAsync(ChatMessage message);
        event Func<ChatMessage, Task> MessageReceived;
        bool IsConnected { get; }
    }

    public class MessageHubService : IMessageHubService
    {
        private HubConnection _hubConnection;
        private string _currentUserPhone;

        public event Func<ChatMessage, Task> MessageReceived;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public async Task StartAsync(string userPhone)
        {
            _currentUserPhone = userPhone;

            // USE YOUR API'S PORT - 7104 from your screenshot
            var hubUrl = "https://localhost:7104/messageHub";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.Headers.Add("X-User-Phone", userPhone);
                })
                .WithAutomaticReconnect()
                .Build();

            // Listen for incoming messages
            _hubConnection.On<ChatMessage>("ReceiveMessage", async (message) =>
            {
                Debug.WriteLine($"Message received from {message.SenderPhone}");
                await MessageReceived?.Invoke(message);
            });

            try
            {
                await _hubConnection.StartAsync();
                Debug.WriteLine($"Connected to SignalR as {userPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }

        public async Task SendMessageAsync(ChatMessage message)
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendMessage", new
                {
                    message.ConversationId,
                    message.RecipientPhone,
                    message.Content,
                    message.MediaPath,
                    message.MediaType,
                    message.IsVoiceMessage,
                    message.VoiceDurationSeconds,
                    message.VoiceWaveformData,
                    message.MediaItemsJson,
                    message.MessageType
                });
            }
        }
    }
}