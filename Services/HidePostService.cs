using Lock.Chat.Services;
using Lock.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class HidePostService
    {
        private static SQLiteAsyncConnection _database;

        public static async Task InitializeAsync()
        {
            if (_database == null)
            {
                await DatabaseService.InitializeAsync();
                _database = DatabaseService.GetConnection();
            }
        }

        /// <summary>
        /// Hide a post for a specific user
        /// </summary>
        public static async Task<bool> HidePostAsync(int postId, string userPhone)
        {
            try
            {
                await InitializeAsync();

                Debug.WriteLine($"HidePostAsync called - PostId: {postId}, UserPhone: {userPhone}");

                var post = await _database.Table<Post>().FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                {
                    Debug.WriteLine($"Post with ID {postId} not found!");
                    return false;
                }

                Debug.WriteLine($"Found post: ID={post.Id}, Author={post.AuthorPhone}");
                Debug.WriteLine($"Current HiddenBy: {post.HiddenByJson}");

                var hiddenBy = post.HiddenBy;
                Debug.WriteLine($"HiddenBy list count: {hiddenBy.Count}");

                if (!hiddenBy.Contains(userPhone))
                {
                    hiddenBy.Add(userPhone);
                    post.HiddenBy = hiddenBy;
                    await _database.UpdateAsync(post);
                    Debug.WriteLine($"Post {postId} hidden by user {userPhone}");
                    Debug.WriteLine($"New HiddenBy: {post.HiddenByJson}");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"Post already hidden by user {userPhone}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error hiding post: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Get all hidden posts for a specific user
        /// </summary>
        public static async Task<List<Post>> GetHiddenPostsAsync(string userPhone)
        {
            try
            {
                await InitializeAsync();

                Debug.WriteLine($"GetHiddenPostsAsync called for user: {userPhone}");

                var allPosts = await _database.Table<Post>().ToListAsync();
                Debug.WriteLine($"Total posts in database: {allPosts.Count}");

                var hiddenPosts = allPosts.Where(p => p.IsHiddenByUser(userPhone)).ToList();
                Debug.WriteLine($"Hidden posts found: {hiddenPosts.Count}");

                foreach (var post in hiddenPosts)
                {
                    Debug.WriteLine($"  - Post ID: {post.Id}, Author: {post.AuthorPhone}, HiddenBy: {post.HiddenByJson}");
                }

                // Resolve author display names for hidden posts
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                foreach (var post in hiddenPosts)
                {
                    if (!string.IsNullOrEmpty(post.AuthorPhone))
                    {
                        var user = await db.Table<Models.User>()
                                           .Where(u => u.PhoneNumber == post.AuthorPhone)
                                           .FirstOrDefaultAsync();
                        if (user != null)
                        {
                            post.AuthorDisplayName = string.IsNullOrWhiteSpace(user.Name)
                                ? post.AuthorPhone
                                : user.Name;
                            post.AuthorProfileImagePath = user.ProfileImagePath ?? string.Empty;
                        }
                    }
                    post.UpdateDisplayContent(100);
                }

                return hiddenPosts.OrderByDescending(p => p.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting hidden posts: {ex}");
                return new List<Post>();
            }
        }

        /// <summary>
        /// Check if a specific post is hidden by a user
        /// </summary>
        public static async Task<bool> IsPostHiddenAsync(int postId, string userPhone)
        {
            try
            {
                await InitializeAsync();

                var post = await _database.Table<Post>().FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                    return false;

                bool isHidden = post.IsHiddenByUser(userPhone);
                Debug.WriteLine($"IsPostHiddenAsync - PostId: {postId}, User: {userPhone}, IsHidden: {isHidden}");
                return isHidden;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if post is hidden: {ex}");
                return false;
            }
        }
       

        /// <summary>
        /// Get all hidden post IDs for a user
        /// </summary>
        public static async Task<List<int>> GetHiddenPostIdsAsync(string userPhone)
        {
            try
            {
                await InitializeAsync();

                var allPosts = await _database.Table<Post>().ToListAsync();
                return allPosts.Where(p => p.IsHiddenByUser(userPhone)).Select(p => p.Id).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting hidden post IDs: {ex}");
                return new List<int>();
            }
        }

        /// <summary>
        /// Unhide a post
        /// </summary>
        public static async Task<bool> UnhidePostAsync(int postId, string userPhone)
        {
            try
            {
                await InitializeAsync();

                var post = await _database.Table<Post>().FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                    return false;

                var hiddenBy = post.HiddenBy;
                if (hiddenBy.Contains(userPhone))
                {
                    hiddenBy.Remove(userPhone);
                    post.HiddenBy = hiddenBy;
                    await _database.UpdateAsync(post);
                    System.Diagnostics.Debug.WriteLine($"Post {postId} unhidden by user {userPhone}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unhiding post: {ex}");
                return false;
            }
        }

    
    }
}