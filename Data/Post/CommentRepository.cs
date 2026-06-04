using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Lock.Models;
using Lock.Services;

namespace Lock.Data.Post
{
    public static class CommentRepository
    {
        // Note: Tables are managed in Supabase - no need to create them in code

        public static async Task<List<Comment>> GetCommentsForPostAsync(int postId, string currentUserPhone = "")
        {
            try
            {
                // Get all comments for this post from Supabase
                var allComments = await SupabaseService.GetAsync<Comment>("Comments",
                    $"PostId=eq.{postId}&order=CreatedAt.desc");

                // Set love status AND ownership for current user
                if (!string.IsNullOrEmpty(currentUserPhone))
                {
                    foreach (var comment in allComments)
                    {
                        comment.IsLovedByCurrentUser = comment.LovedBy.Contains(currentUserPhone);
                        // CRITICAL: Set ownership flag
                        comment.IsOwnedByCurrentUser = comment.AuthorPhone == currentUserPhone;

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

        public static async Task UpdateCommentAsync(int commentId, string newContent)
        {
            try
            {
                var comments = await SupabaseService.GetAsync<Comment>("Comments", $"Id=eq.{commentId}&limit=1");
                var comment = comments.FirstOrDefault();

                if (comment != null)
                {
                    comment.Content = newContent;
                    await SupabaseService.UpdateAsync("Comments", $"Id=eq.{commentId}", new { Content = newContent });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating comment: {ex}");
                throw;
            }
        }

        public static async Task<Comment?> AddCommentAsync(int postId, string authorPhone, string content, int? parentCommentId = null)
        {
            try
            {
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

                var inserted = await SupabaseService.InsertAndReturnAsync<Comment>("Comments", comment);

                // Resolve author info before returning
                if (inserted != null)
                {
                    await ResolveAuthorInfoAsync(new List<Comment> { inserted });
                    inserted.IsOwnedByCurrentUser = true;
                }

                return inserted;
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
                var comments = await SupabaseService.GetAsync<Comment>("Comments", $"Id=eq.{commentId}&limit=1");
                var comment = comments.FirstOrDefault();
                if (comment == null) return;

                var lovedBy = comment.LovedBy;

                if (lovedBy.Contains(userPhone))
                    lovedBy.Remove(userPhone);
                else
                    lovedBy.Add(userPhone);

                // Update the comment with new loved by list
                await SupabaseService.UpdateAsync("Comments", $"Id=eq.{commentId}",
                    new { LovedByJson = System.Text.Json.JsonSerializer.Serialize(lovedBy), LoveCount = lovedBy.Count });

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
                // First delete all replies to this comment
                var replies = await SupabaseService.GetAsync<Comment>("Comments", $"ParentCommentId=eq.{commentId}");

                foreach (var reply in replies)
                {
                    await SupabaseService.DeleteAsync("Comments", $"Id=eq.{reply.Id}");
                }

                // Then delete the comment itself
                await SupabaseService.DeleteAsync("Comments", $"Id=eq.{commentId}");
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
                var comments = await SupabaseService.GetAsync<Comment>("Comments",
                    $"PostId=eq.{postId}&ParentCommentId=is.null");
                return comments.Count;
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
                var phones = comments.Select(c => c.AuthorPhone).Distinct().ToList();

                // Get all users at once
                var allUsers = await SupabaseService.GetAsync<User>("Users", "");
                var userDict = allUsers.ToDictionary(u => u.PhoneNumber, u => u, StringComparer.OrdinalIgnoreCase);

                foreach (var comment in comments)
                {
                    if (userDict.TryGetValue(comment.AuthorPhone, out var user))
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