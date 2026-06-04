using Lock.Chat.Services;
using Lock.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class SparkService
    {
        private const int MAX_SPARKS_PER_HOUR = 5;

        /// <summary>
        /// Check if user can send a spark (within rate limit)
        /// </summary>
        public static async Task<(bool CanSpark, int Remaining, int WaitMinutes)> CanSendSparkAsync(string userPhone)
        {
            try
            {
                // Get rate limit from Supabase
                var rateLimits = await SupabaseService.GetAsync<SparkRateLimit>("SparkRateLimits",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&limit=1");
                var rateLimit = rateLimits.FirstOrDefault();

                var now = DateTime.UtcNow;

                if (rateLimit == null)
                {
                    // First time user - can send up to MAX_SPARKS_PER_HOUR
                    return (true, MAX_SPARKS_PER_HOUR, 0);
                }

                // Get timestamps of sparks in the last hour
                var timestamps = GetSparkTimestamps(rateLimit);
                var recentSparks = timestamps.Where(ts => (now - ts).TotalHours < 1).ToList();

                int sparksUsed = recentSparks.Count;
                int remaining = MAX_SPARKS_PER_HOUR - sparksUsed;

                if (remaining > 0)
                {
                    return (true, remaining, 0);
                }

                // Calculate wait time until next available spark
                var oldestRecent = recentSparks.OrderBy(ts => ts).FirstOrDefault();
                var minutesToWait = 60 - (now - oldestRecent).TotalMinutes;
                int waitMinutes = Math.Max(1, (int)Math.Ceiling(minutesToWait));

                return (false, 0, waitMinutes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CanSendSparkAsync error: {ex}");
                return (true, MAX_SPARKS_PER_HOUR, 0); // Default to allow on error
            }
        }

        /// <summary>
        /// Record a spark send
        /// </summary>
        public static async Task<bool> RecordSparkAsync(string userPhone, int postId, string postAuthorPhone)
        {
            try
            {
                // First check if user can send spark
                var (canSpark, remaining, waitMinutes) = await CanSendSparkAsync(userPhone);

                if (!canSpark)
                {
                    Debug.WriteLine($"User {userPhone} cannot send spark. Wait {waitMinutes} minutes.");
                    return false;
                }

                // Check if user already sparked this specific post
                var existingSparks = await SupabaseService.GetAsync<SparkTransaction>("SparkTransactions",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&PostId=eq.{postId}&limit=1");
                var existingSpark = existingSparks.FirstOrDefault();

                if (existingSpark != null)
                {
                    Debug.WriteLine($"User {userPhone} already sparked post {postId}");
                    return false;
                }

                // Update or create rate limit record
                var rateLimits = await SupabaseService.GetAsync<SparkRateLimit>("SparkRateLimits",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&limit=1");
                var rateLimit = rateLimits.FirstOrDefault();

                var now = DateTime.UtcNow;
                var timestamps = new List<DateTime>();

                if (rateLimit == null)
                {
                    rateLimit = new SparkRateLimit
                    {
                        UserPhone = userPhone,
                        SparkCount = 1,
                        HourStartTime = now,
                        SparkTimestampsJson = JsonSerializer.Serialize(new List<DateTime> { now })
                    };
                    await SupabaseService.InsertAsync("SparkRateLimits", rateLimit);
                }
                else
                {
                    timestamps = GetSparkTimestamps(rateLimit);
                    timestamps.Add(now);

                    // Remove timestamps older than 1 hour
                    timestamps = timestamps.Where(ts => (now - ts).TotalHours < 1).ToList();

                    rateLimit.SparkCount = timestamps.Count;
                    rateLimit.SparkTimestampsJson = JsonSerializer.Serialize(timestamps);
                    rateLimit.HourStartTime = timestamps.FirstOrDefault();

                    await SupabaseService.UpdateAsync("SparkRateLimits", $"Id=eq.{rateLimit.Id}",
                        new
                        {
                            SparkCount = rateLimit.SparkCount,
                            SparkTimestampsJson = rateLimit.SparkTimestampsJson,
                            HourStartTime = rateLimit.HourStartTime
                        });
                }

                // Record transaction
                var transaction = new SparkTransaction
                {
                    UserPhone = userPhone,
                    PostId = postId,
                    PostAuthorPhone = postAuthorPhone,
                    SparkedAt = now
                };
                await SupabaseService.InsertAsync("SparkTransactions", transaction);

                // Toggle spark on the post
                await ToggleSparkOnPostAsync(postId, userPhone);

                Debug.WriteLine($"Spark recorded: {userPhone} sparked post {postId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RecordSparkAsync error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Remove a spark (when user un-sparks)
        /// </summary>
        public static async Task<bool> RemoveSparkAsync(string userPhone, int postId)
        {
            try
            {
                // Find and delete the transaction
                var transactions = await SupabaseService.GetAsync<SparkTransaction>("SparkTransactions",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&PostId=eq.{postId}&limit=1");
                var transaction = transactions.FirstOrDefault();

                if (transaction != null)
                {
                    await SupabaseService.DeleteAsync("SparkTransactions", $"Id=eq.{transaction.Id}");
                }

                // Update rate limit (remove one from count)
                var rateLimits = await SupabaseService.GetAsync<SparkRateLimit>("SparkRateLimits",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&limit=1");
                var rateLimit = rateLimits.FirstOrDefault();

                if (rateLimit != null)
                {
                    var timestamps = GetSparkTimestamps(rateLimit);
                    var now = DateTime.UtcNow;

                    // Remove the timestamp for this spark (approximate - remove oldest if multiple)
                    if (timestamps.Any())
                    {
                        timestamps.RemoveAt(timestamps.Count - 1);
                    }

                    rateLimit.SparkCount = timestamps.Count;
                    rateLimit.SparkTimestampsJson = JsonSerializer.Serialize(timestamps);

                    await SupabaseService.UpdateAsync("SparkRateLimits", $"Id=eq.{rateLimit.Id}",
                        new
                        {
                            SparkCount = rateLimit.SparkCount,
                            SparkTimestampsJson = rateLimit.SparkTimestampsJson
                        });
                }

                // Toggle spark off on the post
                await ToggleSparkOnPostAsync(postId, userPhone, remove: true);

                Debug.WriteLine($"Spark removed: {userPhone} unsparked post {postId}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemoveSparkAsync error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Helper method to toggle spark on a post
        /// </summary>
        private static async Task ToggleSparkOnPostAsync(int postId, string userPhone, bool remove = false)
        {
            try
            {
                // Get the post
                var posts = await SupabaseService.GetAsync<Post>("Posts", $"Id=eq.{postId}&limit=1");
                var post = posts.FirstOrDefault();
                if (post == null) return;

                var sparkedBy = post.SparkedBy;

                if (remove)
                {
                    if (sparkedBy.Contains(userPhone))
                    {
                        sparkedBy.Remove(userPhone);
                    }
                }
                else
                {
                    if (!sparkedBy.Contains(userPhone))
                    {
                        sparkedBy.Add(userPhone);
                    }
                }

                post.SparkedBy = sparkedBy;

                // Update the post
                await SupabaseService.UpdateAsync("Posts", $"Id=eq.{postId}",
                    new { SparkedByJson = post.SparkedByJson, SparkCount = post.SparkCount });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToggleSparkOnPostAsync error: {ex}");
            }
        }

        /// <summary>
        /// Get remaining sparks for user in current hour
        /// </summary>
        public static async Task<int> GetRemainingSparksAsync(string userPhone)
        {
            try
            {
                var (_, remaining, _) = await CanSendSparkAsync(userPhone);
                return remaining;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRemainingSparksAsync error: {ex}");
                return MAX_SPARKS_PER_HOUR;
            }
        }

        /// <summary>
        /// Get spark usage stats for user
        /// </summary>
        public static async Task<(int UsedThisHour, int MaxPerHour, int TotalSparksSent)> GetSparkStatsAsync(string userPhone)
        {
            try
            {
                // Get rate limit
                var rateLimits = await SupabaseService.GetAsync<SparkRateLimit>("SparkRateLimits",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&limit=1");
                var rateLimit = rateLimits.FirstOrDefault();

                // Count total transactions
                var transactions = await SupabaseService.GetAsync<SparkTransaction>("SparkTransactions",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}");
                int totalTransactions = transactions.Count;

                int usedThisHour = 0;
                if (rateLimit != null)
                {
                    var timestamps = GetSparkTimestamps(rateLimit);
                    var now = DateTime.UtcNow;
                    usedThisHour = timestamps.Count(ts => (now - ts).TotalHours < 1);
                }

                return (usedThisHour, MAX_SPARKS_PER_HOUR, totalTransactions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetSparkStatsAsync error: {ex}");
                return (0, MAX_SPARKS_PER_HOUR, 0);
            }
        }

        private static List<DateTime> GetSparkTimestamps(SparkRateLimit rateLimit)
        {
            try
            {
                if (string.IsNullOrEmpty(rateLimit.SparkTimestampsJson))
                    return new List<DateTime>();

                return JsonSerializer.Deserialize<List<DateTime>>(rateLimit.SparkTimestampsJson) ?? new List<DateTime>();
            }
            catch
            {
                return new List<DateTime>();
            }
        }
    }
}