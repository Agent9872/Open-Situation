using Lock.Models;
using Lock.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class EndorsementService
    {
        public static async Task<UserEndorsement?> AddEndorsementAsync(int targetUserId, string targetPhone, int endorserUserId, string endorserPhone, string endorserName, string endorserProfileImage, string testimonial, int rating = 5)
        {
            try
            {
                // Validate inputs
                if (targetUserId <= 0)
                {
                    Debug.WriteLine($"AddEndorsementAsync: Invalid targetUserId: {targetUserId}");
                    return null;
                }

                if (endorserUserId <= 0)
                {
                    Debug.WriteLine($"AddEndorsementAsync: Invalid endorserUserId: {endorserUserId}");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(testimonial))
                {
                    Debug.WriteLine($"AddEndorsementAsync: Testimonial is empty");
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
                    EndorserProfileImage = endorserProfileImage ?? string.Empty,
                    Testimonial = testimonial,
                    Rating = rating,
                    CreatedAt = DateTime.UtcNow,
                    IsApproved = true
                };

                Debug.WriteLine($"AddEndorsementAsync: Attempting to insert endorsement for TargetUserId: {targetUserId}, EndorserUserId: {endorserUserId}");

                var inserted = await SupabaseService.InsertAndReturnAsync<UserEndorsement>("UserEndorsements", endorsement);

                if (inserted != null)
                {
                    Debug.WriteLine($"AddEndorsementAsync: Successfully inserted endorsement with Id: {inserted.Id}");
                }
                else
                {
                    Debug.WriteLine($"AddEndorsementAsync: Failed to insert endorsement");
                }

                return inserted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddEndorsementAsync error: {ex.Message}");
                Debug.WriteLine($"AddEndorsementAsync stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public static async Task<List<UserEndorsement>> GetEndorsementsForUserAsync(int userId, int limit = 10)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"TargetUserId=eq.{userId}&IsApproved=eq.true&order=CreatedAt.desc&limit={limit}");

                Debug.WriteLine($"GetEndorsementsForUserAsync: Found {endorsements.Count} endorsements for user {userId}");
                return endorsements;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetEndorsementsForUserAsync error: {ex}");
                return new List<UserEndorsement>();
            }
        }

        public static async Task<List<UserEndorsement>> GetEndorsementsByUserAsync(int endorserUserId, int limit = 10)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"EndorserUserId=eq.{endorserUserId}&order=CreatedAt.desc&limit={limit}");

                Debug.WriteLine($"GetEndorsementsByUserAsync: Found {endorsements.Count} endorsements by user {endorserUserId}");
                return endorsements;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetEndorsementsByUserAsync error: {ex}");
                return new List<UserEndorsement>();
            }
        }

        public static async Task<bool> DeleteEndorsementAsync(int endorsementId, int userId)
        {
            try
            {
                // First check if the endorsement belongs to the user
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"Id=eq.{endorsementId}&TargetUserId=eq.{userId}&limit=1");

                var endorsement = endorsements.FirstOrDefault();

                if (endorsement != null)
                {
                    var success = await SupabaseService.DeleteAsync("UserEndorsements", $"Id=eq.{endorsementId}");
                    if (success)
                    {
                        Debug.WriteLine($"DeleteEndorsementAsync: Deleted endorsement {endorsementId}");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteEndorsementAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> ApproveEndorsementAsync(int endorsementId, int targetUserId)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"Id=eq.{endorsementId}&TargetUserId=eq.{targetUserId}&limit=1");

                var endorsement = endorsements.FirstOrDefault();

                if (endorsement != null && !endorsement.IsApproved)
                {
                    var success = await SupabaseService.UpdateAsync("UserEndorsements", $"Id=eq.{endorsementId}",
                        new { IsApproved = true, UpdatedAt = DateTime.UtcNow });

                    if (success)
                    {
                        Debug.WriteLine($"ApproveEndorsementAsync: Approved endorsement {endorsementId}");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApproveEndorsementAsync error: {ex}");
                return false;
            }
        }

        public static async Task<double> GetAverageRatingForUserAsync(int userId)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"TargetUserId=eq.{userId}&IsApproved=eq.true");

                if (!endorsements.Any())
                    return 0;

                return endorsements.Average(e => e.Rating);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAverageRatingForUserAsync error: {ex}");
                return 0;
            }
        }

        public static async Task<int> GetEndorsementCountForUserAsync(int userId)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"TargetUserId=eq.{userId}&IsApproved=eq.true");

                return endorsements.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetEndorsementCountForUserAsync error: {ex}");
                return 0;
            }
        }

        public static async Task<bool> HasUserEndorsedAsync(int endorserUserId, int targetUserId)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"EndorserUserId=eq.{endorserUserId}&TargetUserId=eq.{targetUserId}&limit=1");

                return endorsements.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HasUserEndorsedAsync error: {ex}");
                return false;
            }
        }

        public static async Task<UserEndorsement?> GetEndorsementAsync(int endorsementId)
        {
            try
            {
                var endorsements = await SupabaseService.GetAsync<UserEndorsement>("UserEndorsements",
                    $"Id=eq.{endorsementId}&limit=1");

                return endorsements.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetEndorsementAsync error: {ex}");
                return null;
            }
        }
    }
}