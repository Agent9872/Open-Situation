using Lock.Chat.Services;
using Lock.Data.Post;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    // Matching mode enum
    public enum MatchingMode
    {
        Similar,        // Match with similar preferences
        Complementary   // Match with opposite/complementary preferences
    }

    /// <summary>
    /// One scored candidate returned by MatchService.
    /// </summary>
    public class MatchResult
    {
        public User User { get; set; } = null!;
        public string Location { get; set; } = string.Empty;
        public double TotalScore { get; set; }          // 0-100
        public double IntentScore { get; set; }          // T1
        public double LifestyleScore { get; set; }       // T2
        public double DemographicScore { get; set; }     // T3
        public double ActivityScore { get; set; }        // T4
        public List<string> WhyMatched { get; set; } = new();
        public List<string> CommonInterests { get; set; } = new();
        public string SharedMood { get; set; } = string.Empty;
        public bool MoodMatch { get; set; }
        public int Age { get; set; }
        public string ProfileImagePath => User.ProfileImagePath ?? string.Empty;
        public string Name => User.Name ?? User.PhoneNumber ?? string.Empty;
        public string PhoneNumber => User.PhoneNumber ?? string.Empty;
        public string Mood => User.Mood ?? string.Empty;
        public bool HasMood => !string.IsNullOrEmpty(User.Mood);
        public bool HasLocation => !string.IsNullOrEmpty(Location);
        public bool HasAge => Age > 0;

        // New properties for attribute comparison
        public List<string> SharedAttributes { get; set; } = new();
        public List<string> YourUniqueAttributes { get; set; } = new();
        public List<string> TheirUniqueAttributes { get; set; } = new();

        public bool HasSharedAttributes => SharedAttributes.Any();
        public bool HasYourUniqueAttributes => YourUniqueAttributes.Any();
        public bool HasTheirUniqueAttributes => TheirUniqueAttributes.Any();

        public double AttributeMatchPercentage
        {
            get
            {
                int totalCompared = SharedAttributes.Count + YourUniqueAttributes.Count + TheirUniqueAttributes.Count;
                if (totalCompared == 0) return 0;
                return Math.Min(1.0, SharedAttributes.Count / (double)totalCompared);
            }
        }
    }

    public static class MatchService
    {
        // Tier weights
        private const double W_INTENT = 0.35;
        private const double W_LIFESTYLE = 0.25;
        private const double W_DEMOGRAPHIC = 0.25;
        private const double W_ACTIVITY = 0.15;

        // ── Public entry point ────────────────────────────────────────────
        public static async Task<List<MatchResult>> GetMatchesAsync(
      string currentUserPhone,
      string subTab = "TopPicks",
      MatchingMode mode = MatchingMode.Similar)
        {
            try
            {
                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // var me = await db.Table<User>()
                //     .Where(u => u.PhoneNumber == currentUserPhone)
                //     .FirstOrDefaultAsync();

                // Replace with Supabase code:
                var meUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var me = meUsers.FirstOrDefault();

                if (me == null) return new List<MatchResult>();

                // All visible candidates - using Supabase
                var allCandidates = await SupabaseService.GetAsync<User>("Users",
                    $"GhostModeMoodShield=eq.false");

                var candidates = allCandidates
                    .Where(u => !string.Equals(u.PhoneNumber, currentUserPhone,
                                               StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // All posts (for activity + intent signals)
                var allPosts = await PostRepository.GetAllAsync() ?? new List<Post>();

                // Current user's posts (for cross-matching categories/moods)
                var myPosts = allPosts
                    .Where(p => string.Equals(p.AuthorPhone, currentUserPhone,
                                              StringComparison.OrdinalIgnoreCase)
                             && string.IsNullOrEmpty(p.StatusImagePath))
                    .ToList();

                // Posts current user has interacted with (loved / commented)
                var myLovedAuthorPhones = allPosts
                    .Where(p => p.LovedBy?.Contains(currentUserPhone) == true)
                    .Select(p => p.AuthorPhone)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var results = new List<MatchResult>();

                foreach (var candidate in candidates)
                {
                    // Hard disqualify on dealbreakers first
                    if (HasDealbreaker(me, candidate)) continue;

                    var candidatePosts = allPosts
                        .Where(p => string.Equals(p.AuthorPhone, candidate.PhoneNumber,
                                                  StringComparison.OrdinalIgnoreCase)
                                 && string.IsNullOrEmpty(p.StatusImagePath))
                        .ToList();

                    var candidateStatusPosts = allPosts
                        .Where(p => string.Equals(p.AuthorPhone, candidate.PhoneNumber,
                                                  StringComparison.OrdinalIgnoreCase)
                                 && !string.IsNullOrEmpty(p.StatusImagePath))
                        .ToList();

                    var result = mode == MatchingMode.Similar
                        ? ScoreCandidateSimilar(me, candidate, myPosts, candidatePosts, candidateStatusPosts, myLovedAuthorPhones)
                        : ScoreCandidateComplementary(me, candidate, myPosts, candidatePosts, candidateStatusPosts, myLovedAuthorPhones);

                    // Calculate attribute differences for display
                    CalculateAttributeDifferences(me, candidate, result);

                    results.Add(result);
                }

                // Apply sub-tab filter
                results = ApplySubTabFilter(results, me, subTab);

                // Sort descending by score
                results = results
                    .OrderByDescending(r => r.TotalScore)
                    .Take(50)
                    .ToList();

                return results;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MatchService.GetMatchesAsync error: {ex}");
                return new List<MatchResult>();
            }
        }

        // ── Attribute Differences Calculation ─────────────────────────────
        private static void CalculateAttributeDifferences(User me, User candidate, MatchResult result)
        {
            // ── Interests / Hobbies ───────────────────────────────────────────────
            var myInterests = SplitTags(me.Interests);
            var theirInterests = SplitTags(candidate.Interests);

            foreach (var item in myInterests.Intersect(theirInterests, StringComparer.OrdinalIgnoreCase))
                result.SharedAttributes.Add($"🎯 {item}");

            foreach (var item in myInterests.Except(theirInterests, StringComparer.OrdinalIgnoreCase).Take(3))
                result.YourUniqueAttributes.Add($"🎯 {item}");          // I have, they don't

            foreach (var item in theirInterests.Except(myInterests, StringComparer.OrdinalIgnoreCase).Take(3))
                result.TheirUniqueAttributes.Add($"🎯 {item}");         // They have, I don't

            // ── Music Genres ──────────────────────────────────────────────────────
            var myGenres = SplitTags(me.MusicGenres);
            var theirGenres = SplitTags(candidate.MusicGenres);

            foreach (var g in myGenres.Intersect(theirGenres, StringComparer.OrdinalIgnoreCase).Take(2))
                result.SharedAttributes.Add($"🎵 {g}");

            foreach (var g in myGenres.Except(theirGenres, StringComparer.OrdinalIgnoreCase).Take(2))
                result.YourUniqueAttributes.Add($"🎵 {g}");

            foreach (var g in theirGenres.Except(myGenres, StringComparer.OrdinalIgnoreCase).Take(2))
                result.TheirUniqueAttributes.Add($"🎵 {g}");

            // ── Energy Level ──────────────────────────────────────────────────────
            bool meHasEnergy = !string.IsNullOrEmpty(me.EnergyLevel);
            bool themHasEnergy = !string.IsNullOrEmpty(candidate.EnergyLevel);

            if (meHasEnergy && themHasEnergy)
            {
                if (string.Equals(me.EnergyLevel, candidate.EnergyLevel, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"⚡ {me.EnergyLevel} energy");
                else
                {
                    result.YourUniqueAttributes.Add($"⚡ {me.EnergyLevel} energy");
                    result.TheirUniqueAttributes.Add($"⚡ {candidate.EnergyLevel} energy");
                }
            }
            else if (meHasEnergy)
                result.YourUniqueAttributes.Add($"⚡ {me.EnergyLevel} energy");
            else if (themHasEnergy)
                result.TheirUniqueAttributes.Add($"⚡ {candidate.EnergyLevel} energy");

            // ── Top Artist ────────────────────────────────────────────────────────
            bool meHasArtist = !string.IsNullOrEmpty(me.TopArtist);
            bool themHasArtist = !string.IsNullOrEmpty(candidate.TopArtist);

            if (meHasArtist && themHasArtist)
            {
                if (string.Equals(me.TopArtist, candidate.TopArtist, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"🎤 {me.TopArtist}");
                else
                {
                    result.YourUniqueAttributes.Add($"🎤 Likes {me.TopArtist}");
                    result.TheirUniqueAttributes.Add($"🎤 Likes {candidate.TopArtist}");
                }
            }
            else if (meHasArtist)
                result.YourUniqueAttributes.Add($"🎤 Likes {me.TopArtist}");
            else if (themHasArtist)
                result.TheirUniqueAttributes.Add($"🎤 Likes {candidate.TopArtist}");

            // ── Drinking ──────────────────────────────────────────────────────────
            bool meHasDrinks = !string.IsNullOrEmpty(me.Drinks);
            bool themHasDrinks = !string.IsNullOrEmpty(candidate.Drinks);

            if (meHasDrinks && themHasDrinks)
            {
                if (string.Equals(me.Drinks, candidate.Drinks, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"🍷 {me.Drinks} drinker");
                else
                {
                    result.YourUniqueAttributes.Add($"🍷 {me.Drinks} drinker");
                    result.TheirUniqueAttributes.Add($"🍷 {candidate.Drinks} drinker");
                }
            }
            else if (meHasDrinks)
                result.YourUniqueAttributes.Add($"🍷 {me.Drinks} drinker");
            else if (themHasDrinks)
                result.TheirUniqueAttributes.Add($"🍷 {candidate.Drinks} drinker");

            // ── Smoking ───────────────────────────────────────────────────────────
            if (me.Smokes == candidate.Smokes)
                result.SharedAttributes.Add(me.Smokes ? "🚬 Both smoke" : "🚭 Neither smokes");
            else
            {
                if (me.Smokes) result.YourUniqueAttributes.Add("🚬 You smoke");
                else result.YourUniqueAttributes.Add("🚭 You don't smoke");
                if (candidate.Smokes) result.TheirUniqueAttributes.Add("🚬 They smoke");
                else result.TheirUniqueAttributes.Add("🚭 They don't smoke");
            }

            // ── Pets ──────────────────────────────────────────────────────────────
            if (me.HasPets == candidate.HasPets)
                result.SharedAttributes.Add(me.HasPets ? "🐾 Both have pets" : "🏠 Neither has pets");
            else
            {
                if (me.HasPets) result.YourUniqueAttributes.Add("🐾 You have pets");
                if (candidate.HasPets) result.TheirUniqueAttributes.Add("🐾 They have pets");
            }

            // ── Dietary Preference ────────────────────────────────────────────────
            bool meHasDiet = !string.IsNullOrEmpty(me.DietaryPreference);
            bool themHasDiet = !string.IsNullOrEmpty(candidate.DietaryPreference);

            if (meHasDiet && themHasDiet)
            {
                if (string.Equals(me.DietaryPreference, candidate.DietaryPreference, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"🍽️ {me.DietaryPreference}");
                else
                {
                    result.YourUniqueAttributes.Add($"🍽️ {me.DietaryPreference}");
                    result.TheirUniqueAttributes.Add($"🍽️ {candidate.DietaryPreference}");
                }
            }
            else if (meHasDiet)
                result.YourUniqueAttributes.Add($"🍽️ {me.DietaryPreference}");
            else if (themHasDiet)
                result.TheirUniqueAttributes.Add($"🍽️ {candidate.DietaryPreference}");

            // ── Exercise Frequency ────────────────────────────────────────────────
            bool meHasExercise = !string.IsNullOrEmpty(me.ExerciseFrequency);
            bool themHasExercise = !string.IsNullOrEmpty(candidate.ExerciseFrequency);

            if (meHasExercise && themHasExercise)
            {
                if (string.Equals(me.ExerciseFrequency, candidate.ExerciseFrequency, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"💪 {me.ExerciseFrequency}");
                else
                {
                    result.YourUniqueAttributes.Add($"💪 {me.ExerciseFrequency}");
                    result.TheirUniqueAttributes.Add($"💪 {candidate.ExerciseFrequency}");
                }
            }
            else if (meHasExercise)
                result.YourUniqueAttributes.Add($"💪 {me.ExerciseFrequency}");
            else if (themHasExercise)
                result.TheirUniqueAttributes.Add($"💪 {candidate.ExerciseFrequency}");

            // ── Personality Type ──────────────────────────────────────────────────
            bool meHasMBTI = !string.IsNullOrEmpty(me.PersonalityType);
            bool themHasMBTI = !string.IsNullOrEmpty(candidate.PersonalityType);

            if (meHasMBTI && themHasMBTI)
            {
                if (string.Equals(me.PersonalityType, candidate.PersonalityType, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"🧠 {me.PersonalityType}");
                else
                {
                    result.YourUniqueAttributes.Add($"🧠 {me.PersonalityType}");
                    result.TheirUniqueAttributes.Add($"🧠 {candidate.PersonalityType}");
                }
            }
            else if (meHasMBTI)
                result.YourUniqueAttributes.Add($"🧠 {me.PersonalityType}");
            else if (themHasMBTI)
                result.TheirUniqueAttributes.Add($"🧠 {candidate.PersonalityType}");

            // ── Love Language ─────────────────────────────────────────────────────
            bool meHasLL = !string.IsNullOrEmpty(me.LoveLanguage);
            bool themHasLL = !string.IsNullOrEmpty(candidate.LoveLanguage);

            if (meHasLL && themHasLL)
            {
                if (string.Equals(me.LoveLanguage, candidate.LoveLanguage, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"❤️ {me.LoveLanguage}");
                else
                {
                    result.YourUniqueAttributes.Add($"❤️ {me.LoveLanguage}");
                    result.TheirUniqueAttributes.Add($"❤️ {candidate.LoveLanguage}");
                }
            }
            else if (meHasLL)
                result.YourUniqueAttributes.Add($"❤️ {me.LoveLanguage}");
            else if (themHasLL)
                result.TheirUniqueAttributes.Add($"❤️ {candidate.LoveLanguage}");

            // ── Kids Preference ───────────────────────────────────────────────────
            bool meHasKids = !string.IsNullOrEmpty(me.KidsPreference);
            bool themHasKids = !string.IsNullOrEmpty(candidate.KidsPreference);

            if (meHasKids && themHasKids)
            {
                if (string.Equals(me.KidsPreference, candidate.KidsPreference, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"👶 {me.KidsPreference}");
                else
                {
                    result.YourUniqueAttributes.Add($"👶 {me.KidsPreference}");
                    result.TheirUniqueAttributes.Add($"👶 {candidate.KidsPreference}");
                }
            }
            else if (meHasKids)
                result.YourUniqueAttributes.Add($"👶 {me.KidsPreference}");
            else if (themHasKids)
                result.TheirUniqueAttributes.Add($"👶 {candidate.KidsPreference}");

            // ── Height (range-based) ──────────────────────────────────────────────
            bool meHasHeight = me.HeightCm.HasValue && me.HeightCm.Value > 0;
            bool themHasHeight = candidate.HeightCm.HasValue && candidate.HeightCm.Value > 0;

            if (meHasHeight && themHasHeight)
            {
                int diff = Math.Abs(me.HeightCm!.Value - candidate.HeightCm!.Value);
                if (diff <= 10)
                    result.SharedAttributes.Add($"📏 Height compatible ({diff}cm diff)");
                else
                {
                    result.YourUniqueAttributes.Add($"📏 You: {me.HeightCm.Value}cm");
                    result.TheirUniqueAttributes.Add($"📏 Them: {candidate.HeightCm.Value}cm");
                }
            }
            else if (meHasHeight)
                result.YourUniqueAttributes.Add($"📏 You: {me.HeightCm!.Value}cm");
            else if (themHasHeight)
                result.TheirUniqueAttributes.Add($"📏 Them: {candidate.HeightCm!.Value}cm");

            // ── Body Type ─────────────────────────────────────────────────────────
            bool meHasBody = !string.IsNullOrEmpty(me.BodyType);
            bool themHasBody = !string.IsNullOrEmpty(candidate.BodyType);

            if (meHasBody && themHasBody)
            {
                if (string.Equals(me.BodyType, candidate.BodyType, StringComparison.OrdinalIgnoreCase))
                    result.SharedAttributes.Add($"💪 {me.BodyType} body type");
                else
                {
                    result.YourUniqueAttributes.Add($"💪 You: {me.BodyType}");
                    result.TheirUniqueAttributes.Add($"💪 Them: {candidate.BodyType}");
                }
            }
            else if (meHasBody)
                result.YourUniqueAttributes.Add($"💪 You: {me.BodyType}");
            else if (themHasBody)
                result.TheirUniqueAttributes.Add($"💪 Them: {candidate.BodyType}");

            // ── Cap lists to avoid clutter ────────────────────────────────────────
            result.SharedAttributes = result.SharedAttributes.Take(6).ToList();
            result.YourUniqueAttributes = result.YourUniqueAttributes.Take(4).ToList();
            result.TheirUniqueAttributes = result.TheirUniqueAttributes.Take(4).ToList();
        }


        // ── Similar Mode Scoring ─────────────────────────────────────────
        private static MatchResult ScoreCandidateSimilar(
            User me,
            User candidate,
            List<Post> myPosts,
            List<Post> candidatePosts,
            List<Post> candidateStatusPosts,
            HashSet<string> myLovedAuthorPhones)
        {
            var result = new MatchResult
            {
                User = candidate,
                Location = GetLocation(candidate),
                Age = CalcAge(candidate.DateOfBirth)
            };

            // ── TIER 1: Intent (35%) ─────────────────────────────────────
            double t1 = 0;

            // Mood / looking-for alignment
            if (!string.IsNullOrEmpty(me.Mood) && !string.IsNullOrEmpty(candidate.Mood))
            {
                bool exactMoodMatch = string.Equals(me.Mood.Trim(), candidate.Mood.Trim(),
                                                    StringComparison.OrdinalIgnoreCase);
                if (exactMoodMatch)
                {
                    t1 += 0.4;
                    result.MoodMatch = true;
                    result.SharedMood = candidate.Mood;
                    result.WhyMatched.Add($"Same vibe: {candidate.Mood}");
                }
                else if (MoodsAreCompatible(me.Mood, candidate.Mood))
                {
                    t1 += 0.25;
                    result.WhyMatched.Add($"Compatible intent");
                }
            }

            // "By Mood" posts
            var byMoodPosts = candidatePosts
                .Where(p => p.Visibility == "By Mood"
                         && string.Equals(p.AuthorMood ?? candidate.Mood, me.Mood,
                                          StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (byMoodPosts.Any())
            {
                t1 += 0.2;
                result.WhyMatched.Add("Posts mood-matched to you");
            }

            // Status image mood alignment
            var statusWithMatchingMood = candidateStatusPosts
                .Where(s => string.Equals(s.Mood, me.Mood, StringComparison.OrdinalIgnoreCase))
                .Any();
            if (statusWithMatchingMood)
            {
                t1 += 0.15;
                result.WhyMatched.Add("Status vibe matches");
            }

            // Post category overlap
            var myCategories = myPosts
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!.Trim().ToLowerInvariant())
                .ToHashSet();
            var candidateCategories = candidatePosts
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!.Trim().ToLowerInvariant())
                .ToHashSet();
            int sharedCats = myCategories.Intersect(candidateCategories).Count();
            if (sharedCats > 0)
            {
                t1 += Math.Min(0.15, sharedCats * 0.05);
                result.WhyMatched.Add($"{sharedCats} shared post categor{(sharedCats == 1 ? "y" : "ies")}");
            }

            result.IntentScore = Math.Min(1.0, t1);

            // ── TIER 2: Lifestyle (25%) ──────────────────────────────────
            double t2 = 0;

            // Music genre overlap
            var myGenres = SplitTags(me.MusicGenres);
            var candGenres = SplitTags(candidate.MusicGenres);
            int sharedGenres = myGenres.Intersect(candGenres, StringComparer.OrdinalIgnoreCase).Count();
            if (sharedGenres > 0)
            {
                t2 += Math.Min(0.2, sharedGenres * 0.1);
                result.WhyMatched.Add($"Both into {string.Join(", ", myGenres.Intersect(candGenres, StringComparer.OrdinalIgnoreCase).Take(2))}");
            }

            // Top artist match
            if (!string.IsNullOrEmpty(me.TopArtist) && !string.IsNullOrEmpty(candidate.TopArtist)
                && string.Equals(me.TopArtist.Trim(), candidate.TopArtist.Trim(),
                                 StringComparison.OrdinalIgnoreCase))
            {
                t2 += 0.12;
                result.WhyMatched.Add($"Favourite artist: {me.TopArtist}");
            }

            // Interest/hobby overlap
            var myInterests = SplitTags(me.Interests);
            var candInterests = SplitTags(candidate.Interests);
            var sharedInterests = myInterests.Intersect(candInterests, StringComparer.OrdinalIgnoreCase).ToList();
            if (sharedInterests.Any())
            {
                t2 += Math.Min(0.25, sharedInterests.Count * 0.08);
                result.CommonInterests = sharedInterests.Take(4).ToList();
                result.WhyMatched.Add($"Shared interests: {string.Join(", ", sharedInterests.Take(3))}");
            }

            // Energy level match
            if (!string.IsNullOrEmpty(me.EnergyLevel) && !string.IsNullOrEmpty(candidate.EnergyLevel)
                && string.Equals(me.EnergyLevel, candidate.EnergyLevel,
                                 StringComparison.OrdinalIgnoreCase))
            {
                t2 += 0.08;
                result.WhyMatched.Add($"Same energy: {me.EnergyLevel}");
            }

            // Personality Type compatibility
            double personalityScore = CalculatePersonalityCompatibility(me.PersonalityType, candidate.PersonalityType);
            if (personalityScore > 0)
            {
                t2 += personalityScore * 0.1;
                result.WhyMatched.Add($"Personality compatible");
            }

            // Love Language compatibility
            double loveLanguageScore = CalculateLoveLanguageCompatibility(me.LoveLanguage, candidate.LoveLanguage);
            if (loveLanguageScore > 0)
            {
                t2 += loveLanguageScore * 0.08;
                result.WhyMatched.Add($"Love languages align");
            }

            // Prompts (both have written prompts)
            if (!string.IsNullOrEmpty(me.Prompts) && !string.IsNullOrEmpty(candidate.Prompts))
                t2 += 0.05;

            result.LifestyleScore = Math.Min(1.0, t2);

            // ── TIER 3: Demographics (25%) ───────────────────────────────
            double t3 = 0;

            // Age proximity
            int myAge = CalcAge(me.DateOfBirth);
            int candAge = CalcAge(candidate.DateOfBirth);
            if (myAge > 0 && candAge > 0)
            {
                int ageDiff = Math.Abs(myAge - candAge);
                if (ageDiff <= 3) t3 += 0.2;
                else if (ageDiff <= 6) t3 += 0.15;
                else if (ageDiff <= 10) t3 += 0.1;
                else if (ageDiff <= 15) t3 += 0.05;
                else t3 += 0.02;
                result.WhyMatched.Add($"Age compatible");
            }

            // Location match
            string myLoc = GetLocation(me);
            string candLoc = GetLocation(candidate);
            if (!string.IsNullOrEmpty(myLoc) && !string.IsNullOrEmpty(candLoc))
            {
                if (string.Equals(myLoc, candLoc, StringComparison.OrdinalIgnoreCase))
                {
                    t3 += 0.25;
                    result.WhyMatched.Add($"Same location: {myLoc}");
                }
                else if (string.Equals(me.Country, candidate.Country,
                                       StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrEmpty(me.Country))
                {
                    t3 += 0.12;
                    result.WhyMatched.Add($"Same country: {me.Country}");
                }
            }

            // Height compatibility
            double heightScore = CalculateHeightCompatibility(me.HeightCm, candidate.HeightCm);
            if (heightScore > 0)
            {
                t3 += heightScore * 0.08;
                if (heightScore >= 0.8)
                    result.WhyMatched.Add($"Height compatible");
            }

            // Body type compatibility
            double bodyTypeScore = CalculateBodyTypeCompatibility(me.BodyType, candidate.BodyType);
            if (bodyTypeScore > 0)
            {
                t3 += bodyTypeScore * 0.05;
            }

            // Ethnicity/Tribe compatibility
            double ethnicityScore = CalculateEthnicityCompatibility(me.Ethnicity, candidate.Ethnicity, me.Tribe, candidate.Tribe);
            if (ethnicityScore > 0)
            {
                t3 += ethnicityScore * 0.05;
            }

            // Family/Kids alignment
            double familyScore = CalculateFamilyCompatibility(me.KidsPreference, me.HasChildren, candidate.KidsPreference, candidate.HasChildren);
            if (familyScore > 0)
            {
                t3 += familyScore * 0.07;
                if (familyScore >= 0.8)
                    result.WhyMatched.Add($"Family goals align");
            }

            // Dietary preference compatibility
            double dietScore = CalculateDietaryCompatibility(me.DietaryPreference, candidate.DietaryPreference);
            if (dietScore > 0)
            {
                t3 += dietScore * 0.05;
            }

            // Exercise frequency compatibility
            double exerciseScore = CalculateExerciseCompatibility(me.ExerciseFrequency, candidate.ExerciseFrequency);
            if (exerciseScore > 0)
            {
                t3 += exerciseScore * 0.05;
            }

            // Sexual orientation alignment
            if (!string.IsNullOrEmpty(me.SexualOrientation)
                && !string.IsNullOrEmpty(candidate.SexualOrientation))
            {
                if (OrientationsCompatible(me.SexualOrientation, candidate.SexualOrientation,
                                           me.Gender, candidate.Gender))
                    t3 += 0.12;
            }
            else
            {
                t3 += 0.05;
            }

            // Lifestyle compatibility
            if (me.Smokes == candidate.Smokes) t3 += 0.03;
            if (me.HasPets == candidate.HasPets) t3 += 0.03;
            if (string.Equals(me.Drinks, candidate.Drinks, StringComparison.OrdinalIgnoreCase)) t3 += 0.04;

            result.DemographicScore = Math.Min(1.0, t3);

            // ── TIER 4: Activity (15%) ───────────────────────────────────
            double t4 = 0;

            if (myLovedAuthorPhones.Contains(candidate.PhoneNumber ?? ""))
            {
                t4 += 0.35;
                result.WhyMatched.Add("You've liked their posts");
            }

            bool recentlyActive = candidatePosts
                .Any(p => (DateTime.UtcNow - p.CreatedAt).TotalDays <= 7);
            if (recentlyActive) t4 += 0.2;

            bool hasRecentStatus = candidateStatusPosts
                .Any(s => (DateTime.UtcNow - s.CreatedAt).TotalHours <= 24);
            if (hasRecentStatus)
            {
                t4 += 0.2;
                result.WhyMatched.Add("Active status today");
            }

            int activityCatOverlap = myCategories.Intersect(candidateCategories).Count();
            if (activityCatOverlap > 0)
                t4 += Math.Min(0.25, activityCatOverlap * 0.08);

            result.ActivityScore = Math.Min(1.0, t4);

            // ── TOTAL ────────────────────────────────────────────────────
            result.TotalScore = Math.Round(
                (result.IntentScore * W_INTENT
               + result.LifestyleScore * W_LIFESTYLE
               + result.DemographicScore * W_DEMOGRAPHIC
               + result.ActivityScore * W_ACTIVITY) * 100, 1);

            result.WhyMatched = result.WhyMatched.Distinct().Take(4).ToList();

            return result;
        }

        // ── Complementary Mode Scoring ────────────────────────────────────
        private static MatchResult ScoreCandidateComplementary(
            User me,
            User candidate,
            List<Post> myPosts,
            List<Post> candidatePosts,
            List<Post> candidateStatusPosts,
            HashSet<string> myLovedAuthorPhones)
        {
            var result = new MatchResult
            {
                User = candidate,
                Location = GetLocation(candidate),
                Age = CalcAge(candidate.DateOfBirth)
            };

            // ── TIER 1: Intent (35%) - Complementary ─────────────────────
            double t1 = 0;

            // Opposite moods attract
            if (!string.IsNullOrEmpty(me.Mood) && !string.IsNullOrEmpty(candidate.Mood))
            {
                double moodComplementScore = GetMoodComplementarity(me.Mood, candidate.Mood);
                if (moodComplementScore > 0)
                {
                    t1 += moodComplementScore * 0.5;
                    result.WhyMatched.Add($"Opposite vibes attract: {me.Mood} ↔ {candidate.Mood}");
                    result.MoodMatch = true;
                    result.SharedMood = $"{me.Mood} ↔ {candidate.Mood}";
                }
            }

            // Different post categories create intrigue
            var myCategories = myPosts
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!.Trim().ToLowerInvariant())
                .ToHashSet();
            var candidateCategories = candidatePosts
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!.Trim().ToLowerInvariant())
                .ToHashSet();

            int uniqueCats = myCategories.Except(candidateCategories).Count();
            if (uniqueCats > 0)
            {
                t1 += Math.Min(0.2, uniqueCats * 0.05);
                result.WhyMatched.Add($"Different perspectives to explore");
            }

            result.IntentScore = Math.Min(1.0, t1);

            // ── TIER 2: Lifestyle (25%) - Complementary ──────────────────
            double t2 = 0;

            // Different music tastes can be complementary
            var myGenres = SplitTags(me.MusicGenres);
            var candGenres = SplitTags(candidate.MusicGenres);
            int uniqueGenres = myGenres.Except(candGenres, StringComparer.OrdinalIgnoreCase).Count();
            if (uniqueGenres > 0)
            {
                t2 += Math.Min(0.15, uniqueGenres * 0.05);
                result.WhyMatched.Add($"Introduce each other to new music");
            }

            // Complementary interests
            var myInterests = SplitTags(me.Interests);
            var candInterests = SplitTags(candidate.Interests);
            var complementaryInterests = GetComplementaryInterests(myInterests, candInterests);
            if (complementaryInterests.Any())
            {
                t2 += Math.Min(0.25, complementaryInterests.Count * 0.08);
                result.CommonInterests = complementaryInterests.Take(4).ToList();
                result.WhyMatched.Add($"Complementary interests balance each other");
            }

            // Different energy levels can balance each other
            if (!string.IsNullOrEmpty(me.EnergyLevel) && !string.IsNullOrEmpty(candidate.EnergyLevel))
            {
                if (AreEnergyLevelsComplementary(me.EnergyLevel, candidate.EnergyLevel))
                {
                    t2 += 0.12;
                    result.WhyMatched.Add($"Energy levels balance perfectly");
                }
            }

            // Opposite personality types often attract
            double personalityComplementScore = CalculatePersonalityComplementarity(me.PersonalityType, candidate.PersonalityType);
            if (personalityComplementScore > 0)
            {
                t2 += personalityComplementScore * 0.15;
                result.WhyMatched.Add($"Personalities complement each other");
            }

            result.LifestyleScore = Math.Min(1.0, t2);

            // ── TIER 3: Demographics (25%) - Complementary ───────────────
            double t3 = 0;

            // Age gap can be attractive
            int myAge = CalcAge(me.DateOfBirth);
            int candAge = CalcAge(candidate.DateOfBirth);
            if (myAge > 0 && candAge > 0)
            {
                int ageDiff = Math.Abs(myAge - candAge);
                if (ageDiff >= 3 && ageDiff <= 8) t3 += 0.15;
                else if (ageDiff > 8 && ageDiff <= 15) t3 += 0.1;
                else if (ageDiff > 0 && ageDiff < 3) t3 += 0.05;

                if (ageDiff >= 3)
                    result.WhyMatched.Add($"Age difference brings wisdom & energy");
            }

            // Different locations can be exciting
            string myLoc = GetLocation(me);
            string candLoc = GetLocation(candidate);
            if (!string.IsNullOrEmpty(myLoc) && !string.IsNullOrEmpty(candLoc))
            {
                if (!string.Equals(myLoc, candLoc, StringComparison.OrdinalIgnoreCase))
                {
                    t3 += 0.1;
                    if (!string.IsNullOrEmpty(me.Country) && !string.IsNullOrEmpty(candidate.Country) &&
                        !string.Equals(me.Country, candidate.Country, StringComparison.OrdinalIgnoreCase))
                    {
                        t3 += 0.08;
                        result.WhyMatched.Add($"Adventure across cultures");
                    }
                }
            }

            // Different body types can be attractive
            double bodyTypeComplementScore = CalculateBodyTypeComplementarity(me.BodyType, candidate.BodyType);
            if (bodyTypeComplementScore > 0)
            {
                t3 += bodyTypeComplementScore * 0.1;
                result.WhyMatched.Add($"Physical chemistry potential");
            }

            // Different dietary preferences can lead to exploring new cuisines
            double dietComplementScore = CalculateDietaryComplementarity(me.DietaryPreference, candidate.DietaryPreference);
            if (dietComplementScore > 0)
            {
                t3 += dietComplementScore * 0.05;
                result.WhyMatched.Add($"Discover new foods together");
            }

            // Different exercise levels can motivate each other
            double exerciseComplementScore = CalculateExerciseComplementarity(me.ExerciseFrequency, candidate.ExerciseFrequency);
            if (exerciseComplementScore > 0)
            {
                t3 += exerciseComplementScore * 0.05;
                result.WhyMatched.Add($"Motivate each other to stay active");
            }

            result.DemographicScore = Math.Min(1.0, t3);

            // ── TIER 4: Activity (15%) - Same as similar mode ────────────
            double t4 = 0;

            if (myLovedAuthorPhones.Contains(candidate.PhoneNumber ?? ""))
            {
                t4 += 0.35;
                result.WhyMatched.Add("You've liked their posts");
            }

            bool recentlyActive = candidatePosts
                .Any(p => (DateTime.UtcNow - p.CreatedAt).TotalDays <= 7);
            if (recentlyActive) t4 += 0.2;

            bool hasRecentStatus = candidateStatusPosts
                .Any(s => (DateTime.UtcNow - s.CreatedAt).TotalHours <= 24);
            if (hasRecentStatus)
            {
                t4 += 0.2;
                result.WhyMatched.Add("Active status today");
            }

            int activityCatOverlap = myCategories.Intersect(candidateCategories).Count();
            if (activityCatOverlap > 0)
                t4 += Math.Min(0.25, activityCatOverlap * 0.08);

            result.ActivityScore = Math.Min(1.0, t4);

            // ── TOTAL ────────────────────────────────────────────────────
            result.TotalScore = Math.Round(
                (result.IntentScore * W_INTENT
               + result.LifestyleScore * W_LIFESTYLE
               + result.DemographicScore * W_DEMOGRAPHIC
               + result.ActivityScore * W_ACTIVITY) * 100, 1);

            result.WhyMatched = result.WhyMatched.Distinct().Take(4).ToList();

            return result;
        }

        // ── Complementary Helper Methods ───────────────────────────────────

        private static double GetMoodComplementarity(string moodA, string moodB)
        {
            var complementaryPairs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Serious relationship", new[] { "Something casual", "Hook-up / FWB", "Just vibes / casual fun", "Let's see where it goes" } },
                { "Long-term potential", new[] { "Something casual", "Hook-up / FWB", "Just vibes / casual fun" } },
                { "Something casual", new[] { "Serious relationship", "Long-term potential", "Deep talks and connection" } },
                { "Hook-up / FWB", new[] { "Serious relationship", "Long-term potential", "Deep talks and connection" } },
                { "Just vibes / casual fun", new[] { "Serious relationship", "Long-term potential", "Deep talks and connection" } },
                { "Deep talks and connection", new[] { "Something casual", "Hook-up / FWB", "Just vibes / casual fun" } },
                { "Let's see where it goes", new[] { "Serious relationship", "Long-term potential" } }
            };

            if (complementaryPairs.TryGetValue(moodA, out var complements) && complements.Contains(moodB))
                return 1.0;

            if (complementaryPairs.TryGetValue(moodB, out complements) && complements.Contains(moodA))
                return 1.0;

            return 0;
        }

        private static List<string> GetComplementaryInterests(HashSet<string> myInterests, HashSet<string> theirInterests)
        {
            var complementary = new List<string>();

            var complementaryPairs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Travel", new[] { "Homebody", "Local explorer", "Food" } },
                { "Fitness", new[] { "Food", "Coffee lover", "Music" } },
                { "Gym", new[] { "Food", "Coffee lover", "Reading" } },
                { "Tech", new[] { "Art", "Music", "Travel" } },
                { "Music", new[] { "Art", "Dancing", "Reading" } },
                { "Coffee lover", new[] { "Tea drinker", "Food", "Reading" } },
                { "Entrepreneur", new[] { "Art", "Travel", "Music" } }
            };

            foreach (var myInterest in myInterests)
            {
                if (complementaryPairs.TryGetValue(myInterest, out var compats))
                {
                    foreach (var compat in compats)
                    {
                        if (theirInterests.Contains(compat))
                            complementary.Add($"{myInterest} + {compat}");
                    }
                }
            }

            return complementary;
        }

        private static bool AreEnergyLevelsComplementary(string energyA, string energyB)
        {
            if (energyA == "Introvert" && energyB == "Extrovert") return true;
            if (energyA == "Extrovert" && energyB == "Introvert") return true;
            if (energyA == "Balanced" && (energyB == "Introvert" || energyB == "Extrovert")) return true;
            if (energyB == "Balanced" && (energyA == "Introvert" || energyA == "Extrovert")) return true;
            return false;
        }

        private static double CalculatePersonalityComplementarity(string? myPersonality, string? theirPersonality)
        {
            if (string.IsNullOrEmpty(myPersonality) || string.IsNullOrEmpty(theirPersonality))
                return 0;

            var oppositePairs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "INTJ", new[] { "ESFP", "ENFP" } },
                { "INTP", new[] { "ESFJ", "ENFJ" } },
                { "ENTJ", new[] { "ISFP", "INFP" } },
                { "ENTP", new[] { "ISFJ", "INFJ" } },
                { "INFJ", new[] { "ESTP", "ENTP" } },
                { "INFP", new[] { "ESTJ", "ENTJ" } },
                { "ENFJ", new[] { "ISTP", "INTP" } },
                { "ENFP", new[] { "ISTJ", "INTJ" } },
                { "ISTJ", new[] { "ENFP", "ENTP" } },
                { "ISFJ", new[] { "ENTP", "ENFP" } },
                { "ESTJ", new[] { "INFP", "ISFP" } },
                { "ESFJ", new[] { "INTP", "ISTP" } },
                { "ISTP", new[] { "ENFJ", "INFJ" } },
                { "ISFP", new[] { "ENTJ", "INTJ" } },
                { "ESTP", new[] { "INFJ", "INFP" } },
                { "ESFP", new[] { "INTJ", "ISTJ" } }
            };

            string myLetters = ExtractMBTILetters(myPersonality);
            string theirLetters = ExtractMBTILetters(theirPersonality);

            if (string.IsNullOrEmpty(myLetters) || string.IsNullOrEmpty(theirLetters))
                return 0.3;

            if (oppositePairs.TryGetValue(myLetters, out var opposites) && opposites.Contains(theirLetters))
                return 1.0;

            int oppositeCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i < myLetters.Length && i < theirLetters.Length)
                {
                    if (myLetters[i] != theirLetters[i])
                        oppositeCount++;
                }
            }

            return oppositeCount switch
            {
                4 => 0.9,
                3 => 0.7,
                2 => 0.5,
                _ => 0.3
            };
        }

        // ── Range-Based Calculation Methods ───────────────────────────────

        private static double CalculateHeightCompatibility(int? myHeight, int? theirHeight)
        {
            if (!myHeight.HasValue || !theirHeight.HasValue || myHeight.Value <= 0 || theirHeight.Value <= 0)
                return 0;

            int diff = Math.Abs(myHeight.Value - theirHeight.Value);

            if (diff <= 10) return 1.0;
            if (diff <= 15) return 0.8;
            if (diff <= 20) return 0.6;
            if (diff <= 25) return 0.4;
            if (diff <= 30) return 0.2;
            return 0.1;
        }

        private static double CalculateBodyTypeCompatibility(string? myType, string? theirType)
        {
            if (string.IsNullOrEmpty(myType) || string.IsNullOrEmpty(theirType))
                return 0;

            if (string.Equals(myType, theirType, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            var compatible = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Slim", new[] { "Athletic", "Average" } },
                { "Athletic", new[] { "Slim", "Average", "Muscular" } },
                { "Average", new[] { "Slim", "Athletic", "Curvy" } },
                { "Curvy", new[] { "Average", "Full-figured" } },
                { "Full-figured", new[] { "Curvy", "Average" } },
                { "Muscular", new[] { "Athletic", "Average" } }
            };

            if (compatible.TryGetValue(myType, out var compat) && compat.Contains(theirType))
                return 0.7;

            return 0.3;
        }

        private static double CalculateBodyTypeComplementarity(string? myType, string? theirType)
        {
            if (string.IsNullOrEmpty(myType) || string.IsNullOrEmpty(theirType))
                return 0;

            if (string.Equals(myType, theirType, StringComparison.OrdinalIgnoreCase))
                return 0.3;

            var complementaryPairs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Slim", new[] { "Athletic", "Curvy", "Average" } },
                { "Athletic", new[] { "Curvy", "Slim", "Average" } },
                { "Curvy", new[] { "Athletic", "Muscular", "Average" } },
                { "Full-figured", new[] { "Athletic", "Muscular" } },
                { "Muscular", new[] { "Curvy", "Slim", "Average" } },
                { "Average", new[] { "Slim", "Athletic", "Curvy" } }
            };

            if (complementaryPairs.TryGetValue(myType, out var complements) && complements.Contains(theirType))
                return 1.0;

            return 0.2;
        }

        private static double CalculateEthnicityCompatibility(string? myEthnicity, string? theirEthnicity, string? myTribe, string? theirTribe)
        {
            double score = 0;

            if (!string.IsNullOrEmpty(myEthnicity) && !string.IsNullOrEmpty(theirEthnicity))
            {
                if (string.Equals(myEthnicity, theirEthnicity, StringComparison.OrdinalIgnoreCase))
                    score += 0.6;
                else if (myEthnicity == "Mixed" || theirEthnicity == "Mixed")
                    score += 0.4;
                else
                    score += 0.2;
            }

            if (!string.IsNullOrEmpty(myTribe) && !string.IsNullOrEmpty(theirTribe))
            {
                if (string.Equals(myTribe, theirTribe, StringComparison.OrdinalIgnoreCase))
                    score += 0.4;
                else if (myTribe == "Other" || theirTribe == "Other")
                    score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        private static double CalculateFamilyCompatibility(string? myKidsPref, string? myHasKids, string? theirKidsPref, string? theirHasKids)
        {
            double score = 0;

            if (!string.IsNullOrEmpty(myKidsPref) && !string.IsNullOrEmpty(theirKidsPref))
            {
                if (string.Equals(myKidsPref, theirKidsPref, StringComparison.OrdinalIgnoreCase))
                    score += 0.6;
                else if ((myKidsPref.Contains("Open") && theirKidsPref.Contains("Want")) ||
                         (theirKidsPref.Contains("Open") && myKidsPref.Contains("Want")))
                    score += 0.4;
                else if (myKidsPref == "Not sure" || theirKidsPref == "Not sure")
                    score += 0.3;
                else
                    score += 0.1;
            }

            if (!string.IsNullOrEmpty(myHasKids) && !string.IsNullOrEmpty(theirHasKids))
            {
                if (string.Equals(myHasKids, theirHasKids, StringComparison.OrdinalIgnoreCase))
                    score += 0.4;
                else
                    score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        private static double CalculateDietaryCompatibility(string? myDiet, string? theirDiet)
        {
            if (string.IsNullOrEmpty(myDiet) || string.IsNullOrEmpty(theirDiet))
                return 0;

            if (string.Equals(myDiet, theirDiet, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            var compatible = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Omnivore", new[] { "Vegetarian", "Pescatarian" } },
                { "Vegetarian", new[] { "Omnivore", "Pescatarian", "Vegan" } },
                { "Vegan", new[] { "Vegetarian" } },
                { "Pescatarian", new[] { "Omnivore", "Vegetarian" } },
                { "Halal", new[] { "Omnivore", "Vegetarian", "Kosher" } },
                { "Kosher", new[] { "Omnivore", "Vegetarian", "Halal" } }
            };

            if (compatible.TryGetValue(myDiet, out var compat) && compat.Contains(theirDiet))
                return 0.7;

            return 0.2;
        }

        private static double CalculateDietaryComplementarity(string? myDiet, string? theirDiet)
        {
            if (string.IsNullOrEmpty(myDiet) || string.IsNullOrEmpty(theirDiet))
                return 0;

            if (string.Equals(myDiet, theirDiet, StringComparison.OrdinalIgnoreCase))
                return 0.2;

            return 0.6;
        }

        private static double CalculateExerciseCompatibility(string? myExercise, string? theirExercise)
        {
            if (string.IsNullOrEmpty(myExercise) || string.IsNullOrEmpty(theirExercise))
                return 0;

            if (string.Equals(myExercise, theirExercise, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Daily", 5 },
                { "Several times a week", 4 },
                { "Once a week", 3 },
                { "Few times a month", 2 },
                { "Rarely", 1 },
                { "Never", 0 }
            };

            if (levels.TryGetValue(myExercise, out int myLevel) && levels.TryGetValue(theirExercise, out int theirLevel))
            {
                int diff = Math.Abs(myLevel - theirLevel);
                if (diff <= 1) return 0.9;
                if (diff <= 2) return 0.7;
                if (diff <= 3) return 0.5;
                return 0.3;
            }

            return 0.2;
        }

        private static double CalculateExerciseComplementarity(string? myExercise, string? theirExercise)
        {
            if (string.IsNullOrEmpty(myExercise) || string.IsNullOrEmpty(theirExercise))
                return 0;

            if (string.Equals(myExercise, theirExercise, StringComparison.OrdinalIgnoreCase))
                return 0.3;

            var activeLevels = new[] { "Daily", "Several times a week" };
            var moderateLevels = new[] { "Once a week", "Few times a month" };

            bool myActive = activeLevels.Contains(myExercise);
            bool theirActive = activeLevels.Contains(theirExercise);
            bool myModerate = moderateLevels.Contains(myExercise);
            bool theirModerate = moderateLevels.Contains(theirExercise);

            if ((myActive && theirModerate) || (myModerate && theirActive))
                return 0.8;

            return 0.3;
        }

        private static double CalculatePersonalityCompatibility(string? myPersonality, string? theirPersonality)
        {
            if (string.IsNullOrEmpty(myPersonality) || string.IsNullOrEmpty(theirPersonality))
                return 0;

            if (string.Equals(myPersonality, theirPersonality, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            string myLetters = ExtractMBTILetters(myPersonality);
            string theirLetters = ExtractMBTILetters(theirPersonality);

            if (string.IsNullOrEmpty(myLetters) || string.IsNullOrEmpty(theirLetters))
                return 0.3;

            int matches = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(myLetters.Length, theirLetters.Length)); i++)
            {
                if (myLetters[i] == theirLetters[i])
                    matches++;
            }

            return matches switch
            {
                4 => 1.0,
                3 => 0.8,
                2 => 0.5,
                _ => 0.3
            };
        }

        private static double CalculateLoveLanguageCompatibility(string? myLanguage, string? theirLanguage)
        {
            if (string.IsNullOrEmpty(myLanguage) || string.IsNullOrEmpty(theirLanguage))
                return 0;

            if (string.Equals(myLanguage, theirLanguage, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            var complementary = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Words of Affirmation", new[] { "Quality Time", "Acts of Service" } },
                { "Quality Time", new[] { "Words of Affirmation", "Physical Touch" } },
                { "Receiving Gifts", new[] { "Acts of Service", "Quality Time" } },
                { "Acts of Service", new[] { "Words of Affirmation", "Quality Time" } },
                { "Physical Touch", new[] { "Quality Time", "Words of Affirmation" } }
            };

            if (complementary.TryGetValue(myLanguage, out var compat) && compat.Contains(theirLanguage))
                return 0.7;

            return 0.4;
        }

        private static string ExtractMBTILetters(string personality)
        {
            var words = personality.Split(' ', '-', '(');
            foreach (var word in words)
            {
                if (word.Length == 4 && word.All(c => "IEIN".Contains(c) || "SN".Contains(c) || "TF".Contains(c) || "JP".Contains(c)))
                    return word.ToUpper();
            }
            return string.Empty;
        }

        // ── Sub-tab filters ───────────────────────────────────────────────
        private static List<MatchResult> ApplySubTabFilter(
            List<MatchResult> all, User me, string subTab)
        {
            return subTab switch
            {
                "MoodMatch" => all.Where(r => r.MoodMatch || r.IntentScore >= 0.3).ToList(),
                "NearMe" => all.Where(r => r.HasLocation
                    && string.Equals(GetLocation(me), r.Location,
                                     StringComparison.OrdinalIgnoreCase)).ToList(),
                "Vibes" => all.Where(r => r.LifestyleScore >= 0.2
                                           || r.CommonInterests.Any()).ToList(),
                _ => all
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static bool HasDealbreaker(User me, User candidate)
        {
            if (!string.IsNullOrEmpty(candidate.Dealbreakers))
            {
                var dealbreakers = candidate.Dealbreakers.ToLowerInvariant();

                if (me.Smokes && dealbreakers.Contains("smoker")) return true;
                if (!me.HasPets && dealbreakers.Contains("no pets")) return true;
                if (me.Drinks == "Yes" && dealbreakers.Contains("drinker")) return true;
            }

            return false;
        }

        private static bool MoodsAreCompatible(string moodA, string moodB)
        {
            var serious = new[] { "Serious relationship", "Long-term potential" };
            var casual = new[] { "Something casual", "Just vibes / casual fun",
                                  "Let's see where it goes", "OS (open situationship)" };
            var hookup = new[] { "Hook-up / FWB", "ENM / Open to non-monogamy" };
            var deep = new[] { "Deep talks and connection", "Networking / collabs / friends first" };

            bool InGroup(string[] g, string m) =>
                g.Any(x => string.Equals(x, m, StringComparison.OrdinalIgnoreCase));

            return (InGroup(serious, moodA) && InGroup(serious, moodB))
                || (InGroup(casual, moodA) && InGroup(casual, moodB))
                || (InGroup(hookup, moodA) && InGroup(hookup, moodB))
                || (InGroup(deep, moodA) && InGroup(deep, moodB));
        }

        private static bool OrientationsCompatible(
            string orientA, string orientB, string genderA, string genderB)
        {
            bool bisexual(string o) => o.Contains("Bi", StringComparison.OrdinalIgnoreCase)
                                    || o.Contains("Pan", StringComparison.OrdinalIgnoreCase);
            if (bisexual(orientA) || bisexual(orientB)) return true;

            bool sameGender = string.Equals(genderA, genderB, StringComparison.OrdinalIgnoreCase);
            bool gayA = orientA.Contains("Gay", StringComparison.OrdinalIgnoreCase)
                     || orientA.Contains("Lesbian", StringComparison.OrdinalIgnoreCase);
            bool gayB = orientB.Contains("Gay", StringComparison.OrdinalIgnoreCase)
                     || orientB.Contains("Lesbian", StringComparison.OrdinalIgnoreCase);
            bool straightA = orientA.Contains("Straight", StringComparison.OrdinalIgnoreCase)
                          || orientA.Contains("Hetero", StringComparison.OrdinalIgnoreCase);
            bool straightB = orientB.Contains("Straight", StringComparison.OrdinalIgnoreCase)
                          || orientB.Contains("Hetero", StringComparison.OrdinalIgnoreCase);

            if (gayA && gayB && sameGender) return true;
            if (straightA && straightB && !sameGender) return true;
            return false;
        }

        private static HashSet<string> SplitTags(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new HashSet<string>();
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetLocation(User u)
        {
            if (!string.IsNullOrEmpty(u.Country) && !string.IsNullOrEmpty(u.State))
                return $"{u.State}, {u.Country}";
            if (!string.IsNullOrEmpty(u.Country)) return u.Country;
            if (!string.IsNullOrEmpty(u.State)) return u.State;
            return string.Empty;
        }

        private static int CalcAge(DateTime dob)
        {
            if (dob == DateTime.MinValue) return 0;
            var today = DateTime.Today;
            int age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            return age;
        }
    }
}