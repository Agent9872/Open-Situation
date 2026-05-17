using Lock.Helpers;
using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lock.Models
{
    [Table("Users")]
    public class User : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Unique]
        public string PhoneNumber { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Interest { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        [Ignore]
        public string ZodiacSign => CalculateZodiacSign(DateOfBirth);

        public string ProfileImagePath { get; set; } = string.Empty;
        public string CoverImagePath { get; set; } = string.Empty;

        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public string InstagramHandle { get; set; } = string.Empty;
        public string SpotifyArtist { get; set; } = string.Empty;

        // REMOVE the duplicate - keep only ONE PersonalityType property
        // This is the existing one that was already there
        public string PersonalityType { get; set; } = string.Empty;

        public string Mood { get; set; } = string.Empty;
        public DateTime MoodLastUpdated { get; set; } = DateTime.UtcNow;
        public string EnergyLevel { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Interests { get; set; } = string.Empty;
        public string Drinks { get; set; } = string.Empty;
        public bool Smokes { get; set; }
        public bool HasPets { get; set; }
        public string Religion { get; set; } = string.Empty;
        public string PoliticalViews { get; set; } = string.Empty;

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

        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        public bool AllowMoodSearch { get; set; } = true;
        public bool GhostModeMoodShield { get; set; } = false;

        // Add these properties to your User class
        public bool IsVerified { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerificationIdNumber { get; set; }
        public string? VerificationIdType { get; set; }
        public DateTime? VerificationSubmittedAt { get; set; }
        public DateTime? VerificationVerifiedAt { get; set; }
        public string? VerificationStatus { get; set; } // "pending", "verified", "rejected"
        public string? VerificationRejectionReason { get; set; }
        public double VerificationScore { get; set; }

        // ========== MODERATION FIELDS ==========
        public bool IsBanned { get; set; } = false;
        public string BanType { get; set; } = string.Empty;        // "permanent" | "temporary"
        public string BanReason { get; set; } = string.Empty;
        public DateTime? BannedAt { get; set; }
        public DateTime? BanExpiresAt { get; set; }                // null = permanent

        public bool HasWarning { get; set; } = false;
        public string WarningMessage { get; set; } = string.Empty;
        public DateTime? WarnedAt { get; set; }
        public bool WarningAcknowledged { get; set; } = false;

        public string ModerationStatus { get; set; } = string.Empty;   // "warned"|"temp_banned"|"perm_banned"|"resolved"|"dismissed"
        public string ModerationNote { get; set; } = string.Empty;     // Note shown to user
        public DateTime? ModerationUpdatedAt { get; set; }


        // ========== APPEAL FIELDS ==========
        public string AppealText { get; set; } = string.Empty;
        public string AppealStatus { get; set; } = string.Empty; // "pending"|"approved"|"rejected"
        public DateTime? AppealSubmittedAt { get; set; }
        public string AppealAdminResponse { get; set; } = string.Empty;
        public DateTime? AppealReviewedAt { get; set; }

        // In User.cs — add after the Role property
        public string IpAddress { get; set; } = string.Empty;

        public bool HidePhoneNumber { get; set; } = false;

        // ========== ROLE ==========
        public string Role { get; set; } = "User"; // "User" | "Moderator" | "Admin"

        [Ignore]
        public bool IsAdmin => Role == "Admin";

        [Ignore]
        public bool IsModerator => Role == "Moderator" || IsAdmin;

        // ════════════════════════════════════════════════════
        // FILE 1 — User.cs
        // PASTE THIS directly after your existing Role property
        // ════════════════════════════════════════════════════

        // ========== PAGE PERMISSIONS ==========
        /// <summary>
        /// Comma-separated page keys this user is DENIED access to.
        /// Empty string = default access (based on role).
        /// Admins always ignore this field — they have full access.
        /// </summary>
        public string DeniedPages { get; set; } = string.Empty;

        [Ignore]
        public HashSet<string> DeniedPageSet =>
            string.IsNullOrWhiteSpace(DeniedPages)
                ? new HashSet<string>()
                : new HashSet<string>(DeniedPages.Split(',', StringSplitOptions.RemoveEmptyEntries));

        public bool CanAccessPage(string pageKey)
        {
            if (Role == "Admin") return true;
            return !DeniedPageSet.Contains(pageKey);
        }

        public void SetPageAccess(string pageKey, bool allowed)
        {
            var denied = DeniedPageSet;
            if (allowed)
                denied.Remove(pageKey);
            else
                denied.Add(pageKey);
            DeniedPages = string.Join(",", denied);
        }


        // Voice Intro
        private string? _voiceIntroPath;
        public string? VoiceIntroPath
        {
            get => _voiceIntroPath;
            set
            {
                if (_voiceIntroPath != value)
                {
                    _voiceIntroPath = value;
                    OnPropertyChanged();
                }
            }
        }



        // Height
        private int? _heightCm;
        public int? HeightCm
        {
            get => _heightCm;
            set
            {
                if (_heightCm != value)
                {
                    _heightCm = value;
                    OnPropertyChanged();
                }
            }
        }

        // Body Type
        private string? _bodyType;
        public string? BodyType
        {
            get => _bodyType;
            set
            {
                if (_bodyType != value)
                {
                    _bodyType = value;
                    OnPropertyChanged();
                }
            }
        }

        // Ethnicity
        private string? _ethnicity;
        public string? Ethnicity
        {
            get => _ethnicity;
            set
            {
                if (_ethnicity != value)
                {
                    _ethnicity = value;
                    OnPropertyChanged();
                }
            }
        }

        // Tribe
        private string? _tribe;
        public string? Tribe
        {
            get => _tribe;
            set
            {
                if (_tribe != value)
                {
                    _tribe = value;
                    OnPropertyChanged();
                }
            }
        }

        // Voice Intro Last Updated
        private DateTime? _voiceIntroLastUpdated;
        public DateTime? VoiceIntroLastUpdated
        {
            get => _voiceIntroLastUpdated;
            set
            {
                if (_voiceIntroLastUpdated != value)
                {
                    _voiceIntroLastUpdated = value;
                    OnPropertyChanged();
                }
            }
        }

        // Add this method to your User class
        public int GetAge()
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        // Kids / Family Plans
        private string? _kidsPreference;
        public string? KidsPreference
        {
            get => _kidsPreference;
            set
            {
                if (_kidsPreference != value)
                {
                    _kidsPreference = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _hasChildren;
        public string? HasChildren
        {
            get => _hasChildren;
            set
            {
                if (_hasChildren != value)
                {
                    _hasChildren = value;
                    OnPropertyChanged();
                }
            }
        }

        // Diet
        private string? _dietaryPreference;
        public string? DietaryPreference
        {
            get => _dietaryPreference;
            set
            {
                if (_dietaryPreference != value)
                {
                    _dietaryPreference = value;
                    OnPropertyChanged();
                }
            }
        }

        // Exercise Frequency
        private string? _exerciseFrequency;
        public string? ExerciseFrequency
        {
            get => _exerciseFrequency;
            set
            {
                if (_exerciseFrequency != value)
                {
                    _exerciseFrequency = value;
                    OnPropertyChanged();
                }
            }
        }

        // ========== LOVE LANGUAGE (ADD THIS - NOT DUPLICATED) ==========
        private string? _loveLanguage;
        public string? LoveLanguage
        {
            get => _loveLanguage;
            set
            {
                if (_loveLanguage != value)
                {
                    _loveLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public void UpdateMood(string newMood)
        {
            if (Mood != newMood)
            {
                Mood = newMood;
                MoodLastUpdated = DateTime.UtcNow;

                if (!MoodMapping.IsValidDisplayMood(newMood))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Setting mood to potentially invalid value: {newMood}");
                }
            }
        }

        public void UpdateLastActive()
        {
            LastActive = DateTime.UtcNow;
        }

        public string GetMoodKey()
        {
            return MoodMapping.MapDisplayToKey(Mood);
        }

        public bool IsOnline()
        {
            return DateTime.UtcNow - LastActive < TimeSpan.FromMinutes(5);
        }

        public string GetMoodLastUpdatedRelative()
        {
            var timeSpan = DateTime.UtcNow - MoodLastUpdated;

            if (timeSpan.TotalSeconds < 60)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} week{(timeSpan.TotalDays / 7 >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} month{(timeSpan.TotalDays / 30 >= 2 ? "s" : "")} ago";

            return $"{(int)(timeSpan.TotalDays / 365)} year{(timeSpan.TotalDays / 365 >= 2 ? "s" : "")} ago";
        }

        private string CalculateZodiacSign(DateTime dob)
        {
            int month = dob.Month;
            int day = dob.Day;

            if ((month == 3 && day >= 21) || (month == 4 && day <= 19))
                return "Aries";
            if ((month == 4 && day >= 20) || (month == 5 && day <= 20))
                return "Taurus";
            if ((month == 5 && day >= 21) || (month == 6 && day <= 20))
                return "Gemini";
            if ((month == 6 && day >= 21) || (month == 7 && day <= 22))
                return "Cancer";
            if ((month == 7 && day >= 23) || (month == 8 && day <= 22))
                return "Leo";
            if ((month == 8 && day >= 23) || (month == 9 && day <= 22))
                return "Virgo";
            if ((month == 9 && day >= 23) || (month == 10 && day <= 22))
                return "Libra";
            if ((month == 10 && day >= 23) || (month == 11 && day <= 21))
                return "Scorpio";
            if ((month == 11 && day >= 22) || (month == 12 && day <= 21))
                return "Sagittarius";
            if ((month == 12 && day >= 22) || (month == 1 && day <= 19))
                return "Capricorn";
            if ((month == 1 && day >= 20) || (month == 2 && day <= 18))
                return "Aquarius";
            return "Pisces";
        }
    }
}