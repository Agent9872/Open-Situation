using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lock.Pages.Discover
{
    // View-model for a single live user card with INotifyPropertyChanged
    public class LiveUserCard : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Private fields
        private string _phoneNumber = string.Empty;
        private string _name = string.Empty;
        private string _profileImagePath = string.Empty;
        private string _mood = string.Empty;
        private string _message = string.Empty;
        private string _location = string.Empty;
        private bool _chatAvailable;
        private bool _voiceAvailable;
        private bool _videoAvailable;
        private DateTime _startedAt;
        private DateTime? _scheduledEndTime;
        private int? _durationMinutes;
        private int _age;
        private string _bio = string.Empty;
        private string _interests = string.Empty;
        private string _favoriteMusicGenre = string.Empty;
        private string _bestMusic = string.Empty;
        private string _topInterest = string.Empty;

        // NEW PROPERTIES FROM PROFILE PAGE
        private string _gender = string.Empty;
        private string _lookingFor = string.Empty;
        private string _height = string.Empty;
        private string _bodyType = string.Empty;
        private string _ethnicity = string.Empty;
        private string _tribe = string.Empty;
        private string _personalityType = string.Empty;
        private string _loveLanguage = string.Empty;
        private string _energyLevel = string.Empty;
        private string _drinks = string.Empty;
        private bool _smokes;
        private bool _hasPets;
        private string _religion = string.Empty;
        private string _politicalViews = string.Empty;
        private string _kidsPreference = string.Empty;
        private string _hasChildren = string.Empty;
        private string _dietaryPreference = string.Empty;
        private string _exerciseFrequency = string.Empty;
        private string _topArtist = string.Empty;
        private string _topMovie = string.Empty;
        private string _favoriteMovies = string.Empty;
        private string _favoriteBooks = string.Empty;
        private string _languages = string.Empty;
        private string _occupation = string.Empty;
        private string _education = string.Empty;
        private bool _hasVoiceIntro;
        private bool _isVerified;
        private bool _isMoodBlinking = true;

        // Add these fields to LiveUserCard class
        private List<string> _imageCarouselPaths = new List<string>();
        private int _currentImageIndex = 0;
        private Timer? _carouselTimer;
        private ImageSource _currentCarouselImage;
        private ImageSource _nextCarouselImage;
        private float _currentImageOpacity = 1.0f;
        private float _nextImageOpacity = 0.0f;

        private List<ImageSource> _preloadedSources = new();

        public List<string> ImageCarouselPaths
        {
            get => _imageCarouselPaths;
            set
            {
                _imageCarouselPaths = value ?? new List<string>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCarouselImages));

                // Pre-load ALL sources at assignment time
                _preloadedSources = _imageCarouselPaths
                    .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                    .Select(p => (ImageSource)ImageSource.FromFile(p))
                    .ToList();

                _currentCarouselImage = _preloadedSources.Count > 0
                    ? _preloadedSources[0]
                    : ProfileImage;

                OnPropertyChanged(nameof(CurrentCarouselImage));
            }
        }


        public ImageSource CurrentCarouselImage
        {
            get => _currentCarouselImage ?? ProfileImage;
            private set
            {
                _currentCarouselImage = value;
                OnPropertyChanged();
            }
        }

        public void StartCarousel()
        {
            StopCarousel();
            if (_preloadedSources.Count <= 1) return;

            _currentImageIndex = 0;

            // Simple timer — just swap the index, use pre-loaded source
            // No async, no opacity, no crossfade — eliminates all blink
            _carouselTimer = new Timer(_ =>
            {
                _currentImageIndex = (_currentImageIndex + 1) % _preloadedSources.Count;
                var next = _preloadedSources[_currentImageIndex];

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CurrentCarouselImage = next;
                });

            }, null, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4));
        }

        public void StopCarousel()
        {
            _carouselTimer?.Dispose();
            _carouselTimer = null;
        }

        public bool HasCarouselImages => _imageCarouselPaths.Count > 1;

     

        public ImageSource NextCarouselImage
        {
            get => _nextCarouselImage ?? ProfileImage;
            set { _nextCarouselImage = value; OnPropertyChanged(); }
        }

        public float CurrentImageOpacity
        {
            get => _currentImageOpacity;
            set { _currentImageOpacity = value; OnPropertyChanged(); }
        }

        public float NextImageOpacity
        {
            get => _nextImageOpacity;
            set { _nextImageOpacity = value; OnPropertyChanged(); }
        }

        private ImageSource GetImageSource(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return ImageSource.FromFile(path);
            return ProfileImage;
        }

     

        // Public properties with change notification
        public string PhoneNumber
        {
            get => _phoneNumber;
            set { _phoneNumber = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string ProfileImagePath
        {
            get => _profileImagePath;
            set { _profileImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProfileImage)); }
        }

        public string Mood
        {
            get => _mood;
            set { _mood = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMood)); OnPropertyChanged(nameof(MoodBlinkColor)); }
        }

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLocation)); }
        }

        public bool ChatAvailable
        {
            get => _chatAvailable;
            set { _chatAvailable = value; OnPropertyChanged(); }
        }

        public bool VoiceAvailable
        {
            get => _voiceAvailable;
            set { _voiceAvailable = value; OnPropertyChanged(); }
        }

        public bool VideoAvailable
        {
            get => _videoAvailable;
            set { _videoAvailable = value; OnPropertyChanged(); }
        }

        public DateTime StartedAt
        {
            get => _startedAt;
            set { _startedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(LiveSince)); }
        }

        public DateTime? ScheduledEndTime
        {
            get => _scheduledEndTime;
            set
            {
                _scheduledEndTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeRemaining));
                OnPropertyChanged(nameof(CountdownColor));
                OnPropertyChanged(nameof(ShowCountdown));
            }
        }

        public int? DurationMinutes
        {
            get => _durationMinutes;
            set { _durationMinutes = value; OnPropertyChanged(); }
        }

        public int Age
        {
            get => _age;
            set { _age = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAge)); OnPropertyChanged(nameof(AgeText)); }
        }

        public string Bio
        {
            get => _bio;
            set { _bio = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBio)); OnPropertyChanged(nameof(BioPreview)); }
        }

        public string Interests
        {
            get => _interests;
            set { _interests = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInterests)); OnPropertyChanged(nameof(InterestsDisplay)); }
        }

        public string FavoriteMusicGenre
        {
            get => _favoriteMusicGenre;
            set { _favoriteMusicGenre = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMusic)); OnPropertyChanged(nameof(MusicText)); }
        }

        public string BestMusic
        {
            get => _bestMusic;
            set { _bestMusic = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMusic)); OnPropertyChanged(nameof(MusicText)); }
        }

        public string TopInterest
        {
            get => _topInterest;
            set { _topInterest = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTopInterest)); }
        }

        // NEW PROPERTIES
        public string Gender
        {
            get => _gender;
            set { _gender = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGender)); }
        }

        public string LookingFor
        {
            get => _lookingFor;
            set { _lookingFor = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLookingFor)); }
        }

        public string Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasHeight)); }
        }

        public string BodyType
        {
            get => _bodyType;
            set { _bodyType = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBodyType)); }
        }

        public string Ethnicity
        {
            get => _ethnicity;
            set { _ethnicity = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEthnicity)); }
        }

        public string Tribe
        {
            get => _tribe;
            set { _tribe = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTribe)); }
        }

        public string PersonalityType
        {
            get => _personalityType;
            set { _personalityType = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPersonalityType)); OnPropertyChanged(nameof(PersonalityTypeShort)); }
        }

        public string LoveLanguage
        {
            get => _loveLanguage;
            set { _loveLanguage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLoveLanguage)); OnPropertyChanged(nameof(LoveLanguageShort)); }
        }

        public string EnergyLevel
        {
            get => _energyLevel;
            set { _energyLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEnergyLevel)); OnPropertyChanged(nameof(EnergyLevelShort)); }
        }

        public string Drinks
        {
            get => _drinks;
            set { _drinks = value; OnPropertyChanged(); OnPropertyChanged(nameof(DrinkColor)); }
        }

        public bool Smokes
        {
            get => _smokes;
            set { _smokes = value; OnPropertyChanged(); }
        }

        public bool HasPets
        {
            get => _hasPets;
            set { _hasPets = value; OnPropertyChanged(); }
        }

        public string Religion
        {
            get => _religion;
            set { _religion = value; OnPropertyChanged(); }
        }

        public string PoliticalViews
        {
            get => _politicalViews;
            set { _politicalViews = value; OnPropertyChanged(); }
        }

        public string KidsPreference
        {
            get => _kidsPreference;
            set { _kidsPreference = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasKidsPreference)); }
        }

        public string HasChildren
        {
            get => _hasChildren;
            set { _hasChildren = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasChildrenDisplay)); }
        }

        public string DietaryPreference
        {
            get => _dietaryPreference;
            set { _dietaryPreference = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDietaryPreference)); }
        }

        public string ExerciseFrequency
        {
            get => _exerciseFrequency;
            set { _exerciseFrequency = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasExerciseFrequency)); }
        }

        public string TopArtist
        {
            get => _topArtist;
            set { _topArtist = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTopArtist)); }
        }

        public string TopMovie
        {
            get => _topMovie;
            set { _topMovie = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTopMovie)); }
        }

        public string FavoriteMovies
        {
            get => _favoriteMovies;
            set { _favoriteMovies = value; OnPropertyChanged(); }
        }

        public string FavoriteBooks
        {
            get => _favoriteBooks;
            set { _favoriteBooks = value; OnPropertyChanged(); }
        }

        public string Languages
        {
            get => _languages;
            set { _languages = value; OnPropertyChanged(); }
        }

        public string Occupation
        {
            get => _occupation;
            set { _occupation = value; OnPropertyChanged(); }
        }

        public string Education
        {
            get => _education;
            set { _education = value; OnPropertyChanged(); }
        }

        public bool HasVoiceIntro
        {
            get => _hasVoiceIntro;
            set { _hasVoiceIntro = value; OnPropertyChanged(); }
        }

        public bool IsVerified
        {
            get => _isVerified;
            set { _isVerified = value; OnPropertyChanged(); }
        }

        public bool IsMoodBlinking
        {
            get => _isMoodBlinking;
            set { _isMoodBlinking = value; OnPropertyChanged(); }
        }

        public Color MoodBlinkColor
        {
            get
            {
                if (string.IsNullOrEmpty(Mood)) return Color.FromArgb("#FF3B6F");

                var moodLower = Mood.ToLower();

                if (moodLower.Contains("horny")) return Color.FromArgb("#FF1493");
                if (moodLower.Contains("romantic")) return Color.FromArgb("#FF69B4");
                if (moodLower.Contains("chill")) return Color.FromArgb("#00B5B5");
                if (moodLower.Contains("playful")) return Color.FromArgb("#FFA500");
                if (moodLower.Contains("adventurous")) return Color.FromArgb("#FF4500");
                if (moodLower.Contains("talkative")) return Color.FromArgb("#4CAF50");
                if (moodLower.Contains("bored")) return Color.FromArgb("#9E9E9E");
                if (moodLower.Contains("flirty")) return Color.FromArgb("#FF6B6B");
                if (moodLower.Contains("mysterious")) return Color.FromArgb("#9B59B6");
                if (moodLower.Contains("happy")) return Color.FromArgb("#FFD700");
                if (moodLower.Contains("excited")) return Color.FromArgb("#FF6600");
                if (moodLower.Contains("curious")) return Color.FromArgb("#1E90FF");
                if (moodLower.Contains("supportive")) return Color.FromArgb("#20B2AA");
                if (moodLower.Contains("deep talks")) return Color.FromArgb("#8A2BE2");

                return Color.FromArgb("#FF3B6F");
            }
        }

        // Computed properties for binding
        public bool HasLocation => !string.IsNullOrEmpty(Location);
        public bool HasAge => Age > 0;
        public bool HasBio => !string.IsNullOrEmpty(Bio);
        public bool HasInterests => !string.IsNullOrEmpty(Interests);
        public bool HasMusic => !string.IsNullOrEmpty(FavoriteMusicGenre) || !string.IsNullOrEmpty(BestMusic);
        public bool HasMood => !string.IsNullOrEmpty(Mood);
        public bool HasGender => !string.IsNullOrEmpty(Gender);
        public bool HasLookingFor => !string.IsNullOrEmpty(LookingFor);
        public bool HasHeight => !string.IsNullOrEmpty(Height);
        public bool HasBodyType => !string.IsNullOrEmpty(BodyType);
        public bool HasEthnicity => !string.IsNullOrEmpty(Ethnicity);
        public bool HasTribe => !string.IsNullOrEmpty(Tribe);
        public bool HasPersonalityType => !string.IsNullOrEmpty(PersonalityType);
        public bool HasLoveLanguage => !string.IsNullOrEmpty(LoveLanguage);
        public bool HasEnergyLevel => !string.IsNullOrEmpty(EnergyLevel);
        public bool HasKidsPreference => !string.IsNullOrEmpty(KidsPreference);
        public bool HasDietaryPreference => !string.IsNullOrEmpty(DietaryPreference);
        public bool HasExerciseFrequency => !string.IsNullOrEmpty(ExerciseFrequency);
        public bool HasTopInterest => !string.IsNullOrEmpty(TopInterest);
        public bool HasTopArtist => !string.IsNullOrEmpty(TopArtist);
        public bool HasTopMovie => !string.IsNullOrEmpty(TopMovie);

        public string AgeText => HasAge ? $"{Age} years old" : string.Empty;

        public string BioPreview => string.IsNullOrEmpty(Bio) ? string.Empty :
            (Bio.Length > 60 ? Bio.Substring(0, 60) + "..." : Bio);

        public string InterestsDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Interests)) return string.Empty;
                var interestsList = Interests.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim())
                    .Take(2);
                return string.Join(" • ", interestsList);
            }
        }

        public string MusicText
        {
            get
            {
                if (!string.IsNullOrEmpty(FavoriteMusicGenre) && !string.IsNullOrEmpty(BestMusic))
                    return $"{FavoriteMusicGenre} • {BestMusic}";
                if (!string.IsNullOrEmpty(FavoriteMusicGenre))
                    return FavoriteMusicGenre;
                if (!string.IsNullOrEmpty(BestMusic))
                    return BestMusic;
                return string.Empty;
            }
        }

        public string PersonalityTypeShort
        {
            get
            {
                if (string.IsNullOrEmpty(PersonalityType)) return string.Empty;
                var parts = PersonalityType.Split('-');
                return parts[0].Trim();
            }
        }

        public string LoveLanguageShort
        {
            get
            {
                if (string.IsNullOrEmpty(LoveLanguage)) return string.Empty;
                if (LoveLanguage.Length > 12)
                    return LoveLanguage.Substring(0, 10) + "...";
                return LoveLanguage;
            }
        }

        public string EnergyLevelShort
        {
            get
            {
                if (string.IsNullOrEmpty(EnergyLevel)) return string.Empty;
                if (EnergyLevel == "Introvert") return "?? Introvert";
                if (EnergyLevel == "Extrovert") return "?? Extrovert";
                return EnergyLevel;
            }
        }

        public string HasChildrenDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(HasChildren)) return string.Empty;
                if (HasChildren == "Have children") return "?? Has kids";
                if (HasChildren == "Don't have children") return "?? No kids";
                return HasChildren;
            }
        }

        public Color DrinkColor
        {
            get
            {
                if (string.IsNullOrEmpty(Drinks)) return Color.FromArgb("#7A7A8C");
                return Drinks.ToLower() switch
                {
                    "yes" => Color.FromArgb("#FF4444"),
                    "socially" => Color.FromArgb("#FFA500"),
                    "no" => Color.FromArgb("#4CAF50"),
                    _ => Color.FromArgb("#7A7A8C")
                };
            }
        }

        public string LiveSince
        {
            get
            {
                var diff = DateTime.UtcNow - StartedAt;
                if (diff.TotalSeconds < 60)
                    return "just went live";
                if (diff.TotalMinutes < 60)
                    return $"live {(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24)
                    return $"live {(int)diff.TotalHours}h ago";
                return "live today";
            }
        }

        public string TimeRemaining
        {
            get
            {
                if (!ScheduledEndTime.HasValue) return string.Empty;

                var timeRemaining = ScheduledEndTime.Value - DateTime.UtcNow;

                if (timeRemaining.TotalSeconds <= 0)
                    return "Ending now";
                else if (timeRemaining.TotalHours >= 1)
                    return $"{timeRemaining:hh\\:mm\\:ss}";
                else if (timeRemaining.TotalMinutes >= 1)
                    return $"{timeRemaining:mm\\:ss}";
                else
                    return $"{timeRemaining:ss}s";
            }
        }

        public Color CountdownColor
        {
            get
            {
                if (!ScheduledEndTime.HasValue) return Color.FromArgb("#4CAF50");
                var timeRemaining = ScheduledEndTime.Value - DateTime.UtcNow;
                if (timeRemaining.TotalSeconds <= 30) return Color.FromArgb("#FF4444");
                if (timeRemaining.TotalSeconds <= 60) return Color.FromArgb("#FFA500");
                return Color.FromArgb("#4CAF50");
            }
        }

        public bool ShowCountdown => ScheduledEndTime.HasValue && ScheduledEndTime.Value > DateTime.UtcNow;

        public ImageSource ProfileImage =>
            (!string.IsNullOrEmpty(ProfileImagePath) && File.Exists(ProfileImagePath))
                ? ImageSource.FromFile(ProfileImagePath)
                : ImageSource.FromFile("default_avatar.png");

        private Timer _countdownTimer;
        private DateTime? _lastUpdateTime;

        private float _moodOpacity = 1.0f;
        private Timer? _moodBlinkTimer;

        public float MoodOpacity
        {
            get => _moodOpacity;
            set { _moodOpacity = value; OnPropertyChanged(); }
        }

        public void StartMoodBlinking()
        {
            _moodBlinkTimer?.Dispose();
            bool fadingOut = true;
            _moodBlinkTimer = new Timer(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Slower, smoother transition
                    if (fadingOut)
                    {
                        MoodOpacity = Math.Max(0.2f, MoodOpacity - 0.03f);  // Slower decrement
                        if (MoodOpacity <= 0.2f) fadingOut = false;
                    }
                    else
                    {
                        MoodOpacity = Math.Min(1.0f, MoodOpacity + 0.03f);  // Slower increment
                        if (MoodOpacity >= 1.0f) fadingOut = true;
                    }
                });
            }, null, 0, 50); // Update every 50ms (20fps) for smoother, slower animation
        }
        public void StopMoodBlinking()
        {
            _moodBlinkTimer?.Dispose();
            _moodBlinkTimer = null;
            MoodOpacity = 1.0f;
        }

        public void StartCountdownUpdates()
        {
            StopCountdownUpdates();
            // Only update countdown label text, nothing that affects image
            _countdownTimer = new Timer(OnTimerTick, null, 0, 1000);
        }

        private void OnTimerTick(object state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // ONLY notify countdown-specific properties
                // Do NOT notify anything that could trigger card layout recalculation
                OnPropertyChanged(nameof(TimeRemaining));
                OnPropertyChanged(nameof(ShowCountdown));

                // Stop if expired
                if (ScheduledEndTime.HasValue && ScheduledEndTime.Value <= DateTime.UtcNow)
                    StopCountdownUpdates();
            });
        }

        private void UpdateCountdownProperties()
        {
            // Force property change notifications for all countdown-related properties
            OnPropertyChanged(nameof(TimeRemaining));
            OnPropertyChanged(nameof(CountdownColor));
            OnPropertyChanged(nameof(ShowCountdown));
            OnPropertyChanged(nameof(LiveSince));

            // If the session has ended, stop the timer
            if (ScheduledEndTime.HasValue && ScheduledEndTime.Value <= DateTime.UtcNow)
            {
                StopCountdownUpdates();
            }
        }

        public void StopCountdownUpdates()
        {
            _countdownTimer?.Dispose();
            _countdownTimer = null;
        }
    }

    public partial class LiveFeedPage : ContentPage
    {
        private readonly ObservableCollection<LiveUserCard> _cards = new();
        private string _selectedFilter = "All";
        private List<LiveUserCard> _allCards = new();

        private static readonly string[] FilterOptions =
        {
            "All", "Chat", "Voice", "Video",
            "Chill", "Playful", "Romantic", "Adventurous",
            "Talkative", "Bored", "Flirty", "Mysterious",
            "Happy", "Horny", "Lonely", "Excited",
            "Curious", "Supportive", "Deep talks"
        };

        public LiveFeedPage()
        {
            InitializeComponent();
            LiveFeedView.ItemsSource = _cards;
            BuildFilterChips();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // REMOVED: StartHeaderPulse();
            // REMOVED: StartLiveBadgeBlinking();
            await LoadLiveUsersAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // REMOVED: liveNowLabel animation abort
            // REMOVED: StopLiveBadgeBlinking();

            foreach (var card in _cards)
            {
                card.StopCountdownUpdates();
            }
        }

        private void StartLiveBadgeBlinking()
        {
            // DISABLED - causes carousel blink interference
        }
        private void StopLiveBadgeBlinking()
        {
            // DISABLED
            var liveBadge = this.FindByName<Label>("LiveBadgeText");
            if (liveBadge != null) liveBadge.Opacity = 1.0;
        }

        private void StartHeaderPulse()
        {
            // DISABLED - causes carousel blink interference
        }
        private async Task LoadLiveUsersAsync()
        {
            try
            {
                ShowSkeleton(true);

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var currentPhone = Preferences.Get("current_user_phone", string.Empty);

                var allSessions = await SupabaseService.GetAsync<LiveSession>("LiveSessions",
      $"IsLive=eq.true&EndedAt=is.null");

                var sessions = allSessions
                    .Where(s => !string.Equals(s.UserPhoneNumber, currentPhone,
                                              StringComparison.OrdinalIgnoreCase))
                    .GroupBy(s => s.UserPhoneNumber)
                    .Select(g => g.First())
                    .ToList();

                Debug.WriteLine($"Found {sessions.Count} live sessions");

                var cards = new List<LiveUserCard>();

                foreach (var session in sessions)
                {
                    try
                    {
                        if (session.ScheduledEndTime.HasValue &&
                            session.ScheduledEndTime.Value <= DateTime.UtcNow)
                        {
                            Debug.WriteLine($"Session expired for {session.UserPhoneNumber}");
                            session.IsLive = false;
                            session.EndedAt = DateTime.UtcNow;
                            // Replace: await db.UpdateAsync(session);
                            await SupabaseService.UpdateAsync("LiveSessions", $"Id=eq.{session.Id}", session);
                            continue;
                        }

                        // Remove this SQLite code:
                        // var user = await db.Table<User>()
                        //     .Where(u => u.PhoneNumber == session.UserPhoneNumber)
                        //     .FirstOrDefaultAsync();

                        // Replace with Supabase code:
                        var users = await SupabaseService.GetAsync<User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(session.UserPhoneNumber)}&limit=1");
                        var user = users.FirstOrDefault();

                        if (user == null) continue;

                        string heightText = string.Empty;
                        if (user.HeightCm.HasValue && user.HeightCm.Value > 0)
                        {
                            int feet = (int)(user.HeightCm.Value / 30.48);
                            int inches = (int)((user.HeightCm.Value % 30.48) / 2.54);
                            heightText = $"{feet}'{inches}\"";
                        }

                        string ethnicityDisplay = string.Empty;
                        if (!string.IsNullOrEmpty(user.Ethnicity) && !string.IsNullOrEmpty(user.Tribe))
                        {
                            ethnicityDisplay = $"{user.Ethnicity} · {user.Tribe}";
                        }
                        else if (!string.IsNullOrEmpty(user.Ethnicity))
                        {
                            ethnicityDisplay = user.Ethnicity;
                        }
                        else if (!string.IsNullOrEmpty(user.Tribe))
                        {
                            ethnicityDisplay = user.Tribe;
                        }

                        var card = new LiveUserCard
                        {
                            PhoneNumber = session.UserPhoneNumber,
                            Name = string.IsNullOrEmpty(user.Name) ? session.UserPhoneNumber : user.Name,
                            ProfileImagePath = user.ProfileImagePath ?? string.Empty,
                            Mood = session.Mood,
                            Message = session.Message,
                            Location = session.Location,
                            ChatAvailable = session.ChatAvailable,
                            VoiceAvailable = session.VoiceAvailable,
                            VideoAvailable = session.VideoAvailable,
                            StartedAt = session.StartedAt,
                            ScheduledEndTime = session.ScheduledEndTime,
                            DurationMinutes = session.DurationMinutes,
                            Age = GetAgeFromDateOfBirth(user.DateOfBirth),
                            Bio = user.Bio ?? string.Empty,
                            Interests = user.Interests ?? string.Empty,
                            FavoriteMusicGenre = user.FavoriteMusicGenre ?? string.Empty,
                            BestMusic = user.BestMusic ?? string.Empty,
                            TopInterest = user.TopInterest ?? string.Empty,
                            Gender = user.Gender ?? string.Empty,
                            LookingFor = user.Mood ?? string.Empty,
                            Height = heightText,
                            BodyType = user.BodyType ?? string.Empty,
                            Ethnicity = ethnicityDisplay,
                            Tribe = user.Tribe ?? string.Empty,
                            PersonalityType = user.PersonalityType ?? string.Empty,
                            LoveLanguage = user.LoveLanguage ?? string.Empty,
                            EnergyLevel = user.EnergyLevel ?? string.Empty,
                            Drinks = user.Drinks ?? string.Empty,
                            Smokes = user.Smokes,
                            HasPets = user.HasPets,
                            Religion = user.Religion ?? string.Empty,
                            PoliticalViews = user.PoliticalViews ?? string.Empty,
                            KidsPreference = user.KidsPreference ?? string.Empty,
                            HasChildren = user.HasChildren ?? string.Empty,
                            DietaryPreference = user.DietaryPreference ?? string.Empty,
                            ExerciseFrequency = user.ExerciseFrequency ?? string.Empty,
                            TopArtist = user.TopArtist ?? string.Empty,
                            TopMovie = user.TopMovie ?? string.Empty,
                            FavoriteMovies = user.FavoriteMovies ?? string.Empty,
                            FavoriteBooks = user.FavoriteBooks ?? string.Empty,
                            Languages = user.Languages ?? string.Empty,
                            Occupation = user.Occupation ?? string.Empty,
                            Education = user.Education ?? string.Empty,
                            HasVoiceIntro = !string.IsNullOrEmpty(user.VoiceIntroPath) && File.Exists(user.VoiceIntroPath),
                            IsVerified = user.IsVerified
                        };

                        if (!string.IsNullOrEmpty(session.ImagePathsJson))
                        {
                            try
                            {
                                var imagePaths = System.Text.Json.JsonSerializer
                                    .Deserialize<List<string>>(session.ImagePathsJson)
                                    ?? new List<string>();
                                // Filter to only existing files
                                card.ImageCarouselPaths = imagePaths
                                    .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                                    .ToList();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to deserialize carousel images: {ex}");
                                card.ImageCarouselPaths = new List<string>();
                            }
                        }

                        card.StartCountdownUpdates();
                        card.StartCarousel();
                        // REMOVED: card.StartMoodBlinking() if called anywhere
                        cards.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LiveFeedPage: error loading user {session.UserPhoneNumber}: {ex}");
                    }
                }

                _allCards = cards.OrderByDescending(c => c.StartedAt).ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ApplyFilter();
                    ShowSkeleton(false);
                    LiveCountLabel.Text = _cards.Any()
                        ? $"{_cards.Count} {(_cards.Count == 1 ? "person" : "people")} live right now"
                        : "No one is live right now";

                    // REMOVED: StartAllMoodAnimations();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LiveFeedPage.LoadLiveUsersAsync error: {ex}");
                ShowSkeleton(false);
                ShowEmpty("Could not load live users. Pull to refresh.");
            }
        }

        private int GetAgeFromDateOfBirth(DateTime dateOfBirth)
        {
            if (dateOfBirth == default) return 0;
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth > today.AddYears(-age)) age--;
            return age;
        }

        private void BuildFilterChips()
        {
            FilterChipsLayout.Children.Clear();

            foreach (var filter in FilterOptions)
            {
                bool isSelected = filter == "All";

                var chip = new Border
                {
                    BackgroundColor = isSelected ? Color.FromArgb("#008080") : Color.FromArgb("#141414"),
                    StrokeThickness = 0.5,
                    Stroke = isSelected ? Colors.Transparent : Color.FromArgb("#2A2A2A"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(14, 7),
                    VerticalOptions = LayoutOptions.Center
                };

                var label = new Label
                {
                    Text = filter,
                    FontSize = 12,
                    FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isSelected ? Colors.White : Color.FromArgb("#888888"),
                    VerticalOptions = LayoutOptions.Center
                };

                chip.Content = label;

                var capturedFilter = filter;
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => SelectFilter(capturedFilter);
                chip.GestureRecognizers.Add(tap);

                FilterChipsLayout.Children.Add(chip);
            }
        }

        private void SelectFilter(string filter)
        {
            _selectedFilter = filter;

            foreach (var child in FilterChipsLayout.Children)
            {
                if (child is not Border chip) continue;
                if (chip.Content is not Label lbl) continue;

                bool selected = lbl.Text == filter;

                chip.BackgroundColor = selected ? Color.FromArgb("#008080") : Color.FromArgb("#1A1A1A");
                chip.Stroke = selected ? Colors.Transparent : Color.FromArgb("#333333");
                lbl.TextColor = selected ? Colors.White : Color.FromArgb("#888888");
                lbl.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = _selectedFilter switch
            {
                "Chat" => _allCards.Where(c => c.ChatAvailable).ToList(),
                "Voice" => _allCards.Where(c => c.VoiceAvailable).ToList(),
                "Video" => _allCards.Where(c => c.VideoAvailable).ToList(),
                "All" => _allCards.ToList(),
                _ => _allCards.Where(c =>
                    c.Mood.Equals(_selectedFilter, StringComparison.OrdinalIgnoreCase) ||
                    c.Mood.Contains(_selectedFilter.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                    .ToList()
            };

            _cards.Clear();
            foreach (var card in filtered)
                _cards.Add(card);

            bool has = _cards.Any();
            LiveFeedView.IsVisible = has;
            EmptyContainer.IsVisible = !has;

            LiveCountLabel.Text = has
                ? $"{_cards.Count} {(_cards.Count == 1 ? "person" : "people")} live right now"
                : _selectedFilter == "All" ? "No one is live right now" : $"No one with mood '{_selectedFilter}' is live right now";
        }

        // ========== MOOD BLINKING ANIMATION METHODS ==========

        private void StartAllMoodAnimations()
        {
            // No-op — animation is handled per-card via a timer approach instead
            // We animate directly on the LiveUserCard model opacity via a separate overlay
        }

        private void FindAndAnimateMoodBorders(VisualElement parent)
        {
            if (parent == null) return;

            if (parent is Border border && border.Content is Label label && !string.IsNullOrEmpty(label.Text))
            {
                if (border.BindingContext is LiveUserCard card)
                {
                    AnimateMoodBorder(border, card);
                }
            }

            foreach (var child in parent.GetVisualTreeDescendants())
            {
                if (child is VisualElement ve)
                {
                    FindAndAnimateMoodBorders(ve);
                }
            }
        }

        private void AnimateMoodBorder(Border moodBorder, LiveUserCard card)
        {
            if (moodBorder == null || card == null) return;

            var animName = $"MoodAnim_{card.PhoneNumber}";
            moodBorder.AbortAnimation(animName);

            var blinkColor = card.MoodBlinkColor;

            // Find the inner dot BoxView
            BoxView? dot = null;
            if (moodBorder.Content is HorizontalStackLayout hsl)
            {
                dot = hsl.Children.OfType<BoxView>().FirstOrDefault();
            }

            var pulse = new Animation();

            // Fade out smoothly (0 ? 0.5)
            pulse.Add(0, 0.5, new Animation(v =>
            {
                if (moodBorder == null) return;
                moodBorder.Opacity = v;
                moodBorder.Stroke = Color.FromRgba(
                    blinkColor.Red, blinkColor.Green, blinkColor.Blue,
                    (float)v);
                if (dot != null)
                    dot.Opacity = v;
            }, 1.0, 0.25, Easing.SinInOut));

            // Fade back in smoothly (0.5 ? 1)
            pulse.Add(0.5, 1.0, new Animation(v =>
            {
                if (moodBorder == null) return;
                moodBorder.Opacity = v;
                moodBorder.Stroke = Color.FromRgba(
                    blinkColor.Red, blinkColor.Green, blinkColor.Blue,
                    (float)v);
                if (dot != null)
                    dot.Opacity = v;
            }, 0.25, 1.0, Easing.SinInOut));

            pulse.Commit(
                moodBorder,
                animName,
                16,
                900,           // same speed as LiveNowLabel
                Easing.Linear,
                finished: null,
                repeat: () => card.IsMoodBlinking);

            Debug.WriteLine($"? Mood badge blinking started for {card.Name} — color: {blinkColor}");
        }


        // Helper method to get visual tree descendants
        private IEnumerable<VisualElement> GetVisualTreeDescendants(VisualElement element)
        {
            if (element == null) yield break;

            if (element is IViewContainer<View> container)
            {
                foreach (var child in container.Children)
                {
                    yield return child;
                    foreach (var descendant in GetVisualTreeDescendants(child))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        // ========== END OF MOOD BLINKING METHODS ==========

        private async void OnLiveCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not LiveUserCard card) return;
            await ShowConnectOptions(card);
        }

        private async void OnConnectTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not LiveUserCard card) return;
            await ShowConnectOptions(card);
        }

        private async Task ShowConnectOptions(LiveUserCard card)
        {
            try
            {
                var options = new List<string>();
                if (card.ChatAvailable) options.Add("?? Send a message");
                if (card.VoiceAvailable) options.Add("??? Voice call");
                if (card.VideoAvailable) options.Add("?? Video call");
                options.Add("?? View profile");

                var action = await DisplayActionSheet(
                    $"Connect with {card.Name}",
                    "Cancel", null,
                    options.ToArray());

                if (action == null || action == "Cancel") return;

                if (action.Contains("message"))
                {
                    await Shell.Current.GoToAsync("conversations",
                        new Dictionary<string, object>
                        {
                            ["recipientPhone"] = card.PhoneNumber,
                            ["recipientName"] = card.Name,
                            ["openChat"] = "true"
                        });
                }
                else if (action.Contains("profile"))
                {
                    await Shell.Current.GoToAsync("///profile",
                        new Dictionary<string, object>
                        {
                            ["phone"] = card.PhoneNumber,
                            ["viewOnly"] = "true"
                        });
                }
                else
                {
                    await DisplayAlert($"Calling {card.Name}",
                        "Call feature coming soon. Sending a message instead.", "OK");
                    await Shell.Current.GoToAsync("conversations",
                        new Dictionary<string, object>
                        {
                            ["recipientPhone"] = card.PhoneNumber,
                            ["recipientName"] = card.Name,
                            ["openChat"] = "true"
                        });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LiveFeedPage.ShowConnectOptions error: {ex}");
            }
        }

        private async void OnBackTapped(object sender, TappedEventArgs e)
            => await Navigation.PopAsync();

        private async void OnRefreshTapped(object sender, TappedEventArgs e)
            => await LoadLiveUsersAsync();

        private async void OnGoLiveCTATapped(object sender, TappedEventArgs e)
            => await Navigation.PopAsync();

        private void ShowSkeleton(bool show)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SkeletonView.IsVisible = show;
                if (show)
                {
                    LiveFeedView.IsVisible = false;
                    EmptyContainer.IsVisible = false;
                }
            });
        }

        private void ShowEmpty(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SkeletonView.IsVisible = false;
                LiveFeedView.IsVisible = false;
                EmptyContainer.IsVisible = true;
                EmptySubLabel.Text = message;
            });
        }
    }

    // Extension method helper for getting visual tree descendants
    public static class VisualElementExtensions
    {
        public static IEnumerable<VisualElement> GetVisualTreeDescendants(this VisualElement element)
        {
            if (element == null) yield break;

            if (element is IViewContainer<View> container)
            {
                foreach (var child in container.Children)
                {
                    yield return child;
                    foreach (var descendant in child.GetVisualTreeDescendants())
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }
}