using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Plugin.Maui.Audio;

namespace Lock.Pages.Profile
{
    public partial class VoiceIntroModal : ContentPage
    {
        private readonly string _phone;
        private readonly Action<string> _onSaved;
        private IAudioRecorder _recorder;
        private IAudioPlayer _player;
        private string _tempAudioPath;
        private bool _isRecording;
        private bool _isPlaying;
        private System.Timers.Timer _recordingTimer;
        private int _recordingSeconds;
        private const int MAX_RECORDING_SECONDS = 30;
        private IAudioManager _audioManager;

        public VoiceIntroModal(string phone, string existingAudioPath, Action<string> onSaved)
        {
            InitializeComponent();
            _phone = phone;
            _onSaved = onSaved;
            _audioManager = AudioManager.Current;

            if (!string.IsNullOrEmpty(existingAudioPath) && File.Exists(existingAudioPath))
            {
                _tempAudioPath = existingAudioPath;
                if (RecordButton != null) RecordButton.IsVisible = false;
                if (PlayButton != null) PlayButton.IsVisible = true;
                if (DeleteButton != null) DeleteButton.IsVisible = true;
                if (SaveButton != null) SaveButton.IsEnabled = true;
            }
        }

        private async void OnRecordClicked(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                await StopRecording();
            }
            else
            {
                await StartRecording();
            }
        }

        private async Task StartRecording()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Microphone>();
                    if (status != PermissionStatus.Granted)
                    {
                        await DisplayAlert("Permission Required", "Microphone access is needed to record your voice intro.", "OK");
                        return;
                    }
                }

                _recorder = _audioManager.CreateRecorder();
                await _recorder.StartAsync();
                _isRecording = true;

                if (RecordButton != null)
                {
                    RecordButton.Text = "? STOP";
                    RecordButton.BackgroundColor = Color.FromArgb("#FF4444");
                }

                if (PlayButton != null) PlayButton.IsVisible = false;
                if (DeleteButton != null) DeleteButton.IsVisible = false;
                if (SaveButton != null) SaveButton.IsEnabled = false;

                _recordingSeconds = 0;
                UpdateTimerDisplay();

                _recordingTimer = new System.Timers.Timer(1000);
                _recordingTimer.Elapsed += (s, args) =>
                {
                    _recordingSeconds++;
                    MainThread.BeginInvokeOnMainThread(() => UpdateTimerDisplay());

                    if (_recordingSeconds >= MAX_RECORDING_SECONDS)
                    {
                        MainThread.BeginInvokeOnMainThread(async () => await StopRecording());
                    }
                };
                _recordingTimer.Start();

                StartWaveformAnimation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartRecording error: {ex}");
                await DisplayAlert("Error", "Could not start recording: " + ex.Message, "OK");
            }
        }

        private async Task StopRecording()
        {
            try
            {
                _recordingTimer?.Stop();

                if (_recorder != null && _isRecording)
                {
                    var audioSource = await _recorder.StopAsync();
                    _isRecording = false;

                    var tempFolder = FileSystem.CacheDirectory;
                    _tempAudioPath = System.IO.Path.Combine(tempFolder, $"voice_intro_{_phone}_{DateTime.Now.Ticks}.wav");

                    using (var fileStream = File.Create(_tempAudioPath))
                    {
                        var audioStream = audioSource.GetAudioStream();
                        await audioStream.CopyToAsync(fileStream);
                    }

                    if (RecordButton != null)
                    {
                        RecordButton.Text = "? RECORD";
                        RecordButton.BackgroundColor = Color.FromArgb("#FF3B6F");
                    }

                    if (PlayButton != null) PlayButton.IsVisible = true;
                    if (SaveButton != null) SaveButton.IsEnabled = true;

                    StopWaveformAnimation();

                    if (StatusLabel != null)
                    {
                        StatusLabel.Text = $"Recording saved ({_recordingSeconds} seconds)";
                        StatusLabel.IsVisible = true;
                        StatusLabel.TextColor = Color.FromArgb("#4CD964");
                    }

                    await Task.Delay(2000);
                    if (StatusLabel != null) StatusLabel.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopRecording error: {ex}");
                _isRecording = false;
                if (StatusLabel != null)
                {
                    StatusLabel.Text = "Failed to save recording";
                    StatusLabel.IsVisible = true;
                }
                await Task.Delay(2000);
                if (StatusLabel != null) StatusLabel.IsVisible = false;
            }
        }

        private async void OnPlayClicked(object sender, EventArgs e)
        {
            if (_isPlaying)
            {
                _player?.Stop();
                _isPlaying = false;
                if (PlayButton != null) PlayButton.Text = "? PLAY";
            }
            else
            {
                try
                {
                    if (string.IsNullOrEmpty(_tempAudioPath) || !File.Exists(_tempAudioPath))
                    {
                        await DisplayAlert("Error", "No recording found to play.", "OK");
                        return;
                    }

                    _player?.Dispose();

                    var stream = File.OpenRead(_tempAudioPath);
                    _player = _audioManager.CreatePlayer(stream);
                    _player.Play();
                    _isPlaying = true;
                    if (PlayButton != null) PlayButton.Text = "? PAUSE";

                    _player.PlaybackEnded += (s, args) =>
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _isPlaying = false;
                            if (PlayButton != null) PlayButton.Text = "? PLAY";
                            stream?.Dispose();
                        });
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Play error: {ex}");
                    await DisplayAlert("Error", "Could not play recording: " + ex.Message, "OK");
                }
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_tempAudioPath) || !File.Exists(_tempAudioPath))
            {
                await DisplayAlert("Error", "No recording to save.", "OK");
                return;
            }

            var permanentFolder = System.IO.Path.Combine(FileSystem.AppDataDirectory, "voice_intros");
            if (!Directory.Exists(permanentFolder))
                Directory.CreateDirectory(permanentFolder);

            var permanentPath = System.IO.Path.Combine(permanentFolder, $"voice_intro_{_phone}.wav");

            if (File.Exists(permanentPath))
                File.Delete(permanentPath);

            File.Copy(_tempAudioPath, permanentPath);

            _onSaved?.Invoke(permanentPath);

            await Navigation.PopModalAsync();
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            var confirm = await DisplayAlert("Delete Recording", "Are you sure you want to delete your voice intro?", "Delete", "Cancel");
            if (confirm)
            {
                if (File.Exists(_tempAudioPath))
                    File.Delete(_tempAudioPath);

                _onSaved?.Invoke(null);
                await Navigation.PopModalAsync();
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            _player?.Dispose();
            _recorder = null;
            await Navigation.PopModalAsync();
        }

        private void UpdateTimerDisplay()
        {
            if (TimerLabel != null)
            {
                int minutes = _recordingSeconds / 60;
                int seconds = _recordingSeconds % 60;
                TimerLabel.Text = $"{minutes:D2}:{seconds:D2}";

                if (_recordingSeconds >= MAX_RECORDING_SECONDS)
                {
                    TimerLabel.TextColor = Color.FromArgb("#FF4444");
                }
                else
                {
                    TimerLabel.TextColor = Color.FromArgb("#FF3B6F");
                }
            }
        }

        private void StartWaveformAnimation()
        {
            if (WaveformLayout == null) return;

            WaveformLayout.Children.Clear();
            var random = new Random();

            for (int i = 0; i < 20; i++)
            {
                var bar = new BoxView
                {
                    WidthRequest = 3,
                    HeightRequest = 10 + random.Next(30),
                    BackgroundColor = Color.FromArgb("#FF3B6F"),
                    CornerRadius = 1.5f
                };
                WaveformLayout.Children.Add(bar);
            }

            var animationTimer = new System.Timers.Timer(150);
            animationTimer.Elapsed += (s, e) =>
            {
                if (!_isRecording)
                {
                    animationTimer.Stop();
                    animationTimer.Dispose();
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (WaveformLayout == null) return;
                    var randomLocal = new Random();
                    foreach (var child in WaveformLayout.Children)
                    {
                        if (child is BoxView box)
                        {
                            var newHeight = 10 + randomLocal.Next(30);
                            box.HeightRequest = newHeight;
                        }
                    }
                });
            };
            animationTimer.Start();
        }

        private void StopWaveformAnimation()
        {
            if (WaveformLayout == null) return;

            foreach (var child in WaveformLayout.Children)
            {
                if (child is BoxView box)
                {
                    box.HeightRequest = 16;
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _player?.Dispose();
            _recorder = null;
            _recordingTimer?.Dispose();
        }
    }
}