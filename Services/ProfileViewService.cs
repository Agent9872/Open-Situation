using Lock.Chat.Services;
using Lock.Models;
using SQLite;
using System;
using System.Collections.Generic;
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

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Check if already viewed in last 24 hours
                var existingView = await db.Table<ProfileView>()
                    .Where(v => v.ViewerUserId == viewerId && v.ViewedUserId == viewedId && v.ViewedAt > DateTime.UtcNow.AddHours(-24))
                    .FirstOrDefaultAsync();

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
                    await db.InsertAsync(view);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecordProfileViewAsync error: {ex}");
            }
        }

        public static async Task<int> GetProfileViewCountAsync(int userId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                return await db.Table<ProfileView>()
                    .Where(v => v.ViewedUserId == userId)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProfileViewCountAsync error: {ex}");
                return 0;
            }
        }

        public static async Task<List<ProfileView>> GetRecentProfileViewsAsync(int userId, int limit = 10)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                return await db.Table<ProfileView>()
                    .Where(v => v.ViewedUserId == userId)
                    .OrderByDescending(v => v.ViewedAt)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetRecentProfileViewsAsync error: {ex}");
                return new List<ProfileView>();
            }
        }

        public static async Task MarkViewsAsSeenAsync(int userId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var views = await db.Table<ProfileView>()
                    .Where(v => v.ViewedUserId == userId && v.IsNew)
                    .ToListAsync();

                foreach (var view in views)
                {
                    view.IsNew = false;
                    await db.UpdateAsync(view);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarkViewsAsSeenAsync error: {ex}");
            }
        }
    }
}