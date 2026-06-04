using Lock.Models;
using Lock.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class ProfileViewService
    {
        public static async Task RecordProfileViewAsync(int viewerId, string viewerPhone, int viewedId, string viewedPhone)
        {
            try
            {
                if (viewerId == viewedId) return; // Don't record self-views

                // Check if already viewed in last 24 hours
                var existingViews = await SupabaseService.GetAsync<ProfileView>("ProfileViews",
                    $"ViewerUserId=eq.{viewerId}&ViewedUserId=eq.{viewedId}&ViewedAt=gt.{DateTime.UtcNow.AddHours(-24):yyyy-MM-ddTHH:mm:ssZ}&limit=1");

                var existingView = existingViews.FirstOrDefault();

                if (existingView == null)
                {
                    var view = new ProfileView
                    {
                        ViewedUserId = viewedId,
                        ViewedUserPhone = viewedPhone,
                        ViewerUserId = viewerId,
                        ViewerUserPhone = viewerPhone,
                        ViewedAt = DateTime.UtcNow,
                        IsNew = true
                    };
                    await SupabaseService.InsertAsync("ProfileViews", view);
                    Debug.WriteLine($"Recorded profile view: {viewerPhone} viewed {viewedPhone}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RecordProfileViewAsync error: {ex}");
            }
        }

        public static async Task<int> GetProfileViewCountAsync(int userId)
        {
            try
            {
                var views = await SupabaseService.GetAsync<ProfileView>("ProfileViews",
                    $"ViewedUserId=eq.{userId}");
                return views.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProfileViewCountAsync error: {ex}");
                return 0;
            }
        }

        public static async Task<List<ProfileView>> GetRecentProfileViewsAsync(int userId, int limit = 10)
        {
            try
            {
                return await SupabaseService.GetAsync<ProfileView>("ProfileViews",
                    $"ViewedUserId=eq.{userId}&order=ViewedAt.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRecentProfileViewsAsync error: {ex}");
                return new List<ProfileView>();
            }
        }

        public static async Task<int> GetNewProfileViewsCountAsync(int userId)
        {
            try
            {
                var views = await SupabaseService.GetAsync<ProfileView>("ProfileViews",
                    $"ViewedUserId=eq.{userId}&IsNew=eq.true");
                return views.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetNewProfileViewsCountAsync error: {ex}");
                return 0;
            }
        }

        public static async Task<bool> MarkViewsAsSeenAsync(int userId)
        {
            try
            {
                var success = await SupabaseService.UpdateAsync("ProfileViews",
                    $"ViewedUserId=eq.{userId}&IsNew=eq.true",
                    new { IsNew = false });

                if (success)
                {
                    Debug.WriteLine($"Marked profile views as seen for user {userId}");
                }
                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MarkViewsAsSeenAsync error: {ex}");
                return false;
            }
        }

        public static async Task<List<ProfileView>> GetViewsByViewerAsync(int viewerId, int limit = 10)
        {
            try
            {
                return await SupabaseService.GetAsync<ProfileView>("ProfileViews",
                    $"ViewerUserId=eq.{viewerId}&order=ViewedAt.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetViewsByViewerAsync error: {ex}");
                return new List<ProfileView>();
            }
        }

        public static async Task<bool> DeleteOldProfileViewsAsync(int daysOld = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var success = await SupabaseService.DeleteAsync("ProfileViews",
                    $"ViewedAt=lt.{cutoffDate:yyyy-MM-ddTHH:mm:ssZ}");

                Debug.WriteLine($"Deleted profile views older than {daysOld} days");
                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteOldProfileViewsAsync error: {ex}");
                return false;
            }
        }
    }
}