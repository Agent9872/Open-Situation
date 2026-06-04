using Lock.Helpers;
using SQLite;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// Aliases to resolve attribute conflicts between SQLite and Supabase
using SbTable = Supabase.Postgrest.Attributes.TableAttribute;
using SbColumn = Supabase.Postgrest.Attributes.ColumnAttribute;
using SbPK = Supabase.Postgrest.Attributes.PrimaryKeyAttribute;

namespace Lock.Models
{
    [SQLite.Table("Users")]
    [SbTable("Users")]
    public class User : BaseModel, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ── PRIMARY KEY ──────────────────────────────────────
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        [SbPK("Id", false)]
        public int Id { get; set; }

        // ── IDENTITY ─────────────────────────────────────────
        [SQLite.Unique]
        [SbColumn("PhoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [SbColumn("Name")]
        public string Name { get; set; } = string.Empty;

        [SbColumn("DisplayName")]
        public string DisplayName { get; set; } = string.Empty;

        [SbColumn("Gender")]
        public string Gender { get; set; } = string.Empty;

        [SbColumn("Interest")]
        public string Interest { get; set; } = string.Empty;

        [SbColumn("Password")]
        public string Password { get; set; } = string.Empty;

        [SbColumn("DateOfBirth")]
        public DateTime DateOfBirth { get; set; }

        [SQLite.Ignore]
        public string ZodiacSign => CalculateZodiacSign(DateOfBirth);

        [SbColumn("ProfileImagePath")]
        public string ProfileImagePath { get; set; } = string.Empty;

        [SbColumn("CoverImagePath")]
        public string CoverImagePath { get; set; } = string.Empty;

        [SbColumn("JoinDate")]
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        [SbColumn("InstagramHandle")]
        public string InstagramHandle { get; set; } = string.Empty;

        [SbColumn("SpotifyArtist")]
        public string SpotifyArtist { get; set; } = string.Empty;

        [SbColumn("PersonalityType")]
        public string PersonalityType { get; set; } = string.Empty;

        // ── MOOD ─────────────────────────────────────────────
        [SbColumn("Mood")]
        public string Mood { get; set; } = string.Empty;

        [SbColumn("MoodLastUpdated")]
        public DateTime MoodLastUpdated { get; set; } = DateTime.UtcNow;

        [SbColumn("EnergyLevel")]
        public string EnergyLevel { get; set; } = string.Empty;

        // ── LOCATION ─────────────────────────────────────────
        [SbColumn("Country")]
        public string Country { get; set; } = string.Empty;

        [SbColumn("State")]
        public string State { get; set; } = string.Empty;

        // ── PROFILE ──────────────────────────────────────────
        [SbColumn("Bio")]
        public string Bio { get; set; } = string.Empty;

        [SbColumn("Interests")]
        public string Interests { get; set; } = string.Empty;

        [SbColumn("Drinks")]
        public string Drinks { get; set; } = string.Empty;

        [SbColumn("Smokes")]
        public bool Smokes { get; set; }

        [SbColumn("HasPets")]
        public bool HasPets { get; set; }

        [SbColumn("Religion")]
        public string Religion { get; set; } = string.Empty;

        [SbColumn("PoliticalViews")]
        public string PoliticalViews { get; set; } = string.Empty;

        // ── PREFERENCES ──────────────────────────────────────
        [SbColumn("MinAgePreference")]
        public int MinAgePreference { get; set; } = 18;

        [SbColumn("MaxAgePreference")]
        public int MaxAgePreference { get; set; } = 100;

        [SbColumn("MaxDistance")]
        public int MaxDistance { get; set; } = 50;

        [SbColumn("ShowMeOnApp")]
        public bool ShowMeOnApp { get; set; } = true;

        [SbColumn("PreferredFirstDate")]
        public string PreferredFirstDate { get; set; } = string.Empty;

        [SbColumn("CommunicationStyle")]
        public string CommunicationStyle { get; set; } = string.Empty;

        [SbColumn("DietPreference")]
        public string DietPreference { get; set; } = string.Empty;

        [SbColumn("WorkoutFrequency")]
        public string WorkoutFrequency { get; set; } = string.Empty;

        [SbColumn("WantChildren")]
        public bool WantChildren { get; set; }

        [SbColumn("HaveChildren")]
        public bool HaveChildren { get; set; }

        // ── MEDIA & CULTURE ───────────────────────────────────
        [SbColumn("MusicGenres")]
        public string MusicGenres { get; set; } = string.Empty;

        [SbColumn("FavoriteArtists")]
        public string FavoriteArtists { get; set; } = string.Empty;

        [SbColumn("FavoriteMovies")]
        public string FavoriteMovies { get; set; } = string.Empty;

        [SbColumn("FavoriteBooks")]
        public string FavoriteBooks { get; set; } = string.Empty;

        [SbColumn("Languages")]
        public string Languages { get; set; } = string.Empty;

        [SbColumn("Occupation")]
        public string Occupation { get; set; } = string.Empty;

        [SbColumn("Education")]
        public string Education { get; set; } = string.Empty;

        [SbColumn("Prompts")]
        public string Prompts { get; set; } = string.Empty;

        [SbColumn("Dealbreakers")]
        public string Dealbreakers { get; set; } = string.Empty;

        [SbColumn("Sports")]
        public string Sports { get; set; } = string.Empty;

        [SbColumn("CreativeHobbies")]
        public string CreativeHobbies { get; set; } = string.Empty;

        [SbColumn("TravelStyle")]
        public string TravelStyle { get; set; } = string.Empty;

        [SbColumn("FavoriteCuisines")]
        public string FavoriteCuisines { get; set; } = string.Empty;

        [SbColumn("Podcasts")]
        public string Podcasts { get; set; } = string.Empty;

        [SbColumn("TvShows")]
        public string TvShows { get; set; } = string.Empty;

        [SbColumn("GamingInterests")]
        public string GamingInterests { get; set; } = string.Empty;

        [SbColumn("WeekendVibe")]
        public string WeekendVibe { get; set; } = string.Empty;

        [SbColumn("TopInterest")]
        public string TopInterest { get; set; } = string.Empty;

        [SbColumn("TopArtist")]
        public string TopArtist { get; set; } = string.Empty;

        [SbColumn("TopMovie")]
        public string TopMovie { get; set; } = string.Empty;

        [SbColumn("SexualOrientation")]
        public string SexualOrientation { get; set; } = string.Empty;

        [SbColumn("BestMusic")]
        public string BestMusic { get; set; } = string.Empty;

        [SbColumn("FavoriteMusicGenre")]
        public string FavoriteMusicGenre { get; set; } = string.Empty;

        // ── ACTIVITY ─────────────────────────────────────────
        [SbColumn("LastActive")]
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        [SbColumn("AllowMoodSearch")]
        public bool AllowMoodSearch { get; set; } = true;

        [SbColumn("GhostModeMoodShield")]
        public bool GhostModeMoodShield { get; set; } = false;

        // ── VERIFICATION ─────────────────────────────────────
        [SbColumn("IsVerified")]
        public bool IsVerified { get; set; }

        [SbColumn("VerifiedAt")]
        public DateTime? VerifiedAt { get; set; }

        [SbColumn("VerificationIdNumber")]
        public string? VerificationIdNumber { get; set; }

        [SbColumn("VerificationIdType")]
        public string? VerificationIdType { get; set; }

        [SbColumn("VerificationSubmittedAt")]
        public DateTime? VerificationSubmittedAt { get; set; }

        [SbColumn("VerificationVerifiedAt")]
        public DateTime? VerificationVerifiedAt { get; set; }

        [SbColumn("VerificationStatus")]
        public string? VerificationStatus { get; set; }

        [SbColumn("VerificationRejectionReason")]
        public string? VerificationRejectionReason { get; set; }

        [SbColumn("VerificationScore")]
        public double VerificationScore { get; set; }

        // ── MODERATION ───────────────────────────────────────
        [SbColumn("IsBanned")]
        public bool IsBanned { get; set; } = false;

        [SbColumn("BanType")]
        public string BanType { get; set; } = string.Empty;

        [SbColumn("BanReason")]
        public string BanReason { get; set; } = string.Empty;

        [SbColumn("BannedAt")]
        public DateTime? BannedAt { get; set; }

        [SbColumn("BanExpiresAt")]
        public DateTime? BanExpiresAt { get; set; }

        [SbColumn("HasWarning")]
        public bool HasWarning { get; set; } = false;

        [SbColumn("WarningMessage")]
        public string WarningMessage { get; set; } = string.Empty;

        [SbColumn("WarnedAt")]
        public DateTime? WarnedAt { get; set; }

        [SbColumn("WarningAcknowledged")]
        public bool WarningAcknowledged { get; set; } = false;

        [SbColumn("ModerationStatus")]
        public string ModerationStatus { get; set; } = string.Empty;

        [SbColumn("ModerationNote")]
        public string ModerationNote { get; set; } = string.Empty;

        [SbColumn("ModerationUpdatedAt")]
        public DateTime? ModerationUpdatedAt { get; set; }

        // ── APPEALS ──────────────────────────────────────────
        [SbColumn("AppealText")]
        public string AppealText { get; set; } = string.Empty;

        [SbColumn("AppealStatus")]
        public string AppealStatus { get; set; } = string.Empty;

        [SbColumn("AppealSubmittedAt")]
        public DateTime? AppealSubmittedAt { get; set; }

        [SbColumn("AppealAdminResponse")]
        public string AppealAdminResponse { get; set; } = string.Empty;

        [SbColumn("AppealReviewedAt")]
        public DateTime? AppealReviewedAt { get; set; }

        // ── SYSTEM ───────────────────────────────────────────
        [SbColumn("IpAddress")]
        public string IpAddress { get; set; } = string.Empty;

        [SbColumn("HidePhoneNumber")]
        public bool HidePhoneNumber { get; set; } = false;

        // ── ROLE ─────────────────────────────────────────────
        [SbColumn("Role")]
        public string Role { get; set; } = "User";

        [SQLite.Ignore]
        public bool IsAdmin => Role == "Admin";

        [SQLite.Ignore]
        public bool IsModerator => Role == "Moderator" || IsAdmin;

        // ── PAGE PERMISSIONS ─────────────────────────────────
        [SbColumn("DeniedPages")]
        public string DeniedPages { get; set; } = string.Empty;

        [SQLite.Ignore]
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
        [SbColumn("VoiceIntroPath")]
        public string? VoiceIntroPath
        {
            get => _voiceIntroPath;
            set { if (_voiceIntroPath != value) { _voiceIntroPath = value; OnPropertyChanged(); } }
        }

        private DateTime? _voiceIntroLastUpdated;
        [SbColumn("VoiceIntroLastUpdated")]
        public DateTime? VoiceIntroLastUpdated
        {
            get => _voiceIntroLastUpdated;
            set { if (_voiceIntroLastUpdated != value) { _voiceIntroLastUpdated = value; OnPropertyChanged(); } }
        }

        // ── PHYSICAL ─────────────────────────────────────────
        private int? _heightCm;
        [SbColumn("HeightCm")]
        public int? HeightCm
        {
            get => _heightCm;
            set { if (_heightCm != value) { _heightCm = value; OnPropertyChanged(); } }
        }

        private string? _bodyType;
        [SbColumn("BodyType")]
        public string? BodyType
        {
            get => _bodyType;
            set { if (_bodyType != value) { _bodyType = value; OnPropertyChanged(); } }
        }

        private string? _ethnicity;
        [SbColumn("Ethnicity")]
        public string? Ethnicity
        {
            get => _ethnicity;
            set { if (_ethnicity != value) { _ethnicity = value; OnPropertyChanged(); } }
        }

        private string? _tribe;
        [SbColumn("Tribe")]
        public string? Tribe
        {
            get => _tribe;
            set { if (_tribe != value) { _tribe = value; OnPropertyChanged(); } }
        }

        // ── FAMILY ───────────────────────────────────────────
        private string? _kidsPreference;
        [SbColumn("KidsPreference")]
        public string? KidsPreference
        {
            get => _kidsPreference;
            set { if (_kidsPreference != value) { _kidsPreference = value; OnPropertyChanged(); } }
        }

        private string? _hasChildren;
        [SbColumn("HasChildren")]
        public string? HasChildren
        {
            get => _hasChildren;
            set { if (_hasChildren != value) { _hasChildren = value; OnPropertyChanged(); } }
        }

        // ── LIFESTYLE ────────────────────────────────────────
        private string? _dietaryPreference;
        [SbColumn("DietaryPreference")]
        public string? DietaryPreference
        {
            get => _dietaryPreference;
            set { if (_dietaryPreference != value) { _dietaryPreference = value; OnPropertyChanged(); } }
        }

        private string? _exerciseFrequency;
        [SbColumn("ExerciseFrequency")]
        public string? ExerciseFrequency
        {
            get => _exerciseFrequency;
            set { if (_exerciseFrequency != value) { _exerciseFrequency = value; OnPropertyChanged(); } }
        }

        private string? _loveLanguage;
        [SbColumn("LoveLanguage")]
        public string? LoveLanguage
        {
            get => _loveLanguage;
            set { if (_loveLanguage != value) { _loveLanguage = value; OnPropertyChanged(); } }
        }

        // ── COIN BALANCE ─────────────────────────────────────
        [SbColumn("CoinBalance")]
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