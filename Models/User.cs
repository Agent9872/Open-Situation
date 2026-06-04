using Lock.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lock.Models
{
    public class User : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ── PRIMARY KEY ──────────────────────────────────────
        public int Id { get; set; }

        // ── IDENTITY ─────────────────────────────────────────
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Interest { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }

        // Runtime property (not persisted)
        public string ZodiacSign => CalculateZodiacSign(DateOfBirth);

        public string ProfileImagePath { get; set; } = string.Empty;
        public string CoverImagePath { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public string InstagramHandle { get; set; } = string.Empty;
        public string SpotifyArtist { get; set; } = string.Empty;
        public string PersonalityType { get; set; } = string.Empty;

        // ── MOOD ─────────────────────────────────────────────
        public string Mood { get; set; } = string.Empty;
        public DateTime MoodLastUpdated { get; set; } = DateTime.UtcNow;
        public string EnergyLevel { get; set; } = string.Empty;

        // ── LOCATION ─────────────────────────────────────────
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        // ── PROFILE ──────────────────────────────────────────
        public string Bio { get; set; } = string.Empty;
        public string Interests { get; set; } = string.Empty;
        public string Drinks { get; set; } = string.Empty;
        public bool Smokes { get; set; }
        public bool HasPets { get; set; }
        public string Religion { get; set; } = string.Empty;
        public string PoliticalViews { get; set; } = string.Empty;

        // ── PREFERENCES ──────────────────────────────────────
        public int MinAgePreference { get; set; } = 18;
        public int MaxAgePreference { get; set; } = 100;
        public int MaxDistance { get; set; } = 50;
        public bool ShowMeOnApp { get; set; } = true;
        public string PreferredFirstDate { get; set; } = string.Empty;
        public string CommunicationStyle { get; set; } = string.Empty;
        public string DietPreference { get; set; } = string.Empty;
        public string WorkoutFrequency { get; set; } = string.Empty;
        public bool WantChildren { get; set; }
        public bool HaveChildren { get; set; }

        // ── MEDIA & CULTURE ───────────────────────────────────
        public string MusicGenres { get; set; } = string.Empty;
        public string FavoriteArtists { get; set; } = string.Empty;
        public string FavoriteMovies { get; set; } = string.Empty;
        public string FavoriteBooks { get; set; } = string.Empty;
        public string Languages { get; set; } = string.Empty;
        public string Occupation { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Prompts { get; set; } = string.Empty;
        public string Dealbreakers { get; set; } = string.Empty;
        public string Sports { get; set; } = string.Empty;
        public string CreativeHobbies { get; set; } = string.Empty;
        public string TravelStyle { get; set; } = string.Empty;
        public string FavoriteCuisines { get; set; } = string.Empty;
        public string Podcasts { get; set; } = string.Empty;
        public string TvShows { get; set; } = string.Empty;
        public string GamingInterests { get; set; } = string.Empty;
        public string WeekendVibe { get; set; } = string.Empty;
        public string TopInterest { get; set; } = string.Empty;
        public string TopArtist { get; set; } = string.Empty;
        public string TopMovie { get; set; } = string.Empty;
        public string SexualOrientation { get; set; } = string.Empty;
        public string BestMusic { get; set; } = string.Empty;
        public string FavoriteMusicGenre { get; set; } = string.Empty;

        // ── ACTIVITY ─────────────────────────────────────────
        public DateTime LastActive { get; set; } = DateTime.UtcNow;
        public bool AllowMoodSearch { get; set; } = true;
        public bool GhostModeMoodShield { get; set; } = false;

        // ── VERIFICATION ─────────────────────────────────────
        public bool IsVerified { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerificationIdNumber { get; set; }
        public string? VerificationIdType { get; set; }
        public DateTime? VerificationSubmittedAt { get; set; }
        public DateTime? VerificationVerifiedAt { get; set; }
        public string? VerificationStatus { get; set; }
        public string? VerificationRejectionReason { get; set; }
        public double VerificationScore { get; set; }

        // ── MODERATION ───────────────────────────────────────
        public bool IsBanned { get; set; } = false;
        public string BanType { get; set; } = string.Empty;
        public string BanReason { get; set; } = string.Empty;
        public DateTime? BannedAt { get; set; }
        public DateTime? BanExpiresAt { get; set; }
        public bool HasWarning { get; set; } = false;
        public string WarningMessage { get; set; } = string.Empty;
        public DateTime? WarnedAt { get; set; }
        public bool WarningAcknowledged { get; set; } = false;
        public string ModerationStatus { get; set; } = string.Empty;
        public string ModerationNote { get; set; } = string.Empty;
        public DateTime? ModerationUpdatedAt { get; set; }

        // ── APPEALS ──────────────────────────────────────────
        public string AppealText { get; set; } = string.Empty;
        public string AppealStatus { get; set; } = string.Empty;
        public DateTime? AppealSubmittedAt { get; set; }
        public string AppealAdminResponse { get; set; } = string.Empty;
        public DateTime? AppealReviewedAt { get; set; }

        // ── SYSTEM ───────────────────────────────────────────
        public string IpAddress { get; set; } = string.Empty;
        public bool HidePhoneNumber { get; set; } = false;

        // ── ROLE ─────────────────────────────────────────────
        public string Role { get; set; } = "User";

        // Runtime properties (not persisted)
        public bool IsAdmin => Role == "Admin";
        public bool IsModerator => Role == "Moderator" || IsAdmin;

        // ── PAGE PERMISSIONS ─────────────────────────────────
        public string DeniedPages { get; set; } = string.Empty;

        public HashSet<string> DeniedPageSet =>
            string.IsNullOrWhiteSpace(DeniedPages)
                ? new HashSet<string>()
                : new HashSet<string>(DeniedPages.Split(',',
                    StringSplitOptions.RemoveEmptyEntries));

        public bool CanAccessPage(string pageKey)
        {
            if (Role == "Admin") return true;
            return !DeniedPageSet.Contains(pageKey);
        }

        public void SetPageAccess(string pageKey, bool allowed)
        {
            var denied = DeniedPageSet;
            if (allowed) denied.Remove(pageKey);
            else denied.Add(pageKey);
            DeniedPages = string.Join(",", denied);
        }

        // ── VOICE INTRO ───────────────────────────────────────
        private string? _voiceIntroPath;
        public string? VoiceIntroPath
        {
            get => _voiceIntroPath;
            set { if (_voiceIntroPath != value) { _voiceIntroPath = value; OnPropertyChanged(); } }
        }

        private DateTime? _voiceIntroLastUpdated;
        public DateTime? VoiceIntroLastUpdated
        {
            get => _voiceIntroLastUpdated;
            set { if (_voiceIntroLastUpdated != value) { _voiceIntroLastUpdated = value; OnPropertyChanged(); } }
        }

        // ── PHYSICAL ─────────────────────────────────────────
        private int? _heightCm;
        public int? HeightCm
        {
            get => _heightCm;
            set { if (_heightCm != value) { _heightCm = value; OnPropertyChanged(); } }
        }

        private string? _bodyType;
        public string? BodyType
        {
            get => _bodyType;
            set { if (_bodyType != value) { _bodyType = value; OnPropertyChanged(); } }
        }

        private string? _ethnicity;
        public string? Ethnicity
        {
            get => _ethnicity;
            set { if (_ethnicity != value) { _ethnicity = value; OnPropertyChanged(); } }
        }

        private string? _tribe;
        public string? Tribe
        {
            get => _tribe;
            set { if (_tribe != value) { _tribe = value; OnPropertyChanged(); } }
        }

        // ── FAMILY ───────────────────────────────────────────
        private string? _kidsPreference;
        public string? KidsPreference
        {
            get => _kidsPreference;
            set { if (_kidsPreference != value) { _kidsPreference = value; OnPropertyChanged(); } }
        }

        private string? _hasChildren;
        public string? HasChildren
        {
            get => _hasChildren;
            set { if (_hasChildren != value) { _hasChildren = value; OnPropertyChanged(); } }
        }

        // ── LIFESTYLE ────────────────────────────────────────
        private string? _dietaryPreference;
        public string? DietaryPreference
        {
            get => _dietaryPreference;
            set { if (_dietaryPreference != value) { _dietaryPreference = value; OnPropertyChanged(); } }
        }

        private string? _exerciseFrequency;
        public string? ExerciseFrequency
        {
            get => _exerciseFrequency;
            set { if (_exerciseFrequency != value) { _exerciseFrequency = value; OnPropertyChanged(); } }
        }

        private string? _loveLanguage;
        public string? LoveLanguage
        {
            get => _loveLanguage;
            set { if (_loveLanguage != value) { _loveLanguage = value; OnPropertyChanged(); } }
        }

        // ── COIN BALANCE ─────────────────────────────────────
        public int CoinBalance { get; set; } = 0;

        // ── CONSTRUCTORS ─────────────────────────────────────
        public User() { }

        public User(
            string name,
            string phoneNumber,
            string password,
            DateTime dateOfBirth,
            string gender,
            string interest = "",
            string profileImagePath = "",
            string coverImagePath = "",
            string mood = "",
            string energyLevel = "",
            string country = "",
            string state = "",
            string bio = "",
            string interests = "",
            string drinks = "",
            bool smokes = false,
            bool hasPets = false,
            string religion = "",
            string politicalViews = "",
            string sexualOrientation = "",
            bool allowMoodSearch = true,
            bool ghostModeMoodShield = false,
            DateTime moodLastUpdated = default)
        {
            Name = name;
            DisplayName = name;
            PhoneNumber = phoneNumber;
            Password = password;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            Interest = interest;
            ProfileImagePath = profileImagePath;
            CoverImagePath = coverImagePath;
            Mood = mood;
            EnergyLevel = energyLevel;
            Country = country;
            State = state;
            Bio = bio;
            Interests = interests;
            Drinks = drinks;
            Smokes = smokes;
            HasPets = hasPets;
            Religion = religion;
            PoliticalViews = politicalViews;
            SexualOrientation = sexualOrientation;
            AllowMoodSearch = allowMoodSearch;
            GhostModeMoodShield = ghostModeMoodShield;
            MoodLastUpdated = moodLastUpdated == default ? DateTime.UtcNow : moodLastUpdated;
            LastActive = DateTime.UtcNow;
            JoinDate = DateTime.UtcNow;
        }

        // ── METHODS ──────────────────────────────────────────
        public int GetAge()
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        public void UpdateMood(string newMood)
        {
            if (Mood != newMood)
            {
                Mood = newMood;
                MoodLastUpdated = DateTime.UtcNow;
                if (!MoodMapping.IsValidDisplayMood(newMood))
                    System.Diagnostics.Debug.WriteLine(
                        $"Warning: Setting mood to potentially invalid value: {newMood}");
            }
        }

        public void UpdateLastActive() => LastActive = DateTime.UtcNow;

        public string GetMoodKey() => MoodMapping.MapDisplayToKey(Mood);

        public bool IsOnline() =>
            DateTime.UtcNow - LastActive < TimeSpan.FromMinutes(5);

        public string GetMoodLastUpdatedRelative()
        {
            var t = DateTime.UtcNow - MoodLastUpdated;
            if (t.TotalSeconds < 60) return "just now";
            if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes} minute{(t.TotalMinutes >= 2 ? "s" : "")} ago";
            if (t.TotalHours < 24) return $"{(int)t.TotalHours} hour{(t.TotalHours >= 2 ? "s" : "")} ago";
            if (t.TotalDays < 7) return $"{(int)t.TotalDays} day{(t.TotalDays >= 2 ? "s" : "")} ago";
            if (t.TotalDays < 30) return $"{(int)(t.TotalDays / 7)} week{(t.TotalDays / 7 >= 2 ? "s" : "")} ago";
            if (t.TotalDays < 365) return $"{(int)(t.TotalDays / 30)} month{(t.TotalDays / 30 >= 2 ? "s" : "")} ago";
            return $"{(int)(t.TotalDays / 365)} year{(t.TotalDays / 365 >= 2 ? "s" : "")} ago";
        }

        private string CalculateZodiacSign(DateTime dob)
        {
            int m = dob.Month, d = dob.Day;
            if ((m == 3 && d >= 21) || (m == 4 && d <= 19)) return "Aries";
            if ((m == 4 && d >= 20) || (m == 5 && d <= 20)) return "Taurus";
            if ((m == 5 && d >= 21) || (m == 6 && d <= 20)) return "Gemini";
            if ((m == 6 && d >= 21) || (m == 7 && d <= 22)) return "Cancer";
            if ((m == 7 && d >= 23) || (m == 8 && d <= 22)) return "Leo";
            if ((m == 8 && d >= 23) || (m == 9 && d <= 22)) return "Virgo";
            if ((m == 9 && d >= 23) || (m == 10 && d <= 22)) return "Libra";
            if ((m == 10 && d >= 23) || (m == 11 && d <= 21)) return "Scorpio";
            if ((m == 11 && d >= 22) || (m == 12 && d <= 21)) return "Sagittarius";
            if ((m == 12 && d >= 22) || (m == 1 && d <= 19)) return "Capricorn";
            if ((m == 1 && d >= 20) || (m == 2 && d <= 18)) return "Aquarius";
            return "Pisces";
        }
    }
}