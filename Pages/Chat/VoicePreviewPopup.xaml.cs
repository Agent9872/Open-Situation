using CommunityToolkit.Maui.Views;
using Plugin.Maui.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Core;

namespace Lock.Pages.Chat
{
    public partial class VoicePreviewPopup : Popup
    {
        private readonly string _audioPath;
        private readonly int _durationSeconds;
        private readonly List<int> _waveformData;

        private IAudioPlayer? _player;
        private Stream? _audioStream;
        private CancellationTokenSource? _progressCts;
        private bool _isPlaying;
        private bool _isCleanedUp;

        public event EventHandler<bool>? OnSend;

        public VoicePreviewPopup(string audioPath, int durationSeconds, string waveformJson)
        {
            InitializeComponent();

            _audioPath = audioPath;
            _durationSeconds = durationSeconds;
            _waveformData = ParseWaveformData(waveformJson);

            // Show total duration at start
            var totalSpan = TimeSpan.FromSeconds(_durationSeconds);
            DurationLabel.Text = totalSpan.ToString(@"mm\:ss");
            DurationLabel.TextColor = Color.FromArgb("#E6E6E6");

            // Set end time label
            EndTimeLabel.Text = totalSpan.ToString(@"mm\:ss");


            this.Closed += OnPopupClosed;
        }

        private List<int> ParseWaveformData(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                    return GenerateDefaultWaveform();

                var data = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
                return data ?? GenerateDefaultWaveform();
            }
            catch
            {
                return GenerateDefaultWaveform();
            }
        }

        private List<int> GenerateDefaultWaveform()
        {
            var random = new Random();
            var result = new List<int>();
            for (int i = 0; i < 30; i++)
                result.Add(random.Next(15, 45));
            return result;
        }

        private async void OnPlayPauseClicked(object sender, EventArgs e)
        {
            try
            {
                if (_isPlaying)
                {
                    // Pause
                    _player?.Pause();
                    _isPlaying = false;
                    PlayIcon.IsVisible = true;
                    PauseIcon.IsVisible = false;
                    _progressCts?.Cancel();

                    // Show total duration when paused
                    var totalSpan = TimeSpan.FromSeconds(_durationSeconds);
                    DurationLabel.Text = totalSpan.ToString(@"mm\:ss");
                    DurationLabel.TextColor = Color.FromArgb("#E6E6E6");
                }
                else
                {
                    if (_player == null)
                    {
                        // Create new player
                        _audioStream = File.OpenRead(_audioPath);
                        _player = AudioManager.Current.CreatePlayer(_audioStream);

                        _player.PlaybackEnded += (s, ev) =>
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                _isPlaying = false;
                                PlayIcon.IsVisible = true;
                                PauseIcon.IsVisible = false;
                                ProgressBar.WidthRequest = 0;
                                StartTimeLabel.Text = "0:00";

                                // Reset to total duration display
                                var totalSpan = TimeSpan.FromSeconds(_durationSeconds);
                                DurationLabel.Text = totalSpan.ToString(@"mm\:ss");
                                DurationLabel.TextColor = Color.FromArgb("#E6E6E6");

                                _progressCts?.Cancel();
                            });
                        };
                    }

                    _player.Play();
                    _isPlaying = true;
                    PlayIcon.IsVisible = false;
                    PauseIcon.IsVisible = true;

                    // Start countdown immediately
                    _progressCts?.Cancel();
                    _progressCts = new CancellationTokenSource();
                    _ = TrackProgressAsync(_progressCts.Token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayPause error: {ex}");
            }
        }

        private async Task TrackProgressAsync(CancellationToken token)
        {
            try
            {
                // Wait a tiny bit for the player to start and layout to measure
                await Task.Delay(150, token);

                while (!token.IsCancellationRequested && _player != null)
                {
                    if (!_player.IsPlaying && !_isPlaying)
                        break;

                    double currentPosition = 0;
                    double totalDuration = 0;

                    try
                    {
                        currentPosition = _player.CurrentPosition;
                        totalDuration = _player.Duration > 0 ? _player.Duration : _durationSeconds;
                    }
                    catch { }

                    double remaining = Math.Max(0, totalDuration - currentPosition);
                    double progress = totalDuration > 0 ? currentPosition / totalDuration : 0;
                    progress = Math.Clamp(progress, 0, 1);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            // ?? Update countdown label ??
                            var remainingSpan = TimeSpan.FromSeconds(remaining);
                            DurationLabel.Text = $"-{remainingSpan.ToString(@"mm\:ss")}";
                            DurationLabel.TextColor = Color.FromArgb("#4CAF50");

                            // ?? Update elapsed time label ??
                            var elapsedSpan = TimeSpan.FromSeconds(currentPosition);
                            StartTimeLabel.Text = elapsedSpan.ToString(@"mm\:ss");

                            // ?? Update progress bar width ??
                            var parentGrid = ProgressBar.Parent as Grid;
                            if (parentGrid != null && parentGrid.Width > 0)
                            {
                                ProgressBar.WidthRequest = progress * parentGrid.Width;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"UI update error in TrackProgress: {ex}");
                        }
                    });

                    await Task.Delay(80, token); // ~12 updates per second for smooth countdown
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackProgress error: {ex}");
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            CleanupAndClose(false);
        }

        private void OnSendClicked(object sender, EventArgs e)
        {
            CleanupAndClose(true);
        }

        private void CleanupAndClose(bool send)
        {
            if (_isCleanedUp) return;
            _isCleanedUp = true;

            try
            {
                _progressCts?.Cancel();
                _progressCts?.Dispose();
                _progressCts = null;

                if (_player != null)
                {
                    if (_player.IsPlaying)
                        _player.Stop();
                    _player.Dispose();
                    _player = null;
                }

                _audioStream?.Dispose();
                _audioStream = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup error: {ex}");
            }

            if (!send)
            {
                try
                {
                    if (File.Exists(_audioPath))
                    {
                        File.Delete(_audioPath);
                        Debug.WriteLine($"Deleted file: {_audioPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting file: {ex}");
                }
            }

            OnSend?.Invoke(this, send);
            Close();
        }

        private void OnPopupClosed(object? sender, PopupClosedEventArgs e)
        {
            if (!_isCleanedUp)
                CleanupAndClose(false);
        }
    }
}