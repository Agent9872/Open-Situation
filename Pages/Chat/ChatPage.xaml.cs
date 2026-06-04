using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Models.Chat;
using Lock.Services.Chat;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using MauiGrid = Microsoft.Maui.Controls.Grid;
using NAudio.Wave;
using System.ComponentModel;
using Lock.Pages.Post;
using Lock.Pages.Profile;
using Lock.Models;
using Lock.Pages.Chat.Popups;

namespace Lock.Pages.Chat
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextMessageTemplate { get; set; }
        public DataTemplate ImageMessageTemplate { get; set; }
        public DataTemplate VoiceMessageTemplate { get; set; }
        public DataTemplate ContactMessageTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            var message = item as ChatMessage;

            if (message?.MessageType == "contact")
                return ContactMessageTemplate;
            else if (message?.IsVoice == true)
                return VoiceMessageTemplate;
            else if (message?.IsImage == true)
                return ImageMessageTemplate;
            else
                return TextMessageTemplate;
        }
    }

    public partial class ChatPage : ContentPage, IQueryAttributable
    {
        private const string CurrentUserPhoneKey = "current_user_phone";

        private string _conversationId = string.Empty;
        private string _otherPhone = string.Empty;
        private string _me = string.Empty;

        public ObservableCollection
        <ChatMessage> Messages
        { get; } = new();

        // currently selected message for overlay actions
        private ChatMessage? _overlayMessage;
        private bool _overlayBusy;

        // edit state - enhanced for images
        private bool _isEditing;
        private ChatMessage? _editingMessage;
        private List
        <string> _editingImagePaths = new(); // For editing existing images
        private List
        <string> _newImagePaths = new(); // For adding new images during edit

        // pinned preview cycling
        private int _pinnedIndex = 0;

        // pulse feedback
        private CancellationTokenSource? _pulseCts;

        // Folder for storing chat images (created on first use)
        private readonly string _imagesFolder = Path.Combine(FileSystem.AppDataDirectory, "chat_images");

        // Pending images for new message
        private List
            <string> _pendingImagePaths = new();

        // Scroll position tracking - changed from string to int to match ChatMessage.Id type
        // Change this line (around line 40-45)
        private int _lastVisibleMessageId = -1;

        private string _chatBackgroundPath = string.Empty;
        private double _backgroundBrightness = 0.6;

        // Add these fields to your existing fields
        private IAudioManager _audioManager;
        private IAudioRecorder _audioRecorder;
        private string _currentRecordingPath;
        private bool _isRecording;
        private readonly string _voiceFolder = Path.Combine(FileSystem.AppDataDirectory, "LockChat", "voice_messages");

        // Add these fields with your other field declarations
        private Dictionary<int, IAudioPlayer> _activePlayers = new(); // Track active audio players by message Id
        private ChatMessage _currentlyPlayingMessage = null;
        private IAudioPlayer? _currentPlayer;
        private Stream? _currentAudioStream;

        // Add these fields with your other field declarations
        private string _tempRecordingPath = string.Empty;
        private IAudioPlayer? _previewPlayer;
        private bool _isPreviewPlaying;
        private double _waveformContainerWidth = 200.0;

        private bool _isInitializing = false;
        private bool _isFirstLoad = true;
        private bool _migrationDone = false;

        // Add this field to your ChatPage fields
        private int? _scrollToMessageId = null;
        private bool _hasUnlockedForCurrentSession = false;
        private CancellationTokenSource _chatLoaderCts = new();

        // ?? Chat Page Loading Overlay Animations ??????????????????????

        private void StartChatLoadingAnimations()
        {
            _chatLoaderCts = new CancellationTokenSource();
            var token = _chatLoaderCts.Token;
            _ = ChatSpinRingAsync(token);
            _ = ChatHeartPulseAsync(token);
            _ = ChatDotWaveAsync(token);
        }

        private void StopChatLoadingAnimations()
        {
            _chatLoaderCts.Cancel();
            ChatSpinRing.Rotation = 0;
            ChatHeartIcon.ScaleTo(1, 80);
        }

        private async Task ChatSpinRingAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await ChatSpinRing.RotateTo(360, 2000, Easing.Linear);
                ChatSpinRing.Rotation = 0;
            }
        }

        private async Task ChatHeartPulseAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await ChatHeartIcon.ScaleTo(1.22, 200, Easing.CubicOut);
                await ChatHeartIcon.ScaleTo(0.95, 120, Easing.CubicIn);
                await ChatHeartIcon.ScaleTo(1.00, 120, Easing.CubicOut);
                await Task.Delay(900, token).ContinueWith(_ => { });
            }
        }

        private readonly Color _chatDotActive = Color.FromArgb("#FF3B6F");
        private readonly Color _chatDotInactive = Color.FromArgb("#3A3A4C");

        private async Task ChatDotWaveAsync(CancellationToken token)
        {
            var dots = new[] { ChatDot1, ChatDot2, ChatDot3, ChatDot4, ChatDot5 };
            int i = 0;
            while (!token.IsCancellationRequested)
            {
                dots[i].Fill = new SolidColorBrush(_chatDotActive);
                await dots[i].ScaleYTo(1.6, 120, Easing.CubicOut);
                await dots[i].ScaleYTo(1.0, 120, Easing.CubicIn);
                dots[i].Fill = new SolidColorBrush(_chatDotInactive);
                i = (i + 1) % dots.Length;
                await Task.Delay(160, token).ContinueWith(_ => { });
            }
        }

        // Add this method to ChatPage
        private async Task ScrollToSpecificMessageAsync()
        {
            if (_scrollToMessageId.HasValue && Messages.Any())
            {
                var targetMessage = Messages.FirstOrDefault(m => m.Id == _scrollToMessageId.Value);
                if (targetMessage != null)
                {
                    // Small delay to ensure the CollectionView is rendered
                    await Task.Delay(100);

                    var cv = this.FindByName<CollectionView>("MessagesCollectionView");
                    if (cv != null)
                    {
                        cv.ScrollTo(targetMessage, position: ScrollToPosition.Center, animate: true);

                        // Highlight the message briefly
                        await HighlightMessageAsync(targetMessage);
                    }
                }
                _scrollToMessageId = null;
            }
        }

        private async Task HighlightMessageAsync(ChatMessage message)
        {
            try
            {
                // Find the message's visual element (you may need to find a way to highlight it)
                // For now, we'll just show a toast or alert
                await Task.Delay(500);
                // You could add a temporary border or background color to the message bubble
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HighlightMessageAsync error: {ex}");
            }
        }

        // Update your ApplyQueryAttributes method to capture the messageId
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query == null) return;

            if (query.TryGetValue("conversationId", out var convObj) && convObj is string convStr)
            {
                _conversationId = Uri.UnescapeDataString(convStr);
            }

            if (query.TryGetValue("other", out var otherObj) && otherObj is string otherStr)
            {
                _otherPhone = Uri.UnescapeDataString(otherStr);
            }
            else if (query.TryGetValue("otherPhone", out var otherPhoneObj) && otherPhoneObj is string otherPhoneStr)
            {
                _otherPhone = Uri.UnescapeDataString(otherPhoneStr);
            }

            // Add this to capture message ID for scrolling
            if (query.TryGetValue("messageId", out var msgIdObj) && msgIdObj is string msgIdStr)
            {
                if (int.TryParse(msgIdStr, out int msgId))
                {
                    _scrollToMessageId = msgId;
                }
            }
        }
        public ChatPage()
        {
            EnsureInitializeComponent();
            BindingContext = this;

            Shell.SetNavBarIsVisible(this, false);

            Task.Run(EnsureVoiceFolderExistsAsync);

            if (!Directory.Exists(_imagesFolder))
                Directory.CreateDirectory(_imagesFolder);

            if (!Directory.Exists(_voiceFolder))
                Directory.CreateDirectory(_voiceFolder);

            var pinnedCv = this.FindByName<CollectionView>("PinnedMessagesCollectionView");
            if (pinnedCv != null)
                pinnedCv.SelectionChanged += PinnedMessagesCollectionView_SelectionChanged;

            // CHANGED: Editor instead of Entry
            var messageEntry = this.FindByName<Editor>("MessageEntry");
            if (messageEntry != null)
            {
                messageEntry.TextChanged += MessageEntry_TextChanged;
            }
        }
        private void MessageEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            Debug.WriteLine($"Text changed: '{e.NewTextValue}', HasText: {!string.IsNullOrWhiteSpace(e.NewTextValue)}");
            var micIcon = this.FindByName<ContentView>("MicIcon");
            var giftButton = this.FindByName<ContentView>("GiftButton");
            var sendGrid = this.FindByName<Grid>("SendActionGrid");
            var messageEntry = this.FindByName<Editor>("MessageEntry");

            bool hasText = !string.IsNullOrWhiteSpace(messageEntry?.Text);
            bool isRecording = _isRecording;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isRecording)
                {
                    if (micIcon != null) micIcon.IsVisible = false;
                    if (giftButton != null) giftButton.IsVisible = false;
                    if (sendGrid != null) sendGrid.IsVisible = false;
                }
                else if (hasText)
                {
                    if (micIcon != null) micIcon.IsVisible = false;
                    if (giftButton != null) giftButton.IsVisible = false;
                    if (sendGrid != null) sendGrid.IsVisible = true;
                }
                else
                {
                    // Empty - show BOTH mic and gift
                    if (micIcon != null) micIcon.IsVisible = true;
                    if (giftButton != null) giftButton.IsVisible = true;
                    if (sendGrid != null) sendGrid.IsVisible = false;
                }
            });
        }
        public ChatPage(IAudioManager audioManager) : this()
        {
            _audioManager = audioManager;
        }

        private CancellationTokenSource _recordingTimerCts;

        // Call this when the page layout changes
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width <= 0) return;

            // Match exactly what the XAML Grid does:
            // Bubble max width = 75% of screen
            // Column layout: 44 (play) | * (waveform) | 48 (timer)
            // Column spacing = 6 * 2 = 12
            // Border padding = 8 * 2 = 16
            double bubbleWidth = width * 0.75;
            double waveformWidth = bubbleWidth - 44 - 48 - 12 - 16;
            waveformWidth = Math.Max(60, waveformWidth);

            // Update the static width so all active converters use the correct value
            Lock.Converter.Chat.ProgressToWidthConverter.WaveformColumnWidth = waveformWidth;
            _waveformContainerWidth = waveformWidth;

            System.Diagnostics.Debug.WriteLine($"OnSizeAllocated: screen={width}, bubble={bubbleWidth}, waveform={waveformWidth}");
        }
        private async Task StartRecordingAsync()
        {
            try
            {
                if (_audioRecorder != null)
                {
                    try
                    {
                        if (_audioRecorder.IsRecording)
                            await _audioRecorder.StopAsync();
                    }
                    catch { }
                    _audioRecorder = null;
                }

                var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Microphone>();
                    if (status != PermissionStatus.Granted)
                    {
                        await DisplayAlert("Permission Required",
                            "Microphone permission is needed to record voice messages.", "OK");
                        return;
                    }
                }

                if (_audioManager == null)
                    _audioManager = AudioManager.Current;

                _audioRecorder = _audioManager.CreateRecorder();
                await _audioRecorder.StartAsync();
                _isRecording = true;

                UpdateUIForRecording(true);

                // Start the recording timer
                _recordingTimerCts = new CancellationTokenSource();
                _ = RunRecordingTimerAsync(_recordingTimerCts.Token);

                Debug.WriteLine("Recording started successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartRecordingAsync error: {ex}");
                _isRecording = false;
                _audioRecorder = null;
                UpdateUIForRecording(false);
                await DisplayAlert("Error", $"Failed to start recording: {ex.Message}", "OK");
            }
        }

        private async Task RunRecordingTimerAsync(CancellationToken token)
        {
            int seconds = 0;
            try
            {
                while (!token.IsCancellationRequested && _isRecording)
                {
                    await Task.Delay(1000, token);
                    seconds++;

                    int mins = seconds / 60;
                    int secs = seconds % 60;
                    string timeDisplay = $"{mins}:{secs:D2}";

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        var messageEntry = this.FindByName<Editor>("MessageEntry");
                        if (messageEntry != null)
                        {
                            messageEntry.Placeholder = $"Recording  {timeDisplay}  — tap mic to stop";
                            messageEntry.PlaceholderColor = Colors.Red;
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"RunRecordingTimerAsync error: {ex}");
            }
        }

        private void EnsureInitializeComponent()
        {
            var mi = this.GetType().GetMethod("InitializeComponent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (mi != null)
            {
                mi.Invoke(this, null);
                return;
            }

            Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(this, this.GetType());
        }

        public ChatPage(string conversationId, string otherPhone) : this()
        {
            _conversationId = conversationId ?? string.Empty;
            _otherPhone = otherPhone ?? string.Empty;
        }

        private async Task StopAllPlaybackAsync()
        {
            Debug.WriteLine("Stopping all playback");

            var messagesToStop = _activePlayers.Keys.ToList();
            var currentMessage = _currentlyPlayingMessage;

            await Task.Run(() =>
            {
                try
                {
                    // Stop all tracked players
                    foreach (var kvp in _activePlayers.ToList())
                    {
                        try
                        {
                            var player = kvp.Value;
                            if (player != null)
                            {
                                if (player.IsPlaying)
                                    player.Stop();
                                player.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error stopping player for message {kvp.Key}: {ex}");
                        }
                    }
                    _activePlayers.Clear();

                    // Stop current player
                    if (_currentPlayer != null)
                    {
                        try
                        {
                            if (_currentPlayer.IsPlaying)
                                _currentPlayer.Stop();
                            _currentPlayer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error stopping current player: {ex}");
                        }
                        _currentPlayer = null;
                    }

                    // Dispose stream
                    if (_currentAudioStream != null)
                    {
                        try
                        {
                            _currentAudioStream.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error disposing stream: {ex}");
                        }
                        _currentAudioStream = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in StopAllPlaybackAsync: {ex}");
                }
            });

            // Update UI on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    // Update the specific message that was playing
                    if (currentMessage != null)
                    {
                        currentMessage.IsVoicePlaying = false;
                        currentMessage.VoicePlaybackProgress = 0;
                    }

                    // Reset any stuck messages
                    foreach (var msgId in messagesToStop)
                    {
                        var msg = Messages.FirstOrDefault(m => m.Id == msgId);
                        if (msg != null)
                        {
                            msg.IsVoicePlaying = false;
                            msg.VoicePlaybackProgress = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating UI: {ex}");
                }
            });

            Debug.WriteLine("Stop all playback complete");
        }

        private async Task VerifyAndFixOldRecordingAsync(string audioPath)
        {
            try
            {
                if (!File.Exists(audioPath))
                    return;

                // Check if file is valid WAV
                bool isValid = await Task.Run(() => IsValidWavFile(audioPath));

                if (!isValid)
                {
                    Debug.WriteLine($"Old recording {audioPath} is not valid WAV");

                    // Try to read the file and see if it has any content
                    var fileInfo = new FileInfo(audioPath);
                    if (fileInfo.Length > 0)
                    {
                        Debug.WriteLine($"File has {fileInfo.Length} bytes, attempting to use anyway");
                        // We'll still try to play it - maybe it's playable
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VerifyAndFixOldRecordingAsync error: {ex}");
            }
        }

        private async Task TrackPlaybackProgressAsync(ChatMessage message, IAudioPlayer player)
        {
            try
            {
                Debug.WriteLine($"Starting progress tracking for message {message.Id}");

                var mediaItem = message.MediaItems?.FirstOrDefault();
                if (mediaItem == null) return;

                double totalDuration = 0;

                // Wait for player to report duration
                int durationWait = 0;
                while (player.Duration <= 0 && durationWait < 20)
                {
                    await Task.Delay(50);
                    durationWait++;
                }
                totalDuration = player.Duration > 0 ? player.Duration : (mediaItem.DurationSeconds ?? 5);

                Debug.WriteLine($"Total duration for countdown: {totalDuration}s");

                while (_activePlayers.ContainsKey(message.Id) && player.IsPlaying)
                {
                    try
                    {
                        double currentPosition = player.CurrentPosition;
                        double remaining = Math.Max(0, totalDuration - currentPosition);
                        double progress = totalDuration > 0
                            ? Math.Clamp(currentPosition / totalDuration, 0, 1)
                            : 0;

                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            try
                            {
                                // ?? Update progress ??
                                message.VoicePlaybackProgress = progress;
                                mediaItem.PlaybackProgress = progress;

                                // ?? Update countdown directly on the label ??
                                // Override DisplayDuration by setting CurrentDisplayDuration
                                var remainingSpan = TimeSpan.FromSeconds(remaining);
                                string countdownText = remainingSpan.TotalMinutes >= 1
                                    ? $"{(int)remainingSpan.TotalMinutes}:{remainingSpan.Seconds:D2}"
                                    : $"0:{remainingSpan.Seconds:D2}";

                                mediaItem.CurrentDisplayDuration = countdownText;

                                // Fire all property changed events
                                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.CurrentDisplayDuration));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error updating countdown UI: {ex}");
                            }
                        });

                        await Task.Delay(80); // ~12fps for smooth countdown
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in progress tracking loop: {ex}");
                        await Task.Delay(200);
                    }
                }

                // ?? Reset when playback ends ??
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        message.IsVoicePlaying = false;
                        message.VoicePlaybackProgress = 0;
                        mediaItem.IsPlaying = false;
                        mediaItem.PlaybackProgress = 0;
                        mediaItem.CurrentDisplayDuration = null; // Reset to show total duration
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.CurrentDisplayDuration));
                    }
                    catch { }
                });

                Debug.WriteLine($"Progress tracking ended for message {message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPlaybackProgressAsync fatal error: {ex}");
            }
        }


        private void ScrubToPosition(object sender, Point? tapPoint)
        {
            try
            {
                if (tapPoint == null) return;
                double progress = Math.Clamp(tapPoint.Value.X / WaveformWidth, 0, 1);
                SeekActivePlayer(progress);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScrubToPosition error: {ex}");
            }
        }
       


        private void DebugVoiceMessages()
        {
            Debug.WriteLine("\n=== VOICE MESSAGES DEBUG ===");

            var voiceMessages = Messages.Where(m => m.IsVoiceMessageType()).ToList();
            Debug.WriteLine($"Total voice messages: {voiceMessages.Count}");

            foreach (var msg in voiceMessages)
            {
                Debug.WriteLine($"Message ID: {msg.Id}");
                Debug.WriteLine($"  IsVoiceMessage: {msg.IsVoiceMessage}");
                Debug.WriteLine($"  MediaType: {msg.MediaType}");
                Debug.WriteLine($"  MediaPath: {msg.MediaPath}");
                Debug.WriteLine($"  MediaItems count: {msg.MediaItems?.Count ?? 0}");

                if (msg.MediaItems?.Count > 0)
                {
                    foreach (var item in msg.MediaItems)
                    {
                        Debug.WriteLine($"    Item Type: {item.Type}, Path: {item.Path}, Exists: {File.Exists(item.Path)}");
                    }
                }

                if (!string.IsNullOrEmpty(msg.MediaPath))
                {
                    Debug.WriteLine($"  File exists: {File.Exists(msg.MediaPath)}");
                }
            }

            Debug.WriteLine("=== END DEBUG ===\n");
        }

        private async Task SetupBiometricLockAsync()
        {
            try
            {
                // Show loading indicator
                await Application.Current.MainPage.DisplayAlert(
                    "Setting Up Biometric Lock",
                    "Please wait while we check biometric availability...",
                    "OK");

                // Setup and verify biometric
                bool success = await BiometricService.SetupAndVerifyBiometricAsync();

                if (success)
                {
                    // Save the lock setting
                    bool saved = await ChatLockService.SetChatLockAsync(
                        _conversationId,
                        ChatLockService.LockType.Biometric);

                    if (saved)
                    {
                        await DisplayAlert(
                            "Success",
                            "Chat locked with Biometric. Use fingerprint/Face ID to unlock this chat.",
                            "OK");
                    }
                    else
                    {
                        await DisplayAlert(
                            "Error",
                            "Failed to save biometric lock settings. Please try again.",
                            "OK");
                    }
                }
                else
                {
                    // User cancelled or biometric not available
                    await DisplayAlert(
                        "Setup Cancelled",
                        "Biometric lock setup was cancelled or is not available on this device.\n\nYou can still use PIN or Pattern lock.",
                        "OK");
                }
            }
            catch (FeatureNotSupportedException)
            {
                await DisplayAlert(
                    "Not Supported",
                    "Biometric authentication is not supported on this device.\n\nPlease use PIN or Pattern lock instead.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetupBiometricLockAsync error: {ex}");
                await DisplayAlert(
                    "Error",
                    $"Failed to set up biometric lock: {ex.Message}",
                    "OK");
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            // Reset the unlock flag when leaving the page
            _hasUnlockedForCurrentSession = false;

            // Stop all playback when leaving the page
            _ = StopAllPlaybackAsync();

            // Unsubscribe from background change notifications
            MessagingCenter.Unsubscribe<object, string>(this, ChatBackgroundService.BackgroundChangedMessage);

            // Unsubscribe from messages updated events
            MessagingCenter.Unsubscribe<object>(this, "MessagesUpdated");
        }


        private void SaveScrollPosition()
        {
            // Disabled — we always scroll to bottom
        }
        private void RestoreScrollPosition()
        {
            // Disabled — we always scroll to bottom
            ScrollToBottom();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetNavBarIsVisible(this, false);

            if (_isInitializing) return;
            _isInitializing = true;

            // Show overlay immediately
            if (LoadingOverlay != null)
            {
                LoadingOverlay.IsVisible = true;
                LoadingOverlay.Opacity = 0;
                await LoadingOverlay.FadeTo(1, 300, Easing.CubicOut);
                StartChatLoadingAnimations();
            }

            try
            {
                InitializeVoiceUIState();

                var messageEntry = this.FindByName<Editor>("MessageEntry");
                if (messageEntry != null)
                {
                    messageEntry.TextChanged -= MessageEntry_TextChanged;
                    messageEntry.TextChanged += MessageEntry_TextChanged;
                }

                _me = Preferences.Get(CurrentUserPhoneKey, string.Empty).Trim();
                if (string.IsNullOrEmpty(_me))
                {
                    await DisplayAlert("Error", "No current user found.", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(_conversationId))
                {
                    var conv = await ChatRepository.GetOrCreateConversationAsync(_me, _otherPhone);
                    _conversationId = conv.ConversationId;
                }

                if (_isFirstLoad)
                {
                    MessagingCenter.Unsubscribe<object, string>(this, ChatBackgroundService.BackgroundChangedMessage);
                    MessagingCenter.Subscribe<object, string>(this, ChatBackgroundService.BackgroundChangedMessage,
                        (sender, userPhone) =>
                        {
                            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                            if (currentUserPhone == userPhone)
                                MainThread.BeginInvokeOnMainThread(async () => await ReloadBackgroundAsync());
                        });

                    MessagingCenter.Unsubscribe<object>(this, "MessagesUpdated");
                    MessagingCenter.Subscribe<object>(this, "MessagesUpdated",
                        async (sender) => await RefreshMessagesAsync());

                    await LoadMessagesAsync();

                    _ = Task.WhenAll(
                        LoadOtherUserInfoAsync(),
                        LoadChatBackgroundAsync(),
                        EnsureVoiceFolderExistsAsync(),
                        RunFirstLoadMigrationsAsync()
                    );

                    _isFirstLoad = false;

                    if (_scrollToMessageId.HasValue)
                        await ScrollToSpecificMessageAsync();
                }
                else
                {
                    await ChatRepository.MarkMessagesReadAsync(_conversationId, _me);
                    ScrollToBottom();
                }

                _ = CheckAndWarnAboutBlockStatusAsync();
                _ = UpdateOnlineStatusAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAppearing error: {ex}");
            }
            finally
            {
                _isInitializing = false;

                // Hide overlay when done
                if (LoadingOverlay != null)
                {
                    StopChatLoadingAnimations();
                    await LoadingOverlay.FadeTo(0, 400, Easing.CubicIn);
                    LoadingOverlay.IsVisible = false;
                }
            }
        }
        private async Task RunFirstLoadMigrationsAsync()
        {
            try
            {
                if (!Preferences.Get("voice_migration_done", false))
                {
                    await MigrateOldRecordingsAsync();
                    Preferences.Set("voice_migration_done", true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RunFirstLoadMigrationsAsync error: {ex}");
            }
        }

        private void ApplyGroupingAndOutgoing(List<ChatMessage> slice, int offsetInFullList)
        {
            for (int i = 0; i < slice.Count; i++)
            {
                var msg = slice[i];
                msg.IsLocalOutgoing = string.Equals(
                    msg.SenderPhone?.Trim(), _me, StringComparison.OrdinalIgnoreCase);

                // Deserialize MediaItems if needed
                if (!string.IsNullOrEmpty(msg.MediaItemsJson) &&
                    (msg.MediaItems == null || msg.MediaItems.Count == 0))
                {
                    try
                    {
                        msg.MediaItems = System.Text.Json.JsonSerializer
                            .Deserialize<List<ChatMediaItem>>(msg.MediaItemsJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MediaItems deserialize error msg {msg.Id}: {ex}");
                    }
                }

                msg.IsFirstInGroup = true;
                msg.IsLastInGroup = true;

                if (i > 0)
                {
                    var prev = slice[i - 1];
                    bool sameSender = string.Equals(prev.SenderPhone, msg.SenderPhone,
                                                    StringComparison.OrdinalIgnoreCase);
                    bool timeClose = (msg.SentAt - prev.SentAt).TotalMinutes <= 6;

                    if (sameSender && timeClose)
                    {
                        msg.IsFirstInGroup = false;
                        prev.IsLastInGroup = false;
                    }
                }

                if (i < slice.Count - 1)
                {
                    var next = slice[i + 1];
                    bool sameSenderNext = string.Equals(msg.SenderPhone, next.SenderPhone,
                                                        StringComparison.OrdinalIgnoreCase);
                    bool timeCloseNext = (next.SentAt - msg.SentAt).TotalMinutes <= 6;

                    if (sameSenderNext && timeCloseNext)
                        msg.IsLastInGroup = false;
                }
            }
        }

        private void ReapplyGroupingAroundSeam(int seamIndex)
        {
            // seamIndex is the index of the first "recent" message after prepending older ones
            if (seamIndex <= 0 || seamIndex >= Messages.Count) return;

            var older = Messages[seamIndex - 1];
            var newer = Messages[seamIndex];

            bool sameSender = string.Equals(older.SenderPhone, newer.SenderPhone,
                                            StringComparison.OrdinalIgnoreCase);
            bool timeClose = (newer.SentAt - older.SentAt).TotalMinutes <= 6;

            if (sameSender && timeClose)
            {
                older.IsLastInGroup = false;
                newer.IsFirstInGroup = false;
            }
            else
            {
                older.IsLastInGroup = true;
                newer.IsFirstInGroup = true;
            }
        }

        // Add this helper method to show lock screen
        private async Task<bool> ShowLockScreenAsync(ChatLockService.LockType lockType)
        {
            var tcs = new TaskCompletionSource<bool>();

            var lockPopup = new PinEntryPopup(_conversationId, lockType, (success) =>
            {
                tcs.TrySetResult(success);
            });

            await this.ShowPopupAsync(lockPopup);
            return await tcs.Task;
        }

        // Add this as a field
        private bool _debugMode = true;

        private async Task RefreshMessagesAsync()
        {
            try
            {
                Debug.WriteLine("Fast refresh - only loading new messages");

                var lastMessage = Messages.LastOrDefault();
                var newMessages = await ChatRepository.GetMessagesAsync(_conversationId, 50);

                if (lastMessage != null)
                {
                    newMessages = newMessages.Where(m => m.SentAt > lastMessage.SentAt).ToList();
                }

                if (newMessages.Any())
                {
                    var orderedMsgs = newMessages.OrderBy(m => m.SentAt).ToList();

                    // Resolve post author images for new messages
                    await ResolvePostAuthorImagesAsync(orderedMsgs);

                    foreach (var msg in orderedMsgs)
                    {
                        msg.IsLocalOutgoing = string.Equals(
                            msg.SenderPhone?.Trim(), _me, StringComparison.OrdinalIgnoreCase);
                        Messages.Add(msg);
                    }

                    ScrollToBottom();
                    await ChatRepository.MarkMessagesReadAsync(_conversationId, _me);

                    Debug.WriteLine($"Added {newMessages.Count} new messages");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshMessagesAsync error: {ex}");
                await LoadMessagesAsync();
            }
        }
        private async Task LoadOtherUserInfoAsync()
        {
            try
            {
                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();
                var user = await db.Table<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == _otherPhone)
                    .FirstOrDefaultAsync();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var otherNameLabel = this.FindByName<Label>("OtherNameLabel");
                    var otherProfileImage = this.FindByName<Image>("OtherProfileImage");

                    if (user != null)
                    {
                        if (otherNameLabel != null)
                            otherNameLabel.Text = string.IsNullOrEmpty(user.Name) ? user.PhoneNumber : user.Name;
                        if (otherProfileImage != null)
                        {
                            if (!string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                                otherProfileImage.Source = ImageSource.FromFile(user.ProfileImagePath);
                            else
                                otherProfileImage.Source = null;
                        }
                    }
                    else
                    {
                        if (otherNameLabel != null)
                            otherNameLabel.Text = _otherPhone;
                        if (otherProfileImage != null)
                            otherProfileImage.Source = null;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadOtherUserInfoAsync error: {ex}");
            }
        }

        private async Task DiagnoseVoiceFilesAsync()
        {
            try
            {
                Debug.WriteLine("\n=== VOICE FILE DIAGNOSTIC ===");
                Debug.WriteLine($"Voice folder: {_voiceFolder}");
                Debug.WriteLine($"Folder exists: {Directory.Exists(_voiceFolder)}");

                if (Directory.Exists(_voiceFolder))
                {
                    var files = Directory.GetFiles(_voiceFolder, "*.wav");
                    Debug.WriteLine($"Total WAV files in folder: {files.Length}");

                    foreach (var file in files.Take(5))
                    {
                        var info = new FileInfo(file);
                        Debug.WriteLine($"  File: {Path.GetFileName(file)}, Size: {info.Length} bytes, Modified: {info.LastWriteTime}");
                    }
                }

                var voiceMessages = Messages.Where(m => m.IsVoice).ToList();
                Debug.WriteLine($"Voice messages in current conversation: {voiceMessages.Count}");

                foreach (var msg in voiceMessages)
                {
                    string path = msg.MediaPath ?? msg.MediaItems?.FirstOrDefault()?.Path ?? "No path";
                    bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);
                    Debug.WriteLine($"  Msg ID: {msg.Id}, Path: {path}, Exists: {exists}");

                    if (!exists && !string.IsNullOrEmpty(path))
                    {
                        Debug.WriteLine($"  ?? MISSING FILE: {path}");
                    }
                }

                Debug.WriteLine("=== END DIAGNOSTIC ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Diagnostic error: {ex}");
            }
        }

        private string GenerateWaveformData(string audioPath)
        {
            try
            {
                // This is a simplified version - in production, you'd analyze the actual audio
                // For now, generate a pseudo-random but consistent waveform based on duration
                var random = new Random(audioPath.GetHashCode()); // Consistent per file
                var amplitudes = new int[30]; // 30 data points

                // Create a waveform shape (rising, falling, etc.)
                for (int i = 0; i < 30; i++)
                {
                    // Generate values with some pattern
                    double position = (double)i / 29; // 0 to 1
                    double baseValue = Math.Sin(position * Math.PI) * 0.7 + 0.3; // Bell curve shape
                    int amplitude = (int)(20 + (baseValue * 70) + (random.NextDouble() * 10 - 5));
                    amplitudes[i] = Math.Clamp(amplitude, 10, 100);
                }

                return System.Text.Json.JsonSerializer.Serialize(amplitudes);
            }
            catch
            {
                // Fallback to simple random
                return GenerateSimpleWaveformData();
            }
        }

        // Add this method to reload background without reloading all messages
        private async Task ReloadBackgroundAsync()
        {
            try
            {
                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                    return;

                // Load background path - GLOBAL for this user
                string key = $"chat_bg_{currentUserPhone}";
                string savedPath = Preferences.Get(key, string.Empty);

                // Load brightness - GLOBAL for this user
                string brightnessKey = $"chat_bg_brightness_{currentUserPhone}";
                double brightness = Preferences.Get(brightnessKey, 0.6);

                // Update properties
                bool needsUpdate = false;

                if (ChatBackgroundPath != savedPath)
                {
                    _chatBackgroundPath = savedPath;
                    OnPropertyChanged(nameof(ChatBackgroundPath));
                    needsUpdate = true;
                }

                if (Math.Abs(_backgroundBrightness - brightness) > 0.01)
                {
                    _backgroundBrightness = brightness;
                    OnPropertyChanged(nameof(BackgroundBrightness));
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    UpdateBackgroundImage();
                    Debug.WriteLine($"Background reloaded: Path exists: {!string.IsNullOrEmpty(savedPath)}, Brightness: {brightness}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReloadBackgroundAsync error: {ex}");
            }
        }

        private async void HeaderMenu_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                // Get contact name
                string contactName = OtherNameLabel?.Text?.Trim() ?? _otherPhone ?? "Contact";

                // Get profile image path if available
                string profileImagePath = null;
                var otherProfileImage = this.FindByName<Image>("OtherProfileImage");
                if (otherProfileImage?.Source is FileImageSource fileSource)
                {
                    profileImagePath = fileSource.File;
                }

                // SHOW THE NEW POPUP WITH TABS - NOT the old action sheet!
                var popup = new ChatOptionsPopup(
                    contactName,
                    _otherPhone,
                    _conversationId,
                    profileImagePath
                );

                var result = await this.ShowPopupAsync(popup);

                // Handle any result from the popup if needed
                if (result is string action)
                {
                    switch (action)
                    {
                        case "blocked":
                        case "unblocked":
                            // Refresh block status
                            await CheckAndWarnAboutBlockStatusAsync();
                            // Update online status indicator
                            await UpdateOnlineStatusAsync();
                            break;

                        case "Clear chat":
                            bool confirmClear = await DisplayAlert(
                                "Clear Chat",
                                "Delete all messages in this conversation?",
                                "Clear",
                                "Cancel"
                            );
                            if (confirmClear)
                            {
                                // Implement clear chat logic
                                bool cleared = await ClearChatHistoryAsync();
                                if (cleared)
                                {
                                    await DisplayAlert("Cleared", "Chat history cleared.", "OK");
                                    await LoadMessagesAsync();
                                }
                                else
                                {
                                    await DisplayAlert("Error", "Failed to clear chat history.", "OK");
                                }
                            }
                            break;

                        case "Report user":
                            await ReportUserAsync();
                            break;

                        case "Chat lock":
                            await ShowChatLockOptionsAsync();
                            break;

                        case string disappearing when disappearing.StartsWith("disappearing:"):
                            // Handle disappearing messages setting
                            string setting = disappearing.Replace("disappearing:", "");

                            // Save to conversation preferences or database
                            await SaveDisappearingMessagesSettingAsync(setting);

                            // Show confirmation
                            string displayText = setting switch
                            {
                                "5 minutes" => "5 minutes",
                                "15 minutes" => "15 minutes",
                                "1 hour" => "1 hour",
                                "24 hours" => "24 hours",
                                "1 week" => "1 week",
                                "Off" => "Off",
                                _ => setting
                            };

                            await DisplayAlert(
                                "Disappearing Messages",
                                displayText == "Off"
                                    ? "Disappearing messages turned off"
                                    : $"Messages will disappear after {displayText.ToLower()}",
                                "OK"
                            );

                            // Optionally refresh UI to show new setting
                            await LoadConversationSettingsAsync();
                            break;

                        case string background when background.StartsWith("background:"):
                            // Handle background image update with optional brightness
                            string[] backgroundParts = background.Split('|');
                            string bgPath = backgroundParts[0].Replace("background:", "");

                            Debug.WriteLine($"Background update received: {bgPath}");

                            // Update background path (this will affect all chats)
                            ChatBackgroundPath = bgPath;

                            // Check if brightness is included
                            if (backgroundParts.Length > 1 && backgroundParts[1].StartsWith("brightness:"))
                            {
                                string brightnessStr = backgroundParts[1].Replace("brightness:", "");
                                if (double.TryParse(brightnessStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double brightness))
                                {
                                    BackgroundBrightness = brightness;

                                    // Save brightness preference - user specific (global)
                                    string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                                    if (!string.IsNullOrEmpty(currentUserPhone))
                                    {
                                        string brightnessKey = $"chat_bg_brightness_{currentUserPhone}";
                                        Preferences.Set(brightnessKey, brightness);
                                        Debug.WriteLine($"Saved global brightness {brightness} for user {currentUserPhone}");

                                        // Notify all other chat pages about the brightness change
                                        ChatBackgroundService.NotifyBackgroundChanged(currentUserPhone);
                                    }
                                }
                            }
                            else
                            {
                                // Just background changed without brightness, still notify
                                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                                if (!string.IsNullOrEmpty(currentUserPhone))
                                {
                                    ChatBackgroundService.NotifyBackgroundChanged(currentUserPhone);
                                }
                            }

                            // Force immediate UI update on this page
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                UpdateBackgroundImage();
                            });

                            // Show confirmation
                            if (string.IsNullOrEmpty(bgPath))
                            {
                                await DisplayAlert("Background", "Chat background reset to default for all chats", "OK");
                            }
                            else
                            {
                                await DisplayAlert("Background", "Chat background updated for all chats", "OK");
                            }
                            break;

                        case string brightness when brightness.StartsWith("brightness:"):
                            // Handle standalone brightness update
                            string brightnessValue = brightness.Replace("brightness:", "");
                            if (double.TryParse(brightnessValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double newBrightness))
                            {
                                BackgroundBrightness = newBrightness;

                                // Save brightness preference - user specific (global)
                                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                                if (!string.IsNullOrEmpty(currentUserPhone))
                                {
                                    string brightnessKey = $"chat_bg_brightness_{currentUserPhone}";
                                    Preferences.Set(brightnessKey, newBrightness);
                                    Debug.WriteLine($"Saved global brightness {newBrightness} for user {currentUserPhone}");

                                    // Notify all other chat pages about the brightness change
                                    ChatBackgroundService.NotifyBackgroundChanged(currentUserPhone);
                                }

                                // Force immediate UI update on this page
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    UpdateBackgroundImage();
                                });

                                // Get brightness description for confirmation
                                string brightnessDesc = newBrightness switch
                                {
                                    <= 0.3 => "Subtle (30%)",
                                    <= 0.5 => "Balanced (50%)",
                                    <= 0.7 => "Bright (70%)",
                                    _ => "Very Bright (85%)"
                                };

                                await DisplayAlert("Brightness", $"Background brightness set to {brightnessDesc} for all chats", "OK");
                            }
                            break;

                        case "Mute notifications":
                            await ToggleNotificationsAsync();
                            break;

                        case string mediaAction when mediaAction.StartsWith("media:"):
                            // Handle media tap to open full screen
                            string mediaPath = mediaAction.Replace("media:", "");
                            if (!string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath))
                            {
                                await OpenFullScreenImageAsync(mediaPath);
                            }
                            break;

                        case "View contact":
                            // This is handled in the popup, but we could refresh if needed
                            Debug.WriteLine("View contact action completed");
                            break;

                        default:
                            Debug.WriteLine($"Unhandled popup result: {action}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Options menu failed: {ex.Message}", "OK");
                Debug.WriteLine($"HeaderMenu_Tapped error: {ex}");
            }
        }
        public double BackgroundBrightness
        {
            get => _backgroundBrightness;
            set
            {
                if (_backgroundBrightness != value)
                {
                    _backgroundBrightness = value;
                    OnPropertyChanged(nameof(BackgroundBrightness));
                    UpdateBackgroundImage();
                }
            }
        }

        private void UpdateBackgroundImage()
        {
            var bgImage = this.FindByName<Image>("ChatBackgroundImage");
            if (bgImage != null)
            {
                if (!string.IsNullOrEmpty(_chatBackgroundPath) && File.Exists(_chatBackgroundPath))
                {
                    bgImage.Source = ImageSource.FromFile(_chatBackgroundPath);
                    bgImage.Opacity = _backgroundBrightness;
                    bgImage.IsVisible = true;
                }
                else
                {
                    bgImage.Source = null;
                    bgImage.IsVisible = false;
                }
            }
        }

        // Update LoadChatBackgroundAsync to load brightness
        private async Task LoadChatBackgroundAsync()
        {
            try
            {
                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                    return;

                // Load background path - USER SPECIFIC (applies to all chats)
                string key = $"chat_bg_{currentUserPhone}";  // Removed conversation ID!
                string savedPath = Preferences.Get(key, string.Empty);

                // Load brightness - also user specific
                string brightnessKey = $"chat_bg_brightness_{currentUserPhone}";  // Removed conversation ID!
                double brightness = Preferences.Get(brightnessKey, 0.6); // Default 60%

                ChatBackgroundPath = savedPath;
                BackgroundBrightness = brightness;

                Debug.WriteLine($"Loaded GLOBAL chat background for user {currentUserPhone}: {savedPath}, brightness: {brightness}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadChatBackgroundAsync error: {ex}");
            }
        }

        public string ChatBackgroundPath
        {
            get => _chatBackgroundPath;
            set
            {
                if (_chatBackgroundPath != value)
                {
                    _chatBackgroundPath = value;
                    OnPropertyChanged(nameof(ChatBackgroundPath));

                    // Update the Image control
                    var bgImage = this.FindByName<Image>("ChatBackgroundImage");
                    if (bgImage != null)
                    {
                        if (!string.IsNullOrEmpty(value) && File.Exists(value))
                        {
                            bgImage.Source = ImageSource.FromFile(value);
                            bgImage.IsVisible = true;
                        }
                        else
                        {
                            bgImage.Source = null;
                            bgImage.IsVisible = false;
                        }
                    }
                }
            }
        }

        // Add this method to update online status based on block status AND actual online status
        private async Task UpdateOnlineStatusAsync()
        {
            try
            {
                // Get the online status indicator
                var onlineIndicator = this.FindByName<Frame>("OnlineStatusIndicator");
                if (onlineIndicator == null) return;

                // Check block status
                bool iBlockedThem = await HaveIBlockedThisUserAsync();
                bool theyBlockedMe = await HasThisUserBlockedMeAsync();

                if (iBlockedThem || theyBlockedMe)
                {
                    // If blocked in either direction, ALWAYS show as offline (grey)
                    // Regardless of their actual online status
                    onlineIndicator.BackgroundColor = Colors.Gray;
                    onlineIndicator.Opacity = 0.5; // Slightly transparent to indicate blocked
                    Debug.WriteLine($"User is blocked - showing grey indicator");
                }
                else
                {
                    // Not blocked - show actual online status
                    bool isUserOnline = await GetUserOnlineStatusAsync(_otherPhone);

                    if (isUserOnline)
                    {
                        onlineIndicator.BackgroundColor = Colors.Green;
                        onlineIndicator.Opacity = 1;
                        Debug.WriteLine($"User is online and not blocked - showing green");
                    }
                    else
                    {
                        onlineIndicator.BackgroundColor = Colors.Gray;
                        onlineIndicator.Opacity = 1; // Full opacity for normal offline
                        Debug.WriteLine($"User is offline and not blocked - showing grey");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateOnlineStatusAsync error: {ex}");
            }
        }

        // Add this method to test if the file is a valid WAV using NAudio
        private bool IsValidWavFile(string path)
        {
            try
            {
                using (var reader = new WaveFileReader(path))
                {
                    Debug.WriteLine($"? Valid WAV file - Format: {reader.WaveFormat}, Duration: {reader.TotalTime.TotalSeconds:F1}s");

                    // Also log file details for debugging
                    var fileInfo = new FileInfo(path);
                    Debug.WriteLine($"  File size: {fileInfo.Length} bytes");
                    Debug.WriteLine($"  Sample rate: {reader.WaveFormat.SampleRate} Hz");
                    Debug.WriteLine($"  Channels: {reader.WaveFormat.Channels}");
                    Debug.WriteLine($"  Bits per sample: {reader.WaveFormat.BitsPerSample}");

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"? Invalid WAV file: {ex.Message}");

                // Check if file has any content
                try
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Length > 0)
                    {
                        // Read first few bytes to see what's there
                        byte[] header = new byte[Math.Min(44, (int)fileInfo.Length)];
                        using (var fs = File.OpenRead(path))
                        {
                            fs.Read(header, 0, header.Length);
                        }

                        // Show first 16 bytes as hex and ASCII
                        Debug.WriteLine($"First 16 bytes (hex): {BitConverter.ToString(header.Take(16).ToArray())}");

                        string headerStr = System.Text.Encoding.ASCII.GetString(header.Take(16).ToArray());
                        Debug.WriteLine($"As ASCII: {headerStr}");

                        // If it's not a WAV but has content, log the size
                        if (fileInfo.Length > 1000)
                        {
                            Debug.WriteLine($"File has {fileInfo.Length} bytes of data - may be raw PCM");
                        }
                    }
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Error reading file details: {innerEx}");
                }

                return false;
            }
        }

        // Replace your PlayVoiceCommand with this
        public Command<ChatMediaItem> PlayVoiceCommand => new Command<ChatMediaItem>(async (mediaItem) =>
        {
            if (mediaItem == null) return;

            try
            {
                Debug.WriteLine($"PlayVoiceCommand triggered for: {mediaItem.Path}");

                // Find the message
                var message = Messages.FirstOrDefault(m => m.MediaItems?.Contains(mediaItem) == true);
                if (message == null) return;

                // If this message is already playing, STOP it
                if (message.IsVoicePlaying)
                {
                    Debug.WriteLine("Message is already playing - stopping playback");
                    await StopPlaybackForMessageAsync(message);

                    // Update states
                    message.IsVoicePlaying = false;
                    message.VoicePlaybackProgress = 0;

                    // CRITICAL: Update the media item
                    mediaItem.IsPlaying = false;
                    mediaItem.PlaybackProgress = 0;
                    mediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                    mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                    mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                    return;
                }

                // If another message is playing, stop it first
                if (_currentlyPlayingMessage != null && _currentlyPlayingMessage != message)
                {
                    var oldMediaItem = _currentlyPlayingMessage.MediaItems?.FirstOrDefault();
                    if (oldMediaItem != null)
                    {
                        oldMediaItem.IsPlaying = false;
                        oldMediaItem.PlaybackProgress = 0;
                        oldMediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                        oldMediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                        oldMediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                    }

                    _currentlyPlayingMessage.IsVoicePlaying = false;
                    _currentlyPlayingMessage.VoicePlaybackProgress = 0;

                    await StopPlaybackForMessageAsync(_currentlyPlayingMessage);
                }

                // Set playing state
                message.IsVoicePlaying = true;

                // CRITICAL: Set media item playing state
                mediaItem.IsPlaying = true;
                mediaItem.PlaybackProgress = 0;
                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));

                // Play the new message
                await PlayVoiceMessageAsync(mediaItem);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayVoiceCommand error: {ex}");
                await DisplayAlert("Error", "Failed to play voice message", "OK");
            }
        });
        // Keep ONLY this PlayVoiceMessageAsync method
        private async Task PlayVoiceMessageAsync(ChatMediaItem mediaItem)
        {
            IAudioPlayer? player = null;
            Stream? stream = null;
            ChatMessage? message = null;

            try
            {
                Debug.WriteLine($"\n=== PLAYING VOICE MESSAGE ===");
                Debug.WriteLine($"Path: {mediaItem.Path}");

                // Find the message
                message = Messages.FirstOrDefault(m => m.MediaItems?.Contains(mediaItem) == true);
                if (message == null)
                {
                    Debug.WriteLine("Message not found");
                    return;
                }

                // Add monitoring for debugging
                MonitorVoiceProgress(message);

                // Check if file exists
                if (!File.Exists(mediaItem.Path))
                {
                    Debug.WriteLine($"File not found: {mediaItem.Path}");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        DisplayAlert("Error", "Audio file not found", "OK"));
                    return;
                }

                // Get file info
                var fileInfo = new FileInfo(mediaItem.Path);
                Debug.WriteLine($"File size: {fileInfo.Length} bytes");

                if (fileInfo.Length == 0)
                {
                    Debug.WriteLine("File is empty!");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        DisplayAlert("Error", "Audio file is empty", "OK"));
                    return;
                }

                // Show loading indicator
                message.IsVoicePlaying = true;
                mediaItem.IsPlaying = true;
                mediaItem.PlaybackProgress = 0;

                // Force UI updates
                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));

                // Read file bytes (do this on a background thread)
                byte[] audioBytes = await Task.Run(() => File.ReadAllBytes(mediaItem.Path));
                Debug.WriteLine($"Read {audioBytes.Length} bytes");

                // Create memory stream
                stream = new MemoryStream(audioBytes);

                // Create audio player
                if (_audioManager == null)
                    _audioManager = AudioManager.Current;

                // Create player
                Debug.WriteLine("Creating player...");
                player = AudioManager.Current.CreatePlayer(stream);

                Debug.WriteLine($"Player created. Duration: {player.Duration}");

                // Store the message reference for the event handler
                var currentMessage = message;
                var currentPlayer = player;
                var currentStream = stream;
                var currentMediaItem = mediaItem;

                // Set up completion handler
                player.PlaybackEnded += (s, e) =>
                {
                    Debug.WriteLine("Playback ended event fired");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            if (currentMessage != null)
                            {
                                currentMessage.IsVoicePlaying = false;
                                currentMessage.VoicePlaybackProgress = 0;
                            }

                            if (currentMediaItem != null)
                            {
                                currentMediaItem.IsPlaying = false;
                                currentMediaItem.PlaybackProgress = 0;
                                currentMediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                                currentMediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                                currentMediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                            }

                            // Force UI refresh
                            if (currentMessage != null)
                            {

                            }

                            // Clean up in background
                            Task.Run(() =>
                            {
                                try
                                {
                                    currentPlayer?.Dispose();
                                    currentStream?.Dispose();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error disposing in PlaybackEnded: {ex}");
                                }
                            });

                            if (currentMessage != null && _activePlayers.ContainsKey(currentMessage.Id))
                                _activePlayers.Remove(currentMessage.Id);

                            if (_currentlyPlayingMessage == currentMessage)
                                _currentlyPlayingMessage = null;

                            if (_currentPlayer == currentPlayer)
                                _currentPlayer = null;
                            if (_currentAudioStream == currentStream)
                                _currentAudioStream = null;

                            Debug.WriteLine("Playback cleanup complete");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error in PlaybackEnded: {ex}");
                        }
                    });
                };

                // Start playing
                Debug.WriteLine("Starting playback...");
                player.Play();

                // Store references
                _currentPlayer = player;
                _currentAudioStream = stream;
                _activePlayers[message.Id] = player;
                _currentlyPlayingMessage = message;

                // Start progress tracking (don't await it)
                _ = TrackPlaybackProgressAsync(message, player);

                Debug.WriteLine("Playback started successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayVoiceMessageAsync error: {ex}");

                // Clean up
                player?.Dispose();
                stream?.Dispose();

                _currentPlayer = null;
                _currentAudioStream = null;

                // Update message and media item state
                if (message != null)
                {
                    message.IsVoicePlaying = false;
                    message.VoicePlaybackProgress = 0;

                    if (mediaItem != null)
                    {
                        mediaItem.IsPlaying = false;
                        mediaItem.PlaybackProgress = 0;
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                        mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                    }

                    if (_activePlayers.ContainsKey(message.Id))
                        _activePlayers.Remove(message.Id);

                    if (_currentlyPlayingMessage == message)
                        _currentlyPlayingMessage = null;

                    // Force UI refresh
                    var index = Messages.IndexOf(message);
                    if (index >= 0)
                    {
                        Messages[index] = message;
                    }
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", $"Could not play audio: {ex.Message}", "OK"));
            }
        }

        private void MonitorVoiceProgress(ChatMessage message)
        {
            try
            {
                if (message.MediaItems?.FirstOrDefault() is ChatMediaItem mediaItem)
                {
                    // Remove any existing handlers to avoid duplicates
                    mediaItem.PropertyChanged -= OnMediaItemPropertyChanged;
                    mediaItem.PropertyChanged += OnMediaItemPropertyChanged;

                    message.PropertyChanged -= OnMessagePropertyChanged;
                    message.PropertyChanged += OnMessagePropertyChanged;

                    Debug.WriteLine($"Monitoring voice progress for message {message.Id}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MonitorVoiceProgress error: {ex}");
            }
        }

        private void OnMediaItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ChatMediaItem mediaItem)
            {
                Debug.WriteLine($"MediaItem [{mediaItem.Path}] property changed: {e.PropertyName}");

                if (e.PropertyName == nameof(ChatMediaItem.PlaybackProgress) ||
                    e.PropertyName == nameof(ChatMediaItem.DisplayDuration))
                {
                    Debug.WriteLine($"  Progress: {mediaItem.PlaybackProgress:F2}, Display: {mediaItem.DisplayDuration}");
                }
            }
        }

        private void OnMessagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ChatMessage message)
            {
                Debug.WriteLine($"Message [{message.Id}] property changed: {e.PropertyName}");

                if (e.PropertyName == nameof(ChatMessage.VoicePlaybackProgress) ||
                    e.PropertyName == nameof(ChatMessage.DisplayDuration))
                {
                    Debug.WriteLine($"  Progress: {message.VoicePlaybackProgress:F2}, Display: {message.DisplayDuration}");
                }
            }
        }

        // Update your recording to save in a more compatible format
        private async Task StopAndSendVoiceMessageAsync()
        {
            // Cancel the recording timer first
            _recordingTimerCts?.Cancel();
            _recordingTimerCts = null;

            try
            {
                if (_audioRecorder == null || !_audioRecorder.IsRecording)
                {
                    Debug.WriteLine("StopAndSendVoiceMessageAsync: No active recording");
                    ResetVoiceMessageUI();
                    return;
                }

                Debug.WriteLine("=== STOPPING RECORDING ===");

                var audioSource = await _audioRecorder.StopAsync();
                _isRecording = false;

                Debug.WriteLine("Recording stopped successfully");

                var recorder = _audioRecorder;
                _audioRecorder = null;

                string audioFileName = $"voice_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.wav";
                string audioFolder = _voiceFolder;

                if (!Directory.Exists(audioFolder))
                    Directory.CreateDirectory(audioFolder);

                string permanentAudioPath = Path.Combine(audioFolder, audioFileName);
                Debug.WriteLine($"Saving to: {permanentAudioPath}");

                using var audioStream = audioSource.GetAudioStream();
                byte[] audioData;
                using (var memoryStream = new MemoryStream())
                {
                    await audioStream.CopyToAsync(memoryStream);
                    audioData = memoryStream.ToArray();
                }

                Debug.WriteLine($"Audio data: {audioData.Length} bytes");

                if (audioData.Length == 0)
                {
                    Debug.WriteLine("ERROR: No audio data");
                    await DisplayAlert("Error", "No audio was recorded. Please try again.", "OK");
                    ResetVoiceMessageUI();
                    return;
                }

                await File.WriteAllBytesAsync(permanentAudioPath, audioData);

                if (!File.Exists(permanentAudioPath))
                {
                    Debug.WriteLine("ERROR: File not saved");
                    await DisplayAlert("Error", "Failed to save voice message.", "OK");
                    ResetVoiceMessageUI();
                    return;
                }

                var fileInfo = new FileInfo(permanentAudioPath);
                Debug.WriteLine($"File saved: {fileInfo.Length} bytes");

                int durationSeconds = await GetAudioDurationAsync(permanentAudioPath);
                if (durationSeconds <= 0)
                    durationSeconds = Math.Max(1, (int)(fileInfo.Length / 16000));

                Debug.WriteLine($"Duration: {durationSeconds}s");

                // NEW: Show preview UI BEFORE popup
                UpdateUIForPreviewMode(true, durationSeconds);

                // Store temp path for preview
                _tempRecordingPath = permanentAudioPath;

                // Show preview popup
                await ShowVoicePreviewPopup(permanentAudioPath, durationSeconds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopAndSendVoiceMessageAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to save recording: {ex.Message}", "OK");
                ResetVoiceMessageUI();
            }
        }
        private void UpdateUIForRecording(bool isRecording)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var attachButton = this.FindByName<ContentView>("AttachButton");
                var cancelRecordingIcon = this.FindByName<ContentView>("CancelRecordingIcon");
                var micIcon = this.FindByName<ContentView>("MicIcon");
                var giftButton = this.FindByName<ContentView>("GiftButton");
                var sendActionGrid = this.FindByName<Grid>("SendActionGrid");
                var messageEntry = this.FindByName<Editor>("MessageEntry");
                var micPath = this.FindByName<Microsoft.Maui.Controls.Shapes.Path>("MicPath");

                if (isRecording)
                {
                    if (attachButton != null) attachButton.IsVisible = false;
                    if (cancelRecordingIcon != null) cancelRecordingIcon.IsVisible = true;
                    if (micIcon != null) micIcon.IsVisible = true;
                    if (giftButton != null) giftButton.IsVisible = false;  // hide gift while recording
                    if (sendActionGrid != null) sendActionGrid.IsVisible = false;

                    if (micPath != null) micPath.Fill = new SolidColorBrush(Colors.Red);

                    if (messageEntry != null)
                    {
                        messageEntry.IsEnabled = false;
                        messageEntry.Text = string.Empty;
                        messageEntry.Placeholder = "Recording...";
                        messageEntry.PlaceholderColor = Colors.Red;
                    }
                }
                else
                {
                    if (attachButton != null) attachButton.IsVisible = true;
                    if (cancelRecordingIcon != null) cancelRecordingIcon.IsVisible = false;
                    if (micPath != null) micPath.Fill = new SolidColorBrush(Color.FromArgb("#00B5B5"));

                    if (messageEntry != null)
                    {
                        messageEntry.IsEnabled = true;
                        messageEntry.Placeholder = "All Messages Encrypted...";
                        messageEntry.PlaceholderColor = Color.FromArgb("#999999");
                    }

                    bool hasText = !string.IsNullOrWhiteSpace(messageEntry?.Text);

                    // Show mic + gift together when idle, send when typing
                    if (micIcon != null) micIcon.IsVisible = !hasText;
                    if (giftButton != null) giftButton.IsVisible = !hasText;
                    if (sendActionGrid != null) sendActionGrid.IsVisible = hasText;
                }
            });
        }
        private void UpdateUIForPreviewMode(bool showPreview, int durationSeconds)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var micIcon = this.FindByName<ContentView>("MicIcon");
                var giftButton = this.FindByName<ContentView>("GiftButton");
                var cancelRecordingIcon = this.FindByName<ContentView>("CancelRecordingIcon");
                var sendIcon = this.FindByName<Grid>("SendActionGrid");
                var messageEntry = this.FindByName<Editor>("MessageEntry");

                if (showPreview)
                {
                    if (micIcon != null) micIcon.IsVisible = false;
                    if (giftButton != null) giftButton.IsVisible = false;
                    if (cancelRecordingIcon != null) cancelRecordingIcon.IsVisible = true;
                    if (sendIcon != null) sendIcon.IsVisible = true;
                    if (messageEntry != null)
                    {
                        messageEntry.Placeholder = "?? Voice message recorded";
                        messageEntry.IsEnabled = false;
                        messageEntry.Text = string.Empty;
                    }
                }
                else
                {
                    if (micIcon != null) micIcon.IsVisible = true;
                    if (giftButton != null) giftButton.IsVisible = true;  // restore gift alongside mic
                    if (cancelRecordingIcon != null) cancelRecordingIcon.IsVisible = false;
                    if (sendIcon != null) sendIcon.IsVisible = false;
                    if (messageEntry != null)
                    {
                        messageEntry.Placeholder = "All Messages Encrypted...";
                        messageEntry.IsEnabled = true;
                        messageEntry.Text = string.Empty;
                    }

                    _isRecording = false;
                    _tempRecordingPath = string.Empty;

                    if (_audioRecorder != null)
                    {
                        try
                        {
                            if (_audioRecorder.IsRecording)
                                _audioRecorder.StopAsync().ConfigureAwait(false);
                        }
                        catch { }
                        _audioRecorder = null;
                    }
                }
            });
        }

        private async Task ShowVoicePreviewPopup(string audioPath, int durationSeconds)
        {
            try
            {
                // Reset any active recording
                _isRecording = false;
                _audioRecorder = null;

                string waveformData = GenerateSimpleWaveformData();

                // Show preview popup (this should handle play + send + close)
                var previewPopup = new VoicePreviewPopup(audioPath, durationSeconds, waveformData);
                bool? sendResult = null;
                previewPopup.OnSend += (s, send) => { sendResult = send; };

                await this.ShowPopupAsync(previewPopup);

                if (sendResult == true)
                {
                    await SendVoiceMessageAsync(audioPath, durationSeconds, waveformData);
                }
                else
                {
                    // User cancelled in popup ? delete temp file
                    try { if (File.Exists(audioPath)) File.Delete(audioPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowVoicePreviewPopup error: {ex}");
            }
            finally
            {
                ResetVoiceMessageUI();   // This should now correctly show mic again
            }
        }
        private async Task PlayVoicePreviewAsync(string audioPath)
        {
            try
            {
                if (!File.Exists(audioPath))
                {
                    await DisplayAlert("Error", "Audio file not found", "OK");
                    return;
                }

                if (_previewPlayer != null)
                {
                    if (_previewPlayer.IsPlaying)
                        _previewPlayer.Stop();
                    _previewPlayer.Dispose();
                    _previewPlayer = null;
                }

                var stream = new FileStream(audioPath, FileMode.Open, FileAccess.Read);
                _previewPlayer = AudioManager.Current.CreatePlayer(stream);

                await Task.Delay(150);

                double totalDurationSeconds = _previewPlayer.Duration;
                if (totalDurationSeconds <= 0)
                    totalDurationSeconds = await GetAudioDurationAsync(audioPath);

                var totalTimeSpan = TimeSpan.FromSeconds(totalDurationSeconds);
                string totalDurationText = totalTimeSpan.ToString(@"mm\:ss");

                Debug.WriteLine($"Preview duration: {totalDurationSeconds}s ({totalDurationText})");

                var tcs = new TaskCompletionSource<bool>();
                var cts = new CancellationTokenSource();

                _previewPlayer.PlaybackEnded += (s, e) =>
                {
                    Debug.WriteLine("Preview playback ended");
                    cts.Cancel();
                    tcs.TrySetResult(true);
                };

                _previewPlayer.Play();
                _isPreviewPlaying = true;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var messageEntry = this.FindByName<Editor>("MessageEntry");
                    if (messageEntry != null)
                    {
                        messageEntry.Placeholder = $"?? Playing... 0:00 / {totalDurationText}";
                        messageEntry.PlaceholderColor = Colors.Orange;
                    }
                });

                var progressTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(100);
                            if (cts.Token.IsCancellationRequested) break;
                            if (_previewPlayer == null) break;

                            double currentPos = _previewPlayer.CurrentPosition;
                            double remaining = Math.Max(0, totalDurationSeconds - currentPos);
                            var elapsedSpan = TimeSpan.FromSeconds(currentPos);
                            var remainingSpan = TimeSpan.FromSeconds(remaining);
                            string elapsed = elapsedSpan.ToString(@"mm\:ss");
                            string remainingText = remainingSpan.ToString(@"mm\:ss");

                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                var messageEntry = this.FindByName<Editor>("MessageEntry");
                                if (messageEntry != null)
                                    messageEntry.Placeholder = $"?? {elapsed} / {totalDurationText} (remaining: {remainingText})";
                            });
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Progress tracking error: {ex}");
                    }
                });

                int timeoutMs = (int)((totalDurationSeconds + 3) * 1000);
                await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

                cts.Cancel();
                try { await progressTask; } catch { }

                _isPreviewPlaying = false;

                if (_previewPlayer != null)
                {
                    if (_previewPlayer.IsPlaying)
                        _previewPlayer.Stop();
                    _previewPlayer.Dispose();
                    _previewPlayer = null;
                }
                stream.Dispose();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var messageEntry = this.FindByName<Editor>("MessageEntry");
                    if (messageEntry != null)
                    {
                        messageEntry.Placeholder = "All Messages Encrypted...";
                        messageEntry.PlaceholderColor = Color.FromArgb("#999999");
                    }
                });

                Debug.WriteLine("Preview playback complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayVoicePreviewAsync error: {ex}");
                _isPreviewPlaying = false;
                await DisplayAlert("Error", $"Failed to play preview: {ex.Message}", "OK");
            }
        }
        
        // Add this temporary test button command
        public Command TestPlaybackCommand => new Command(async () =>
        {
            try
            {
                // Find the most recent voice message
                var voiceMessage = Messages.LastOrDefault(m => m.IsVoiceMessageType());
                if (voiceMessage?.MediaItems?.FirstOrDefault() is ChatMediaItem mediaItem)
                {
                    Debug.WriteLine("=== TESTING PLAYBACK WITH SIMPLE APPROACH ===");
                    Debug.WriteLine($"MediaItem Path: {mediaItem.Path}");
                    Debug.WriteLine($"File exists: {File.Exists(mediaItem.Path)}");

                    if (!File.Exists(mediaItem.Path))
                    {
                        // Try to find the file in the correct folder
                        string fileName = Path.GetFileName(mediaItem.Path);
                        string correctPath = Path.Combine(_voiceFolder, fileName);
                        Debug.WriteLine($"Looking in correct folder: {correctPath}");
                        Debug.WriteLine($"File exists in correct folder: {File.Exists(correctPath)}");

                        if (File.Exists(correctPath))
                        {
                            // Update the path
                            mediaItem.Path = correctPath;
                            if (voiceMessage.MediaItems != null && voiceMessage.MediaItems.Count > 0)
                            {
                                voiceMessage.MediaItems[0].Path = correctPath;
                            }
                            voiceMessage.MediaPath = correctPath;
                            Debug.WriteLine("Updated path to correct folder");
                        }
                    }

                    using (var fs = File.OpenRead(mediaItem.Path))
                    {
                        var player = AudioManager.Current.CreatePlayer(fs);
                        Debug.WriteLine($"Player created, duration: {player.Duration}");

                        player.PlaybackEnded += (s, e) =>
                        {
                            Debug.WriteLine("Test playback ended");
                            player.Dispose();
                        };

                        player.Play();
                        Debug.WriteLine("Test playback started");

                        await Task.Delay(3000);
                    }

                    Debug.WriteLine("=== TEST COMPLETE ===");
                }
                else
                {
                    Debug.WriteLine("No voice message found to test");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test failed: {ex}");
            }
        });

        public async void ScrollToMessage(int messageId)
        {
            try
            {
                // Find the message in the current collection
                var targetMessage = Messages.FirstOrDefault(m => m.Id == messageId);
                if (targetMessage != null)
                {
                    await Task.Delay(100);

                    var cv = this.FindByName<CollectionView>("MessagesCollectionView");
                    if (cv != null)
                    {
                        cv.ScrollTo(targetMessage, position: ScrollToPosition.Center, animate: true);

                        // Optional: Highlight the message briefly
                        await HighlightMessageAsync(targetMessage);
                    }
                }
                else
                {
                    // Message not in current collection, reload messages
                    await LoadMessagesAsync();
                    targetMessage = Messages.FirstOrDefault(m => m.Id == messageId);
                    if (targetMessage != null)
                    {
                        var cv = this.FindByName<CollectionView>("MessagesCollectionView");
                        cv?.ScrollTo(targetMessage, position: ScrollToPosition.Center, animate: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScrollToMessage error: {ex}");
            }
        }

        private async void OnSearchTapped(object sender, TappedEventArgs e)
        {
            try
            {
                // Use the registered route name
                await Shell.Current.GoToAsync(nameof(ChatSearchPage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSearchTapped error: {ex}");

                // Fallback: Push the page directly
                try
                {
                    var searchPage = new ChatSearchPage();
                    await Navigation.PushAsync(searchPage);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"Fallback navigation failed: {ex2}");
                    await DisplayAlert("Error", "Could not open search. Please restart the app.", "OK");
                }
            }
        }


        // Add this method to stop a specific message playback
        private async Task StopPlaybackForMessageAsync(ChatMessage message)
        {
            try
            {
                Debug.WriteLine($"Stopping playback for message {message.Id}");

                if (_activePlayers.TryGetValue(message.Id, out var player))
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (player != null)
                            {
                                if (player.IsPlaying)
                                    player.Stop();
                                player.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error stopping player for message {message.Id}: {ex}");
                        }
                    });

                    _activePlayers.Remove(message.Id);

                    if (_currentlyPlayingMessage == message)
                    {
                        _currentlyPlayingMessage = null;
                    }

                    if (_currentPlayer == player)
                    {
                        _currentPlayer = null;
                    }

                    // Update UI
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        message.IsVoicePlaying = false;
                        message.VoicePlaybackProgress = 0;

                        // CRITICAL: Update the media item
                        var mediaItem = message.MediaItems?.FirstOrDefault();
                        if (mediaItem != null)
                        {
                            mediaItem.IsPlaying = false;
                            mediaItem.PlaybackProgress = 0;
                            mediaItem.OnPropertyChanged(nameof(ChatMediaItem.IsPlaying));
                            mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                            mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopPlaybackForMessageAsync error: {ex}");
            }
        }

        private async Task LoadMessagesAsync()
        {
            try
            {
                Messages.Clear();

                var recentMsgs = await ChatRepository.GetMessagesAsync(_conversationId, 50);
                var orderedRecent = recentMsgs.OrderBy(m => m.SentAt).ToList();

                ApplyGroupingAndOutgoing(orderedRecent, 0);

                // Resolve post author images before displaying
                await ResolvePostAuthorImagesAsync(orderedRecent);

                foreach (var m in orderedRecent)
                    Messages.Add(m);

                ScrollToBottom();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var allMsgs = await ChatRepository.GetMessagesAsync(_conversationId, 1000);
                        var older = allMsgs.OrderBy(m => m.SentAt)
                                             .Take(allMsgs.Count - 50)
                                             .ToList();

                        if (!older.Any()) return;

                        ApplyGroupingAndOutgoing(older, 0);

                        // Resolve post author images for older messages too
                        await ResolvePostAuthorImagesAsync(older);

                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            for (int i = older.Count - 1; i >= 0; i--)
                                Messages.Insert(0, older[i]);

                            ReapplyGroupingAroundSeam(older.Count);
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Background message load error: {ex}");
                    }
                });

                _ = Task.Run(async () =>
                {
                    await DisappearingMessagesService.CleanupExpiredMessages();
                    await FixVoiceMessagePathsAsync();
                });

                await ChatRepository.MarkMessagesReadAsync(_conversationId, _me);
                RefreshPinnedStrip();

                Debug.WriteLine($"Loaded {Messages.Count} messages (fast path)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadMessagesAsync error: " + ex);
            }
        }

        // Add this helper method to your ChatPage.xaml.cs
        private async Task FixVoiceMessagePathsAsync()
        {
            try
            {
                Debug.WriteLine("\n=== FIXING VOICE MESSAGE PATHS ===");

                var voiceMessages = Messages.Where(m => m.IsVoice).ToList();
                bool needsDatabaseUpdate = false;

                foreach (var msg in voiceMessages)
                {
                    // Get the path from MediaItems first
                    string path = msg.MediaItems?.FirstOrDefault()?.Path ?? msg.MediaPath;

                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.WriteLine($"Message {msg.Id} has no path");
                        continue;
                    }

                    if (!File.Exists(path))
                    {
                        Debug.WriteLine($"File missing: {path}");

                        // Try to find in the correct voice folder
                        string fileName = Path.GetFileName(path);
                        string correctPath = Path.Combine(_voiceFolder, fileName);

                        if (File.Exists(correctPath))
                        {
                            Debug.WriteLine($"Found file in correct folder: {correctPath}");

                            // Update the path
                            if (msg.MediaItems != null && msg.MediaItems.Count > 0)
                            {
                                msg.MediaItems[0].Path = correctPath;
                            }
                            msg.MediaPath = correctPath;

                            needsDatabaseUpdate = true;
                        }
                        else
                        {
                            Debug.WriteLine($"File not found anywhere for message {msg.Id}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"File exists: {path}");
                    }
                }

                // Update database if we fixed any paths
                if (needsDatabaseUpdate)
                {
                    foreach (var msg in voiceMessages.Where(m => m.MediaItems?.Count > 0))
                    {
                        msg.MediaItemsJson = System.Text.Json.JsonSerializer.Serialize(msg.MediaItems);
                        await ChatRepository.UpdateMessageAsync(msg);
                        Debug.WriteLine($"Updated message {msg.Id} in database with correct path");
                    }
                }

                Debug.WriteLine("=== PATH FIXING COMPLETE ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FixVoiceMessagePathsAsync error: {ex}");
            }
        }


        // Add this helper method
        private void ScrollToBottom()
        {
            var cv = this.FindByName<CollectionView>("MessagesCollectionView");
            if (cv == null || Messages.Count == 0) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    cv.ScrollTo(Messages.Last(), position: ScrollToPosition.End, animate: false);
                }
                catch { }
            });

            // Second attempt after layout settles
            Task.Delay(200).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (Messages.Count > 0)
                            cv.ScrollTo(Messages.Last(), position: ScrollToPosition.End, animate: false);
                    }
                    catch { }
                });
            });
        }

        private async Task EnsureVoiceFolderExistsAsync()
{
    try
    {
        if (!Directory.Exists(_voiceFolder))
        {
            Directory.CreateDirectory(_voiceFolder);
            Debug.WriteLine($"Created voice folder: {_voiceFolder}");
        }
        
        // Test write permission
        string testFile = Path.Combine(_voiceFolder, "test.txt");
        await File.WriteAllTextAsync(testFile, "test");
        if (File.Exists(testFile))
        {
            File.Delete(testFile);
            Debug.WriteLine("Voice folder is writable");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error ensuring voice folder exists: {ex}");
    }
}



private async Task MigrateOldRecordingsAsync()
{
    try
    {
        Debug.WriteLine("\n=== MIGRATING OLD RECORDINGS ===");
        
        var voiceMessages = Messages.Where(m => m.IsVoice).ToList();
        int migratedCount = 0;
        
        foreach (var msg in voiceMessages)
        {
            string oldPath = msg.MediaPath ?? msg.MediaItems?.FirstOrDefault()?.Path;
            
            if (string.IsNullOrEmpty(oldPath) || !File.Exists(oldPath))
                continue;
                
            // Check if file is in old location (not in our voice folder)
            if (!oldPath.StartsWith(_voiceFolder, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"Found recording in old location: {oldPath}");
                
                // Generate new path in our voice folder
                string fileName = Path.GetFileName(oldPath);
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"voice_{msg.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                    
                string newPath = Path.Combine(_voiceFolder, fileName);
                
                try
                {
                    // Copy file to new location
                    File.Copy(oldPath, newPath, true);
                    Debug.WriteLine($"Copied to: {newPath}");
                    
                    // Verify the copy worked
                    if (File.Exists(newPath))
                    {
                        var newInfo = new FileInfo(newPath);
                        var oldInfo = new FileInfo(oldPath);
                        Debug.WriteLine($"Original size: {oldInfo.Length}, New size: {newInfo.Length}");
                        
                        // Update message with new path
                        msg.MediaPath = newPath;
                        if (msg.MediaItems != null && msg.MediaItems.Count > 0)
                        {
                            msg.MediaItems[0].Path = newPath;
                        }
                        
                        // Update in database
                        await ChatRepository.UpdateMessageAsync(msg);
                        
                        migratedCount++;
                        
                        // Optionally delete old file after successful migration
                        // File.Delete(oldPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to migrate {oldPath}: {ex}");
                }
            }
            else
            {
                Debug.WriteLine($"File already in correct location: {oldPath}");
            }
        }
        
        Debug.WriteLine($"Migrated {migratedCount} recordings to persistent folder");
        Debug.WriteLine("=== MIGRATION COMPLETE ===\n");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Migration error: {ex}");
    }
}
        public static async Task<List<ChatMessage>> GetMessagesAsync(
     string conversationId, int max = 200, int skip = 0)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            try
            {
                var query = db.Table<ChatMessage>()
                    .Where(m => m.ConversationId == conversationId && !m.IsBlocked)
                    .OrderByDescending(m => m.SentAt)   // newest first for efficient skip
                    .Skip(skip)
                    .Take(max);

                var messages = await query.ToListAsync();
                messages = messages.OrderBy(m => m.SentAt).ToList(); // re-sort oldest?newest

                // Deserialize MediaItems
                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.MediaItemsJson))
                    {
                        try
                        {
                            msg.MediaItems = System.Text.Json.JsonSerializer
                                .Deserialize<List<ChatMediaItem>>(msg.MediaItemsJson)
                                ?? new List<ChatMediaItem>();
                        }
                        catch { msg.MediaItems = new List<ChatMediaItem>(); }
                    }
                    else
                    {
                        msg.MediaItems = new List<ChatMediaItem>();
                    }

                    // Backward compat: build MediaItems from legacy fields
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

                        if (msg.MediaItems.Count > 0)
                        {
                            msg.MediaItemsJson = System.Text.Json.JsonSerializer.Serialize(msg.MediaItems);
                            await db.UpdateAsync(msg);
                        }
                    }
                }

                return messages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetMessagesAsync: {ex.Message}");
                return new List<ChatMessage>();
            }
        }

        private async void OnEndorsementRequestTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is ChatMessage message && message.MessageType == "endorsement_request")
                {
                    // Check if the request has already been processed
                    if (message.EndorsementStatus == "accepted")
                    {
                        await DisplayAlert(
                            "Already Accepted",
                            $"You have already endorsed {message.EndorsementRequestorName}.\n\nTestimonial: \"{message.EndorsementTestimonial}\"\n\nRating: {message.EndorsementRating}",
                            "OK"
                        );
                        return;
                    }

                    if (message.EndorsementStatus == "declined")
                    {
                        await DisplayAlert(
                            "Already Declined",
                            $"You have already declined this endorsement request from {message.EndorsementRequestorName}.",
                            "OK"
                        );
                        return;
                    }

                    string requestId = message.EndorsementRequestId ?? string.Empty;
                    string requestorName = message.EndorsementRequestorName ?? "Someone";
                    string testimonial = message.EndorsementTestimonial ?? "";
                    string rating = message.EndorsementRating ?? "?????";

                    // Show action sheet for accept/decline
                    string[] options = { "Accept Endorsement", "Decline", "View Details" };

                    string selected = await DisplayActionSheet(
                        $"Endorsement Request from {requestorName}",
                        "Cancel",
                        null,
                        options
                    );

                    if (selected == "Accept Endorsement")
                    {
                        bool confirm = await DisplayAlert(
                            "Accept Endorsement",
                            $"Do you want to endorse {requestorName}?\n\n\"{testimonial}\"\n\nRating: {rating}",
                            "Accept",
                            "Cancel"
                        );

                        if (confirm)
                        {
                            await ProcessEndorsementResponseAsync(requestId, true, message);
                        }
                    }
                    else if (selected == "Decline")
                    {
                        bool confirm = await DisplayAlert(
                            "Decline Endorsement",
                            $"Are you sure you want to decline endorsing {requestorName}?",
                            "Decline",
                            "Cancel"
                        );

                        if (confirm)
                        {
                            await ProcessEndorsementResponseAsync(requestId, false, message);
                        }
                    }
                    else if (selected == "View Details")
                    {
                        await DisplayAlert(
                            "Endorsement Request Details",
                            $"From: {requestorName}\n\nTestimonial:\n\"{testimonial}\"\n\nRating: {rating}\n\nWould you like to endorse {requestorName}?",
                            "OK"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnEndorsementRequestTapped error: {ex}");
                await DisplayAlert("Error", "Could not process endorsement request", "OK");
            }
        }
        private async Task ProcessEndorsementResponseAsync(string requestId, bool accept, ChatMessage originalMessage)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get current user (the endorser)
                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var currentUser = await db.Table<User>().Where(u => u.PhoneNumber == currentUserPhone).FirstOrDefaultAsync();

                if (currentUser == null) return;

                // Get the requestor (person being endorsed)
                int requestorId = 0;
                if (!string.IsNullOrEmpty(originalMessage.EndorsementRequestorId))
                {
                    int.TryParse(originalMessage.EndorsementRequestorId, out requestorId);
                }

                // Parse rating from string
                int ratingValue = 5;
                if (!string.IsNullOrEmpty(originalMessage.EndorsementRating))
                {
                    var ratingText = originalMessage.EndorsementRating;
                    if (ratingText.Contains("5 out of 5")) ratingValue = 5;
                    else if (ratingText.Contains("4 out of 5")) ratingValue = 4;
                    else if (ratingText.Contains("3 out of 5")) ratingValue = 3;
                    else if (ratingText.Contains("2 out of 5")) ratingValue = 2;
                    else if (ratingText.Contains("1 out of 5")) ratingValue = 1;
                }

                // Build plain star string for display
                string starDisplay = new string('\u2605', ratingValue) + new string('\u2606', 5 - ratingValue);

                if (accept)
                {
                    // Create the endorsement using the service
                    var endorsement = await EndorsementService.AddEndorsementAsync(
                        targetUserId: requestorId,
                        targetPhone: originalMessage.SenderPhone,
                        endorserUserId: currentUser.Id,
                        endorserPhone: currentUserPhone,
                        endorserName: currentUser.Name,
                        endorserProfileImage: currentUser.ProfileImagePath,
                        testimonial: originalMessage.EndorsementTestimonial ?? "",
                        rating: ratingValue
                    );

                    if (endorsement != null)
                    {
                        // Send confirmation message back
                        var confirmationMessage = new ChatMessage
                        {
                            ConversationId = _conversationId,
                            SenderPhone = _me,
                            RecipientPhone = originalMessage.SenderPhone,
                            MessageType = "text",
                            Content = $"Endorsed! Thanks for the kind words!\n\nTestimonial: \"{originalMessage.EndorsementTestimonial}\"\nRating: {starDisplay}  ({ratingValue} out of 5)",
                            SentAt = DateTime.UtcNow,
                            IsDelivered = true,
                            IsRead = false,
                            IsLocalOutgoing = true
                        };

                        await ChatRepository.AddMessageAsync(confirmationMessage);
                        Messages.Add(confirmationMessage);

                        // Update the original message to show it was accepted
                        originalMessage.EndorsementStatus = "accepted";
                        await ChatRepository.UpdateMessageAsync(originalMessage);

                        // Find and update the message in the collection to refresh UI
                        var index = Messages.IndexOf(originalMessage);
                        if (index >= 0)
                        {
                            Messages[index] = originalMessage;
                        }

                        await DisplayAlert(
                            "Endorsement Sent",
                            $"You have successfully endorsed {originalMessage.EndorsementRequestorName}!",
                            "OK"
                        );

                        // Notify ProfilePage to refresh endorsements
                        MessagingCenter.Send(this, "EndorsementAdded", requestorId.ToString());
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to save endorsement. Please try again.", "OK");
                    }
                }
                else
                {
                    // Send decline message
                    var declineMessage = new ChatMessage
                    {
                        ConversationId = _conversationId,
                        SenderPhone = _me,
                        RecipientPhone = originalMessage.SenderPhone,
                        MessageType = "text",
                        Content = "I have declined the endorsement request at this time.",
                        SentAt = DateTime.UtcNow,
                        IsDelivered = true,
                        IsRead = false,
                        IsLocalOutgoing = true
                    };

                    await ChatRepository.AddMessageAsync(declineMessage);
                    Messages.Add(declineMessage);

                    // Update the original message status
                    originalMessage.EndorsementStatus = "declined";
                    await ChatRepository.UpdateMessageAsync(originalMessage);

                    // Find and update the message in the collection to refresh UI
                    var index = Messages.IndexOf(originalMessage);
                    if (index >= 0)
                    {
                        Messages[index] = originalMessage;
                    }

                    await DisplayAlert("Declined", "You have declined the endorsement request.", "OK");
                }

                ScrollToBottom();
                MessagingCenter.Send(this, "ConversationsUpdated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessEndorsementResponseAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to process endorsement: {ex.Message}", "OK");
            }
        }

        private async void EncryptionNotice_Tapped(object sender, TappedEventArgs e)
        {
            await DisplayAlert("End-to-End Encryption",
                "Your messages are secured with end-to-end encryption. Only you and the recipient can read them.",
                "Got it");
        }

        private void RefreshMessagesOrdering()
        {
            RefreshPinnedStrip();
        }

        private void RefreshPinnedStrip()
        {
            try
            {
                var pinnedList = Messages.Where(m => m.IsPinned).ToList();
                var pinnedStrip = this.FindByName
                                                            <Grid>("PinnedStrip");
                var pinnedCv = this.FindByName
                                                                <CollectionView>("PinnedMessagesCollectionView");
                var countLabel = this.FindByName
                                                                    <Label>("PinnedCountLabel");

                if (pinnedStrip != null)
                    pinnedStrip.IsVisible = pinnedList.Count > 0;

                if (countLabel != null)
                    countLabel.Text = pinnedList.Count.ToString();

                if (pinnedList.Count == 0)
                {
                    _pinnedIndex = 0;
                    if (pinnedCv != null) pinnedCv.ItemsSource = null;
                    return;
                }

                if (_pinnedIndex >= pinnedList.Count)
                    _pinnedIndex = 0;

                var current = pinnedList[_pinnedIndex];
                if (pinnedCv != null)
                    pinnedCv.ItemsSource = new[] { current };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RefreshPinnedStrip error: " + ex);
            }
        }

        private async Task ResolvePostAuthorImagesAsync(List<ChatMessage> messages)
        {
            try
            {
                var postMessages = messages.Where(m => m.MessageType == "post").ToList();
                if (!postMessages.Any()) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                foreach (var msg in postMessages)
                {
                    try
                    {
                        // Try to find the post author by PostAuthor name or SenderPhone
                        User user = null;

                        // First try: look up by the original post's author phone
                        // The PostAuthor field has the display name, so search by name
                        if (!string.IsNullOrEmpty(msg.PostAuthor))
                        {
                            user = await db.Table<User>()
                                .Where(u => u.Name == msg.PostAuthor)
                                .FirstOrDefaultAsync();
                        }

                        // Second try: look up the post itself to get the author phone
                        if (user == null && msg.PostId.HasValue && msg.PostId.Value > 0)
                        {
                            var post = await db.Table<Lock.Models.Post>()
                                .Where(p => p.Id == msg.PostId.Value)
                                .FirstOrDefaultAsync();

                            if (post != null && !string.IsNullOrEmpty(post.AuthorPhone))
                            {
                                user = await db.Table<User>()
                                    .Where(u => u.PhoneNumber == post.AuthorPhone)
                                    .FirstOrDefaultAsync();
                            }
                        }

                        // Inside the foreach loop, add PostAuthorPhone as first priority:
                        if (user == null && !string.IsNullOrEmpty(msg.PostAuthorPhone))
                        {
                            user = await db.Table<User>()
                                .Where(u => u.PhoneNumber == msg.PostAuthorPhone)
                                .FirstOrDefaultAsync();
                        }

                        // Third try: use sender phone as fallback
                        if (user == null && !string.IsNullOrEmpty(msg.SenderPhone))
                        {
                            user = await db.Table<User>()
                                .Where(u => u.PhoneNumber == msg.SenderPhone)
                                .FirstOrDefaultAsync();
                        }

                        if (user != null && !string.IsNullOrEmpty(user.ProfileImagePath)
                            && File.Exists(user.ProfileImagePath))
                        {
                            msg.PostAuthorProfileImage = user.ProfileImagePath;
                            Debug.WriteLine($"Resolved post author image for msg {msg.Id}: {user.ProfileImagePath}");
                        }
                        else
                        {
                            msg.PostAuthorProfileImage = string.Empty;
                            Debug.WriteLine($"No profile image found for post author in msg {msg.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ResolvePostAuthorImages error for msg {msg.Id}: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResolvePostAuthorImagesAsync error: {ex}");
            }
        }
        private void PinnedPreview_Tapped(object? sender, EventArgs e)
        {
            try
            {
                var pinnedList = Messages.Where(m => m.IsPinned).ToList();
                if (pinnedList.Count == 0) return;

                var current = pinnedList[_pinnedIndex % pinnedList.Count];
                var messagesCv = this.FindByName
                                                                        <CollectionView>("MessagesCollectionView");
                if (messagesCv != null)
                    messagesCv.ScrollTo(current, position: ScrollToPosition.Center, animate: true);

                _pinnedIndex = (_pinnedIndex + 1) % pinnedList.Count;

                var pinnedCv = this.FindByName
                                                                            <CollectionView>("PinnedMessagesCollectionView");
                if (pinnedCv != null)
                    pinnedCv.ItemsSource = new[] { pinnedList[_pinnedIndex] };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PinnedPreview_Tapped error: " + ex);
            }
        }

        private void PinnedMessagesCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0) return;
            if (e.CurrentSelection[0] is not ChatMessage pinned) return;

            try
            {
                var messagesCv = this.FindByName
                                                                                <CollectionView>("MessagesCollectionView");
                if (messagesCv != null)
                {
                    messagesCv.ScrollTo(pinned, position: ScrollToPosition.Center, animate: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PinnedMessagesCollectionView_SelectionChanged error: " + ex);
            }

            if (sender is CollectionView cv) cv.SelectedItem = null;
        }

        private async void AttachImageButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select images",
                    FileTypes = FilePickerFileType.Images
                };

                var results = await FilePicker.Default.PickMultipleAsync(options);

                if (results == null || !results.Any()) return;

                _pendingImagePaths.Clear();

                foreach (var result in results)
                {
                    if (result == null) continue;

                    string extension = Path.GetExtension(result.FileName) ?? ".jpg";
                    string fileName = $"{Guid.NewGuid():N}{extension}";
                    string targetPath = Path.Combine(_imagesFolder, fileName);

                    await using var source = await result.OpenReadAsync();
                    await using var destination = File.Create(targetPath);
                    await source.CopyToAsync(destination);

                    _pendingImagePaths.Add(targetPath);
                }

                if (!_pendingImagePaths.Any()) return;

                // Show preview overlay with multiple images
                ShowMultiImagePreview();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not load images.", "OK");
                Debug.WriteLine(ex);
            }
        }

        // Show preview of multiple selected images
        private void ShowMultiImagePreview()
        {
            var overlay = this.FindByName
                                                                                    <Grid>("ImagePreviewOverlay");
            if (overlay == null) return;

            var previewCollection = this.FindByName
                                                                                        <CollectionView>("PreviewImagesCollection");
            if (previewCollection != null)
            {
                previewCollection.ItemsSource = _pendingImagePaths.Select(p => new { Path = p }).ToList();
            }

            overlay.IsVisible = true;
        }

        private async void SendImages_Clicked(object sender, EventArgs e)
        {
            if (!_pendingImagePaths.Any()) return;

            // ========== CRITICAL BLOCK CHECK (BOTH DIRECTIONS) ==========
            bool theyBlockedMe = await ChatRepository.IsSenderBlockedByRecipientAsync(_me, _otherPhone);
            bool iBlockedThem = await ChatRepository.IsUserBlockedAsync(_me, _otherPhone);

            // Get the overlay reference ONCE at the beginning
            var overlay = this.FindByName<Grid>("ImagePreviewOverlay");

            if (theyBlockedMe)
            {
                await DisplayAlert(
                    "Cannot Send Images",
                    "You cannot send images to this user because they have blocked you.",
                    "OK"
                );
                // Hide overlay and cleanup
                if (overlay != null) overlay.IsVisible = false;
                foreach (var path in _pendingImagePaths)
                {
                    try { if (File.Exists(path)) File.Delete(path); } catch { }
                }
                _pendingImagePaths.Clear();
                return; // EXIT - DO NOT SEND
            }

            if (iBlockedThem)
            {
                bool confirm = await DisplayAlert(
                    "User is Blocked",
                    "You have blocked this user. Do you still want to send these images?",
                    "Send Anyway",
                    "Cancel"
                );
                if (!confirm)
                {
                    // Hide overlay and cleanup
                    if (overlay != null) overlay.IsVisible = false;
                    foreach (var path in _pendingImagePaths)
                    {
                        try { if (File.Exists(path)) File.Delete(path); } catch { }
                    }
                    _pendingImagePaths.Clear();
                    return;
                }
            }
            // ============================================================

            var captionEntry = this.FindByName<Entry>("PreviewCaptionEntry");
            string caption = captionEntry?.Text?.Trim() ?? "";

            // Create media items from all pending images
            var mediaItems = _pendingImagePaths.Select(path => new ChatMediaItem
            {
                Path = path,
                Type = "image"
            }).ToList();

            // Create a SINGLE message with ALL images in MediaItems collection
            var msg = new ChatMessage
            {
                ConversationId = _conversationId,
                SenderPhone = _me,
                RecipientPhone = _otherPhone,
                Content = caption,
                MediaItems = mediaItems,
                SentAt = DateTime.UtcNow,
                IsDelivered = true,
                IsRead = false,
                IsLocalOutgoing = true
            };

            // Add to UI
            Messages.Add(msg);

            // Save to database
            await ChatRepository.AddMessageAsync(msg, isMultiImageMessage: true);

            // ========== ADD THIS LINE ==========
            // Notify ConversationsPage that a message was sent
            MessagingCenter.Send(this, "ConversationsUpdated");

            // Scroll to the new message
            var cv = this.FindByName<CollectionView>("MessagesCollectionView");
            if (cv != null)
                cv.ScrollTo(msg, position: ScrollToPosition.End, animate: true);

            // Hide overlay - use the same overlay variable declared at the beginning
            if (overlay != null) overlay.IsVisible = false;

            // Clear caption entry
            if (captionEntry != null) captionEntry.Text = string.Empty;

            // Clear pending images
            _pendingImagePaths.Clear();

            // Refresh pinned messages if needed
            RefreshPinnedStrip();

            Debug.WriteLine($"Images sent and ConversationsUpdated notification sent");
        }

        private void CancelPreview_Clicked(object sender, EventArgs e)
        {
            var overlay = this.FindByName
                                                                                                        <Grid>("ImagePreviewOverlay");
            if (overlay != null) overlay.IsVisible = false;

            // Delete temporary files
            foreach (var path in _pendingImagePaths)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch { }
            }
            _pendingImagePaths.Clear();
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            var messageEntry = this.FindByName<Editor>("MessageEntry");
            var overlay = this.FindByName<Grid>("ImagePreviewOverlay");

            if (overlay?.IsVisible == true)
            {
                if (_pendingImagePaths.Any())
                    SendImages_Clicked(sender, e);
                return;
            }

            string text = messageEntry?.Text?.Trim() ?? "";

            if (_isEditing && _editingMessage != null)
            {
                await SaveEditButton_Clicked(sender, e);
                return;
            }

            if (string.IsNullOrEmpty(text)) return;

            bool theyBlockedMe = await ChatRepository.IsSenderBlockedByRecipientAsync(_me, _otherPhone);
            bool iBlockedThem = await ChatRepository.IsUserBlockedAsync(_me, _otherPhone);

            if (theyBlockedMe)
            {
                await DisplayAlert("Cannot Send Message",
                    "You cannot send messages to this user because they have blocked you.", "OK");
                return;
            }

            if (iBlockedThem)
            {
                bool confirm = await DisplayAlert("User is Blocked",
                    "You have blocked this user. Do you still want to send this message?",
                    "Send Anyway", "Cancel");
                if (!confirm) return;
            }

            var msg = new ChatMessage
            {
                ConversationId = _conversationId,
                SenderPhone = _me,
                RecipientPhone = _otherPhone,
                Content = text,
                SentAt = DateTime.UtcNow,
                IsDelivered = true,
                IsRead = false,
                IsLocalOutgoing = true
            };

            Messages.Add(msg);

            if (messageEntry != null)
                messageEntry.Text = string.Empty;

            var cv = this.FindByName<CollectionView>("MessagesCollectionView");
            if (cv != null)
                cv.ScrollTo(msg, position: ScrollToPosition.End, animate: true);

            try
            {
                await ChatRepository.AddMessageAsync(msg);
                MessagingCenter.Send(this, "ConversationsUpdated");
                Debug.WriteLine("Message sent and ConversationsUpdated notification sent");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to send message: {ex.Message}", "OK");
            }
        }

        // Fix this method - change from void to async Task
        private async Task DecryptAndRefreshMessage(ChatMessage message)
        {
            if (message == null || !message.IsEncrypted)
                return;

            // Decrypt the message
            await ChatEncryptionService.DecryptMessageAsync(message);

            // Find and update in collection
            var index = Messages.IndexOf(message);
            if (index >= 0)
            {
                // Force UI update by replacing the item
                Messages[index] = message;
            }
        }

        // ===== BLOCK/UNBLOCK HELPER METHODS =====

        // Check if current user has blocked the other user
        private async Task<bool> HaveIBlockedThisUserAsync()
        {
            return await ChatRepository.IsUserBlockedAsync(_me, _otherPhone);
        }

        // Check if the other user has blocked current user
        private async Task<bool> HasThisUserBlockedMeAsync()
        {
            return await ChatRepository.IsSenderBlockedByRecipientAsync(_me, _otherPhone);
        }

        // Toggle block/unblock for the current conversation partner
        private async Task ToggleBlockUserAsync()
        {
            try
            {
                bool iBlockedThem = await HaveIBlockedThisUserAsync();
                string contactName = OtherNameLabel?.Text?.Trim() ?? _otherPhone ?? "this user";

                if (iBlockedThem)
                {
                    // === UNBLOCK FLOW ===
                    bool confirm = await DisplayAlert(
                        "Unblock User",
                        $"Do you want to unblock {contactName}?\n\nYou will be able to send and receive messages again.",
                        "Unblock",
                        "Cancel"
                    );

                    if (!confirm) return;

                    bool success = await ChatRepository.UnblockUserAsync(_me, _otherPhone);

                    if (success)
                    {
                        await DisplayAlert("Success", $"{contactName} has been unblocked.", "OK");
                        UpdateBlockStatusUI();
                        await LoadMessagesAsync(); // Reload to show any hidden messages
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to unblock user. Please try again.", "OK");
                    }
                }
                else
                {
                    // === BLOCK FLOW ===
                    bool confirm = await DisplayAlert(
                        "Block User",
                        $"Block {contactName}?\n\n• You won't receive their messages\n• They won't see when you're online\n• You can unblock anytime",
                        "Block",
                        "Cancel"
                    );

                    if (!confirm) return;

                    bool success = await ChatRepository.BlockUserAsync(_me, _otherPhone);

                    if (success)
                    {
                        await DisplayAlert("Blocked", $"{contactName} has been blocked.", "OK");
                        UpdateBlockStatusUI();
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to block user. Please try again.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToggleBlockUserAsync error: {ex}");
                await DisplayAlert("Error", $"Could not update block status: {ex.Message}", "OK");
            }
        }

        // Update UI elements to reflect current block status
        private void UpdateBlockStatusUI()
        {
            // If you have a dedicated block button in XAML, update it here:
            var blockButton = this.FindByName<Button>("BlockToggleButton");
            if (blockButton != null)
            {
                // Note: Using .Result for UI sync - acceptable for small apps
                bool iBlockedThem = HaveIBlockedThisUserAsync().Result;
                blockButton.Text = iBlockedThem ? "?? Unblock" : "?? Block";
                blockButton.BorderColor = iBlockedThem ? Colors.Green : Color.FromArgb("#FF6B6B");
                blockButton.TextColor = iBlockedThem ? Colors.Green : Color.FromArgb("#FF6B6B");
            }
        }

        // Check block status when page loads and adjust UI accordingly
        private async Task CheckAndWarnAboutBlockStatusAsync()
        {
            bool theyBlockedMe = await HasThisUserBlockedMeAsync();
            bool iBlockedThem = await HaveIBlockedThisUserAsync();

            var statusLabel = this.FindByName<Label>("BlockStatusLabel");
            var onlineIndicator = this.FindByName<Frame>("OnlineStatusIndicator");

            var entry = this.FindByName<Editor>("MessageEntry");
            var sendBtn = this.FindByName<Button>("SendButton");
            var attachBtn = this.FindByName<Button>("AttachImageButton");
            var micBtn = this.FindByName<Button>("MicButton");

            if (theyBlockedMe)
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "?? This user has blocked you";
                    statusLabel.IsVisible = true;
                    statusLabel.TextColor = Colors.Red;
                }

                if (onlineIndicator != null)
                {
                    onlineIndicator.BackgroundColor = Colors.Gray;
                    onlineIndicator.Opacity = 0.5;
                }

                if (entry != null) entry.IsEnabled = false;
                if (sendBtn != null) sendBtn.IsEnabled = false;
                if (attachBtn != null) attachBtn.IsEnabled = false;
                if (micBtn != null) micBtn.IsEnabled = false;

                Debug.WriteLine("They blocked me - showing grey indicator and disabling input");
            }
            else if (iBlockedThem)
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "?? You have blocked this user";
                    statusLabel.IsVisible = true;
                    statusLabel.TextColor = Colors.Orange;
                }

                if (onlineIndicator != null)
                {
                    onlineIndicator.BackgroundColor = Colors.Gray;
                    onlineIndicator.Opacity = 0.5;
                }

                if (entry != null) entry.IsEnabled = false;
                if (sendBtn != null) sendBtn.IsEnabled = false;
                if (attachBtn != null) attachBtn.IsEnabled = false;
                if (micBtn != null) micBtn.IsEnabled = false;

                Debug.WriteLine("I blocked them - showing grey indicator and disabling input");
            }
            else
            {
                if (statusLabel != null) statusLabel.IsVisible = false;

                if (onlineIndicator != null)
                {
                    bool isUserOnline = await GetUserOnlineStatusAsync(_otherPhone);
                    onlineIndicator.BackgroundColor = isUserOnline ? Colors.Green : Colors.Gray;
                    onlineIndicator.Opacity = 1;
                }

                if (entry != null) entry.IsEnabled = true;
                if (sendBtn != null) sendBtn.IsEnabled = true;
                if (attachBtn != null) attachBtn.IsEnabled = true;
                if (micBtn != null) micBtn.IsEnabled = true;
            }
        }

        // Flag to prevent duplicate block warnings per session
        private bool _blockWarningShown = false;

        private async void AddMoreImages_Clicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select more images",
                    FileTypes = FilePickerFileType.Images
                };

                var moreResults = await FilePicker.Default.PickMultipleAsync(options);
                if (moreResults == null || !moreResults.Any()) return;

                foreach (var result in moreResults)
                {
                    if (result == null) continue;
                    string extension = Path.GetExtension(result.FileName) ?? ".jpg";
                    string fileName = $"{Guid.NewGuid():N}{extension}";
                    string targetPath = Path.Combine(_imagesFolder, fileName);

                    await using var source = await result.OpenReadAsync();
                    await using var destination = File.Create(targetPath);
                    await source.CopyToAsync(destination);

                    _pendingImagePaths.Add(targetPath);
                }

                // Refresh preview
                var previewCollection = this.FindByName
                                                                                                                        <CollectionView>("PreviewImagesCollection");
                if (previewCollection != null)
                {
                    var temp = _pendingImagePaths.ToList();
                    previewCollection.ItemsSource = null;
                    previewCollection.ItemsSource = temp.Select(p => new { Path = p }).ToList();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not add more images.", "OK");
                Debug.WriteLine(ex);
            }
        }

        private void RemovePreviewImage_Clicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string pathToRemove)
            {
                if (string.IsNullOrEmpty(pathToRemove))
                    return;

                _pendingImagePaths.Remove(pathToRemove);

                try
                {
                    if (File.Exists(pathToRemove))
                        File.Delete(pathToRemove);
                }
                catch { }

                var previewCollection = this.FindByName
                                                                                                                            <CollectionView>("PreviewImagesCollection");
                if (previewCollection != null)
                {
                    var currentItems = _pendingImagePaths.ToList();
                    previewCollection.ItemsSource = null;
                    previewCollection.ItemsSource = currentItems.Select(p => new { Path = p }).ToList();
                }

                if (!_pendingImagePaths.Any())
                {
                    var overlay = this.FindByName
                                                                                                                                <Grid>("ImagePreviewOverlay");
                    if (overlay != null)
                    {
                        overlay.IsVisible = false;
                    }
                }
            }
        }

        private void ShowActionsOverlay(ChatMessage msg)
        {
            if (msg == null) return;
            _overlayMessage = msg;

            try
            {
                string preview = "—";
                if (msg.HasText)
                {
                    var words = msg.Content!.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                    preview = words.Length
                                                                                                                                    <= 4 ? msg.Content : string.Join(" ", words.Take(4)) + " …";
                }
                else if (msg.IsImage)
                {
                    int imageCount = msg.MediaCount;
                    preview = imageCount > 1 ? $"?? {imageCount} photos" : "?? Photo";
                }

                var previewLabel = this.FindByName
                                                                                                                                        <Label>("OverlayPreviewLabel");
                var overlayGrid = this.FindByName
                                                                                                                                            <Grid>("ActionsOverlay");

                if (previewLabel != null) previewLabel.Text = preview;
                if (overlayGrid != null) overlayGrid.IsVisible = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ShowActionsOverlay error: " + ex);
            }
        }

        private void HideActionsOverlay()
        {
            _overlayMessage = null;
            var overlayGrid = this.FindByName
                                                                                                                                                <Grid>("ActionsOverlay");
            if (overlayGrid != null)
                overlayGrid.IsVisible = false;
        }

        private async void MessageMenuButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.CommandParameter is ChatMessage msg)
                {
                    ShowActionsOverlay(msg);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("MessageMenuButton_Clicked error: " + ex);
                await DisplayAlert("Error", "Could not open message actions: " + ex.Message, "OK");
            }
        }


        // Add these helper methods to your ChatPage class:

        private async Task<bool> ClearChatHistoryAsync()
        {
            try
            {
                // TODO: Implement your actual chat clearing logic
                // For example:
                // await ChatRepository.ClearConversationMessagesAsync(_conversationId);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClearChatHistoryAsync error: {ex}");
                return false;
            }
        }

        private async Task ReportUserAsync()
        {
            try
            {
                bool confirm = await DisplayAlert(
                    "Report User",
                    $"Are you sure you want to report {OtherNameLabel?.Text?.Trim() ?? _otherPhone}?\n\nThis report is anonymous and will be reviewed by our team.",
                    "Report",
                    "Cancel"
                );

                if (confirm)
                {
                    // TODO: Implement actual reporting logic
                    await DisplayAlert(
                        "Report Submitted",
                        "Thank you for your report. Our team will review it shortly.",
                        "OK"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReportUserAsync error: {ex}");
            }
        }

        public async Task SetupBiometricLockFromPopup(string conversationId)
        {
            try
            {
                _conversationId = conversationId;
                await SetupBiometricLockAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetupBiometricLockFromPopup error: {ex}");
                await DisplayAlert("Error", $"Failed to set up biometric lock: {ex.Message}", "OK");
            }
        }

        // Replace the entire ShowChatLockOptionsAsync method with this:
        private async Task ShowChatLockOptionsAsync()
        {
            try
            {
                // Check current lock status
                bool isLocked = await ChatLockService.IsChatLockedAsync(_conversationId);
                var currentLockType = await ChatLockService.GetLockTypeAsync(_conversationId);

                List<string> options = new List<string>();

                if (isLocked)
                {
                    options.Add("Remove Lock");
                    options.Add("Change Lock");
                }

                // Check if biometric is available
                bool isBiometricAvailable = await BiometricService.IsBiometricAvailableAsync();

                if (isBiometricAvailable)
                {
                    options.Add("Lock with Biometric");
                }

                options.Add("Lock with PIN");
                options.Add("Lock with Pattern");
                options.Add("Cancel");

                string selected = await DisplayActionSheet(
                    isLocked ? "Chat Lock (Currently Locked)" : "Chat Lock",
                    "Cancel",
                    null,
                    options.ToArray()
                );

                if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                    return;

                switch (selected)
                {
                    case "Lock with Biometric":
                        await SetupBiometricLockAsync();
                        break;

                    case "Lock with PIN":
                        await SetupPinLockAsync();
                        break;

                    case "Lock with Pattern":
                        await SetupPatternLockAsync();
                        break;

                    case "Remove Lock":
                        await RemoveChatLockAsync();
                        break;

                    case "Change Lock":
                        await ChangeChatLockAsync(currentLockType);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowChatLockOptionsAsync error: {ex}");
                await DisplayAlert("Error", $"Could not set up chat lock: {ex.Message}", "OK");
            }
        }



        // Add these methods after ShowChatLockOptionsAsync:
        private async Task SetupPinLockAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            var setupPopup = new PinSetupPopup(_conversationId, (success, pin) =>
            {
                tcs.TrySetResult(success);
            });

            await this.ShowPopupAsync(setupPopup);
            bool success = await tcs.Task;

            if (success)
            {
                await DisplayAlert("Success", "Chat locked with PIN. The chat will be locked when you close it.", "OK");
            }
        }

        private async Task SetupPatternLockAsync()
        {
            string patternCode = await DisplayPromptAsync(
                "Set Pattern Lock",
                "Enter a pattern code (e.g., 12369874 for a pattern shape)\n\nUse numbers 1-9 in the order of your pattern:",
                "Set Pattern",
                "Cancel",
                keyboard: Keyboard.Numeric,
                maxLength: 9
            );

            if (string.IsNullOrEmpty(patternCode))
                return;

            if (patternCode.Length < 4)
            {
                await DisplayAlert("Error", "Pattern must be at least 4 points", "OK");
                return;
            }

            string confirmPattern = await DisplayPromptAsync(
                "Confirm Pattern",
                "Enter your pattern code again to confirm:",
                "Confirm",
                "Cancel",
                keyboard: Keyboard.Numeric,
                maxLength: 9
            );

            if (patternCode != confirmPattern)
            {
                await DisplayAlert("Error", "Patterns do not match", "OK");
                return;
            }

            bool success = await ChatLockService.SetChatLockAsync(
                _conversationId,
                ChatLockService.LockType.Pattern,
                patternCode);

            if (success)
            {
                await DisplayAlert("Success", "Chat locked with pattern. The chat will be locked when you close it.", "OK");
            }
        }

        private async Task RemoveChatLockAsync()
        {
            bool confirm = await DisplayAlert(
                "Remove Chat Lock",
                "Are you sure you want to remove the lock from this chat?",
                "Remove",
                "Cancel"
            );

            if (!confirm) return;

            bool verified = await VerifyCurrentLockAsync();

            if (!verified)
            {
                await DisplayAlert("Verification Failed", "Incorrect lock credentials", "OK");
                return;
            }

            bool success = await ChatLockService.SetChatLockAsync(_conversationId, ChatLockService.LockType.None);

            if (success)
            {
                await DisplayAlert("Success", "Chat lock has been removed", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Failed to remove chat lock", "OK");
            }
        }

        private async Task ChangeChatLockAsync(ChatLockService.LockType currentLockType)
        {
            bool verified = await VerifyCurrentLockAsync();

            if (!verified)
            {
                await DisplayAlert("Verification Failed", "Incorrect lock credentials", "OK");
                return;
            }

            string[] changeOptions = { "PIN", "Pattern", "Cancel" };
            string selected = await DisplayActionSheet(
                "Change Lock Type",
                "Cancel",
                null,
                changeOptions
            );

            if (selected == "PIN")
            {
                await SetupPinLockAsync();
            }
            else if (selected == "Pattern")
            {
                await SetupPatternLockAsync();
            }
        }

        public async Task ShowChatLockOptionsFromPopup()
        {
            await ShowChatLockOptionsAsync();
        }

        private async Task<bool> VerifyCurrentLockAsync()
        {
            var lockType = await ChatLockService.GetLockTypeAsync(_conversationId);

            if (lockType == ChatLockService.LockType.Pin)
            {
                string enteredPin = await DisplayPromptAsync(
                    "Verify PIN",
                    "Enter your current PIN to continue:",
                    "Verify",
                    "Cancel",
                    keyboard: Keyboard.Numeric,
                    maxLength: 6
                );

                if (string.IsNullOrEmpty(enteredPin))
                    return false;

                return await ChatLockService.VerifyChatLockAsync(_conversationId, enteredPin, lockType);
            }
            else if (lockType == ChatLockService.LockType.Pattern)
            {
                string enteredPattern = await DisplayPromptAsync(
                    "Verify Pattern",
                    "Enter your current pattern code to continue:",
                    "Verify",
                    "Cancel",
                    keyboard: Keyboard.Numeric,
                    maxLength: 9
                );

                if (string.IsNullOrEmpty(enteredPattern))
                    return false;

                return await ChatLockService.VerifyChatLockAsync(_conversationId, enteredPattern, lockType);
            }

            return true;
        }



        private async Task SaveDisappearingMessagesSettingAsync(string setting)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == _conversationId)
                    .FirstOrDefaultAsync();

                if (conversation != null)
                {
                    // Use the helper method we added to the Conversation model
                    conversation.UpdateDisappearingSetting(setting, _me);
                    await db.UpdateAsync(conversation);

                    Debug.WriteLine($"Saved disappearing messages setting: {setting} for conversation {_conversationId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveDisappearingMessagesSettingAsync error: {ex}");
            }
        }

        private async Task LoadConversationSettingsAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == _conversationId)
                    .FirstOrDefaultAsync();

                if (conversation != null && conversation.HasDisappearingMessages)
                {
                    // Update UI to show that disappearing messages are enabled
                    var statusLabel = this.FindByName<Label>("DisappearingStatusLabel");
                    if (statusLabel != null)
                    {
                        statusLabel.Text = conversation.FormattedDisappearingSetting;
                        statusLabel.IsVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadConversationSettingsAsync error: {ex}");
            }
        }

        private async Task OpenFullScreenImageAsync(string imagePath)
        {
            try
            {
                // TODO: Implement full screen image viewer
                // You might have a FullScreenImagePopup or navigation
                Debug.WriteLine($"Opening full screen image: {imagePath}");

                // Example: Show popup with image
                // var imagePopup = new FullScreenImagePopup(imagePath);
                // await this.ShowPopupAsync(imagePopup);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenFullScreenImageAsync error: {ex}");
            }
        }


        // Add this method to get actual online status (implement based on your system)
        private async Task<bool> GetUserOnlineStatusAsync(string userPhone)
        {
            try
            {
                // TODO: Implement your actual online status checking logic
                // This could be from a real-time service, last seen, etc.

                // For now, return false as placeholder
                // You might have something like:
                // return await OnlineStatusService.IsUserOnlineAsync(userPhone);

                return false; // Placeholder - implement your actual logic
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserOnlineStatusAsync error: {ex}");
                return false;
            }
        }

        

        private async Task ToggleNotificationsAsync()
        {
            try
            {
                // Implement notification toggling logic
                string contactName = OtherNameLabel?.Text?.Trim() ?? _otherPhone ?? "Contact";

                // Get current notification status (you'd need to store this)
                bool currentlyOn = true; // Replace with actual stored value

                if (currentlyOn)
                {
                    bool confirm = await DisplayAlert(
                        "Mute Notifications",
                        $"Mute notifications for {contactName}?",
                        "Mute",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        // Turn off notifications
                        // await ChatRepository.SetNotificationMuteAsync(_conversationId, true);
                        await DisplayAlert("Muted", $"Notifications muted for {contactName}", "OK");

                        // Update menu item text for next time
                        // You might want to update a local variable or preference
                    }
                }
                else
                {
                    bool confirm = await DisplayAlert(
                        "Unmute Notifications",
                        $"Unmute notifications for {contactName}?",
                        "Unmute",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        // Turn on notifications
                        // await ChatRepository.SetNotificationMuteAsync(_conversationId, false);
                        await DisplayAlert("Unmuted", $"Notifications unmuted for {contactName}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToggleNotificationsAsync error: {ex}");
                await DisplayAlert("Error", "Could not update notification settings", "OK");
            }
        }

        private async Task BlockUserAsync()
        {
            try
            {
                string contactName = OtherNameLabel?.Text?.Trim() ?? _otherPhone ?? "this user";

                bool confirm = await DisplayAlert(
                    "Block User",
                    $"Are you sure you want to block {contactName}?\n\n• You won't receive their messages\n• They won't see when you're online\n• You can unblock anytime",
                    "Block",
                    "Cancel"
                );

                if (!confirm) return;

                bool blocked = await ChatRepository.BlockUserAsync(_me, _otherPhone);

                if (blocked)
                {
                    await DisplayAlert("Blocked", $"{contactName} has been blocked.", "OK");

                    // Update UI to reflect blocked status
                    UpdateBlockStatusUI();

                    // CRITICAL: Update the online status indicator (will show grey)
                    await UpdateOnlineStatusAsync();

                    // Show status label
                    var statusLabel = this.FindByName<Label>("BlockStatusLabel");
                    if (statusLabel != null)
                    {
                        statusLabel.Text = "?? You have blocked this user";
                        statusLabel.IsVisible = true;
                        statusLabel.TextColor = Colors.Orange;
                    }
                }
                else
                {
                    await DisplayAlert("Error", "Failed to block user. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BlockUserAsync error: {ex}");
                await DisplayAlert("Error", $"Could not block user: {ex.Message}", "OK");
            }
        }

        private async Task UnblockUserAsync()
        {
            try
            {
                string contactName = OtherNameLabel?.Text?.Trim() ?? _otherPhone ?? "this user";

                bool confirm = await DisplayAlert(
                    "Unblock User",
                    $"Do you want to unblock {contactName}?\n\nYou will be able to send and receive messages again.",
                    "Unblock",
                    "Cancel"
                );

                if (!confirm) return;

                bool unblocked = await ChatRepository.UnblockUserAsync(_me, _otherPhone);

                if (unblocked)
                {
                    await DisplayAlert("Unblocked", $"{contactName} has been unblocked.", "OK");

                    // Update UI to reflect unblocked status
                    UpdateBlockStatusUI();

                    // CRITICAL: Update the online status indicator (will show actual status)
                    await UpdateOnlineStatusAsync();

                    // Hide status label
                    var statusLabel = this.FindByName<Label>("BlockStatusLabel");
                    if (statusLabel != null)
                    {
                        statusLabel.IsVisible = false;
                    }

                    // Reload messages to show any that were hidden
                    await LoadMessagesAsync();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to unblock user. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnblockUserAsync error: {ex}");
                await DisplayAlert("Error", $"Could not unblock user: {ex.Message}", "OK");
            }
        }

        private void OverlayBackground_Tapped(object sender, EventArgs e)
        {
            if (_overlayBusy) return;

            if (_isEditing)
            {
                CancelEditMode();
            }

            HideActionsOverlay();
        }

        private async void OverlayAction_Clicked(object sender, EventArgs e)
        {
            if (_overlayBusy) return;
            if (_overlayMessage == null) return;

            string action = string.Empty;

            if (sender is Button btn)
                action = (btn.CommandParameter as string) ?? btn.Text ?? string.Empty;
            else if (sender is TapGestureRecognizer tr)
                action = tr.CommandParameter as string ?? string.Empty;
            else if (sender is VisualElement ve && ve is View view)
            {
                var tap = view.GestureRecognizers?.OfType
                                                                                                                                                        <TapGestureRecognizer>().FirstOrDefault();
                if (tap != null) action = tap.CommandParameter as string ?? string.Empty;
            }

            if (string.IsNullOrEmpty(action))
            {
                HideActionsOverlay();
                return;
            }

            _overlayBusy = true;

            try
            {
                switch (action)
                {
                    case "Copy text":
                        if (_overlayMessage.HasText)
                        {
                            await Clipboard.SetTextAsync(_overlayMessage.Content);
                            _ = ShowTopPulseAsync();
                        }
                        break;

                    case "Forward":
                        await DisplayAlert("Forward", "Forward flow not implemented yet.", "OK");
                        break;

                    case "Pin":
                        _overlayMessage.IsPinned = !_overlayMessage.IsPinned;
                        await ChatRepository.UpdateMessageAsync(_overlayMessage);
                        var idxPin = Messages.IndexOf(_overlayMessage);
                        if (idxPin >= 0) Messages[idxPin] = _overlayMessage;
                        RefreshMessagesOrdering();
                        _ = ShowTopPulseAsync();
                        break;

                    case "Star":
                        _overlayMessage.IsStarred = !_overlayMessage.IsStarred;
                        await ChatRepository.UpdateMessageAsync(_overlayMessage);
                        var idxStar = Messages.IndexOf(_overlayMessage);
                        if (idxStar >= 0) Messages[idxStar] = _overlayMessage;
                        _ = ShowTopPulseAsync();
                        break;

                    case "Edit":
                        await EnterEditMode(_overlayMessage);
                        break;

                    case "Delete":
                        var confirm = await DisplayAlert("Delete", "Delete this message?", "Delete", "Cancel");
                        if (confirm)
                        {
                            // Store current position before deletion
                            ChatMessage msgToDelete = _overlayMessage;
                            int deleteIndex = Messages.IndexOf(msgToDelete);

                            await ChatRepository.DeleteMessageAsync(msgToDelete);
                            Messages.Remove(msgToDelete);
                            RefreshPinnedStrip();

                            // Restore scroll position after deletion
                            var cv = this.FindByName
                                                                                                                                                            <CollectionView>("MessagesCollectionView");
                            if (cv != null && Messages.Count > 0)
                            {
                                if (deleteIndex
                                                                                                                                                                < Messages.Count)
                                {
                                    // Scroll to the message that took its place
                                    cv.ScrollTo(Messages[deleteIndex], position: ScrollToPosition.Center, animate: false);
                                }
                                else
                                {
                                    // Scroll to last message
                                    cv.ScrollTo(Messages.Last(), position: ScrollToPosition.End, animate: false);
                                }
                            }
                        }
                        break;

                    case "Report":
                        await DisplayAlert("Report", "Thank you — the message has been reported.", "OK");
                        break;

                    default:
                        await DisplayAlert("Action", $"Selected: {action}", "OK");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OverlayAction_Clicked error: " + ex);
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                _overlayBusy = false;
                HideActionsOverlay();
            }
        }

        private async Task EnterEditMode(ChatMessage message)
        {
            _isEditing = true;
            _editingMessage = message;

            _editingImagePaths.Clear();
            _newImagePaths.Clear();

            if (message.MediaItems?.Count > 0)
            {
                _editingImagePaths = message.MediaItems.Select(m => m.Path).ToList();
            }

            var messageEntry = this.FindByName<Editor>("MessageEntry");
            var cancelGrid = this.FindByName<Grid>("CancelActionGrid");
            var editImagesPreview = this.FindByName<Grid>("EditImagesPreviewGrid");
            var editImagesCollection = this.FindByName<CollectionView>("EditImagesCollection");
            var attachButton = this.FindByName<ContentView>("AttachButton");
            var sendGrid = this.FindByName<Grid>("SendActionGrid");
            var micIcon = this.FindByName<ContentView>("MicIcon");
            var giftButton = this.FindByName<ContentView>("GiftButton");

            if (attachButton != null) attachButton.IsVisible = false;
            if (cancelGrid != null) cancelGrid.IsVisible = true;
            if (sendGrid != null) sendGrid.IsVisible = true;

            // Hide both mic and gift in edit mode
            if (micIcon != null) micIcon.IsVisible = false;
            if (giftButton != null) giftButton.IsVisible = false;

            if (messageEntry != null)
            {
                messageEntry.Text = message.Content ?? string.Empty;
                messageEntry.Focus();
            }

            if (_editingImagePaths.Any() && editImagesPreview != null)
            {
                editImagesPreview.IsVisible = true;
                if (editImagesCollection != null)
                {
                    var displayList = _editingImagePaths.Select(p => new
                    {
                        Path = p,
                        DisplayPath = System.IO.Path.GetFileName(p)
                    }).ToList();

                    editImagesCollection.ItemsSource = displayList;
                    editImagesCollection.HeightRequest = 100;
                }
            }
            else if (editImagesPreview != null)
            {
                editImagesPreview.IsVisible = false;
            }
        }
        private async void AddMoreImagesDuringEdit_Clicked(object sender, EventArgs e)
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Select images to add",
                    FileTypes = FilePickerFileType.Images
                };

                var results = await FilePicker.Default.PickMultipleAsync(options);
                if (results == null || !results.Any()) return;

                foreach (var result in results)
                {
                    if (result == null) continue;

                    string extension = Path.GetExtension(result.FileName) ?? ".jpg";
                    string fileName = $"{Guid.NewGuid():N}{extension}";
                    string targetPath = Path.Combine(_imagesFolder, fileName);

                    await using var source = await result.OpenReadAsync();
                    await using var destination = File.Create(targetPath);
                    await source.CopyToAsync(destination);

                    _newImagePaths.Add(targetPath);
                }

                // Refresh the edit images preview
                RefreshEditImagesPreview();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not add images.", "OK");
                Debug.WriteLine(ex);
            }
        }

        private void RemoveEditImage_Clicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string pathToRemove)
            {
                if (string.IsNullOrEmpty(pathToRemove))
                    return;

                // Check if it's from original images or new images
                if (_editingImagePaths.Contains(pathToRemove))
                {
                    _editingImagePaths.Remove(pathToRemove);
                }
                else if (_newImagePaths.Contains(pathToRemove))
                {
                    _newImagePaths.Remove(pathToRemove);
                    // Delete the physical file for new images (they weren't saved yet)
                    try
                    {
                        if (File.Exists(pathToRemove))
                            File.Delete(pathToRemove);
                    }
                    catch { }
                }

                RefreshEditImagesPreview();
            }
        }

        private void RefreshEditImagesPreview()
        {
            var editImagesCollection = this.FindByName
                                                                                                                                                                                    <CollectionView>("EditImagesCollection");
            if (editImagesCollection != null)
            {
                var allPaths = _editingImagePaths.Concat(_newImagePaths).ToList();
                Debug.WriteLine($"Refreshing preview with {allPaths.Count} images");

                // Create a proper display list
                var displayList = allPaths.Select(p => new
                {
                    Path = p,
                    DisplayName = System.IO.Path.GetFileName(p) // Show filename for debugging
                }).ToList();

                editImagesCollection.ItemsSource = null;
                editImagesCollection.ItemsSource = displayList;

                // Force the CollectionView to update
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    editImagesCollection.HeightRequest = allPaths.Any() ? 100 : 0;
                });
            }
        }

        private async Task SaveEditButton_Clicked(object sender, EventArgs e)
        {
            if (!_isEditing || _editingMessage == null) return;

            try
            {
                var messageEntry = this.FindByName<Editor>("MessageEntry");
                string newText = messageEntry?.Text?.Trim() ?? string.Empty;

                var allImagePaths = _editingImagePaths.Concat(_newImagePaths).ToList();

                Debug.WriteLine($"Saving edit with {allImagePaths.Count} images");

                if (!allImagePaths.Any() && string.IsNullOrEmpty(newText))
                {
                    await DisplayAlert("Error", "Message cannot be empty", "OK");
                    return;
                }

                var mediaItems = allImagePaths.Select(path => new ChatMediaItem
                {
                    Path = path,
                    Type = "image"
                }).ToList();

                int originalMessageId = _editingMessage.Id;
                DateTime originalSentAt = _editingMessage.SentAt;
                bool wasPinned = _editingMessage.IsPinned;
                bool wasStarred = _editingMessage.IsStarred;

                int editIndex = Messages.IndexOf(_editingMessage);

                await ChatRepository.DeleteMessageAsync(_editingMessage);

                var updatedMessage = new ChatMessage
                {
                    Id = originalMessageId,
                    ConversationId = _editingMessage.ConversationId,
                    SenderPhone = _editingMessage.SenderPhone,
                    RecipientPhone = _editingMessage.RecipientPhone,
                    Content = newText,
                    MediaItems = mediaItems,
                    SentAt = originalSentAt,
                    IsDelivered = _editingMessage.IsDelivered,
                    IsRead = _editingMessage.IsRead,
                    IsPinned = wasPinned,
                    IsStarred = wasStarred,
                    IsEdited = true,
                    IsLocalOutgoing = true
                };

                await ChatRepository.AddMessageAsync(updatedMessage);

                if (editIndex >= 0)
                {
                    Messages.RemoveAt(editIndex);
                    Messages.Insert(editIndex, updatedMessage);
                }

                var cv = this.FindByName<CollectionView>("MessagesCollectionView");
                if (cv != null && editIndex >= 0)
                    cv.ScrollTo(updatedMessage, position: ScrollToPosition.Center, animate: false);

                var oldPaths = _editingMessage.MediaItems?.Select(m => m.Path) ?? new List<string>();
                var removedPaths = oldPaths.Except(allImagePaths).ToList();

                foreach (var path in removedPaths)
                {
                    try { if (File.Exists(path)) File.Delete(path); } catch { }
                }

                await DisplayAlert("Success", "Message updated successfully", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving edit: {ex}");
                await DisplayAlert("Error", $"Failed to update message: {ex.Message}", "OK");
            }
            finally
            {
                CancelEditMode();
            }
        }
        private void CancelEditMode()
        {
            _isEditing = false;
            _editingMessage = null;
            _editingImagePaths.Clear();
            _newImagePaths.Clear();

            var messageEntry = this.FindByName<Editor>("MessageEntry");
            var editImagesPreview = this.FindByName<Grid>("EditImagesPreviewGrid");
            var attachButton = this.FindByName<ContentView>("AttachButton");
            var cancelGrid = this.FindByName<Grid>("CancelActionGrid");
            var micIcon = this.FindByName<ContentView>("MicIcon");
            var giftButton = this.FindByName<ContentView>("GiftButton");
            var sendGrid = this.FindByName<Grid>("SendActionGrid");

            if (attachButton != null) attachButton.IsVisible = true;
            if (cancelGrid != null) cancelGrid.IsVisible = false;

            if (messageEntry != null)
                messageEntry.Text = string.Empty;

            if (editImagesPreview != null)
                editImagesPreview.IsVisible = false;

            bool hasText = !string.IsNullOrWhiteSpace(messageEntry?.Text);

            // Restore mic + gift when cancelling edit
            if (micIcon != null) micIcon.IsVisible = !hasText;
            if (giftButton != null) giftButton.IsVisible = !hasText;
            if (sendGrid != null) sendGrid.IsVisible = hasText;
        }
        private void CancelEditButton_Clicked(object sender, EventArgs e)
        {
            CancelEditMode();
        }

        private async Task ShowTopPulseAsync()
        {
            try
            {
                _pulseCts?.Cancel();
                _pulseCts = new CancellationTokenSource();
                var token = _pulseCts.Token;

                var pulse = this.FindByName
                                                                                                                                                                                                                <BoxView>("ActionPulse");
                if (pulse == null) return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    pulse.Opacity = 1;
                    pulse.IsVisible = true;
                });

                await Task.Delay(500, token);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    pulse.IsVisible = false;
                    pulse.Opacity = 0;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine("ShowTopPulseAsync error: " + ex);
            }
        }

        // Add this method to ChatPage.xaml.cs
        public void Cleanup()
        {
            try
            {
                // Stop all playback
                _ = StopAllPlaybackAsync();

                // Clear messages to free memory
                Messages.Clear();

                // Unsubscribe from events
                MessagingCenter.Unsubscribe<object, string>(this, ChatBackgroundService.BackgroundChangedMessage);
                MessagingCenter.Unsubscribe<object>(this, "MessagesUpdated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup error: {ex}");
            }
        }

        // Add this field at the top with other fields
        private bool _isNavigationComplete = false;

        // Add this method to pre-load the page faster
        public static async Task<ChatPage> CreateFastAsync(string conversationId, string otherPhone)
        {
            var page = new ChatPage();
            page._conversationId = conversationId;
            page._otherPhone = otherPhone;

            // Pre-initialize critical components
            await page.InitializeCriticalAsync();

            return page;
        }

        private async Task InitializeCriticalAsync()
        {
            try
            {
                _me = Preferences.Get(CurrentUserPhoneKey, string.Empty).Trim();

                if (string.IsNullOrEmpty(_conversationId))
                {
                    var conv = await ChatRepository.GetOrCreateConversationAsync(_me, _otherPhone);
                    _conversationId = conv.ConversationId;
                }

                // Load only essential data first
                await LoadOtherUserInfoAsync();
                await LoadMessagesAsync();
                await LoadChatBackgroundAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeCriticalAsync error: {ex}");
            }
        }

        // Bottom send/cancel tap (paper plane or X)
        private async void BottomAction_Tapped(object sender, EventArgs e)
        {
            string action = string.Empty;
            if (sender is TapGestureRecognizer tr)
                action = tr.CommandParameter as string ?? "";
            else if (sender is VisualElement ve && ve is View v)
            {
                var tr2 = v.GestureRecognizers?.OfType<TapGestureRecognizer>().FirstOrDefault();
                action = tr2?.CommandParameter as string ?? "";
            }

            if (string.IsNullOrEmpty(action)) return;

            switch (action)
            {
                case "Cancel":
                    CancelEditButton_Clicked(this, EventArgs.Empty);
                    break;

                case "Send":
                    var messageEntry = this.FindByName<Editor>("MessageEntry");
                    string textToSend = messageEntry?.Text?.Trim() ?? string.Empty;

                    if (_isEditing && _editingMessage != null)
                    {
                        await SaveEditButton_Clicked(this, EventArgs.Empty);
                        return;
                    }

                    var overlay = this.FindByName<Grid>("ImagePreviewOverlay");
                    if (overlay?.IsVisible == true)
                    {
                        if (_pendingImagePaths.Any())
                            SendImages_Clicked(this, EventArgs.Empty);
                        return;
                    }

                    if (string.IsNullOrEmpty(textToSend)) return;

                    bool theyBlockedMe = await ChatRepository.IsSenderBlockedByRecipientAsync(_me, _otherPhone);
                    bool iBlockedThem = await ChatRepository.IsUserBlockedAsync(_me, _otherPhone);

                    if (theyBlockedMe)
                    {
                        await DisplayAlert("Cannot Send Message",
                            "You cannot send messages to this user because they have blocked you.", "OK");
                        return;
                    }

                    if (iBlockedThem)
                    {
                        bool confirm = await DisplayAlert("User is Blocked",
                            "You have blocked this user. Do you still want to send this message?",
                            "Send Anyway", "Cancel");
                        if (!confirm) return;
                    }

                    var msg = new ChatMessage
                    {
                        ConversationId = _conversationId,
                        SenderPhone = _me,
                        RecipientPhone = _otherPhone,
                        Content = textToSend,
                        SentAt = DateTime.UtcNow,
                        IsDelivered = true,
                        IsRead = false,
                        IsLocalOutgoing = true
                    };

                    if (messageEntry != null)
                        messageEntry.Text = string.Empty;

                    Messages.Add(msg);

                    var cv = this.FindByName<CollectionView>("MessagesCollectionView");
                    cv?.ScrollTo(msg, position: ScrollToPosition.End, animate: true);

                    try
                    {
                        await ChatRepository.AddMessageAsync(msg);
                        MessagingCenter.Send(this, "ConversationsUpdated");
                        Debug.WriteLine("Message sent successfully");
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", $"Failed to send message: {ex.Message}", "OK");
                        Debug.WriteLine($"Send error: {ex}");
                    }
                    break;
            }
        }
        private async void MicButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                bool theyBlockedMe = await HasThisUserBlockedMeAsync();
                bool iBlockedThem = await HaveIBlockedThisUserAsync();

                if (theyBlockedMe)
                {
                    await DisplayAlert("Cannot Record",
                        "You cannot send voice messages because this user has blocked you.", "OK");
                    return;
                }

                if (iBlockedThem)
                {
                    bool confirm = await DisplayAlert("User is Blocked",
                        "You have blocked this user. Send voice message anyway?",
                        "Send Anyway", "Cancel");
                    if (!confirm) return;
                }

                if (!_isRecording)
                {
                    // Check permission before starting
                    var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<Permissions.Microphone>();
                        if (status != PermissionStatus.Granted)
                        {
                            await DisplayAlert("Permission Required",
                                "Microphone permission is needed to record voice messages.", "OK");
                            return;
                        }
                    }

                    // START recording
                    await StartRecordingAsync();
                }
                else
                {
                    // STOP recording — show preview popup so user can listen then send
                    await StopAndSendVoiceMessageAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MicButton_Clicked error: {ex}");
                _isRecording = false;
                _audioRecorder = null;
                ResetVoiceMessageUI();
                await DisplayAlert("Error", $"Microphone error: {ex.Message}", "OK");
            }
        }
        private async void CancelRecording_Clicked(object sender, EventArgs e)
        {
            // Cancel the recording timer first
            _recordingTimerCts?.Cancel();
            _recordingTimerCts = null;

            try
            {
                if (_audioRecorder != null && _audioRecorder.IsRecording)
                {
                    // Stop recording without saving
                    await _audioRecorder.StopAsync();
                    _isRecording = false;

                    // Clean up any temporary file if it exists
                    if (!string.IsNullOrEmpty(_tempRecordingPath) && File.Exists(_tempRecordingPath))
                    {
                        try { File.Delete(_tempRecordingPath); } catch { }
                        _tempRecordingPath = string.Empty;
                    }

                    // Stop preview if playing
                    if (_previewPlayer != null)
                    {
                        if (_previewPlayer.IsPlaying)
                            _previewPlayer.Stop();
                        _previewPlayer.Dispose();
                        _previewPlayer = null;
                    }

                    // Update UI back to normal mode - FULL RESET
                    UpdateUIForRecording(false);

                    // Make sure mic icon is visible
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var micIcon = this.FindByName<ContentView>("MicIcon");
                        var sendIcon = this.FindByName<Grid>("SendActionGrid");

                        if (micIcon != null)
                        {
                            micIcon.IsVisible = true;
                            micIcon.Scale = 1.0;
                        }
                        if (sendIcon != null)
                            sendIcon.IsVisible = false;

                        Debug.WriteLine("Recording cancelled - mic icon restored");
                    });

                    Debug.WriteLine("Recording cancelled by user");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CancelRecording_Clicked error: {ex}");
                await DisplayAlert("Error", "Failed to cancel recording", "OK");
            }
            finally
            {
                _audioRecorder = null;
            }
        }

        private void VerifyAudioFile(string path)
        {
            try
            {
                Debug.WriteLine($"\n=== Verifying audio file: {path} ===");

                if (!File.Exists(path))
                {
                    Debug.WriteLine("File does not exist!");
                    return;
                }

                var fileInfo = new FileInfo(path);
                Debug.WriteLine($"Size: {fileInfo.Length} bytes");

                // Try to read the header (WAV files start with "RIFF")
                using var fs = File.OpenRead(path);
                byte[] header = new byte[4];
                fs.Read(header, 0, 4);
                string headerStr = System.Text.Encoding.ASCII.GetString(header);
                Debug.WriteLine($"File header: {headerStr}");

                if (headerStr == "RIFF")
                {
                    Debug.WriteLine("This appears to be a valid WAV file");
                }
                else
                {
                    Debug.WriteLine($"Unexpected file header: {headerStr}");
                }

                Debug.WriteLine("=== Verification complete ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Verification error: {ex}");
            }
        }

        private async Task TestAudioPlayback(string audioPath)
        {
            try
            {
                Debug.WriteLine("=== Testing playback immediately ===");

                // Create a simple test player
                var testStream = File.OpenRead(audioPath);
                var testPlayer = AudioManager.Current.CreatePlayer(testStream);

                Debug.WriteLine($"Test player created. Duration: {testPlayer.Duration}");
                Debug.WriteLine($"Can play: {testPlayer.CanSeek}");

                // Try to play for a moment
                testPlayer.Play();
                Debug.WriteLine("Test playback started");

                await Task.Delay(1000);

                if (testPlayer.IsPlaying)
                {
                    Debug.WriteLine("Test playback is working!");
                    testPlayer.Stop();
                }
                else
                {
                    Debug.WriteLine("Test playback stopped unexpectedly");
                }

                testPlayer.Dispose();
                testStream.Dispose();

                Debug.WriteLine("=== Test complete ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Test playback failed: {ex}");
            }
        }
        private async Task SendVoiceMessageAsync(string audioPath, int durationSeconds, string waveformData)
        {
            try
            {
                Debug.WriteLine($"\n=== SENDING VOICE MESSAGE ===");
                Debug.WriteLine($"Audio path: {audioPath}");
                Debug.WriteLine($"File exists: {File.Exists(audioPath)}");

                // ========== BLOCK CHECK (BOTH DIRECTIONS) ==========
                bool theyBlockedMe = await HasThisUserBlockedMeAsync();
                bool iBlockedThem = await HaveIBlockedThisUserAsync();

                if (theyBlockedMe)
                {
                    await DisplayAlert(
                        "Cannot Send Voice Message",
                        "You cannot send voice messages to this user because they have blocked you.",
                        "OK"
                    );
                    // Clean up the file
                    try { if (File.Exists(audioPath)) File.Delete(audioPath); } catch { }
                    return;
                }

                if (iBlockedThem)
                {
                    bool confirm = await DisplayAlert(
                        "User is Blocked",
                        "You have blocked this user. Do you still want to send this voice message?",
                        "Send Anyway",
                        "Cancel"
                    );
                    if (!confirm)
                    {
                        // Clean up the file
                        try { if (File.Exists(audioPath)) File.Delete(audioPath); } catch { }
                        return;
                    }
                }
                // ============================================================

                // Verify file exists before sending
                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine("ERROR: Audio file not found before sending!");
                    await DisplayAlert("Error", "Audio file not found. Please record again.", "OK");
                    return;
                }

                // Get file info for debugging
                var fileInfo = new FileInfo(audioPath);
                Debug.WriteLine($"File size: {fileInfo.Length} bytes");

                // Validate file size (minimum 1KB, maximum 30MB)
                if (fileInfo.Length < 1024) // Less than 1KB
                {
                    Debug.WriteLine("ERROR: Audio file too small!");
                    await DisplayAlert("Error", "Recording too short. Please record a longer message.", "OK");
                    try { File.Delete(audioPath); } catch { }
                    return;
                }

                if (fileInfo.Length > 30 * 1024 * 1024) // 30MB
                {
                    Debug.WriteLine("ERROR: Audio file too large!");
                    await DisplayAlert("Error", "Voice message too large (max 30MB).", "OK");
                    try { File.Delete(audioPath); } catch { }
                    return;
                }

                // Validate duration
                if (durationSeconds <= 0)
                {
                    durationSeconds = Math.Max(1, (int)(fileInfo.Length / 16000));
                    Debug.WriteLine($"Using estimated duration: {durationSeconds}s");
                }

                if (durationSeconds > 300) // Max 5 minutes
                {
                    await DisplayAlert("Error", "Voice message too long (max 5 minutes).", "OK");
                    try { File.Delete(audioPath); } catch { }
                    return;
                }

                // Ensure waveform data exists
                if (string.IsNullOrEmpty(waveformData))
                {
                    waveformData = GenerateSimpleWaveformData();
                }

                // Create media item for the voice message
                var mediaItem = new ChatMediaItem
                {
                    Path = audioPath,
                    Type = "audio",
                    DurationSeconds = durationSeconds,
                    WaveformData = waveformData,
                    Caption = null
                };

                // Create voice message with MediaItems properly set
                var voiceMsg = new ChatMessage
                {
                    ConversationId = _conversationId,
                    SenderPhone = _me,
                    RecipientPhone = _otherPhone,
                    Content = null,
                    MediaPath = audioPath,
                    MediaType = "audio",
                    IsVoiceMessage = true,
                    VoiceDurationSeconds = durationSeconds,
                    VoiceWaveformData = waveformData,
                    SentAt = DateTime.UtcNow,
                    IsDelivered = true,
                    IsRead = false,
                    IsLocalOutgoing = true,
                    MediaItems = new List<ChatMediaItem> { mediaItem }
                };

                // CRITICAL: Serialize MediaItems to JSON
                voiceMsg.MediaItemsJson = System.Text.Json.JsonSerializer.Serialize(voiceMsg.MediaItems);

                Debug.WriteLine($"Voice message created with MediaItems count: {voiceMsg.MediaItems.Count}");
                Debug.WriteLine($"MediaItemsJson: {voiceMsg.MediaItemsJson}");

                // Save to database
                Debug.WriteLine("Saving to database...");
                await ChatRepository.AddMessageAsync(voiceMsg);
                Debug.WriteLine($"Saved to database successfully with ID: {voiceMsg.Id}");

                // Notify ConversationsPage that a message was sent
                MessagingCenter.Send(this, "ConversationsUpdated");

                // Add to UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Messages.Add(voiceMsg);
                    Debug.WriteLine($"Added to UI. Messages count: {Messages.Count}, Message ID: {voiceMsg.Id}");
                    ScrollToBottom();
                    ResetVoiceMessageUI();
                });

                Debug.WriteLine("=== VOICE MESSAGE SENT SUCCESSFULLY ===\n");

                // IMPORTANT: DO NOT DELETE THE FILE HERE!
                // The file should remain in the voice folder for playback later
                // The preview popup only deletes if user cancels
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendVoiceMessageAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to send voice message: {ex.Message}", "OK");

                // Clean up the file on error
                try
                {
                    if (File.Exists(audioPath))
                        File.Delete(audioPath);
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"Error cleaning up file: {cleanupEx}");
                }

                // Reset UI
                ResetVoiceMessageUI();
            }
        }

        private void InitializeVoiceUIState()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var micIcon = this.FindByName<ContentView>("MicIcon");
                var giftButton = this.FindByName<ContentView>("GiftButton");
                var sendActionGrid = this.FindByName<Grid>("SendActionGrid");
                var cancelRecordingIcon = this.FindByName<ContentView>("CancelRecordingIcon");
                var messageEntry = this.FindByName<Editor>("MessageEntry");

                if (micIcon != null)
                {
                    micIcon.IsVisible = true;
                    micIcon.Scale = 1.0;

                    if (micIcon.Content is Grid grid)
                    {
                        foreach (var child in grid.Children.OfType<Microsoft.Maui.Controls.Shapes.Path>())
                        {
                            child.Fill = new SolidColorBrush(Color.FromArgb("#00B5B5"));
                            break;
                        }
                    }
                }

                // Show gift button alongside mic
                if (giftButton != null) giftButton.IsVisible = true;

                if (sendActionGrid != null) sendActionGrid.IsVisible = false;
                if (cancelRecordingIcon != null) cancelRecordingIcon.IsVisible = false;

                if (messageEntry != null)
                {
                    messageEntry.IsEnabled = true;
                    messageEntry.Placeholder = "All Messages Encrypted...";
                    messageEntry.PlaceholderColor = Color.FromArgb("#999999");
                    messageEntry.Text = string.Empty;
                }

                _isRecording = false;
                Debug.WriteLine("Voice UI initialized to default state (mic + gift visible)");
            });
        }

        // Helper method to reset UI after voice message handling
        private void ResetVoiceMessageUI()
        {
            InitializeVoiceUIState();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _isRecording = false;
                _tempRecordingPath = string.Empty;

                var micIcon = this.FindByName<ContentView>("MicIcon");
                var giftButton = this.FindByName<ContentView>("GiftButton");
                var sendActionGrid = this.FindByName<Grid>("SendActionGrid");
                var cancelRecordingIcon = this.FindByName<ContentView>("CancelRecordingIcon");
                var messageEntry = this.FindByName<Editor>("MessageEntry");

                if (micIcon != null)
                {
                    micIcon.IsVisible = true;
                    micIcon.Scale = 1.0;

                    if (micIcon.Content is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is Microsoft.Maui.Controls.Shapes.Path path)
                            {
                                path.Fill = new SolidColorBrush(Color.FromArgb("#00B5B5"));
                                break;
                            }
                        }
                    }
                }

                // Show gift button alongside mic
                if (giftButton != null) giftButton.IsVisible = true;

                if (cancelRecordingIcon != null) cancelRecordingIcon.IsVisible = false;
                if (sendActionGrid != null) sendActionGrid.IsVisible = false;

                if (messageEntry != null)
                {
                    messageEntry.IsEnabled = true;
                    messageEntry.Placeholder = "All Messages Encrypted...";
                    messageEntry.PlaceholderColor = Color.FromArgb("#999999");
                    messageEntry.Text = string.Empty;
                }
            });
        }
        private async Task DebugVoiceFiles()
        {
            try
            {
                Debug.WriteLine("\n=== VOICE FILE DEBUG ===");
                Debug.WriteLine($"Voice folder: {_voiceFolder}");
        
                if (Directory.Exists(_voiceFolder))
                {
                    var files = Directory.GetFiles(_voiceFolder, "*.wav");
                    Debug.WriteLine($"Total WAV files: {files.Length}");
            
                    foreach (var file in files)
                    {
                        var info = new FileInfo(file);
                        Debug.WriteLine($"  {Path.GetFileName(file)}: {info.Length} bytes, Modified: {info.LastWriteTime}");
                    }
                }
        
                // Check messages
                var voiceMessages = Messages.Where(m => m.IsVoice).ToList();
                Debug.WriteLine($"Voice messages in conversation: {voiceMessages.Count}");
        
                foreach (var msg in voiceMessages)
                {
                    string path = msg.MediaPath ?? msg.MediaItems?.FirstOrDefault()?.Path ?? "null";
                    bool exists = File.Exists(path);
                    Debug.WriteLine($"  Message {msg.Id}: Path={path}, Exists={exists}");
            
                    if (!exists && !string.IsNullOrEmpty(path))
                    {
                        Debug.WriteLine($"    MISSING FILE: {path}");
                    }
                }
        
                Debug.WriteLine("=== END DEBUG ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DebugVoiceFiles error: {ex}");
            }
        }

        private void UpdateMicIconForRecording(bool isRecording)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Find the mic icon ContentView
                var micIcon = this.FindByName<ContentView>("MicIcon");

                // Find the send icon grid
                var sendIcon = this.FindByName<Grid>("SendActionGrid");

                // Find the entry field
                var messageEntry = this.FindByName<Entry>("MessageEntry");

                if (isRecording)
                {
                    // Start recording animation - scale up
                    if (micIcon != null)
                    {
                        micIcon.Scale = 1.2;
                        // Change the mic icon color to red
                        var grid = micIcon.Content as Microsoft.Maui.Controls.Grid;
                        if (grid != null)
                        {
                            foreach (var element in grid.Children)
                            {
                                if (element is Microsoft.Maui.Controls.Shapes.Path path)
                                {
                                    path.Fill = new SolidColorBrush(Colors.Red);
                                    break;
                                }
                            }
                        }
                    }

                    // HIDE the send icon during recording
                    if (sendIcon != null)
                    {
                        sendIcon.IsVisible = false;
                    }
                }
                else
                {
                    // Stop recording animation
                    if (micIcon != null)
                    {
                        micIcon.Scale = 1.0;
                        // Reset to original color
                        var grid = micIcon.Content as Microsoft.Maui.Controls.Grid;
                        if (grid != null)
                        {
                            foreach (var element in grid.Children)
                            {
                                if (element is Microsoft.Maui.Controls.Shapes.Path path)
                                {
                                    path.Fill = new SolidColorBrush(Color.FromArgb("#E6E6E6"));
                                    break;
                                }
                            }
                        }
                    }

                    // Determine visibility based on entry text after recording
                    if (messageEntry != null && !string.IsNullOrWhiteSpace(messageEntry.Text))
                    {
                        // If there's text after recording, show send icon
                        if (sendIcon != null)
                            sendIcon.IsVisible = true;
                        if (micIcon != null)
                            micIcon.IsVisible = false;
                    }
                    else
                    {
                        // If no text, show mic icon
                        if (sendIcon != null)
                            sendIcon.IsVisible = false;
                        if (micIcon != null)
                            micIcon.IsVisible = true;
                    }
                }
            });
        }
        private async Task<int> GetAudioDurationAsync(string audioPath)
        {
            try
            {
                using var fs = File.OpenRead(audioPath);
                using var reader = new BinaryReader(fs);

                // Check if it's a WAV file
                string riff = new string(reader.ReadChars(4));
                if (riff != "RIFF") return 5;

                reader.ReadInt32(); // File size
                string wave = new string(reader.ReadChars(4));
                if (wave != "WAVE") return 5;

                // Find fmt chunk
                while (fs.Position < fs.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        short audioFormat = reader.ReadInt16();
                        short numChannels = reader.ReadInt16();
                        int sampleRate = reader.ReadInt32();
                        int byteRate = reader.ReadInt32();
                        short blockAlign = reader.ReadInt16();
                        short bitsPerSample = reader.ReadInt16();

                        // Skip extra format bytes if any
                        if (chunkSize > 16)
                            fs.Seek(chunkSize - 16, SeekOrigin.Current);

                        // Find data chunk
                        while (fs.Position < fs.Length)
                        {
                            string dataChunkId = new string(reader.ReadChars(4));
                            int dataSize = reader.ReadInt32();

                            if (dataChunkId == "data")
                            {
                                // Calculate duration: dataSize / (sampleRate * numChannels * bitsPerSample/8)
                                int bytesPerSecond = sampleRate * numChannels * (bitsPerSample / 8);
                                if (bytesPerSecond > 0)
                                {
                                    int duration = dataSize / bytesPerSecond;
                                    return Math.Max(1, duration);
                                }
                            }
                            else
                            {
                                fs.Seek(dataSize, SeekOrigin.Current);
                            }
                        }
                        break;
                    }
                    else
                    {
                        fs.Seek(chunkSize, SeekOrigin.Current);
                    }
                }

                return 5;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAudioDurationAsync error: {ex}");

                // Fallback: estimate from file size
                try
                {
                    var fileInfo = new FileInfo(audioPath);
                    // Rough estimate: ~16KB per second for mono 16-bit 8kHz
                    return Math.Max(1, (int)(fileInfo.Length / 16000));
                }
                catch
                {
                    return 5;
                }
            }
        }
        private async Task<int> GetWavDurationAsync(string audioPath)
        {
            try
            {
                using var fileStream = File.OpenRead(audioPath);
                using var reader = new BinaryReader(fileStream);

                // Read WAV header
                // RIFF header
                string riff = new string(reader.ReadChars(4));
                if (riff != "RIFF") return 5;

                reader.ReadInt32(); // File size - 8
                string wave = new string(reader.ReadChars(4));
                if (wave != "WAVE") return 5;

                // Find fmt chunk
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        // Audio format
                        short audioFormat = reader.ReadInt16();
                        short numChannels = reader.ReadInt16();
                        int sampleRate = reader.ReadInt32();
                        int byteRate = reader.ReadInt32();
                        short blockAlign = reader.ReadInt16();
                        short bitsPerSample = reader.ReadInt16();

                        // Skip any extra format bytes
                        if (chunkSize > 16)
                            reader.BaseStream.Seek(chunkSize - 16, SeekOrigin.Current);

                        // Find data chunk
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            string dataChunkId = new string(reader.ReadChars(4));
                            int dataSize = reader.ReadInt32();

                            if (dataChunkId == "data")
                            {
                                // Calculate duration: dataSize / (sampleRate * numChannels * bitsPerSample/8)
                                int bytesPerSecond = sampleRate * numChannels * (bitsPerSample / 8);
                                if (bytesPerSecond > 0)
                                {
                                    int durationSeconds = dataSize / bytesPerSecond;
                                    return Math.Max(1, durationSeconds);
                                }
                                break;
                            }
                            else
                            {
                                // Skip other chunks
                                reader.BaseStream.Seek(dataSize, SeekOrigin.Current);
                            }
                        }
                        break;
                    }
                    else
                    {
                        // Skip other chunks
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                    }
                }

                return 5; // Default
            }
            catch
            {
                return 5;
            }
        }

        private string GenerateSimpleWaveformData()
        {
            try
            {
                var random = new Random();
                var amplitudes = new int[40]; // 40 bars to fill container properly

                for (int i = 0; i < 40; i++)
                {
                    // Create natural speech pattern: 
                    // silence at start/end, varied middle
                    double position = (double)i / 39;

                    // Speech envelope - rises and falls naturally
                    double envelope = Math.Sin(position * Math.PI);

                    // Add multiple frequency components like real speech
                    double wave1 = Math.Sin(position * Math.PI * 3) * 0.3;
                    double wave2 = Math.Sin(position * Math.PI * 7) * 0.2;
                    double wave3 = Math.Sin(position * Math.PI * 13) * 0.15;

                    // Combine and add randomness
                    double combined = envelope + wave1 + wave2 + wave3;
                    combined = Math.Max(0, combined);

                    // Scale to 0-100 with minimum visibility
                    int amplitude = (int)(15 + (combined * 55) + (random.NextDouble() * 20));
                    amplitudes[i] = Math.Clamp(amplitude, 8, 95);
                }

                return System.Text.Json.JsonSerializer.Serialize(amplitudes);
            }
            catch
            {
                return "[20,35,50,65,80,70,60,75,85,70,55,40,60,75,85,90,80,65,50,70,85,75,60,45,65,80,70,55,40,55,70,80,65,50,35,45,60,45,30,20]";
            }
        }

        private const double WaveformWidth = 200.0;
        private double _panStartProgress = 0;
        private bool _wasPausedForScrub = false;

        private void OnWaveformTapped_Incoming(object sender, TappedEventArgs e)
        {
            try
            {
                var pos = e.GetPosition((View)sender);
                if (pos == null) return;
                // Use the sender's actual width for accurate scrubbing
                double containerWidth = (sender as View)?.Width ?? _waveformContainerWidth;
                double progress = Math.Clamp(pos.Value.X / containerWidth, 0, 1);
                SeekActivePlayer(progress);
            }
            catch (Exception ex) { Debug.WriteLine($"Tap incoming error: {ex}"); }
        }


        private void OnWaveformTapped_Outgoing(object sender, TappedEventArgs e)
        {
            try
            {
                var pos = e.GetPosition((View)sender);
                if (pos == null) return;
                double containerWidth = (sender as View)?.Width ?? _waveformContainerWidth;
                double progress = Math.Clamp(pos.Value.X / containerWidth, 0, 1);
                SeekActivePlayer(progress);
            }
            catch (Exception ex) { Debug.WriteLine($"Tap outgoing error: {ex}"); }
        }
        private void OnWaveformPanUpdated_Incoming(object sender, PanUpdatedEventArgs e)
            => HandleWaveformPan(e);

        private void OnWaveformPanUpdated_Outgoing(object sender, PanUpdatedEventArgs e)
            => HandleWaveformPan(e);

        private void HandleWaveformPan(PanUpdatedEventArgs e)
        {
            // Same fix — use _waveformContainerWidth instead of WaveformWidth
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _panStartProgress = _currentlyPlayingMessage?.VoicePlaybackProgress ?? 0;
                    _wasPausedForScrub = false;
                    if (_currentPlayer?.IsPlaying == true)
                    {
                        _currentPlayer.Pause();
                        _wasPausedForScrub = true;
                    }
                    break;

                case GestureStatus.Running:
                    double runningDelta = e.TotalX / _waveformContainerWidth;  // ? fix
                    double runningProgress = Math.Clamp(_panStartProgress + runningDelta, 0, 1);
                    UpdateScrubUI(runningProgress);
                    break;

                case GestureStatus.Completed:
                    double finalDelta = e.TotalX / _waveformContainerWidth;  // ? fix
                    double finalProgress = Math.Clamp(_panStartProgress + finalDelta, 0, 1);
                    SeekActivePlayer(finalProgress);
                    if (_wasPausedForScrub && _currentPlayer != null)
                    {
                        _currentPlayer.Play();
                        _wasPausedForScrub = false;
                    }
                    break;

                case GestureStatus.Canceled:
                    UpdateScrubUI(_panStartProgress);
                    if (_wasPausedForScrub && _currentPlayer != null)
                    {
                        _currentPlayer.Play();
                        _wasPausedForScrub = false;
                    }
                    break;
            }
        }
        private void UpdateScrubUI(double progress)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_currentlyPlayingMessage == null) return;
                    var mediaItem = _currentlyPlayingMessage.MediaItems?.FirstOrDefault();
                    if (mediaItem == null) return;

                    _currentlyPlayingMessage.VoicePlaybackProgress = progress;
                    mediaItem.PlaybackProgress = progress;

                    // Update countdown display during scrub
                    if (mediaItem.DurationSeconds.HasValue)
                    {
                        double remaining = mediaItem.DurationSeconds.Value * (1 - progress);
                        var span = TimeSpan.FromSeconds(Math.Max(0, remaining));
                        mediaItem.CurrentDisplayDuration = span.TotalMinutes >= 1
                            ? $"{(int)span.TotalMinutes}:{span.Seconds:D2}"
                            : $"0:{span.Seconds:D2}";
                    }

                    mediaItem.OnPropertyChanged(nameof(ChatMediaItem.PlaybackProgress));
                    mediaItem.OnPropertyChanged(nameof(ChatMediaItem.DisplayDuration));
                    mediaItem.OnPropertyChanged(nameof(ChatMediaItem.CurrentDisplayDuration));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UpdateScrubUI error: {ex}");
                }
            });
        }

        private void SeekActivePlayer(double progress)
        {
            try
            {
                if (_currentPlayer == null)
                {
                    Debug.WriteLine("SeekActivePlayer: no active player");
                    return;
                }

                double duration = _currentPlayer.Duration;
                if (duration <= 0)
                {
                    Debug.WriteLine("SeekActivePlayer: duration is 0");
                    return;
                }

                double seekTo = progress * duration;

                if (_currentPlayer.CanSeek)
                {
                    _currentPlayer.Seek(seekTo);
                    Debug.WriteLine($"Seeked to {progress:P0} = {seekTo:F2}s of {duration:F2}s");
                }
                else
                {
                    Debug.WriteLine("Player does not support seeking");
                }

                UpdateScrubUI(progress);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SeekActivePlayer error: {ex}");
            }
        }

        private async void PhoneCallIcon_Tapped(object sender, EventArgs e)
        {
            try
            {
                // Get the phone number - this is the user's phone number they registered with
                string phoneNumber = _otherPhone?.Trim();

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    // Fallback to the name label if phone number is missing
                    phoneNumber = OtherNameLabel?.Text?.Trim();
                }

                // Validate we have a phone number
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    await DisplayAlert("Error", "Phone number not available for this user", "OK");
                    return;
                }

                // Show confirmation dialog
                bool confirm = await DisplayAlert(
                    "Call User",
                    $"Call {OtherNameLabel?.Text ?? phoneNumber}?\n\nPhone: {phoneNumber}",
                    "Call",
                    "Cancel");

                if (confirm)
                {
                    // Check if dialer is supported on this device
                    if (PhoneDialer.IsSupported)
                    {
                        // Open the native phone dialer with the number pre-filled
                        PhoneDialer.Open(phoneNumber);
                    }
                    else
                    {
                        await DisplayAlert("Not Supported", "Phone dialer is not available on this device", "OK");
                    }
                }
            }
            catch (FeatureNotSupportedException)
            {
                await DisplayAlert("Not Supported", "Phone dialer is not supported on this device", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Phone call error: {ex}");
                await DisplayAlert("Error", $"Could not initiate call: {ex.Message}", "OK");
            }
        }

        private async void VideoCallIcon_Tapped(object sender, EventArgs e)
        {
            // For now, just show a message that video calls are coming soon
            await DisplayAlert(
                "Video Calls",
                "Video calling feature is coming soon!\n\nFor now, you can use WhatsApp or your preferred video calling app.",
                "OK");
        }

        // Add these properties to ChatPage class
        private List<ChatMediaItem> _fullScreenImages = new();
        private int _currentImageIndex = 0;

        public List<ChatMediaItem> FullScreenImages
        {
            get => _fullScreenImages;
            set
            {
                _fullScreenImages = value;
                OnPropertyChanged();
            }
        }

        // Add this Command property
        public Command<ChatMediaItem> ImageTapCommand => new Command<ChatMediaItem>(async (mediaItem) =>
        {
            if (mediaItem == null) return;
            await OpenFullScreenImageAsync(mediaItem);
        });

        // Add these methods
        private async Task OpenFullScreenImageAsync(ChatMediaItem tappedImage)
        {
            try
            {
                var message = Messages.FirstOrDefault(m => m.MediaItems?.Contains(tappedImage) == true);
                if (message == null || message.MediaItems == null) return;

                // Get all image items from this message
                var imageItems = message.MediaItems.Where(m => m.Type == "image").ToList();
                if (!imageItems.Any()) return;

                // Get the actual file paths
                var imagePaths = imageItems.Select(m => m.Path).ToList();

                // Find the index of the tapped image
                int startIndex = imageItems.IndexOf(tappedImage);

                // Create and navigate to the full screen media page
                var fullScreenPage = new FullScreenMediaPage(imagePaths, startIndex);

                // Use PushModalAsync for full-screen modal presentation
                await Navigation.PushModalAsync(fullScreenPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenFullScreenImageAsync error: {ex}");
                await DisplayAlert("Error", "Could not open image viewer", "OK");
            }
        }
        private void UpdateFullScreenImageDisplay()
        {
            var collection = this.FindByName<CollectionView>("FullScreenImageCollection");
            var counterLabel = this.FindByName<Label>("ImageCounterLabel");

            if (collection != null && _fullScreenImages.Count > 0)
            {
                // Scroll to the current image with animation
                collection.ScrollTo(_fullScreenImages[_currentImageIndex],
                    position: ScrollToPosition.Center,
                    animate: true);
            }

            if (counterLabel != null)
                counterLabel.Text = $"{_currentImageIndex + 1} / {_fullScreenImages.Count}";
        }

        private void CloseFullScreenImage_Clicked(object sender, EventArgs e)
        {
            var overlay = this.FindByName<Grid>("FullScreenImageOverlay");
            if (overlay != null)
                overlay.IsVisible = false;
        }

        private void FullScreenOverlay_Tapped(object sender, EventArgs e)
        {
            CloseFullScreenImage_Clicked(sender, e);
        }

        private async void PreviousImage_Clicked(object sender, EventArgs e)
        {
            if (_fullScreenImages.Count == 0) return;
            _currentImageIndex = (_currentImageIndex - 1 + _fullScreenImages.Count) % _fullScreenImages.Count;
            UpdateFullScreenImageDisplay();
        }

        private async void NextImage_Clicked(object sender, EventArgs e)
        {
            if (_fullScreenImages.Count == 0) return;
            _currentImageIndex = (_currentImageIndex + 1) % _fullScreenImages.Count;
            UpdateFullScreenImageDisplay();
        }


        private async void DownloadImage_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Get the current image from the full-screen collection
                if (_fullScreenImages == null || _fullScreenImages.Count == 0 ||
                    _currentImageIndex < 0 || _currentImageIndex >= _fullScreenImages.Count)
                {
                    await DisplayAlert("Error", "No image to save", "OK");
                    return;
                }

                var currentImage = _fullScreenImages[_currentImageIndex];
                string imagePath = currentImage.Path;

                if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
                {
                    await DisplayAlert("Error", "Image file not found", "OK");
                    return;
                }

                var saveOption = await this.DisplayActionSheet(
                    "Save Image To",
                    "Cancel",
                    null,
                    "Pictures Folder",
                    "Downloads Folder",
                    "Choose Custom Folder"
                );

                if (saveOption == "Cancel" || saveOption == null)
                    return;

                string destinationFolder = "";
                string appFolderName = "LockChat";

                switch (saveOption)
                {
                    case "Pictures Folder":
                        destinationFolder = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            appFolderName);
                        break;

                    case "Downloads Folder":
                        destinationFolder = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads",
                            appFolderName);
                        break;

                    case "Choose Custom Folder":
                        try
                        {
                            // FIXED: Use IFolderPicker instead of IMyFolderPicker
                            var folderPicker = DependencyService.Get<IFolderPicker>();
                            if (folderPicker != null)
                            {
                                destinationFolder = await folderPicker.PickFolder();
                                if (string.IsNullOrEmpty(destinationFolder))
                                {
                                    await this.DisplayAlert("Info", "No folder selected", "OK");
                                    return;
                                }
                            }
                            else
                            {
                                await this.DisplayAlert("Error",
                                    "Folder picker not available. Please use Pictures or Downloads folder.",
                                    "OK");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            await this.DisplayAlert("Error",
                                $"Folder picker error: {ex.Message}",
                                "OK");
                            return;
                        }
                        break;
                }

                // Create directory if it doesn't exist
                if (!Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);

                // Generate unique filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string extension = System.IO.Path.GetExtension(imagePath) ?? ".jpg";
                string fileName = $"chat_image_{timestamp}{extension}";
                string destPath = System.IO.Path.Combine(destinationFolder, fileName);

                // Copy the file
                System.IO.File.Copy(imagePath, destPath, true);

                await this.DisplayAlert("Success", "Image saved successfully!", "OK");
            }
            catch (Exception ex)
            {
                await this.DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"DownloadImage_Clicked error: {ex}");
            }
        }

        private async Task DebugVoiceFilePaths()
        {
            try
            {
                Debug.WriteLine("\n=== VOICE FILE PATH DEBUG ===");

                var voiceMessages = Messages.Where(m => m.IsVoice).ToList();
                Debug.WriteLine($"Found {voiceMessages.Count} voice messages");

                foreach (var msg in voiceMessages)
                {
                    Debug.WriteLine($"Message ID: {msg.Id}");
                    Debug.WriteLine($"  MediaPath: {msg.MediaPath ?? "null"}");
                    Debug.WriteLine($"  IsVoiceMessage: {msg.IsVoiceMessage}");

                    if (msg.MediaItems != null && msg.MediaItems.Count > 0)
                    {
                        Debug.WriteLine($"  MediaItems count: {msg.MediaItems.Count}");
                        foreach (var item in msg.MediaItems)
                        {
                            Debug.WriteLine($"    Item Path: {item.Path}");
                            Debug.WriteLine($"    Item Type: {item.Type}");
                            Debug.WriteLine($"    File Exists: {File.Exists(item.Path)}");
                        }
                    }

                    // Check if file exists in the correct folder
                    string path = msg.MediaPath ?? msg.MediaItems?.FirstOrDefault()?.Path ?? "";
                    if (!string.IsNullOrEmpty(path))
                    {
                        bool exists = File.Exists(path);
                        Debug.WriteLine($"  File exists: {exists}");

                        if (exists)
                        {
                            var info = new FileInfo(path);
                            Debug.WriteLine($"  File size: {info.Length} bytes");
                            Debug.WriteLine($"  File folder: {Path.GetDirectoryName(path)}");
                            Debug.WriteLine($"  Expected folder: {_voiceFolder}");
                            Debug.WriteLine($"  In correct folder: {path.StartsWith(_voiceFolder, StringComparison.OrdinalIgnoreCase)}");
                        }
                        else
                        {
                            Debug.WriteLine($"  ? FILE MISSING: {path}");

                            // Try to find the file in the correct folder
                            string fileName = Path.GetFileName(path);
                            string correctPath = Path.Combine(_voiceFolder, fileName);
                            if (File.Exists(correctPath))
                            {
                                Debug.WriteLine($"  ? Found file in correct folder: {correctPath}");
                            }
                        }
                    }
                }

                Debug.WriteLine("=== END VOICE FILE PATH DEBUG ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DebugVoiceFilePaths error: {ex}");
            }
        }

        private async void OnSaveContactTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is ChatMessage message)
                {
                    // Create contact info
                    string contactName = message.ContactName ?? "Unknown";
                    string contactPhone = message.ContactPhone ?? "";

                    if (string.IsNullOrEmpty(contactPhone))
                    {
                        await DisplayAlert("Error", "Phone number not available for this contact", "OK");
                        return;
                    }

                    // Show options for saving the contact
                    string[] options = {
                "Save to device contacts",
                "Copy to clipboard",
                "Share contact",
                "Cancel"
            };

                    string selected = await DisplayActionSheet(
                        $"Save {contactName}",
                        "Cancel",
                        null,
                        options
                    );

                    if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                        return;

                    switch (selected)
                    {
                        case "Save to device contacts":
                            await SaveContactToDeviceAsync(contactName, contactPhone, message.ContactProfileImage);
                            break;

                        case "Copy to clipboard":
                            await Clipboard.Default.SetTextAsync($"{contactName}: {contactPhone}");
                            await DisplayAlert("Copied", "Contact information copied to clipboard", "OK");
                            break;

                        case "Share contact":
                            await ShareContactAsync(contactName, contactPhone, message.ContactProfileImage);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSaveContactTapped error: {ex}");
                await DisplayAlert("Error", $"Could not save contact: {ex.Message}", "OK");
            }
        }

        // Add these helper methods as well
        private async Task SaveContactToDeviceAsync(string name, string phone, string? profileImagePath = null)
        {
            try
            {
                // Create a vCard string (cross-platform approach)
                string vCard = $@"BEGIN:VCARD
VERSION:3.0
FN:{name}
TEL:{phone}
END:VCARD";

                // Save to temporary file
                string fileName = $"{name.Replace(" ", "_")}_{DateTime.Now:yyyyMMddHHmmss}.vcf";
                string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllTextAsync(filePath, vCard);

                // Share the vCard file - this will open the contacts app on most platforms
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Save {name} to contacts",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveContactToDeviceAsync error: {ex}");

                // Fallback - just copy to clipboard
                await Clipboard.Default.SetTextAsync($"{name}: {phone}");
                await DisplayAlert("Info", "Contact information copied to clipboard instead", "OK");
            }
        }

        private async Task ShareContactAsync(string name, string phone, string? profileImagePath = null)
        {
            try
            {
                string[] shareOptions = {
            "Share as text",
            "Share as vCard",
            "Share via WhatsApp",
            "Share via SMS",
            "Share via Email",
            "Cancel"
        };

                string selected = await DisplayActionSheet(
                    $"Share {name}",
                    "Cancel",
                    null,
                    shareOptions
                );

                if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                    return;

                switch (selected)
                {
                    case "Share as text":
                        await Share.Default.RequestAsync(new ShareTextRequest
                        {
                            Text = $"{name}: {phone}",
                            Title = "Share Contact"
                        });
                        break;

                    case "Share as vCard":
                        string vCard = $@"BEGIN:VCARD
VERSION:3.0
FN:{name}
TEL:{phone}
END:VCARD";

                        string fileName = $"{name.Replace(" ", "_")}_{DateTime.Now:yyyyMMddHHmmss}.vcf";
                        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                        await File.WriteAllTextAsync(filePath, vCard);

                        await Share.Default.RequestAsync(new ShareFileRequest
                        {
                            Title = "Share vCard",
                            File = new ShareFile(filePath)
                        });
                        break;

                    case "Share via WhatsApp":
                        string whatsappText = $"{name}: {phone}";
                        var whatsappUri = $"whatsapp://send?text={Uri.EscapeDataString(whatsappText)}";

                        try
                        {
                            await Launcher.Default.OpenAsync(whatsappUri);
                        }
                        catch
                        {
                            await Launcher.Default.OpenAsync($"https://wa.me/?text={Uri.EscapeDataString(whatsappText)}");
                        }
                        break;

                    case "Share via SMS":
                        var smsUri = $"sms:?body={Uri.EscapeDataString($"{name}: {phone}")}";
                        await Launcher.Default.OpenAsync(smsUri);
                        break;

                    case "Share via Email":
                        var emailUri = $"mailto:?subject={Uri.EscapeDataString($"Contact: {name}")}&body={Uri.EscapeDataString($"{name}\n{phone}")}";
                        await Launcher.Default.OpenAsync(emailUri);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShareContactAsync error: {ex}");
                await DisplayAlert("Error", $"Could not share contact: {ex.Message}", "OK");
            }
        }


        private async void OnSharePostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is ChatMessage message && message.MessageType == "post")
                {
                    string postAuthor = message.PostAuthor ?? "Unknown";
                    string postContent = message.Content ?? "";
                    int postId = message.PostId ?? 0;
                    string postPreview = message.PostPreview ?? "";

                    // Show options for sharing the post
                    string[] options = {
                "Forward to contact",
                "Share via social media",
                "Copy link",
                "Share as text",
                "Cancel"
            };

                    string selected = await DisplayActionSheet(
                        $"Share post from {postAuthor}",
                        "Cancel",
                        null,
                        options
                    );

                    if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                        return;

                    switch (selected)
                    {
                        case "Forward to contact":
                            await ForwardPostToContactAsync(message);
                            break;

                        case "Share via social media":
                            await SharePostViaSocialMediaAsync(message);
                            break;

                        case "Copy link":
                            string postLink = $"https://lockapp.com/post/{postId}";
                            await Clipboard.Default.SetTextAsync(postLink);
                            await DisplayAlert("Copied", "Post link copied to clipboard", "OK");
                            break;

                        case "Share as text":
                            string shareText = $"Post from {postAuthor}\n\n{postContent}\n\nShared from Lock App";
                            await Share.Default.RequestAsync(new ShareTextRequest
                            {
                                Text = shareText,
                                Title = "Share Post"
                            });
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSharePostTapped error: {ex}");
                await DisplayAlert("Error", $"Could not share post: {ex.Message}", "OK");
            }
        }

        private async Task ForwardPostToContactAsync(ChatMessage postMessage)
        {
            try
            {
                // Create a contact picker popup for forwarding
                var contactPicker = new ContactPickerPopup(
                    postMessage.PostAuthor ?? "Unknown",
                    postMessage.RecipientPhone ?? string.Empty,
                    postMessage.MediaPath ?? "default_profile.png",
                    async (targetPhone, targetName, targetProfileImage) =>
                    {
                        string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                        string targetConversationId = await GetOrCreateConversationAsync(
                            currentUserPhone,
                            targetPhone,
                            targetName
                        );

                        await ForwardPostAsMessageAsync(postMessage, targetPhone, targetConversationId, targetName);
                    }
                );

                await this.ShowPopupAsync(contactPicker);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ForwardPostToContactAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to forward post: {ex.Message}", "OK");
            }
        }

        private async Task ForwardPostAsMessageAsync(ChatMessage originalPost, string targetUserPhone, string targetConversationId, string targetUserName)
        {
            try
            {
                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                // Create a forwarded post message
                var forwardedPost = new ChatMessage
                {
                    ConversationId = targetConversationId,
                    SenderPhone = currentUserPhone,
                    RecipientPhone = targetUserPhone,
                    MessageType = "post",
                    Content = originalPost.Content,
                    PostId = originalPost.PostId,
                    PostAuthor = originalPost.PostAuthor,
                    PostPreview = originalPost.PostPreview,
                    PostImageCount = originalPost.PostImageCount ?? 0,
                    SentAt = DateTime.UtcNow,
                    IsDelivered = true,
                    IsRead = false,
                    IsLocalOutgoing = true,
                    IsEncrypted = false,
                    WillDisappear = false,
                    IsDisappearingMessage = false,
                    DisappearAfterSeconds = 0
                };

                // If original post has images, copy the first image as preview
                if (!string.IsNullOrEmpty(originalPost.MediaPath))
                {
                    forwardedPost.MediaPath = originalPost.MediaPath;
                }

                // Save to database
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                await db.InsertAsync(forwardedPost);

                // Update conversation last message
                var conversation = await db.Table<Conversation>()
                    .Where(c => c.ConversationId == targetConversationId)
                    .FirstOrDefaultAsync();

                if (conversation != null)
                {
                    conversation.LastMessagePreview = $"?? Forwarded post: {originalPost.PostPreview}";
                    conversation.LastMessageAt = DateTime.UtcNow;
                    conversation.LastMessageType = "post";
                    await db.UpdateAsync(conversation);
                }

                // Notify that messages have been updated
                MessagingCenter.Send(this, "MessagesUpdated");
                MessagingCenter.Send(this, "ConversationsUpdated");

                await DisplayAlert(
                    "Post Forwarded",
                    $"Post has been forwarded to {targetUserName}.",
                    "OK"
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ForwardPostAsMessageAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to forward post: {ex.Message}", "OK");
            }
        }

        private async Task SharePostViaSocialMediaAsync(ChatMessage postMessage)
        {
            try
            {
                string[] socialOptions = {
            "WhatsApp",
            "Telegram",
            "Facebook",
            "Twitter / X",
            "Instagram",
            "Messages (SMS)",
            "Email",
            "More...",
            "Cancel"
        };

                string selected = await DisplayActionSheet(
                    "Share post via",
                    "Cancel",
                    null,
                    socialOptions
                );

                if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                    return;

                string shareText = $"Check out this post from {postMessage.PostAuthor}\n\n";

                if (!string.IsNullOrEmpty(postMessage.Content))
                {
                    shareText += $"\"{postMessage.Content}\"\n\n";
                }

                if (postMessage.PostImageCount > 0)
                {
                    shareText += $"?? {postMessage.PostImageCount} image(s)\n\n";
                }

                shareText += $"Shared from Lock App";

                string postLink = $"https://lockapp.com/post/{postMessage.PostId}";
                string fullShareText = $"{shareText}\n\n{postLink}";

                switch (selected)
                {
                    case "WhatsApp":
                        var whatsappUri = $"whatsapp://send?text={Uri.EscapeDataString(fullShareText)}";
                        await Launcher.Default.OpenAsync(whatsappUri);
                        break;

                    case "Telegram":
                        var telegramUri = $"tg://msg?text={Uri.EscapeDataString(fullShareText)}";
                        await Launcher.Default.OpenAsync(telegramUri);
                        break;

                    case "Facebook":
                        var facebookUri = $"fb://facewebmodal/f?href=https://facebook.com/sharer.php?u={Uri.EscapeDataString(postLink)}&quote={Uri.EscapeDataString(shareText)}";
                        await Launcher.Default.OpenAsync(facebookUri);
                        break;

                    case "Twitter / X":
                        var twitterUri = $"twitter://post?message={Uri.EscapeDataString(fullShareText)}";
                        await Launcher.Default.OpenAsync(twitterUri);
                        break;

                    case "Instagram":
                        await Clipboard.Default.SetTextAsync(fullShareText);
                        bool openInstagram = await DisplayAlert(
                            "Share to Instagram",
                            "Post information copied to clipboard.\n\nOpen Instagram to paste in story or DM?",
                            "Open Instagram",
                            "Cancel"
                        );
                        if (openInstagram)
                        {
                            await Launcher.Default.OpenAsync("instagram://");
                        }
                        break;

                    case "Messages (SMS)":
                        var smsUri = $"sms:?body={Uri.EscapeDataString(fullShareText)}";
                        await Launcher.Default.OpenAsync(smsUri);
                        break;

                    case "Email":
                        var emailUri = $"mailto:?subject={Uri.EscapeDataString($"Post from {postMessage.PostAuthor}")}&body={Uri.EscapeDataString(fullShareText)}";
                        await Launcher.Default.OpenAsync(emailUri);
                        break;

                    case "More...":
                        await Share.Default.RequestAsync(new ShareTextRequest
                        {
                            Text = fullShareText,
                            Title = "Share Post"
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SharePostViaSocialMediaAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to share: {ex.Message}", "OK");
            }
        }

        private async void OnViewPostTapped(object sender, TappedEventArgs e)
        {
            try
            {
                ChatMessage message = null;

                if (e.Parameter is ChatMessage paramMsg)
                    message = paramMsg;
                else if (sender is VisualElement ve && ve.BindingContext is ChatMessage bindingMsg)
                    message = bindingMsg;

                if (message == null || message.MessageType != "post")
                {
                    await DisplayAlert("Error", "Could not identify post", "OK");
                    return;
                }

                Debug.WriteLine($"View post tapped - PostId: {message.PostId}, MessageType: {message.MessageType}");

                if (!message.PostId.HasValue || message.PostId.Value <= 0)
                {
                    await DisplayAlert("Post Not Found",
                        "This post ID is missing. It may have been deleted.", "OK");
                    return;
                }

                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await DisplayAlert("Error", "Please log in to view posts", "OK");
                    return;
                }

                // Verify the post still exists
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var post = await db.Table<Lock.Models.Post>()
                    .Where(p => p.Id == message.PostId.Value)
                    .FirstOrDefaultAsync();

                if (post == null)
                {
                    await DisplayAlert("Post Not Found",
                        "This post may have been deleted or is no longer available.", "OK");
                    return;
                }

                var commentsPage = new Lock.Pages.Post.CommentsPage(message.PostId.Value, currentUserPhone);
                await Navigation.PushAsync(commentsPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnViewPostTapped error: {ex}");
                await DisplayAlert("Error", $"Could not open post: {ex.Message}", "OK");
            }
        }
        private async void PlayVoiceMessage_Clicked(object sender, EventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.CommandParameter is ChatMessage message)
                {
                    var mediaItem = message.MediaItems?.FirstOrDefault();
                    if (mediaItem != null)
                    {
                        // Call your existing PlayVoiceCommand logic
                        if (PlayVoiceCommand != null && PlayVoiceCommand.CanExecute(mediaItem))
                        {
                            PlayVoiceCommand.Execute(mediaItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayVoiceMessage_Clicked error: {ex}");
                await DisplayAlert("Error", "Failed to play voice message", "OK");
            }
        }

        private async void OnSharePostFromMessageTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is ChatMessage message && message.MessageType == "post")
                {
                    // Get current user phone
                    string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                    if (string.IsNullOrEmpty(currentUserPhone))
                    {
                        await DisplayAlert("Error", "Please log in to share posts", "OK");
                        return;
                    }

                    // Create a Post object from the message data
                    var post = new Lock.Models.Post
                    {
                        Id = message.PostId ?? 0,
                        AuthorDisplayName = message.PostAuthor ?? "Unknown",
                        AuthorPhone = message.RecipientPhone ?? string.Empty,
                        Content = message.Content ?? "",
                        ImagePathsList = message.MediaItems?.Select(m => m.Path).ToArray() ?? Array.Empty<string>(),
                        AuthorProfileImagePath = message.MediaPath
                    };

                    // Show the PostSharePopup
                    var sharePopup = new PostSharePopup(post, currentUserPhone);
                    await this.ShowPopupAsync(sharePopup);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSharePostFromMessageTapped error: {ex}");
                await DisplayAlert("Error", $"Could not share post: {ex.Message}", "OK");
            }
        }

        // Add this helper method to get or create a conversation
        private async Task<string> GetOrCreateConversationAsync(string userPhone, string contactPhone, string contactName)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if conversation already exists
                var existingConversation = await db.Table<Conversation>()
                    .Where(c => (c.ParticipantA == userPhone && c.ParticipantB == contactPhone) ||
                               (c.ParticipantA == contactPhone && c.ParticipantB == userPhone))
                    .FirstOrDefaultAsync();

                if (existingConversation != null)
                    return existingConversation.ConversationId;

                // Create new conversation
                string conversationId = Guid.NewGuid().ToString();
                var conversation = new Conversation
                {
                    ConversationId = conversationId,
                    ParticipantA = userPhone,
                    ParticipantB = contactPhone,
                    LastMessageAt = DateTime.UtcNow,
                    LastMessagePreview = "",
                    CreatedAt = DateTime.UtcNow
                };

                await db.InsertAsync(conversation);
                return conversationId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrCreateConversationAsync error: {ex}");
                throw;
            }
        }

        // ?? Gift button tapped ?????????????????????????????????????????????
        private async void GiftButton_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                bool theyBlockedMe = await HasThisUserBlockedMeAsync();
                bool iBlockedThem = await HaveIBlockedThisUserAsync();

                if (theyBlockedMe)
                {
                    await DisplayAlert("Cannot Send Gift",
                        "You cannot send gifts to this user because they have blocked you.", "OK");
                    return;
                }

                if (iBlockedThem)
                {
                    bool confirm = await DisplayAlert("User is Blocked",
                        "You have blocked this user. Send a gift anyway?",
                        "Send Anyway", "Cancel");
                    if (!confirm) return;
                }

                var picker = new GiftPickerPopup();
                picker.GiftSelected += async (_, gift) => await SendGiftAsync(gift);
                await this.ShowPopupAsync(picker);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GiftButton_Tapped error: {ex}");
            }
        }

        // ?? Save gift to DB and show burst animation ???????????????????????
        private async Task SendGiftAsync(GiftDefinition gift)
        {
            try
            {
                // Build the message first
                var msg = new ChatMessage
                {
                    ConversationId = _conversationId,
                    SenderPhone = _me,
                    RecipientPhone = _otherPhone,
                    MessageType = "gift",
                    Content = gift.Id,       // store gift Id e.g. "diamond"
                    SentAt = DateTime.UtcNow,
                    IsDelivered = true,
                    IsRead = false,
                    IsLocalOutgoing = true,
                    MediaItemsJson = "[]"
                };

                // Add to UI immediately
                Messages.Add(msg);
                ScrollToBottom();

                // Save to database
                await ChatRepository.AddMessageAsync(msg);
                MessagingCenter.Send(this, "ConversationsUpdated");

                // Play burst animation over the whole page
                var rootGrid = this.FindByName<Grid>("ChatRootGrid");
                if (rootGrid != null)
                {
                    var burst = new GiftBurstOverlay(gift);
                    rootGrid.Add(burst);
                    await burst.RunAndRemoveAsync(rootGrid);
                }

                Debug.WriteLine($"Gift sent: {gift.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendGiftAsync error: {ex}");
            }
        }

        private async void OtherProfileImage_Tapped(object sender, EventArgs e)
        {
            try
            {
                var phone = _otherPhone?.Trim();
                if (string.IsNullOrEmpty(phone))
                {
                    var lbl = this.FindByName
                                                                                                                                                                                                                        <Label>("OtherNameLabel");
                    if (lbl != null && !string.IsNullOrWhiteSpace(lbl.Text))
                        phone = lbl.Text.Trim();
                }

                if (string.IsNullOrEmpty(phone))
                {
                    await DisplayAlert("Navigation", "Other user's phone is not available.", "OK");
                    return;
                }

                var route = $"//profile?phone={Uri.EscapeDataString(phone)}&viewOnly=true";
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OtherProfileImage_Tapped error: " + ex);
                await DisplayAlert("Navigation error", ex.Message, "OK");
            }
        }

    }
}
