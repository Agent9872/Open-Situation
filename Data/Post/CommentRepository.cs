using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Lock.Models;
using Lock.Chat.Services;
using SQLite;

namespace Lock.Data.Post
{
    public static class CommentRepository
    {
        public static async Task InitializeAsync()
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Create Comments table if it doesn't exist (with new columns)
            await db.CreateTableAsync<Comment>();

            // Add new columns if they don't exist (for existing databases)
            try
            {
                await db.ExecuteAsync("ALTER TABLE Comments ADD COLUMN ParentCommentId INTEGER");
            }
            catch { /* Column may already exist */ }

            try
            {
                await db.ExecuteAsync("ALTER TABLE Comments ADD COLUMN LoveCount INTEGER DEFAULT 0");
                await db.ExecuteAsync("ALTER TABLE Comments ADD COLUMN LovedByJson TEXT DEFAULT '[]'");
            }
            catch { /* Columns may already exist */ }

            Debug.WriteLine("Comments table initialized with love reactions and nesting");
        }

        public static async Task<List<Comment>> GetCommentsForPostAsync(int postId, string currentUserPhone = "")
        {
            try
            {
                await InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get all comments for this post (including nested)
                var allComments = await db.Table<Comment>()
                    .Where(c => c.PostId == postId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                // Set love status AND ownership for current user
                if (!string.IsNullOrEmpty(currentUserPhone))
                {
                    foreach (var comment in allComments)
                    {
                        comment.IsLovedByCurrentUser = comment.LovedBy.Contains(currentUserPhone);
                        // CRITICAL: Set ownership flag
                        comment.IsOwnedByCurrentUser = comment.AuthorPhone == currentUserPhone;

                        // Debug output to verify
                        Debug.WriteLine($"Comment {comment.Id}: Author={comment.AuthorPhone}, Current={currentUserPhone}, IsOwned={comment.IsOwnedByCurrentUser}");
                    }
                }

                // Resolve author info
                await ResolveAuthorInfoAsync(allComments);

                // Organize into hierarchy
                var topLevelComments = allComments
                    .Where(c => !c.ParentCommentId.HasValue)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();

                foreach (var comment in topLevelComments)
                {
                    comment.Replies = allComments
                        .Where(c => c.ParentCommentId == comment.Id)
                        .OrderBy(c => c.CreatedAt)
                        .ToList();

                    // Set ownership for replies too
                    if (!string.IsNullOrEmpty(currentUserPhone))
                    {
                        foreach (var reply in comment.Replies)
                        {
                            reply.IsOwnedByCurrentUser = reply.AuthorPhone == currentUserPhone;
                        }
                    }
                }

                return topLevelComments;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting comments: {ex}");
                return new List<Comment>();
            }
        }


        // Make sure this method exists in CommentRepository.cs
        public static async Task UpdateCommentAsync(int commentId, string newContent)
        {
            try
            {
                await InitializeAsync();
                var db = DatabaseService.GetConnection();

                var comment = await db.Table<Comment>()
                    .Where(c => c.Id == commentId)
                    .FirstOrDefaultAsync();

                if (comment != null)
                {
                    comment.Content = newContent;
                    await db.UpdateAsync(comment);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating comment: {ex}");
                throw;
            }
        }
        public static async Task<Comment> AddCommentAsync(int postId, string authorPhone, string content, int? parentCommentId = null)
        {
            try
            {
                await InitializeAsync();
                var db = DatabaseService.GetConnection();

                var comment = new Comment
                {
                    PostId = postId,
                    ParentCommentId = parentCommentId,
                    AuthorPhone = authorPhone,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    LovedByJson = "[]",
                    LoveCount = 0
                };

                await db.InsertAsync(comment);

                // Resolve author info before returning
                await ResolveAuthorInfoAsync(new List<Comment> { comment });

                // Set ownership for the new comment
                comment.IsOwnedByCurrentUser = true;

                return comment;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding comment: {ex}");
                throw;
            }
        }
        public static async Task ToggleLoveAsync(int commentId, string userPhone)
        {
            try
            {
                await InitializeAsync();
                var db = DatabaseService.GetConnection();

                var comment = await db.Table<Comment>().Where(c => c.Id == commentId).FirstOrDefaultAsync();
                if (comment == null) return;

                var lovedBy = comment.LovedBy;

                if (lovedBy.Contains(userPhone))
                    lovedBy.Remove(userPhone);
                else
                    lovedBy.Add(userPhone);

                comment.LovedBy = lovedBy; // Updates LoveCount and LovedByJson

                await db.UpdateAsync(comment);

                Debug.WriteLine($"Toggled love for comment {commentId}: {lovedBy.Count} loves");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling love for comment: {ex}");
            }
        }

        public static async Task DeleteCommentAsync(int commentId)
        {
            try
            {
                await InitializeAsync();
                var db = DatabaseService.GetConnection();

                // First delete all replies to this comment
                var replies = await db.Table<Comment>()
                    .Where(c => c.ParentCommentId == commentId)
                    .ToListAsync();

                foreach (var reply in replies)
                {
                    await db.DeleteAsync(reply);
                }

                // Then delete the comment itself
                await db.DeleteAsync<Comment>(commentId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting comment: {ex}");
            }
        }

        public static async Task<int> GetCommentCountForPostAsync(int postId)
        {
            try
            {
                await InitializeAsync();
                var db = DatabaseService.GetConnection();
                return await db.Table<Comment>()
                    .Where(c => c.PostId == postId && !c.ParentCommentId.HasValue) // Only count top-level comments
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting comment count: {ex}");
                return 0;
            }
        }

        private static async Task ResolveAuthorInfoAsync(List<Comment> comments)
        {
            if (!comments.Any()) return;

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var phones = comments.Select(c => c.AuthorPhone).Distinct().ToList();
                var users = await db.Table<User>()
                    .Where(u => phones.Contains(u.PhoneNumber))
                    .ToListAsync();

                foreach (var comment in comments)
                {
                    var user = users.FirstOrDefault(u => u.PhoneNumber == comment.AuthorPhone);
                    if (user != null)
                    {
                        comment.AuthorDisplayName = string.IsNullOrWhiteSpace(user.Name) ? user.PhoneNumber : user.Name;
                        comment.AuthorProfileImagePath = user.ProfileImagePath ?? string.Empty;
                    }
                    else
                    {
                        comment.AuthorDisplayName = comment.AuthorPhone;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving comment author info: {ex}");
            }
        }
    }
}