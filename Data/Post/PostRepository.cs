using Lock.Helpers;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class PostRepository
    {
        public static async Task<List<Post>> GetAllAsync()
        {
            try
            {
                return await SupabaseService.GetAsync<Post>("Posts", "order=CreatedAt.desc");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllAsync error: {ex}");
                return new List<Post>();
            }
        }

        public static async Task<List<Post>> GetByAuthorAsync(string authorPhone)
        {
            try
            {
                return await SupabaseService.GetAsync<Post>("Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(authorPhone ?? string.Empty)}&order=CreatedAt.desc");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetByAuthorAsync error: {ex}");
                return new List<Post>();
            }
        }

        public static async Task<Post?> GetByIdAsync(int id)
        {
            try
            {
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{id}&limit=1");
                return posts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetByIdAsync error: {ex}");
                return null;
            }
        }

        public static async Task<Post?> InsertAsync(Post post)
        {
            try
            {
                var payload = new
                {
                    AuthorPhone = post.AuthorPhone,
                    Content = post.Content,
                    Category = post.Category,
                    Visibility = post.Visibility,
                    ImagePathsJson = post.ImagePathsJson,
                    Mood = post.Mood,
                    StatusImagePath = post.StatusImagePath,
                    LoveCount = post.LoveCount,
                    LovedByJson = post.LovedByJson,
                    SparkCount = post.SparkCount,
                    SparkedByJson = post.SparkedByJson,
                    HiddenByJson = post.HiddenByJson,
                    CreatedAt = post.CreatedAt
                };

                return await SupabaseService.InsertPayloadAndReturnAsync<Post>("Posts", payload);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InsertAsync error: {ex}");
                return null;
            }
        }

        public static async Task<bool> UpdateAsync(Post post)
        {
            try
            {
                return await SupabaseService.UpdateAsync("Posts", $"Id=eq.{post.Id}", post);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> DeleteAsync(int id)
        {
            try
            {
                return await SupabaseService.DeleteAsync("Posts", $"Id=eq.{id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteAsync error: {ex}");
                return false;
            }
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
                // Get all posts
                var allPosts = await SupabaseService.GetAsync<Post>("Posts", "");

                // Get all seen posts by current user
                var seenPosts = await SupabaseService.GetAsync<SeenPost>("SeenPosts",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}");

                // Get all conversations for current user
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(ParticipantA.eq.{Uri.EscapeDataString(currentUserPhone)},ParticipantB.eq.{Uri.EscapeDataString(currentUserPhone)})");

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
                    var relevantPosts = new List<Post>();

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

        public static async Task<bool> ToggleSparkAsync(int postId, string userPhone)
        {
            try
            {
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{postId}&limit=1");
                var post = posts.FirstOrDefault();
                if (post == null) return false;

                var sparkedBy = post.SparkedBy;

                if (sparkedBy.Contains(userPhone))
                    sparkedBy.Remove(userPhone);
                else
                    sparkedBy.Add(userPhone);

                post.SparkedBy = sparkedBy;

                var success = await SupabaseService.UpdateAsync("Posts", $"Id=eq.{postId}",
                    new { SparkedByJson = post.SparkedByJson, SparkCount = post.SparkCount });

                Debug.WriteLine($"Toggled spark for post {postId}: {sparkedBy.Count} sparks");
                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling spark: {ex}");
                return false;
            }
        }

        public static async Task<bool> HasUserSparkedPostAsync(int postId, string userPhone)
        {
            try
            {
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{postId}&limit=1");
                var post = posts.FirstOrDefault();
                if (post == null) return false;

                return post.SparkedBy.Contains(userPhone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if user sparked post: {ex}");
                return false;
            }
        }

        public static async Task<bool> ToggleLoveAsync(int postId, string userPhone)
        {
            try
            {
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{postId}&limit=1");
                var post = posts.FirstOrDefault();
                if (post == null) return false;

                var lovedBy = post.LovedBy;

                if (lovedBy.Contains(userPhone))
                    lovedBy.Remove(userPhone);
                else
                    lovedBy.Add(userPhone);

                post.LovedBy = lovedBy;

                return await SupabaseService.UpdateAsync("Posts", $"Id=eq.{postId}",
                    new { LovedByJson = post.LovedByJson, LoveCount = lovedBy.Count });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling love: {ex}");
                return false;
            }
        }
        public static async Task<bool> HasUserLovedPostAsync(int postId, string userPhone)
        {
            try
            {
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{postId}&limit=1");
                var post = posts.FirstOrDefault();
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
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{postId}&limit=1");
                var post = posts.FirstOrDefault();
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
        public static async Task<bool> MarkPostsAsSeenAsync(string userPhone, string authorPhone)
        {
            try
            {
                // Get all posts by this author
                var posts = await SupabaseService.GetAsync<Post>("Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(authorPhone)}");

                // Get already seen posts
                var seenPosts = await SupabaseService.GetAsync<SeenPost>("SeenPosts",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&AuthorPhone=eq.{Uri.EscapeDataString(authorPhone)}");

                var seenPostIds = seenPosts.Select(s => s.PostId).ToHashSet();

                // Mark each post as seen if not already seen
                foreach (var post in posts)
                {
                    if (!seenPostIds.Contains(post.Id))
                    {
                        var seenPost = new SeenPost
                        {
                            UserPhone = userPhone,
                            AuthorPhone = authorPhone,
                            PostId = post.Id,
                            SeenAt = DateTime.UtcNow
                        };
                        await SupabaseService.InsertAsync("SeenPosts", seenPost);
                    }
                }

                Debug.WriteLine($"Marked posts from {authorPhone} as seen for user {userPhone}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MarkPostsAsSeenAsync: {ex}");
                return false;
            }
        }
    }
}