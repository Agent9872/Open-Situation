using Microsoft.Maui.Controls.Shapes;
using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Maui.Media;
namespace Lock.Pages.Discover
{
    public partial class DiscoverPage : ContentPage
    {
        // ?? State ?????????????????????????????????????????????????????????
        private string _selectedMood = string.Empty;
        private bool _chatSelected = false;
        private bool _voiceSelected = false;
        private bool _videoSelected = false;
        private bool _isLive = false;
        private string _currentUserPhone = string.Empty;
        private string _currentUserName = string.Empty;
        private string _currentUserImagePath = string.Empty;
        private int _currentLiveSessionId = 0; // Track current live session ID

        private CancellationTokenSource _durationTimerCts;
        private const int TIMER_CHECK_INTERVAL = 1000; // Check every second
        private List<string> _selectedImagePaths = new List<string>();
        private List<string> _liveSessionImagePaths = new List<string>(); // ? ADD THIS
        private const int MAX_IMAGES = 5;
        public bool IsTimedLive { get; set; }
        public string ImagePathsJson { get; set; } = "[]"; // ? ADD THIS

        // ?? All moods a user can broadcast right now ??????????????????????
        private static readonly string[] AllMoods = new[]
        {
            "Chill", "Playful", "Romantic", "Adventurous",
            "Talkative", "Bored", "Flirty", "Mysterious",
            "Happy", "Horny", "Lonely", "Excited",
            "Curious", "Supportive", "Deep talks"
        };

        // ?? Quick message suggestions per mood ????????????????????????????
        private static readonly Dictionary<string, string[]> MoodMessages =
      new(StringComparer.OrdinalIgnoreCase)
      {
          ["Chill"] = new[]
          {
            "Just chilling, looking for easy conversation",
            "Nothing serious — just vibing and open to chat",
            "Relaxed mode activated. Come say hi!",
            "Slow evening, good music, open DMs"
          },
          ["Playful"] = new[]
          {
            "In the mood to play games or swap memes",
            "Feeling cheeky today — entertain me!",
            "Let's trade jokes or funny stories",
            "Playful energy only. Keep it fun!"
          },
          ["Romantic"] = new[]
          {
            "Feeling soft tonight. Let's talk about something real",
            "In my romantic era. Who wants to vibe?",
            "Looking for a genuine connection right now",
            "Slow-burn conversation anyone?"
          },
          ["Adventurous"] = new[]
          {
            "Feeling bold — let's do something different tonight",
            "Up for anything spontaneous right now!",
            "Adventure mode on. What are we doing?",
            "Thrill-seeker energy. Hit me up"
          },
          ["Talkative"] = new[]
          {
            "In full talk mode right now — let's go deep!",
            "I have things to say and I need ears",
            "Voice call? I'm in a very chatty mood tonight",
            "Could talk for hours. Who's free?"
          },
          ["Bored"] = new[]
          {
            "Someone entertain me PLEASE",
            "Bored out of my mind — save me with good convo",
            "Nothing to do tonight. Chat?",
            "SOS — I need human interaction"
          },
          ["Flirty"] = new[]
          {
            "Feeling a little flirty tonight, come talk to me",
            "Good energy, open vibes — let's see where this goes",
            "I don't bite… much",
            "Feeling bold. Your move"
          },
          ["Mysterious"] = new[]
          {
            "Something's on my mind. Who can keep a secret?",
            "In that 2am introspective mood…",
            "Deep thoughts, no filter. Who's brave enough?",
            "Come find out what I'm thinking"
          },
          ["Happy"] = new[]
          {
            "Having such a good day! Want to share the energy?",
            "Great mood = great conversation. Join me!",
            "Smiling for no reason — come match my vibe",
            "Spreading good vibes tonight"
          },
          ["Horny"] = new[]
          {
            "That kind of mood tonight… Adults only",
            "Feeling it tonight. Open to the right vibe",
            "Bold and unbothered. You know the vibe",
            "18+ conversation only. Come talk to me"
          },
          ["Lonely"] = new[]
          {
            "Could really use some company tonight",
            "It's one of those quiet nights… say hi?",
            "Missing good conversation. Anyone there?",
            "Not looking for anything heavy — just connection"
          },
          ["Excited"] = new[]
          {
            "SOMETHING HAPPENED and I need to tell someone!!!",
            "Big energy right now — match it if you can!",
            "Buzzing with excitement. Come vibe!",
            "Can't sit still. Let's talk!"
          },
          ["Curious"] = new[]
          {
            "I have so many questions tonight. Let's explore",
            "Wondering about everything. Philosophical talk?",
            "Teach me something interesting today",
            "What's your take on… everything?"
          },
          ["Supportive"] = new[]
          {
            "If you need to vent, I'm here. No judgment",
            "Good listener mode activated. Talk to me",
            "Supportive energy only tonight. How are you really?",
            "Sometimes you just need someone to listen"
          },
          ["Deep talks"] = new[]
          {
            "No small talk tonight — only the deep stuff",
            "Let's talk about life, dreams, and everything in between",
            "I want a conversation that makes me think",
            "Philosophy, feelings, ideas — let's go there"
          },
      };

        // ?? Constructor ???????????????????????????????????????????????????
        public DiscoverPage()
        {
            InitializeComponent();
            BuildMoodChips();

            // Set default duration selection
            DurationPicker.SelectedIndex = 3; // Default to 1 hour
        }

        private async void OnAddImagesTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var result = await FilePicker.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select up to 5 images",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null) return;

                var remainingSlots = MAX_IMAGES - _selectedImagePaths.Count;
                if (remainingSlots <= 0)
                {
                    await DisplayAlert("Limit reached", $"You can only add up to {MAX_IMAGES} images.", "OK");
                    return;
                }

                var newImages = result.Take(remainingSlots).ToList();

                if (newImages.Count < result.Count())
                {
                    await DisplayAlert("Limit reached",
                        $"You can only add up to {MAX_IMAGES} images. Added {newImages.Count} of {result.Count()}.",
                        "OK");
                }

                foreach (var file in newImages)
                {
                    var savedPath = await SaveImageToLocalStorage(file);
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        _selectedImagePaths.Add(savedPath);
                    }
                }

                UpdateImagesPreview();

                if (_isLive)
                    ShowPreview();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAddImagesTapped error: {ex}");
                await DisplayAlert("Error", "Failed to load images. Please try again.", "OK");
            }
        }

        private async Task<string> SaveImageToLocalStorage(FileResult file)
        {
            try
            {
                var appDataDir = FileSystem.AppDataDirectory;
                var liveImagesDir = System.IO.Path.Combine(appDataDir, "live_images");

                if (!Directory.Exists(liveImagesDir))
                    Directory.CreateDirectory(liveImagesDir);

                var fileName = $"{Guid.NewGuid()}_{System.IO.Path.GetFileName(file.FileName)}";
                var filePath = System.IO.Path.Combine(liveImagesDir, fileName);

                using var stream = await file.OpenReadAsync();
                using var fileStream = File.Create(filePath);
                await stream.CopyToAsync(fileStream);

                return filePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveImageToLocalStorage error: {ex}");
                return string.Empty;
            }
        }
        private void RemoveImage(string imagePath)
        {
            try
            {
                _selectedImagePaths.Remove(imagePath);

                if (File.Exists(imagePath))
                    File.Delete(imagePath);

                UpdateImagesPreview();

                // Do NOT call ShowPreview() here — preview uses _liveSessionImagePaths
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemoveImage error: {ex}");
            }
        }
        private void UpdateImagesPreview()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ImagesPreviewLayout.Children.Clear();
                ImagesPreviewLayout.Spacing = 10;
                ImagesPreviewLayout.HorizontalOptions = LayoutOptions.Start;
                ImagesPreviewLayout.VerticalOptions = LayoutOptions.Center;

                if (_selectedImagePaths.Count > 0)
                {
                    ImagesScrollView.IsVisible = true;
                    ImagesScrollView.Orientation = ScrollOrientation.Horizontal;

                    foreach (var imagePath in _selectedImagePaths)
                    {
                        var imageFrame = new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 1,
                            Stroke = Color.FromArgb("#333333"),
                            StrokeShape = new RoundRectangle { CornerRadius = 8 },
                            WidthRequest = 100,
                            HeightRequest = 100,
                            Padding = new Thickness(0)
                        };

                        var image = new Image
                        {
                            Source = ImageSource.FromFile(imagePath),
                            Aspect = Aspect.AspectFill,
                            WidthRequest = 100,
                            HeightRequest = 100
                        };

                        var grid = new Grid();
                        grid.Children.Add(image);

                        var deleteButton = new Border
                        {
                            BackgroundColor = Color.FromArgb("#FF4444"),
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = 12 },
                            WidthRequest = 24,
                            HeightRequest = 24,
                            Padding = new Thickness(0),
                            HorizontalOptions = LayoutOptions.End,
                            VerticalOptions = LayoutOptions.Start,
                            Margin = new Thickness(0, 4, 4, 0)
                        };

                        var deleteIcon = new Label
                        {
                            Text = "×",
                            FontSize = 18,
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };

                        deleteButton.Content = deleteIcon;

                        var currentPath = imagePath;
                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += (s, e) => RemoveImage(currentPath);
                        deleteButton.GestureRecognizers.Add(tapGesture);

                        grid.Children.Add(deleteButton);
                        imageFrame.Content = grid;
                        ImagesPreviewLayout.Children.Add(imageFrame);
                    }
                }
                else
                {
                    ImagesScrollView.IsVisible = false;
                }
            });
        }


        private void UpdatePreviewCountdown(TimeSpan timeRemaining)
        {
            if (!_isLive) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PreviewCountdownBadge.IsVisible = true; // ? ensure always visible

                string countdownText;
                if (timeRemaining.TotalHours >= 1)
                    countdownText = $"{timeRemaining:hh\\:mm\\:ss}";
                else if (timeRemaining.TotalMinutes >= 1)
                    countdownText = $"{timeRemaining:mm\\:ss}";
                else
                    countdownText = $"{(int)timeRemaining.TotalSeconds}s";

                PreviewCountdownLabel.Text = countdownText;

                if (timeRemaining.TotalSeconds <= 30)
                {
                    PreviewCountdownBadge.BackgroundColor = Color.FromArgb("#2A1A1A");
                    PreviewCountdownBadge.Stroke = Color.FromArgb("#FF4444");
                    PreviewCountdownLabel.TextColor = Color.FromArgb("#FF4444");

                    PreviewCountdownBadge.AbortAnimation("CriticalPulse");
                    var criticalPulse = new Animation();
                    criticalPulse.Add(0, 0.5, new Animation(v =>
                        PreviewCountdownBadge.Opacity = v, 1.0, 0.5, Easing.SinInOut));
                    criticalPulse.Add(0.5, 1.0, new Animation(v =>
                        PreviewCountdownBadge.Opacity = v, 0.5, 1.0, Easing.SinInOut));
                    criticalPulse.Commit(PreviewCountdownBadge, "CriticalPulse", 16, 500,
                        Easing.Linear, null, () => _isLive); // ? stops when offline
                }
                else if (timeRemaining.TotalSeconds <= 60)
                {
                    PreviewCountdownBadge.AbortAnimation("CriticalPulse");
                    PreviewCountdownBadge.Opacity = 1.0;
                    PreviewCountdownBadge.BackgroundColor = Color.FromArgb("#2A1A1A");
                    PreviewCountdownBadge.Stroke = Color.FromArgb("#FFA500");
                    PreviewCountdownLabel.TextColor = Color.FromArgb("#FFA500");
                }
                else
                {
                    PreviewCountdownBadge.AbortAnimation("CriticalPulse");
                    PreviewCountdownBadge.Opacity = 1.0;
                    PreviewCountdownBadge.BackgroundColor = Color.FromArgb("#1A2A1A");
                    PreviewCountdownBadge.Stroke = Color.FromArgb("#4CAF50");
                    PreviewCountdownLabel.TextColor = Color.FromArgb("#4CAF50");
                }
            });
        }
        // ?? Lifecycle ?????????????????????????????????????????????????????
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserAsync();
            await UpdateLiveCountBannerAsync();
            await CheckExistingLiveSession();
        }

        private async void OnDurationInfoTapped(object sender, TappedEventArgs e)
        {
            await DisplayAlert("Live Duration",
                "Your live session will automatically end after the selected time. " +
                "A countdown will appear so you know when it will end.", "OK");
        }

        // ?? Live Blinking Animation ?????????????????????????????????????????????
        private void StartLiveBlinking()
        {
            LiveStatusBadge.AbortAnimation("LiveBlink");

            var pulseAnimation = new Animation();

            pulseAnimation.Add(0, 0.5, new Animation(v =>
            {
                LiveStatusBadge.Opacity = v;
            }, 1.0, 0.35, Easing.SinInOut));

            pulseAnimation.Add(0.5, 1.0, new Animation(v =>
            {
                LiveStatusBadge.Opacity = v;
            }, 0.35, 1.0, Easing.SinInOut));

            pulseAnimation.Commit(LiveStatusBadge, "LiveBlink", 16, 1200, Easing.Linear, null, () => true);
        }

        private void StopLiveBlinking()
        {
            LiveStatusBadge.AbortAnimation("LiveBlink");
            LiveStatusBadge.Opacity = 1.0;
        }

        // ?? Load current user's profile ???????????????????????????????????
        private async Task LoadUserAsync()
        {
            try
            {
                _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(_currentUserPhone)) return;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                                   .Where(u => u.PhoneNumber == _currentUserPhone)
                                   .FirstOrDefaultAsync();

                if (user == null) return;

                _currentUserName = user.Name ?? _currentUserPhone;
                _currentUserImagePath = user.ProfileImagePath ?? string.Empty;

                UserNameLabel.Text = _currentUserName;
                UserPhoneLabel.Text = _currentUserPhone;

                if (!string.IsNullOrEmpty(_currentUserImagePath) && File.Exists(_currentUserImagePath))
                {
                    ProfileImage.Source = ImageSource.FromFile(_currentUserImagePath);
                    PreviewAvatar.Source = ImageSource.FromFile(_currentUserImagePath);
                }

                PreviewName.Text = _currentUserName;

                if (!string.IsNullOrEmpty(user.Mood))
                {
                    var matchingMood = AllMoods.FirstOrDefault(m =>
                        m.Contains(user.Mood, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(matchingMood))
                        SelectMood(matchingMood);
                }

                if (string.IsNullOrEmpty(_selectedMood))
                    SelectMood(AllMoods[0]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DiscoverPage.LoadUserAsync error: {ex}");
            }
        }

        // ?? Check if user already has an active live session ???????????????
        private async Task CheckExistingLiveSession()
        {
            try
            {
                var db = DatabaseService.GetConnection();
                var activeSession = await db.Table<LiveSession>()
                    .Where(s => s.UserPhoneNumber == _currentUserPhone && s.IsLive && s.EndedAt == null)
                    .FirstOrDefaultAsync();

                if (activeSession != null)
                {
                    if (activeSession.ScheduledEndTime.HasValue && activeSession.ScheduledEndTime.Value <= DateTime.UtcNow)
                    {
                        activeSession.IsLive = false;
                        activeSession.EndedAt = DateTime.UtcNow;
                        await db.UpdateAsync(activeSession);
                        return;
                    }

                    _isLive = true;
                    _currentLiveSessionId = activeSession.Id;
                    _selectedMood = activeSession.Mood;
                    _chatSelected = activeSession.ChatAvailable;
                    _voiceSelected = activeSession.VoiceAvailable;
                    _videoSelected = activeSession.VideoAvailable;
                    MessageEditor.Text = activeSession.Message;
                    LocationEntry.Text = activeSession.Location;

                    // Restore images into live list only — upload picker stays empty
                    if (!string.IsNullOrEmpty(activeSession.ImagePathsJson))
                    {
                        try
                        {
                            _liveSessionImagePaths = JsonSerializer.Deserialize<List<string>>(activeSession.ImagePathsJson)
                                                  ?? new List<string>();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to deserialize images: {ex}");
                            _liveSessionImagePaths = new List<string>();
                        }
                    }

                    SelectMood(activeSession.Mood);

                    SelectMood(activeSession.Mood);

                    SetAvailabilityStyle(ChatBorder, ChatIcon, ChatLabel, _chatSelected);
                    SetAvailabilityStyle(VoiceBorder, VoiceIcon, VoiceLabel, _voiceSelected);
                    SetAvailabilityStyle(VideoBorder, VideoIcon, VideoLabel, _videoSelected);

                    GoLiveLabel.Text = "Go Offline";
                    GoLiveToggle.BackgroundColor = Color.FromArgb("#1A2A1A");
                    GoLiveLabel.TextColor = Color.FromArgb("#4CAF50");

                    LiveStatusBadge.IsVisible = true;

                    StartLiveBlinking();
                    ShowPreview();

                    if (activeSession.ScheduledEndTime.HasValue)
                    {
                        StartDurationTimer(activeSession.ScheduledEndTime.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckExistingLiveSession error: {ex}");
            }
        }

        // ?? Build mood chips ??????????????????????????????????????????????
        private void BuildMoodChips()
        {
            MoodChipsLayout.Children.Clear();

            foreach (var mood in AllMoods)
            {
                var chip = new Border
                {
                    BackgroundColor = Color.FromArgb("#1A1A1A"),
                    StrokeThickness = 0.5,
                    Stroke = Color.FromArgb("#333333"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(14, 8),
                    VerticalOptions = LayoutOptions.Center
                };

                var label = new Label
                {
                    Text = mood,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#888888"),
                    VerticalOptions = LayoutOptions.Center
                };

                chip.Content = label;

                var capturedMood = mood;
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => SelectMood(capturedMood);
                chip.GestureRecognizers.Add(tap);

                MoodChipsLayout.Children.Add(chip);
            }
        }

        private void SelectMood(string mood)
        {
            _selectedMood = mood;

            foreach (var child in MoodChipsLayout.Children)
            {
                if (child is not Border chip) continue;
                if (chip.Content is not Label lbl) continue;

                bool isSelected = lbl.Text == mood;
                chip.BackgroundColor = isSelected
                    ? Color.FromArgb("#2A1A1A")
                    : Color.FromArgb("#1A1A1A");
                chip.Stroke = isSelected
                    ? Color.FromArgb("#C05050")
                    : Color.FromArgb("#333333");
                lbl.TextColor = isSelected
                    ? Color.FromArgb("#C05050")
                    : Color.FromArgb("#888888");
            }

            BuildSuggestionChips(mood);

            var suggestions = GetSuggestions(mood);
            if (!string.IsNullOrEmpty(suggestions[0]) && string.IsNullOrWhiteSpace(MessageEditor.Text))
                MessageEditor.Text = suggestions[0];

            if (_isLive)
                PreviewMood.Text = mood;
        }

        // ?? Suggestion chips ??????????????????????????????????????????????
        private void BuildSuggestionChips(string mood)
        {
            SuggestionChipsLayout.Children.Clear();

            var suggestions = GetSuggestions(mood);

            for (int i = 0; i < suggestions.Length; i++)
            {
                var suggestion = suggestions[i];

                var chip = new Border
                {
                    BackgroundColor = Color.FromArgb("#1E1E1E"),
                    StrokeThickness = 0.5,
                    Stroke = Color.FromArgb("#333333"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(10, 5),
                    VerticalOptions = LayoutOptions.Center
                };

                var words = suggestion.Split(' ').Take(5);
                var shortLabel = string.Join(" ", words) + "…";

                var label = new Label
                {
                    Text = shortLabel,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#888888"),
                    VerticalOptions = LayoutOptions.Center
                };

                chip.Content = label;

                var capturedSuggestion = suggestion;
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) =>
                {
                    MessageEditor.Text = capturedSuggestion;
                    foreach (var c in SuggestionChipsLayout.Children)
                    {
                        if (c is Border b2 && b2.Content is Label l2)
                        {
                            bool isThis = b2 == chip;
                            b2.BackgroundColor = isThis ? Color.FromArgb("#2A1A1A") : Color.FromArgb("#1E1E1E");
                            b2.Stroke = isThis ? Color.FromArgb("#C05050") : Color.FromArgb("#333333");
                            l2.TextColor = isThis ? Color.FromArgb("#C05050") : Color.FromArgb("#888888");
                        }
                    }
                };
                chip.GestureRecognizers.Add(tap);

                SuggestionChipsLayout.Children.Add(chip);
            }
        }

        private string[] GetSuggestions(string mood)
        {
            if (MoodMessages.TryGetValue(mood, out var messages))
                return messages;

            return new[]
            {
        $"Feeling {mood.Split(' ')[0]} right now — come chat!",
        $"In a {mood.Split(' ')[0]} mood. Who's around?",
        $"Current vibe: {mood}. Say hi!",
        $"{mood} energy tonight. Open to connect!"
    };
        }
        // ?? Availability toggles ??????????????????????????????????????????
        private void OnAvailabilityTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not string type) return;

            switch (type)
            {
                case "Chat":
                    _chatSelected = !_chatSelected;
                    SetAvailabilityStyle(ChatBorder, ChatIcon, ChatLabel, _chatSelected);
                    break;
                case "Voice":
                    _voiceSelected = !_voiceSelected;
                    SetAvailabilityStyle(VoiceBorder, VoiceIcon, VoiceLabel, _voiceSelected);
                    break;
                case "Video":
                    _videoSelected = !_videoSelected;
                    SetAvailabilityStyle(VideoBorder, VideoIcon, VideoLabel, _videoSelected);
                    break;
            }

            if (_isLive)
                ShowPreview();
        }

        private void SetAvailabilityStyle(
            Border border, Microsoft.Maui.Controls.Shapes.Path icon,
            Label label, bool selected)
        {
            border.BackgroundColor = selected
                ? Color.FromArgb("#2A1A1A")
                : Color.FromArgb("#141414");
            border.Stroke = selected
                ? Color.FromArgb("#C05050")
                : Color.FromArgb("#252525");
            icon.Fill = selected
                ? Color.FromArgb("#C05050")
                : Color.FromArgb("#555555");
            label.TextColor = selected
                ? Color.FromArgb("#C05050")
                : Color.FromArgb("#666666");
        }

        // ?? Go-Live header toggle ?????????????????????????????????????????
        private async void OnGoLiveToggled(object sender, TappedEventArgs e)
        {
            if (_isLive)
            {
                await EndLiveSession();
            }
            else
            {
                if (!_chatSelected && !_voiceSelected && !_videoSelected)
                {
                    await DisplayAlert("Select availability",
                        "Please select at least one option: Chat, Voice or Video", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(MessageEditor.Text))
                {
                    await DisplayAlert("Add a message",
                        "Write a quick message so people know what to expect", "OK");
                    return;
                }

                await StartLiveSession();
            }
        }

        private async Task StartLiveSession()
        {
            try
            {
                _isLive = true;

                int durationMinutes = ParseDuration(DurationPicker.SelectedItem?.ToString() ?? "1 hour");
                DateTime scheduledEndTime = DateTime.UtcNow.AddMinutes(durationMinutes);

                var imagesJson = JsonSerializer.Serialize(_selectedImagePaths);

                var liveSession = new LiveSession
                {
                    UserPhoneNumber = _currentUserPhone,
                    Mood = _selectedMood,
                    Message = MessageEditor.Text?.Trim() ?? string.Empty,
                    Location = LocationEntry.Text?.Trim() ?? string.Empty,
                    ChatAvailable = _chatSelected,
                    VoiceAvailable = _voiceSelected,
                    VideoAvailable = _videoSelected,
                    IsLive = true,
                    StartedAt = DateTime.UtcNow,
                    DurationMinutes = durationMinutes,
                    ScheduledEndTime = scheduledEndTime,
                    IsTimedLive = true,
                    ViewCount = 0,
                    ConnectionCount = 0,
                    ImagePathsJson = imagesJson  // ? save images to DB
                };

                var db = DatabaseService.GetConnection();
                _currentLiveSessionId = await db.InsertAsync(liveSession);

                // Snapshot for preview, then clear the upload picker UI
                _liveSessionImagePaths = new List<string>(_selectedImagePaths);
                _selectedImagePaths.Clear();
                UpdateImagesPreview(); // clears upload strip only

                GoLiveLabel.Text = "Go Offline";
                GoLiveToggle.BackgroundColor = Color.FromArgb("#1A2A1A");
                GoLiveLabel.TextColor = Color.FromArgb("#4CAF50");

                UpdateMainButtonState(true);
                LiveStatusBadge.IsVisible = true;
                StartLiveBlinking();
                ShowPreview(); // now reads _liveSessionImagePaths

                PreviewCountdownBadge.IsVisible = true;
                UpdatePreviewCountdown(scheduledEndTime - DateTime.UtcNow);
                StartDurationTimer(scheduledEndTime);

                await DisplayAlert("You're Live! ??",
                    $"Your live session will automatically end at {scheduledEndTime.ToLocalTime():HH:mm}.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartLiveSession error: {ex}");
                await DisplayAlert("Error", "Failed to start live session. Please try again.", "OK");
                _isLive = false;
            }
        }
        private int ParseDuration(string durationString)
        {
            return durationString.ToLower() switch
            {
                "5 minutes" => 5,
                "15 minutes" => 15,
                "30 minutes" => 30,
                "1 hour" => 60,
                "2 hours" => 120,
                "3 hours" => 180,
                "6 hours" => 360,
                "12 hours" => 720,
                "24 hours" => 1440,
                _ => 60 // Default to 1 hour
            };
        }

        private void StartDurationTimer(DateTime scheduledEndTime)
        {
            // Cancel any existing timer
            _durationTimerCts?.Cancel();
            _durationTimerCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (_isLive && !_durationTimerCts.Token.IsCancellationRequested)
                {
                    var timeRemaining = scheduledEndTime - DateTime.UtcNow;

                    if (timeRemaining.TotalSeconds <= 0)
                    {
                        // Time's up! End the live session
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await EndLiveSession();
                            await DisplayAlert("Live Session Ended",
                                "Your live session has automatically ended as the duration has expired.", "OK");
                        });
                        break;
                    }
                    else
                    {
                        // Update the countdown displays
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            // Update the preview card countdown
                            UpdatePreviewCountdown(timeRemaining);

                            // Update the status badge if within last minute
                            if (timeRemaining.TotalMinutes <= 1 && timeRemaining.TotalSeconds > 0)
                            {
                                UpdateLiveBadgeWithCountdown(timeRemaining);
                            }

                            // Update main button if within last minute
                            if (timeRemaining.TotalMinutes <= 1)
                            {
                                MainButtonLabel.Text = $"Ends in {timeRemaining:mm\\:ss}";
                            }
                        });
                    }

                    await Task.Delay(TIMER_CHECK_INTERVAL, _durationTimerCts.Token);
                }
            }, _durationTimerCts.Token);
        }
        private void UpdateLiveBadgeWithCountdown(TimeSpan timeRemaining)
        {
            // Update the live badge to show remaining time
            if (LiveStatusBadge.Content is HorizontalStackLayout stackLayout)
            {
                // Clear existing children
                stackLayout.Children.Clear();

                // Add the live dot
                stackLayout.Children.Add(new BoxView
                {
                    Color = Color.FromArgb("#4CAF50"),
                    WidthRequest = 8,
                    HeightRequest = 8,
                    CornerRadius = 4,
                    VerticalOptions = LayoutOptions.Center
                });

                // Add the countdown text
                stackLayout.Children.Add(new Label
                {
                    Text = $"Ends in {timeRemaining:mm\\:ss}",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#4CAF50"),
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            // Also update the main button to show remaining time
            MainButtonLabel.Text = $"Ends in {timeRemaining:mm\\:ss}";

            // Make the badge pulse faster when time is running out
            if (timeRemaining.TotalSeconds <= 30)
            {
                // Pulse faster for final 30 seconds
                LiveStatusBadge.AbortAnimation("LiveBlink");
                var fastPulse = new Animation();
                fastPulse.Add(0, 0.25, new Animation(v => LiveStatusBadge.Opacity = v, 1.0, 0.2, Easing.SinInOut));
                fastPulse.Add(0.25, 0.5, new Animation(v => LiveStatusBadge.Opacity = v, 0.2, 1.0, Easing.SinInOut));
                fastPulse.Commit(LiveStatusBadge, "LiveBlink", 16, 500, Easing.Linear, null, () => true);
            }
        }
        private async Task EndLiveSession()
        {
            try
            {
                _durationTimerCts?.Cancel();
                _durationTimerCts?.Dispose();
                _durationTimerCts = null;

                var db = DatabaseService.GetConnection();
                var liveSession = await db.Table<LiveSession>()
                    .Where(s => s.Id == _currentLiveSessionId)
                    .FirstOrDefaultAsync();

                if (liveSession != null)
                {
                    liveSession.IsLive = false;
                    liveSession.EndedAt = DateTime.UtcNow;
                    await db.UpdateAsync(liveSession);
                }

                _isLive = false;
                _currentLiveSessionId = 0;

                // ?? Clear image lists and upload UI ??
                _selectedImagePaths.Clear();
                _liveSessionImagePaths.Clear();
                UpdateImagesPreview();
                ClearPreviewCard();

                // ?? Reset Go Live toggle button (top strip) ??
                GoLiveLabel.Text = "Go Live";
                GoLiveToggle.BackgroundColor = Color.FromArgb("#C05050");
                GoLiveLabel.TextColor = Colors.White;

                // ?? Hide live dot on avatar ??
                LiveDot.IsVisible = false;

                // ?? Reset availability borders back to unselected ??
                _chatSelected = false;
                _voiceSelected = false;
                _videoSelected = false;
                SetAvailabilityStyle(ChatBorder, ChatIcon, ChatLabel, false);
                SetAvailabilityStyle(VoiceBorder, VoiceIcon, VoiceLabel, false);
                SetAvailabilityStyle(VideoBorder, VideoIcon, VideoLabel, false);

                // ?? Reset Live Status Badge ??
                StopLiveBlinking();
                LiveStatusBadge.IsVisible = false;
                LiveStatusBadge.Opacity = 1.0;

                // Restore badge content to original "Live now" text
                // (UpdateLiveBadgeWithCountdown may have replaced it with a countdown)
                if (LiveStatusBadge.Content is HorizontalStackLayout badgeStack)
                {
                    badgeStack.Children.Clear();
                    badgeStack.Children.Add(new Label
                    {
                        Text = "Live now",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#4CAF50"),
                        FontAttributes = FontAttributes.Bold,
                        VerticalOptions = LayoutOptions.Center
                    });
                }

                // ?? Reset main bottom button ??
                UpdateMainButtonState(false);
                MainButtonLabel.Text = "Go Live Now";

                // ?? Hide preview section and countdown ??
                PreviewSection.IsVisible = false;
                PreviewCountdownBadge.IsVisible = false;
                PreviewCountdownLabel.Text = string.Empty;
                PreviewCountdownBadge.AbortAnimation("CriticalPulse");
                PreviewCountdownBadge.Opacity = 1.0;
                PreviewCountdownBadge.BackgroundColor = Color.FromArgb("#1A2A1A");
                PreviewCountdownBadge.Stroke = Color.FromArgb("#4CAF50");
                PreviewCountdownLabel.TextColor = Color.FromArgb("#4CAF50");

                // ?? Reset message and location fields ??
                MessageEditor.Text = string.Empty;
                LocationEntry.Text = string.Empty;

                // ?? Rebuild mood chips to clear selected state ??
                BuildMoodChips();
                SelectMood(AllMoods[0]);

                await DisplayAlert("Offline", "You're now offline and no longer discoverable.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EndLiveSession error: {ex}");
                await DisplayAlert("Error", "Failed to end live session. Please try again.", "OK");
            }
        }
        private void ClearPreviewCard()
        {
            try
            {
                var previewCard = PreviewSection.Children.LastOrDefault() as Border;
                if (previewCard?.Content is not VerticalStackLayout cardContent) return;

                // Remove ProfileDetailsStack
                var details = cardContent.Children
                    .OfType<VerticalStackLayout>()
                    .FirstOrDefault(v => v.ClassId == "ProfileDetailsStack");
                if (details != null)
                    cardContent.Children.Remove(details);

                // Remove ProfileBadgesStack
                var badges = cardContent.Children
                    .OfType<HorizontalStackLayout>()
                    .FirstOrDefault(v => v.ClassId == "ProfileBadgesStack");
                if (badges != null)
                    cardContent.Children.Remove(badges);

                // Remove ALL ScrollViews (image previews added dynamically)
                var scrollViews = cardContent.Children
                    .OfType<ScrollView>()
                    .ToList();
                foreach (var sv in scrollViews)
                    cardContent.Children.Remove(sv);

                // Remove extra BoxView dividers (dynamically added ones)
                // Keep only the original static divider (first BoxView)
                var boxViews = cardContent.Children
                    .OfType<BoxView>()
                    .Skip(1) // keep the first/original divider
                    .ToList();
                foreach (var bv in boxViews)
                    cardContent.Children.Remove(bv);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClearPreviewCard error: {ex}");
            }
        }

        // ?? Update Bottom Main Button Appearance ???????????????????????????????
        private void UpdateMainButtonState(bool isLive)
        {
            if (isLive)
            {
                MainButtonBorder.BackgroundColor = Color.FromArgb("#1A2A1A");
                MainButtonLabel.Text = "Go Offline";
                MainButtonLabel.TextColor = Color.FromArgb("#4CAF50");
                MainButtonDot.Color = Color.FromArgb("#4CAF50");
            }
            else
            {
                MainButtonBorder.BackgroundColor = Color.FromArgb("#C05050");
                MainButtonLabel.Text = "Go Live Now";
                MainButtonLabel.TextColor = Colors.White;
                MainButtonDot.Color = Colors.White;
            }
        }

        // ?? Main post button ???????????????????????????????????????????????
        private async void OnPostDiscoverClicked(object sender, TappedEventArgs e)
        {
            Debug.WriteLine($"OnPostDiscoverClicked: _isLive={_isLive}");

            if (_isLive)
            {
                await EndLiveSession();
                return; // ? prevents fall-through
            }

            if (string.IsNullOrEmpty(_currentUserPhone))
            {
                await DisplayAlert("Error", "Please log in first", "OK");
                return;
            }

            if (!_chatSelected && !_voiceSelected && !_videoSelected)
            {
                await DisplayAlert("Select availability",
                    "Please select at least one option: Chat, Voice or Video", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(MessageEditor.Text))
            {
                await DisplayAlert("Add a message",
                    "Write a quick message so people know what to expect", "OK");
                return;
            }

            await StartLiveSession();
        }
        
        // ?? Build preview card ????????????????????????????????????????????
        private async void ShowPreview()
        {
            // ALWAYS clean up dynamic content first before rebuilding
            ClearPreviewCard();

            PreviewMood.Text = _selectedMood;

            var loc = LocationEntry.Text?.Trim() ?? string.Empty;
            PreviewLocation.Text = loc;
            PreviewLocation.IsVisible = !string.IsNullOrEmpty(loc);

            PreviewMessage.Text = MessageEditor.Text?.Trim() ?? string.Empty;

            if (_isLive && _currentLiveSessionId > 0)
            {
                try
                {
                    var db = DatabaseService.GetConnection();
                    var liveSession = await db.Table<LiveSession>()
                        .Where(s => s.Id == _currentLiveSessionId)
                        .FirstOrDefaultAsync();

                    if (liveSession?.ScheduledEndTime.HasValue == true)
                    {
                        var timeRemaining = liveSession.ScheduledEndTime.Value - DateTime.UtcNow;
                        if (timeRemaining.TotalSeconds > 0)
                            UpdatePreviewCountdown(timeRemaining);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ShowPreview: Error loading live session: {ex}");
                }
            }

            var existingImageSection = PreviewSection.Children
                .OfType<ScrollView>()
                .FirstOrDefault(sv => sv.ClassId == "PreviewImagesScrollView");

            if (existingImageSection != null)
            {
                var parent = existingImageSection.Parent as VerticalStackLayout;
                if (parent != null)
                    parent.Children.Remove(existingImageSection);
            }

            User user = null;
            try
            {
                var db = DatabaseService.GetConnection();
                user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == _currentUserPhone)
                    .FirstOrDefaultAsync();

                if (user != null && PreviewSection.Children.LastOrDefault() is Border previewCard &&
                    previewCard.Content is VerticalStackLayout cardContent)
                {
                    var existingDetails = cardContent.Children
                        .OfType<VerticalStackLayout>()
                        .FirstOrDefault(v => v.ClassId == "ProfileDetailsStack");
                    if (existingDetails != null)
                        cardContent.Children.Remove(existingDetails);

                    var existingBadges = cardContent.Children
                        .OfType<HorizontalStackLayout>()
                        .FirstOrDefault(v => v.ClassId == "ProfileBadgesStack");
                    if (existingBadges != null)
                        cardContent.Children.Remove(existingBadges);

                    var badgesStack = new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Margin = new Thickness(0, 4, 0, 4),
                        ClassId = "ProfileBadgesStack"
                    };

                    if (user.DateOfBirth != default)
                    {
                        var today = DateTime.Today;
                        var age = today.Year - user.DateOfBirth.Year;
                        if (user.DateOfBirth > today.AddYears(-age)) age--;

                        if (age > 0)
                        {
                            badgesStack.Children.Add(new Border
                            {
                                BackgroundColor = Color.FromArgb("#1A2A1A"),
                                StrokeThickness = 0.5,
                                Stroke = Color.FromArgb("#008080"),
                                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                                Padding = new Thickness(8, 3),
                                Content = new Label
                                {
                                    Text = $"{age}",
                                    FontSize = 11,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#008080")
                                }
                            });
                        }
                    }

                    if (!string.IsNullOrEmpty(user.Gender))
                    {
                        badgesStack.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#333333"),
                            StrokeShape = new RoundRectangle { CornerRadius = 8 },
                            Padding = new Thickness(8, 3),
                            Content = new Label
                            {
                                Text = user.Gender,
                                FontSize = 11,
                                TextColor = Color.FromArgb("#888888")
                            }
                        });
                    }

                    if (user.HeightCm.HasValue && user.HeightCm.Value > 0)
                    {
                        int feet = (int)(user.HeightCm.Value / 30.48);
                        int inches = (int)((user.HeightCm.Value % 30.48) / 2.54);
                        badgesStack.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#333333"),
                            StrokeShape = new RoundRectangle { CornerRadius = 8 },
                            Padding = new Thickness(8, 3),
                            Content = new Label
                            {
                                Text = $"{feet}'{inches}\"",
                                FontSize = 11,
                                TextColor = Color.FromArgb("#888888")
                            }
                        });
                    }

                    if (!string.IsNullOrEmpty(user.BodyType))
                    {
                        badgesStack.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#333333"),
                            StrokeShape = new RoundRectangle { CornerRadius = 8 },
                            Padding = new Thickness(8, 3),
                            Content = new Label
                            {
                                Text = user.BodyType,
                                FontSize = 11,
                                TextColor = Color.FromArgb("#888888")
                            }
                        });
                    }

                    if (badgesStack.Children.Count > 0)
                    {
                        var moodRow = cardContent.Children.OfType<HorizontalStackLayout>().FirstOrDefault();
                        if (moodRow != null)
                        {
                            int insertIndex = cardContent.Children.IndexOf(moodRow) + 1;
                            cardContent.Children.Insert(insertIndex, badgesStack);
                        }
                    }

                    var profileDetails = new VerticalStackLayout
                    {
                        Spacing = 6,
                        Margin = new Thickness(0, 6, 0, 0),
                        ClassId = "ProfileDetailsStack"
                    };

                    if (!string.IsNullOrEmpty(user.Bio))
                    {
                        var bio = user.Bio.Length > 80 ? user.Bio.Substring(0, 80) + "..." : user.Bio;
                        profileDetails.Children.Add(new Label
                        {
                            Text = bio,
                            FontSize = 11,
                            TextColor = Color.FromArgb("#AAAAAA"),
                            LineBreakMode = LineBreakMode.WordWrap,
                            MaxLines = 2
                        });
                    }

                    if (!string.IsNullOrEmpty(user.Mood))
                    {
                        profileDetails.Children.Add(new Label
                        {
                            Text = $"Looking for: {user.Mood}",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#008080"),
                            FontAttributes = FontAttributes.Bold
                        });
                    }

                    if (!string.IsNullOrEmpty(user.Ethnicity))
                    {
                        var ethnicityText = user.Ethnicity;
                        if (!string.IsNullOrEmpty(user.Tribe))
                            ethnicityText += $" · {user.Tribe}";

                        profileDetails.Children.Add(new Label
                        {
                            Text = ethnicityText,
                            FontSize = 11,
                            TextColor = Color.FromArgb("#888888")
                        });
                    }

                    if (!string.IsNullOrEmpty(user.PersonalityType))
                    {
                        var shortType = user.PersonalityType.Split('-')[0].Trim();
                        profileDetails.Children.Add(new Label
                        {
                            Text = shortType,
                            FontSize = 11,
                            TextColor = Color.FromArgb("#888888")
                        });
                    }

                    if (!string.IsNullOrEmpty(user.LoveLanguage))
                    {
                        var shortLove = user.LoveLanguage.Length > 20
                            ? user.LoveLanguage.Substring(0, 18) + "..."
                            : user.LoveLanguage;
                        profileDetails.Children.Add(new Label
                        {
                            Text = shortLove,
                            FontSize = 11,
                            TextColor = Color.FromArgb("#888888")
                        });
                    }

                    if (!string.IsNullOrEmpty(user.EnergyLevel))
                    {
                        profileDetails.Children.Add(new Label
                        {
                            Text = user.EnergyLevel,
                            FontSize = 11,
                            TextColor = Color.FromArgb("#888888")
                        });
                    }

                    if (!string.IsNullOrEmpty(user.Interests))
                    {
                        var interests = user.Interests.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(i => i.Trim())
                            .Take(3)
                            .ToList();

                        if (interests.Any())
                        {
                            var interestScrollView = new ScrollView
                            {
                                Orientation = ScrollOrientation.Horizontal,
                                HeightRequest = 32,
                                Margin = new Thickness(0, 2, 0, 0)
                            };

                            var interestStack = new HorizontalStackLayout { Spacing = 6 };

                            foreach (var interest in interests)
                            {
                                interestStack.Children.Add(new Border
                                {
                                    BackgroundColor = Color.FromArgb("#1A2A1A"),
                                    StrokeThickness = 0.5,
                                    Stroke = Color.FromArgb("#008080"),
                                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                                    Padding = new Thickness(8, 3),
                                    Content = new Label
                                    {
                                        Text = interest,
                                        FontSize = 10,
                                        TextColor = Color.FromArgb("#008080")
                                    }
                                });
                            }

                            interestScrollView.Content = interestStack;
                            profileDetails.Children.Add(interestScrollView);
                        }
                    }

                    var lifestyleIcons = new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Margin = new Thickness(0, 4, 0, 0)
                    };

                    if (!string.IsNullOrEmpty(user.Drinks))
                    {
                        var drinkColor = user.Drinks.ToLower() switch
                        {
                            "yes" => "#FF4444",
                            "socially" => "#FFA500",
                            _ => "#888888"
                        };
                        lifestyleIcons.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb(drinkColor),
                            StrokeShape = new RoundRectangle { CornerRadius = 6 },
                            Padding = new Thickness(7, 3),
                            Content = new Label
                            {
                                Text = "Drinks",
                                FontSize = 10,
                                TextColor = Color.FromArgb(drinkColor)
                            }
                        });
                    }

                    if (user.Smokes)
                    {
                        lifestyleIcons.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#FF4444"),
                            StrokeShape = new RoundRectangle { CornerRadius = 6 },
                            Padding = new Thickness(7, 3),
                            Content = new Label
                            {
                                Text = "Smokes",
                                FontSize = 10,
                                TextColor = Color.FromArgb("#FF4444")
                            }
                        });
                    }

                    if (user.HasPets)
                    {
                        lifestyleIcons.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#4CAF50"),
                            StrokeShape = new RoundRectangle { CornerRadius = 6 },
                            Padding = new Thickness(7, 3),
                            Content = new Label
                            {
                                Text = "Has Pets",
                                FontSize = 10,
                                TextColor = Color.FromArgb("#4CAF50")
                            }
                        });
                    }

                    if (!string.IsNullOrEmpty(user.FavoriteMusicGenre) || !string.IsNullOrEmpty(user.BestMusic))
                    {
                        var musicText = !string.IsNullOrEmpty(user.FavoriteMusicGenre)
                            ? user.FavoriteMusicGenre
                            : user.BestMusic;
                        lifestyleIcons.Children.Add(new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#008080"),
                            StrokeShape = new RoundRectangle { CornerRadius = 6 },
                            Padding = new Thickness(7, 3),
                            Content = new Label
                            {
                                Text = musicText,
                                FontSize = 10,
                                TextColor = Color.FromArgb("#008080")
                            }
                        });
                    }

                    if (lifestyleIcons.Children.Count > 0)
                        profileDetails.Children.Add(lifestyleIcons);

                    if (profileDetails.Children.Count > 0)
                    {
                        var messageBubble = cardContent.Children
                            .OfType<Border>()
                            .FirstOrDefault(b => b.Content is Label);

                        if (messageBubble != null)
                        {
                            int insertIndex = cardContent.Children.IndexOf(messageBubble) + 1;

                            cardContent.Children.Insert(insertIndex, new BoxView
                            {
                                HeightRequest = 0.5,
                                BackgroundColor = Color.FromArgb("#252525"),
                                Margin = new Thickness(0, 6, 0, 4)
                            });

                            cardContent.Children.Insert(insertIndex + 1, profileDetails);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowPreview: Error loading profile details: {ex}");
            }

            PreviewAvailability.Children.Clear();

            void AddAvailIcon(string svgPath, string label, string colorHex = "#008080")
            {
                var stack = new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.Center
                };

                var converter = new PathGeometryConverter();
                var geometry = converter.ConvertFromInvariantString(svgPath) as Geometry;

                var path = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = geometry ?? new PathGeometry(),
                    Fill = Color.FromArgb(colorHex),
                    HeightRequest = 18,
                    WidthRequest = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Aspect = Stretch.Uniform
                };

                stack.Children.Add(path);
                stack.Children.Add(new Label
                {
                    Text = label,
                    FontSize = 10,
                    TextColor = Color.FromArgb(colorHex == "#008080" ? "#888888" : colorHex),
                    HorizontalOptions = LayoutOptions.Center
                });

                PreviewAvailability.Children.Add(stack);
            }

            if (_chatSelected)
                AddAvailIcon(
                    "M266-426h428v-28H266v28Zm0-120h428v-28H266v28Zm0-120h428v-28H266v28ZM132-178v-590q0-24 18-42t42-18h596q24 0 42 18t18 42v416q0 24-18 42t-42 18H244L132-178Zm98-138h562v-432H192v498l38-66Zm-38 0v-432 432Z",
                    "Chat");

            if (_voiceSelected)
                AddAvailIcon(
                    "M798-120q-125 0-247-54.5T329-329Q229-429 174.5-551T120-798q0-18 12-30t30-12h162q14 0 25 9.5t13 22.5l26 140q2 16-1 27t-11 19l-97 98q20 37 47.5 71.5T387-386q31 31 65 57.5t72 48.5l94-94q9-9 23.5-13.5T670-390l138 28q14 3 23 14t9 25v162q0 18-12 30t-30 12Z",
                    "Voice");

            if (_videoSelected)
                AddAvailIcon(
                    "M160-160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h480q33 0 56.5 23.5T720-720v180l160-160v440L720-420v180q0 33-23.5 56.5T640-160H160Zm0-80h480v-480H160v480Zm0 0v-480 480Z",
                    "Video");

            if (user?.IsVerified == true)
                AddAvailIcon(
                    "m366-126-64-108-122-26 12-126-82-94 82-94-12-126 122-26 64-108 114 48 114-48 64 108 122 26-12 126 82 94-82 94 12 126-122 26-64 108-114-48-114 48Zm12-36 102-42 102 42 58-96 110-24-10-114 74-84-74-84 10-114-110-24-58-96-102 42-102-42-58 96-110 24 10 114-74 84 74 84-10 114 110 24 58 96Zm102-318Zm-42 106 190-190-20-20-170 170-86-86-20 20 106 106Z",
                    "Verified",
                    "#00B5B5");

            if (_selectedImagePaths.Any())
            {
                var imageScrollView = new ScrollView
                {
                    Orientation = ScrollOrientation.Horizontal,
                    HeightRequest = 120,
                    Margin = new Thickness(0, 8, 0, 0),
                    ClassId = "PreviewImagesScrollView"
                };

                var imageStack = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Padding = new Thickness(0, 0, 16, 0)
                };

                foreach (var imagePath in _selectedImagePaths.Take(MAX_IMAGES))
                {
                    if (File.Exists(imagePath))
                    {
                        var imageBorder = new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#333333"),
                            StrokeShape = new RoundRectangle { CornerRadius = 8 },
                            WidthRequest = 100,
                            HeightRequest = 100,
                            Padding = new Thickness(0)
                        };

                        var image = new Image
                        {
                            Source = ImageSource.FromFile(imagePath),
                            Aspect = Aspect.AspectFill,
                            WidthRequest = 100,
                            HeightRequest = 100
                        };

                        imageBorder.Content = image;
                        imageStack.Children.Add(imageBorder);
                    }
                }

                imageScrollView.Content = imageStack;

                var previewCard = PreviewSection.Children.LastOrDefault() as Border;
                if (previewCard?.Content is VerticalStackLayout cardContent && imageStack.Children.Count > 0)
                {
                    var messageBubble = cardContent.Children
                        .OfType<Border>()
                        .FirstOrDefault(b => b.ClassId == "MessageBubble");

                    if (messageBubble != null)
                    {
                        int insertIndex = cardContent.Children.IndexOf(messageBubble) + 1;
                        cardContent.Children.Insert(insertIndex, imageScrollView);
                    }
                }
            }

            // Add live session images to preview card
            if (_liveSessionImagePaths.Any())
            {
                var imageScrollView = new ScrollView
                {
                    Orientation = ScrollOrientation.Horizontal,
                    HeightRequest = 120,
                    Margin = new Thickness(0, 8, 0, 0),
                    ClassId = "PreviewImagesScrollView"
                };

                var imageStack = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Padding = new Thickness(0, 0, 16, 0)
                };

                foreach (var imagePath in _liveSessionImagePaths.Take(MAX_IMAGES))
                {
                    if (File.Exists(imagePath))
                    {
                        var imageBorder = new Border
                        {
                            BackgroundColor = Color.FromArgb("#1A1A1A"),
                            StrokeThickness = 0.5,
                            Stroke = Color.FromArgb("#333333"),
                            StrokeShape = new RoundRectangle { CornerRadius = 8 },
                            WidthRequest = 100,
                            HeightRequest = 100,
                            Padding = new Thickness(0)
                        };
                        imageBorder.Content = new Image
                        {
                            Source = ImageSource.FromFile(imagePath),
                            Aspect = Aspect.AspectFill,
                            WidthRequest = 100,
                            HeightRequest = 100
                        };
                        imageStack.Children.Add(imageBorder);
                    }
                }

                imageScrollView.Content = imageStack;

                var previewCard = PreviewSection.Children.LastOrDefault() as Border;
                if (previewCard?.Content is VerticalStackLayout cardContent && imageStack.Children.Count > 0)
                {
                    var messageBubble = cardContent.Children
                        .OfType<Border>()
                        .FirstOrDefault(b => b.ClassId == "MessageBubble");

                    if (messageBubble != null)
                    {
                        int insertIndex = cardContent.Children.IndexOf(messageBubble) + 1;
                        cardContent.Children.Insert(insertIndex, imageScrollView);
                    }
                }
            }

            PreviewSection.IsVisible = true;

            PreviewSection.IsVisible = true;
        }
        // ???????????????????????????????????????????????????????????????????????????
        // NEW METHODS FOR "SEE WHO'S LIVE" BANNER
        // ???????????????????????????????????????????????????????????????????????????

        private async void OnSeeWhoIsLiveTapped(object sender, TappedEventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new LiveFeedPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSeeWhoIsLiveTapped error: {ex}");
            }
        }

        private async Task UpdateLiveCountBannerAsync()
        {
            try
            {
                var db = DatabaseService.GetConnection();
                var currentPhone = Preferences.Get("current_user_phone", string.Empty);

                var liveSessions = await db.Table<LiveSession>().ToListAsync();
                var count = liveSessions.Count(s => s.IsLive
                                                 && s.EndedAt == null
                                                 && !string.Equals(s.UserPhoneNumber, currentPhone,
                                                                   StringComparison.OrdinalIgnoreCase));

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (LiveCountBanner == null) return;

                    if (count > 0)
                    {
                        LiveCountBanner.Text = $"{count} {(count == 1 ? "person" : "people")} live right now — tap to see";
                    }
                    else
                    {
                        LiveCountBanner.Text = "No one live yet — be the first!";
                    }
                });

                if (SeeWhoLiveDot != null)
                {
                    SeeWhoLiveDot.AbortAnimation("BannerPulse");
                    if (count > 0)
                    {
                        var anim = new Animation();
                        anim.Add(0, 0.5, new Animation(v => SeeWhoLiveDot.Opacity = v,
                            1.0, 0.25, Easing.SinInOut));
                        anim.Add(0.5, 1.0, new Animation(v => SeeWhoLiveDot.Opacity = v,
                            0.25, 1.0, Easing.SinInOut));
                        anim.Commit(SeeWhoLiveDot, "BannerPulse",
                            length: 1000, easing: Easing.Linear,
                            finished: null, repeat: () => true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateLiveCountBannerAsync error: {ex}");
            }
        }
    }
}