using Microsoft.Maui.Storage;
using SQLite;
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
        private static SQLiteAsyncConnection? _db;
        private static bool _isInitialized = false;

        public static async Task InitializeAsync()
        {
            if (_isInitialized && _db != null)
            {
                return;
            }

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lock.db3");
            _db = new SQLiteAsyncConnection(dbPath);

            // Create tables
            await _db.CreateTableAsync<Models.User>();
            await _db.CreateTableAsync<Models.Post>();
            await _db.CreateTableAsync<Models.Chat.Conversation>();
            await _db.CreateTableAsync<Models.Chat.ChatMessage>();
            await _db.CreateTableAsync<Models.Chat.MessageRequest>();
            await _db.CreateTableAsync<Models.BlockedUser>();
            await _db.CreateTableAsync<UserPhoto>();
            await _db.CreateTableAsync<UserPrompt>();
            await _db.CreateTableAsync<DateIdea>();
            await _db.CreateTableAsync<UserEvent>();
            await _db.CreateTableAsync<EventAttendance>();
            await _db.CreateTableAsync<UserBlock>();
            await _db.CreateTableAsync<Models.SeenPost>();
            await _db.CreateTableAsync<Models.EmergencyContact>();
            await _db.CreateTableAsync<LiveSession>();

            // Add Spark tables
            await _db.CreateTableAsync<SparkRateLimit>();
            await _db.CreateTableAsync<SparkTransaction>();

            // ========== ADD ADMIN TRACKING TABLES ==========
            await _db.CreateTableAsync<UserMoodTracking>();
            await _db.CreateTableAsync<UserProfileTracking>();
            await _db.CreateTableAsync<UserLoginTracking>();
            await _db.CreateTableAsync<PostTracking>();
            await _db.CreateTableAsync<GroupTracking>();

            // Run migrations to add new columns
            await DatabaseMigration.EnsureDatabaseSchemaAsync(_db);
            await AddMatchTypeNotificationsColumnIfNeeded();
            await AddHiddenByJsonColumnIfNeeded();
            await AddDeniedPagesColumnIfNeeded();   // ? NEW
            await AddTrackingIndexes();

            _isInitialized = true;
        }

        // ?? NEW: Add DeniedPages column to Users table ????????????????????????
        private static async Task AddDeniedPagesColumnIfNeeded()
        {
            try
            {
                await _db!.ExecuteScalarAsync<string>(
                    "SELECT DeniedPages FROM Users LIMIT 1");
                System.Diagnostics.Debug.WriteLine("DeniedPages column already exists");
            }
            catch (SQLiteException)
            {
                await _db!.ExecuteAsync(
                    "ALTER TABLE Users ADD COLUMN DeniedPages TEXT DEFAULT ''");
                System.Diagnostics.Debug.WriteLine("Added DeniedPages column to Users table");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error checking DeniedPages column: {ex.Message}");
            }
        }

        // Add indexes for better query performance on tracking tables
        private static async Task AddTrackingIndexes()
        {
            try
            {
                // Add indexes for UserMoodTracking
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_mood_userphone ON UserMoodTracking(UserPhone)");
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_mood_timestamp ON UserMoodTracking(Timestamp DESC)");

                // Add indexes for UserProfileTracking
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_profile_userphone ON UserProfileTracking(UserPhone)");
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_profile_timestamp ON UserProfileTracking(Timestamp DESC)");

                // Add indexes for UserLoginTracking
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_login_userphone ON UserLoginTracking(UserPhone)");
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_login_timestamp ON UserLoginTracking(LoginTime DESC)");

                // Add indexes for PostTracking
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_post_author ON PostTracking(AuthorPhone)");
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_post_timestamp ON PostTracking(Timestamp DESC)");

                // Add indexes for GroupTracking
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_group_groupid ON GroupTracking(GroupId)");
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_group_timestamp ON GroupTracking(Timestamp DESC)");
                await _db!.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_group_actor ON GroupTracking(ActorPhone)");

                System.Diagnostics.Debug.WriteLine("All tracking indexes created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating tracking indexes: {ex.Message}");
            }
        }

        // Method to add MatchTypeNotifications column if needed
        private static async Task AddMatchTypeNotificationsColumnIfNeeded()
        {
            try
            {
                await _db!.ExecuteScalarAsync<string>("SELECT MatchTypeNotificationsJson FROM Conversations LIMIT 1");
                System.Diagnostics.Debug.WriteLine("MatchTypeNotificationsJson column already exists");
            }
            catch (SQLiteException)
            {
                await _db!.ExecuteAsync("ALTER TABLE Conversations ADD COLUMN MatchTypeNotificationsJson TEXT NOT NULL DEFAULT '{}'");
                System.Diagnostics.Debug.WriteLine("Added MatchTypeNotificationsJson column to Conversations table");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking MatchTypeNotificationsJson column: {ex}");
            }
        }

        private static async Task AddHiddenByJsonColumnIfNeeded()
        {
            try
            {
                var connection = GetConnection();
                var tableInfo = await connection.QueryAsync<TableInfo>("PRAGMA table_info(Posts)");
                var columnExists = tableInfo.Any(c => c.name == "HiddenByJson");

                if (!columnExists)
                {
                    await connection.ExecuteAsync("ALTER TABLE Posts ADD COLUMN HiddenByJson TEXT DEFAULT '[]'");
                    System.Diagnostics.Debug.WriteLine("Added HiddenByJson column to Posts table");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("HiddenByJson column already exists");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding HiddenByJson column: {ex}");
            }
        }

        public static SQLiteAsyncConnection GetConnection()
        {
            if (_db == null)
            {
                throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");
            }
            return _db;
        }

        public static async Task RecreateDatabaseAsync()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "lock.db3");
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            _db = null;
            _isInitialized = false;
            await InitializeAsync();
        }

        // Helper method to get tracking statistics
        public static async Task<TrackingStats> GetTrackingStatsAsync()
        {
            var stats = new TrackingStats();
            try
            {
                var db = GetConnection();

                stats.TotalMoodChanges = await db.Table<UserMoodTracking>().CountAsync();
                stats.TotalProfileChanges = await db.Table<UserProfileTracking>().CountAsync();
                stats.TotalLogins = await db.Table<UserLoginTracking>().CountAsync();
                stats.TotalPostsTracked = await db.Table<PostTracking>().CountAsync();
                stats.TotalGroupActivities = await db.Table<GroupTracking>().CountAsync();

                // Get today's stats
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                stats.TodayMoodChanges = await db.Table<UserMoodTracking>()
                    .Where(t => t.Timestamp >= today && t.Timestamp < tomorrow)
                    .CountAsync();

                stats.TodayProfileChanges = await db.Table<UserProfileTracking>()
                    .Where(t => t.Timestamp >= today && t.Timestamp < tomorrow)
                    .CountAsync();

                stats.TodayLogins = await db.Table<UserLoginTracking>()
                    .Where(t => t.LoginTime >= today && t.LoginTime < tomorrow)
                    .CountAsync();

                stats.TodayGroupActivities = await db.Table<GroupTracking>()
                    .Where(t => t.Timestamp >= today && t.Timestamp < tomorrow)
                    .CountAsync();
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
                var db = GetConnection();
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

                var oldMoodChanges = await db.Table<UserMoodTracking>()
                    .Where(t => t.Timestamp < cutoffDate)
                    .ToListAsync();

                var oldProfileChanges = await db.Table<UserProfileTracking>()
                    .Where(t => t.Timestamp < cutoffDate)
                    .ToListAsync();

                var oldPostTracking = await db.Table<PostTracking>()
                    .Where(t => t.Timestamp < cutoffDate)
                    .ToListAsync();

                var oldGroupTracking = await db.Table<GroupTracking>()
                    .Where(t => t.Timestamp < cutoffDate)
                    .ToListAsync();

                // Keep login history longer (90 days)
                var loginCutoff = DateTime.UtcNow.AddDays(-90);
                var oldLogins = await db.Table<UserLoginTracking>()
                    .Where(t => t.LoginTime < loginCutoff)
                    .ToListAsync();

                foreach (var item in oldMoodChanges) await db.DeleteAsync(item);
                foreach (var item in oldProfileChanges) await db.DeleteAsync(item);
                foreach (var item in oldPostTracking) await db.DeleteAsync(item);
                foreach (var item in oldGroupTracking) await db.DeleteAsync(item);
                foreach (var item in oldLogins) await db.DeleteAsync(item);

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
        public string name { get; set; }
        public string type { get; set; }
        public int notnull { get; set; }
        public string dflt_value { get; set; }
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