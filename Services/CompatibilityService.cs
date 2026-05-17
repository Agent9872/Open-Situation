using Lock.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class CompatibilityService
    {
        public static async Task<int> CalculateCompatibilityScoreAsync(User currentUser, User targetUser)
        {
            if (currentUser == null || targetUser == null) return 0;

            int totalPoints = 0;
            int maxPoints = 0;

            // 1. Age compatibility (10 points)
            maxPoints += 10;
            int age1 = DateTime.Today.Year - currentUser.DateOfBirth.Year;
            int age2 = DateTime.Today.Year - targetUser.DateOfBirth.Year;
            int ageDiff = Math.Abs(age1 - age2);
            if (ageDiff <= 3) totalPoints += 10;
            else if (ageDiff <= 6) totalPoints += 7;
            else if (ageDiff <= 10) totalPoints += 4;
            else totalPoints += 2;

            // 2. Interest match (25 points)
            maxPoints += 25;
            var currentInterests = (currentUser.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim().ToLower());
            var targetInterests = (targetUser.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(i => i.Trim().ToLower());
            var commonInterests = currentInterests.Intersect(targetInterests).Count();
            var interestScore = Math.Min(25, (commonInterests * 25) / Math.Max(1, Math.Max(currentInterests.Count(), targetInterests.Count())));
            totalPoints += interestScore;

            // 3. Location match (15 points)
            maxPoints += 15;
            bool sameCountry = string.Equals(currentUser.Country, targetUser.Country, StringComparison.OrdinalIgnoreCase);
            bool sameState = string.Equals(currentUser.State, targetUser.State, StringComparison.OrdinalIgnoreCase);
            if (sameCountry && sameState) totalPoints += 15;
            else if (sameCountry) totalPoints += 10;
            else totalPoints += 5;

            // 4. Lifestyle compatibility (20 points)
            maxPoints += 20;
            int lifestyleScore = 0;
            if (currentUser.Smokes == targetUser.Smokes) lifestyleScore += 5;
            if (currentUser.HasPets == targetUser.HasPets) lifestyleScore += 5;
            if (currentUser.Drinks == targetUser.Drinks) lifestyleScore += 5;
            if (currentUser.EnergyLevel == targetUser.EnergyLevel) lifestyleScore += 5;
            totalPoints += lifestyleScore;

            // 5. Personality type compatibility (15 points)
            maxPoints += 15;
            if (!string.IsNullOrEmpty(currentUser.PersonalityType) && !string.IsNullOrEmpty(targetUser.PersonalityType))
            {
                // Simple logic: same first letter (I/E) gives compatibility
                if (currentUser.PersonalityType.Length > 0 && targetUser.PersonalityType.Length > 0)
                {
                    if (currentUser.PersonalityType[0] == targetUser.PersonalityType[0])
                        totalPoints += 10;
                    totalPoints += 5;
                }
            }
            else
            {
                totalPoints += 8; // Neutral if not specified
            }

            // 6. Love language compatibility (15 points)
            maxPoints += 15;
            if (!string.IsNullOrEmpty(currentUser.LoveLanguage) && !string.IsNullOrEmpty(targetUser.LoveLanguage))
            {
                if (currentUser.LoveLanguage == targetUser.LoveLanguage)
                    totalPoints += 15;
                else if (AreLoveLanguagesCompatible(currentUser.LoveLanguage, targetUser.LoveLanguage))
                    totalPoints += 10;
                else
                    totalPoints += 5;
            }
            else
            {
                totalPoints += 8;
            }

            // Calculate percentage
            return (int)((double)totalPoints / maxPoints * 100);
        }

        private static bool AreLoveLanguagesCompatible(string lang1, string lang2)
        {
            // Complementary love languages
            var compatiblePairs = new Dictionary<string, string[]>
            {
                { "Words of Affirmation", new[] { "Quality Time" } },
                { "Quality Time", new[] { "Words of Affirmation", "Physical Touch" } },
                { "Physical Touch", new[] { "Quality Time", "Acts of Service" } },
                { "Acts of Service", new[] { "Physical Touch", "Receiving Gifts" } },
                { "Receiving Gifts", new[] { "Acts of Service" } }
            };

            return compatiblePairs.ContainsKey(lang1) && compatiblePairs[lang1].Contains(lang2);
        }
    }
}