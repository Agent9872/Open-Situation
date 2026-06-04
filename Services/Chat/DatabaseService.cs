using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lock.Models;
using Lock.Services.Admin;

namespace Lock.Chat.Services
{
    public static class DatabaseService
    {
        private static bool _isInitialized = false;

        public static Task InitializeAsync()
        {
            if (_isInitialized)
                return Task.CompletedTask;

            try
            {
                // Just verify Supabase connection is available
                // Your SupabaseService should already be configured
                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("DatabaseService initialized (using Supabase)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DatabaseService init error: {ex}");
                throw;
            }

            return Task.CompletedTask;
        }

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService methods directly. This is a compatibility shim.")]
        public static object GetConnection()
        {
            throw new InvalidOperationException(
                "Direct SQLite connection is no longer available. Use SupabaseService methods instead.");
        }

        // Legacy compatibility - redirect to SupabaseService
        public static async Task RecreateDatabaseAsync()
        {
            // This doesn't apply to Supabase
            await Task.CompletedTask;
            System.Diagnostics.Debug.WriteLine("RecreateDatabaseAsync called - no action needed for Supabase");
        }

        // Helper method to get tracking statistics using Supabase
        public static async Task<TrackingStats> GetTrackingStatsAsync()
        {
            var stats = new TrackingStats();
            try
            {
                // Get counts from Supabase
                var moodChanges = await SupabaseService.GetAsync<UserMoodTracking>("UserMoodTracking", "");
                var profileChanges = await SupabaseService.GetAsync<UserProfileTracking>("UserProfileTracking", "");
                var logins = await SupabaseService.GetAsync<UserLoginTracking>("UserLoginTracking", "");
                var postsTracked = await SupabaseService.GetAsync<PostTracking>("PostTracking", "");
                var groupActivities = await SupabaseService.GetAsync<GroupTracking>("GroupTracking", "");

                stats.TotalMoodChanges = moodChanges.Count;
                stats.TotalProfileChanges = profileChanges.Count;
                stats.TotalLogins = logins.Count;
                stats.TotalPostsTracked = postsTracked.Count;
                stats.TotalGroupActivities = groupActivities.Count;

                // Get today's stats
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                stats.TodayMoodChanges = moodChanges.Count(t => t.Timestamp >= today && t.Timestamp < tomorrow);
                stats.TodayProfileChanges = profileChanges.Count(t => t.Timestamp >= today && t.Timestamp < tomorrow);
                stats.TodayLogins = logins.Count(t => t.LoginTime >= today && t.LoginTime < tomorrow);
                stats.TodayGroupActivities = groupActivities.Count(t => t.Timestamp >= today && t.Timestamp < tomorrow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTrackingStatsAsync error: {ex}");
            }
            return stats;
        }

        // Helper method to clear old tracking data (keep last 30 days)
        public static async Task CleanupOldTrackingDataAsync(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

                // Get items to delete
                var oldMoodChanges = await SupabaseService.GetAsync<UserMoodTracking>("UserMoodTracking",
                    $"Timestamp=lt.{cutoffDate:yyyy-MM-ddTHH:mm:ssZ}");
                var oldProfileChanges = await SupabaseService.GetAsync<UserProfileTracking>("UserProfileTracking",
                    $"Timestamp=lt.{cutoffDate:yyyy-MM-ddTHH:mm:ssZ}");
                var oldPostTracking = await SupabaseService.GetAsync<PostTracking>("PostTracking",
                    $"Timestamp=lt.{cutoffDate:yyyy-MM-ddTHH:mm:ssZ}");
                var oldGroupTracking = await SupabaseService.GetAsync<GroupTracking>("GroupTracking",
                    $"Timestamp=lt.{cutoffDate:yyyy-MM-ddTHH:mm:ssZ}");

                // Keep login history longer (90 days)
                var loginCutoff = DateTime.UtcNow.AddDays(-90);
                var oldLogins = await SupabaseService.GetAsync<UserLoginTracking>("UserLoginTracking",
                    $"LoginTime=lt.{loginCutoff:yyyy-MM-ddTHH:mm:ssZ}");

                // Delete old records
                foreach (var item in oldMoodChanges)
                {
                    await SupabaseService.DeleteAsync("UserMoodTracking", $"Id=eq.{item.Id}");
                }
                foreach (var item in oldProfileChanges)
                {
                    await SupabaseService.DeleteAsync("UserProfileTracking", $"Id=eq.{item.Id}");
                }
                foreach (var item in oldPostTracking)
                {
                    await SupabaseService.DeleteAsync("PostTracking", $"Id=eq.{item.Id}");
                }
                foreach (var item in oldGroupTracking)
                {
                    await SupabaseService.DeleteAsync("GroupTracking", $"Id=eq.{item.Id}");
                }
                foreach (var item in oldLogins)
                {
                    await SupabaseService.DeleteAsync("UserLoginTracking", $"Id=eq.{item.Id}");
                }

                System.Diagnostics.Debug.WriteLine($"Cleaned up tracking data older than {daysToKeep} days");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanupOldTrackingDataAsync error: {ex}");
            }
        }
    }

    public class TableInfo
    {
        public int cid { get; set; }
        public string name { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public int notnull { get; set; }
        public string dflt_value { get; set; } = string.Empty;
        public int pk { get; set; }
    }

    public class TrackingStats
    {
        public int TotalMoodChanges { get; set; }
        public int TotalProfileChanges { get; set; }
        public int TotalLogins { get; set; }
        public int TotalPostsTracked { get; set; }
        public int TotalGroupActivities { get; set; }
        public int TodayMoodChanges { get; set; }
        public int TodayProfileChanges { get; set; }
        public int TodayLogins { get; set; }
        public int TodayGroupActivities { get; set; }

        public int TotalTrackedItems => TotalMoodChanges + TotalProfileChanges + TotalLogins + TotalPostsTracked + TotalGroupActivities;
        public int TodayTotal => TodayMoodChanges + TodayProfileChanges + TodayLogins + TodayGroupActivities;
    }
}