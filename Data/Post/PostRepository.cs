using Lock.Chat.Services;
using Lock.Helpers;
using Lock.Models;
using Lock.Models.Chat;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class PostRepository
    {
        public static async Task<List<Lock.Models.Post>> GetAllAsync()
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<Lock.Models.Post>().OrderByDescending(p => p.CreatedAt).ToListAsync();
        }

        public static async Task<List<Lock.Models.Post>> GetByAuthorAsync(string authorPhone)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<Lock.Models.Post>()
                           .Where(p => p.AuthorPhone == (authorPhone ?? string.Empty))
                           .OrderByDescending(p => p.CreatedAt)
                           .ToListAsync();
        }

        public static async Task<Lock.Models.Post?> GetByIdAsync(int id)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<Lock.Models.Post>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        public static async Task InsertAsync(Lock.Models.Post post)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            await db.InsertAsync(post);
        }

        public static async Task UpdateAsync(Lock.Models.Post post)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            await db.UpdateAsync(post);
        }

        public static async Task DeleteAsync(int id)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            await db.DeleteAsync<Lock.Models.Post>(id);
        }

        /// <summary>
        /// Get unread posts for a user based on their notification preferences
        /// </summary>
        public static async Task<Dictionary<string, int>> GetUnreadPostsCountAsync(
            string currentUserPhone,
            Dictionary<string, bool> notificationPreferences)
        {
            var result = new Dictionary<string, int>();

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get all posts
                var allPosts = await db.Table<Lock.Models.Post>().ToListAsync();

                // Get all seen posts by current user
                var seenPosts = await db.Table<SeenPost>()
                    .Where(s => s.UserPhone == currentUserPhone)
                    .ToListAsync();

                // Get all conversations for current user
                var conversations = await db.Table<Conversation>()
                    .Where(c => c.ParticipantA == currentUserPhone || c.ParticipantB == currentUserPhone)
                    .ToListAsync();

                // Get notification preferences for this user (only enabled moods)
                var enabledMoods = notificationPreferences
                    .Where(kvp => kvp.Value) // Only enabled ones
                    .Select(kvp => kvp.Key)
                    .ToList();

                Debug.WriteLine($"=== POST REPOSITORY FILTERING ===");
                Debug.WriteLine($"Current user: {currentUserPhone}");
                Debug.WriteLine($"Enabled mood keys: {string.Join(", ", enabledMoods)}");

                // CRITICAL: If no moods are enabled, return empty result immediately
                if (!enabledMoods.Any())
                {
                    Debug.WriteLine("No moods enabled - returning empty result (no badges)");
                    return new Dictionary<string, int>();
                }

                Debug.WriteLine($"Total posts in DB: {allPosts.Count}");
                Debug.WriteLine($"Total seen posts: {seenPosts.Count}");

                // For each conversation partner, count unread posts
                foreach (var conv in conversations)
                {
                    var otherPhone = conv.ParticipantA == currentUserPhone
                        ? conv.ParticipantB
                        : conv.ParticipantA;

                    if (otherPhone == currentUserPhone) continue;

                    // Get posts by this author
                    var authorPosts = allPosts
                        .Where(p => p.AuthorPhone == otherPhone)
                        .ToList();

                    if (!authorPosts.Any()) continue;

                    Debug.WriteLine($"\nAuthor {otherPhone} has {authorPosts.Count} total posts");

                    // Filter posts where the mapped mood key is in enabledMoods
                    var relevantPosts = new List<Lock.Models.Post>();

                    foreach (var post in authorPosts)
                    {
                        if (string.IsNullOrEmpty(post.Mood))
                        {
                            Debug.WriteLine($"  Post {post.Id}: No mood set");
                            continue;
                        }

                        // Map the display mood to internal key using the helper
                        string moodKey = MoodMapping.MapDisplayToKey(post.Mood);
                        bool isEnabled = enabledMoods.Contains(moodKey);

                        Debug.WriteLine($"  Post {post.Id}: Display='{post.Mood}' -> Key='{moodKey}' -> Enabled={isEnabled}");

                        if (isEnabled)
                        {
                            relevantPosts.Add(post);
                        }
                    }

                    Debug.WriteLine($"  Posts with enabled moods: {relevantPosts.Count}");

                    // Filter out posts that have been seen
                    var unreadPosts = relevantPosts
                        .Where(p => !seenPosts.Any(s => s.PostId == p.Id))
                        .ToList();

                    if (unreadPosts.Any())
                    {
                        result[otherPhone] = unreadPosts.Count;
                        Debug.WriteLine($"  UNREAD: {unreadPosts.Count} posts");
                    }
                }

                Debug.WriteLine($"\nFinal result: {result.Count} users have unread posts");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetUnreadPostsCountAsync: {ex}");
            }

            return result;
        }

        // Add these methods to PostRepository.cs

        public static async Task ToggleSparkAsync(int postId, string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var post = await db.Table<Post>().Where(p => p.Id == postId).FirstOrDefaultAsync();
                if (post == null) return;

                var sparkedBy = post.SparkedBy;

                if (sparkedBy.Contains(userPhone))
                    sparkedBy.Remove(userPhone);
                else
                    sparkedBy.Add(userPhone);

                post.SparkedBy = sparkedBy;

                await db.UpdateAsync(post);

                Debug.WriteLine($"Toggled spark for post {postId}: {sparkedBy.Count} sparks");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling spark: {ex}");
            }
        }

        public static async Task<bool> HasUserSparkedPostAsync(int postId, string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var post = await db.Table<Post>().Where(p => p.Id == postId).FirstOrDefaultAsync();
                if (post == null) return false;

                return post.SparkedBy.Contains(userPhone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if user sparked post: {ex}");
                return false;
            }
        }
        // Add to PostRepository.cs

        public static async Task ToggleLoveAsync(int postId, string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var post = await db.Table<Post>().Where(p => p.Id == postId).FirstOrDefaultAsync();
                if (post == null) return;

                var lovedBy = post.LovedBy;

                if (lovedBy.Contains(userPhone))
                    lovedBy.Remove(userPhone);
                else
                    lovedBy.Add(userPhone);

                post.LovedBy = lovedBy; // This updates LoveCount and LovedByJson

                await db.UpdateAsync(post);

                Debug.WriteLine($"Toggled love for post {postId}: {lovedBy.Count} loves");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling love: {ex}");
            }
        }
        public static async Task<bool> HasUserLovedPostAsync(int postId, string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var post = await db.Table<Post>().Where(p => p.Id == postId).FirstOrDefaultAsync();
                if (post == null) return false;

                return post.LovedBy.Contains(userPhone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if user loved post: {ex}");
                return false;
            }
        }

        public static async Task<int> GetLoveCountAsync(int postId)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var post = await db.Table<Post>().Where(p => p.Id == postId).FirstOrDefaultAsync();
                return post?.LoveCount ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting love count: {ex}");
                return 0;
            }
        }

        /// <summary>
        /// Mark posts as seen when user opens a conversation
        /// </summary>
        public static async Task MarkPostsAsSeenAsync(string userPhone, string authorPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get all posts by this author
                var posts = await db.Table<Lock.Models.Post>()
                    .Where(p => p.AuthorPhone == authorPhone)
                    .ToListAsync();

                // Get already seen posts
                var seenPosts = await db.Table<SeenPost>()
                    .Where(s => s.UserPhone == userPhone && s.AuthorPhone == authorPhone)
                    .ToListAsync();

                // Mark each post as seen if not already seen
                foreach (var post in posts)
                {
                    if (!seenPosts.Any(s => s.PostId == post.Id))
                    {
                        await db.InsertAsync(new SeenPost
                        {
                            UserPhone = userPhone,
                            AuthorPhone = authorPhone,
                            PostId = post.Id,
                            SeenAt = DateTime.UtcNow
                        });
                    }
                }

                Debug.WriteLine($"Marked posts from {authorPhone} as seen for user {userPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MarkPostsAsSeenAsync: {ex}");
            }
        }
    }
}