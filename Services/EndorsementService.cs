using Lock.Chat.Services;
using Lock.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class EndorsementService
    {
        public static async Task<UserEndorsement> AddEndorsementAsync(int targetUserId, string targetPhone, int endorserUserId, string endorserPhone, string endorserName, string endorserProfileImage, string testimonial, int rating = 5)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Ensure the table exists with all required columns
                await db.CreateTableAsync<UserEndorsement>();

                // Validate inputs
                if (targetUserId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync: Invalid targetUserId: {targetUserId}");
                    return null;
                }

                if (endorserUserId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync: Invalid endorserUserId: {endorserUserId}");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(testimonial))
                {
                    System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync: Testimonial is empty");
                    return null;
                }

                // Ensure rating is between 1 and 5
                rating = Math.Clamp(rating, 1, 5);

                var endorsement = new UserEndorsement
                {
                    TargetUserId = targetUserId,
                    TargetUserPhone = targetPhone ?? string.Empty,
                    EndorserUserId = endorserUserId,
                    EndorserUserPhone = endorserPhone ?? string.Empty,
                    EndorserName = endorserName ?? string.Empty,
                    EndorserProfileImage = endorserProfileImage ?? string.Empty,  // ADD THIS LINE
                    Testimonial = testimonial,
                    Rating = rating,
                    CreatedAt = DateTime.UtcNow,
                    IsApproved = true
                };

                System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync: Attempting to insert endorsement for TargetUserId: {targetUserId}, EndorserUserId: {endorserUserId}");

                await db.InsertAsync(endorsement);
                System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync: Successfully inserted endorsement with Id: {endorsement.Id}");

                return endorsement;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AddEndorsementAsync stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public static async Task<List<UserEndorsement>> GetEndorsementsForUserAsync(int userId, int limit = 10)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var endorsements = await db.Table<UserEndorsement>()
                    .Where(e => e.TargetUserId == userId && e.IsApproved)
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(limit)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"GetEndorsementsForUserAsync: Found {endorsements.Count} endorsements for user {userId}");
                return endorsements;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEndorsementsForUserAsync error: {ex}");
                return new List<UserEndorsement>();
            }
        }

        public static async Task<bool> DeleteEndorsementAsync(int endorsementId, int userId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var endorsement = await db.Table<UserEndorsement>()
                    .Where(e => e.Id == endorsementId && e.TargetUserId == userId)
                    .FirstOrDefaultAsync();

                if (endorsement != null)
                {
                    await db.DeleteAsync(endorsement);
                    System.Diagnostics.Debug.WriteLine($"DeleteEndorsementAsync: Deleted endorsement {endorsementId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteEndorsementAsync error: {ex}");
                return false;
            }
        }
    }
}