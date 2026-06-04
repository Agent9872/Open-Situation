using Lock.Models;
using Lock.Models.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Services.Admin
{
    public class UserTrackingService
    {
        private static readonly Lazy<UserTrackingService> _instance = new(() => new UserTrackingService());
        public static UserTrackingService Instance => _instance.Value;

        private UserTrackingService() { }

        #region Mood Tracking

        // Track mood changes with history
        public async Task TrackMoodChangeAsync(string userPhone, string oldMood, string newMood, string source = "profile")
        {
            try
            {
                var tracking = new UserMoodTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    UserPhone = userPhone,
                    OldMood = oldMood ?? string.Empty,
                    NewMood = newMood ?? string.Empty,
                    Source = source,
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("UserMoodTracking", tracking);
                Debug.WriteLine($"[TRACKING] Mood change for {userPhone}: '{oldMood}' -> '{newMood}' at {tracking.Timestamp}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackMoodChangeAsync error: {ex}");
            }
        }

        // Get mood history for a user
        public async Task<List<UserMoodTracking>> GetMoodHistoryAsync(string userPhone, int limit = 50)
        {
            try
            {
                return await SupabaseService.GetAsync<UserMoodTracking>("UserMoodTracking",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetMoodHistoryAsync error: {ex}");
                return new List<UserMoodTracking>();
            }
        }

        // Get all mood changes across all users
        public async Task<List<UserMoodTracking>> GetAllMoodChangesAsync(int limit = 500)
        {
            try
            {
                return await SupabaseService.GetAsync<UserMoodTracking>("UserMoodTracking",
                    $"order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllMoodChangesAsync error: {ex}");
                return new List<UserMoodTracking>();
            }
        }

        #endregion

        #region Profile Change Tracking

        // Track profile field changes
        public async Task TrackProfileChangeAsync(string userPhone, string fieldName, string oldValue, string newValue, string userName = "")
        {
            try
            {
                // Only track if values are different
                if (oldValue == newValue) return;

                var tracking = new UserProfileTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    UserPhone = userPhone,
                    UserName = userName,
                    FieldName = fieldName,
                    OldValue = TruncateValue(oldValue),
                    NewValue = TruncateValue(newValue),
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("UserProfileTracking", tracking);
                Debug.WriteLine($"[TRACKING] Profile change for {userPhone}: {fieldName} changed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackProfileChangeAsync error: {ex}");
            }
        }

        // Get profile change history for a user
        public async Task<List<UserProfileTracking>> GetProfileChangeHistoryAsync(string userPhone, int limit = 100)
        {
            try
            {
                return await SupabaseService.GetAsync<UserProfileTracking>("UserProfileTracking",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProfileChangeHistoryAsync error: {ex}");
                return new List<UserProfileTracking>();
            }
        }

        // Get all profile changes across all users
        public async Task<List<UserProfileTracking>> GetAllProfileChangesAsync(int limit = 500)
        {
            try
            {
                return await SupabaseService.GetAsync<UserProfileTracking>("UserProfileTracking",
                    $"order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllProfileChangesAsync error: {ex}");
                return new List<UserProfileTracking>();
            }
        }

        #endregion

        #region Login/Logout Tracking

        // Track user login
        public async Task TrackUserLoginAsync(string userPhone, string deviceId)
        {
            try
            {
                var tracking = new UserLoginTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    UserPhone = userPhone,
                    DeviceId = deviceId,
                    LoginTime = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("UserLoginTracking", tracking);
                Debug.WriteLine($"[TRACKING] User login: {userPhone} at {tracking.LoginTime}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackUserLoginAsync error: {ex}");
            }
        }

        // Track user logout
        public async Task TrackUserLogoutAsync(string userPhone)
        {
            try
            {
                // Get the last login record without logout time
                var logins = await SupabaseService.GetAsync<UserLoginTracking>("UserLoginTracking",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&LogoutTime=is.null&order=LoginTime.desc&limit=1");

                var lastLogin = logins.FirstOrDefault();

                if (lastLogin != null)
                {
                    lastLogin.LogoutTime = DateTime.UtcNow;
                    await SupabaseService.UpdateAsync("UserLoginTracking", $"Id=eq.{lastLogin.Id}",
                        new { LogoutTime = lastLogin.LogoutTime });
                    Debug.WriteLine($"[TRACKING] User logout: {userPhone} at {lastLogin.LogoutTime}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackUserLogoutAsync error: {ex}");
            }
        }

        // Get user login history
        public async Task<List<UserLoginTracking>> GetUserLoginHistoryAsync(string userPhone, int limit = 50)
        {
            try
            {
                return await SupabaseService.GetAsync<UserLoginTracking>("UserLoginTracking",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&order=LoginTime.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserLoginHistoryAsync error: {ex}");
                return new List<UserLoginTracking>();
            }
        }

        // Get all login history
        public async Task<List<UserLoginTracking>> GetAllLoginHistoryAsync(int limit = 500)
        {
            try
            {
                return await SupabaseService.GetAsync<UserLoginTracking>("UserLoginTracking",
                    $"order=LoginTime.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllLoginHistoryAsync error: {ex}");
                return new List<UserLoginTracking>();
            }
        }

        #endregion

        #region Post Tracking

        // Track post creation
        public async Task TrackPostCreationAsync(int postId, string authorPhone, string content, string category)
        {
            try
            {
                var tracking = new PostTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    PostId = postId,
                    AuthorPhone = authorPhone,
                    Action = "Created",
                    Content = TruncateValue(content, 200),
                    Category = category,
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("PostTracking", tracking);
                Debug.WriteLine($"[TRACKING] Post created: {postId} by {authorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPostCreationAsync error: {ex}");
            }
        }

        // Track post edit
        public async Task TrackPostEditAsync(int postId, string authorPhone, string oldContent, string newContent, string category)
        {
            try
            {
                var tracking = new PostTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    PostId = postId,
                    AuthorPhone = authorPhone,
                    Action = "Edited",
                    Content = TruncateValue(newContent, 200),
                    Category = category,
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("PostTracking", tracking);
                Debug.WriteLine($"[TRACKING] Post edited: {postId} by {authorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPostEditAsync error: {ex}");
            }
        }

        // Track post deletion
        public async Task TrackPostDeletionAsync(int postId, string authorPhone, string content, string category)
        {
            try
            {
                var tracking = new PostTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    PostId = postId,
                    AuthorPhone = authorPhone,
                    Action = "Deleted",
                    Content = TruncateValue(content, 200),
                    Category = category,
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("PostTracking", tracking);
                Debug.WriteLine($"[TRACKING] Post deleted: {postId} by {authorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPostDeletionAsync error: {ex}");
            }
        }

        // Get user post history
        public async Task<List<PostTracking>> GetUserPostHistoryAsync(string userPhone, int limit = 50)
        {
            try
            {
                return await SupabaseService.GetAsync<PostTracking>("PostTracking",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(userPhone)}&order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserPostHistoryAsync error: {ex}");
                return new List<PostTracking>();
            }
        }

        // Get all post tracking
        public async Task<List<PostTracking>> GetAllPostTrackingAsync(int limit = 500)
        {
            try
            {
                return await SupabaseService.GetAsync<PostTracking>("PostTracking",
                    $"order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllPostTrackingAsync error: {ex}");
                return new List<PostTracking>();
            }
        }

        #endregion

        #region Group Tracking

        // Track group creation
        public async Task TrackGroupCreationAsync(Group group, string creatorPhone)
        {
            try
            {
                var tracking = new GroupTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Action = "Created",
                    ActorPhone = creatorPhone,
                    Details = $"Group '{group.Name}' created with type {group.GroupType} and visibility {group.Visibility}",
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("GroupTracking", tracking);
                Debug.WriteLine($"[TRACKING] Group created: {group.Name} by {creatorPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackGroupCreationAsync error: {ex}");
            }
        }

        // Track group membership changes
        public async Task TrackGroupMembershipAsync(string groupId, string groupName, string userPhone, string action, string actorPhone)
        {
            try
            {
                var tracking = new GroupTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = groupId,
                    GroupName = groupName,
                    Action = action,
                    ActorPhone = actorPhone,
                    TargetPhone = userPhone,
                    Details = $"User {userPhone} {action} group '{groupName}'",
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("GroupTracking", tracking);
                Debug.WriteLine($"[TRACKING] Group membership: {userPhone} {action} {groupName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackGroupMembershipAsync error: {ex}");
            }
        }

        // Track group updates (name, description, settings)
        public async Task TrackGroupUpdateAsync(string groupId, string groupName, string fieldChanged, string oldValue, string newValue, string actorPhone)
        {
            try
            {
                var tracking = new GroupTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = groupId,
                    GroupName = groupName,
                    Action = $"Updated {fieldChanged}",
                    ActorPhone = actorPhone,
                    Details = $"Changed '{fieldChanged}' from '{oldValue}' to '{newValue}'",
                    Timestamp = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("GroupTracking", tracking);
                Debug.WriteLine($"[TRACKING] Group updated: {groupName} - {fieldChanged} changed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackGroupUpdateAsync error: {ex}");
            }
        }

        // Get group tracking history
        public async Task<List<GroupTracking>> GetGroupTrackingAsync(string groupId)
        {
            try
            {
                return await SupabaseService.GetAsync<GroupTracking>("GroupTracking",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&order=Timestamp.desc");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetGroupTrackingAsync error: {ex}");
                return new List<GroupTracking>();
            }
        }

        // Get all group tracking
        public async Task<List<GroupTracking>> GetAllGroupTrackingAsync(int limit = 500)
        {
            try
            {
                return await SupabaseService.GetAsync<GroupTracking>("GroupTracking",
                    $"order=Timestamp.desc&limit={limit}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllGroupTrackingAsync error: {ex}");
                return new List<GroupTracking>();
            }
        }

        #endregion

        #region Combined Data Methods

        // Get all tracking data for a user
        public async Task<UserTrackingData> GetAllUserTrackingDataAsync(string userPhone)
        {
            var data = new UserTrackingData
            {
                UserPhone = userPhone,
                MoodHistory = await GetMoodHistoryAsync(userPhone),
                ProfileChanges = await GetProfileChangeHistoryAsync(userPhone),
                LoginHistory = await GetUserLoginHistoryAsync(userPhone),
                PostHistory = await GetUserPostHistoryAsync(userPhone)
            };

            return data;
        }

        // Get comprehensive activity feed for admin
        public async Task<List<AdminActivityItem>> GetAdminActivityFeedAsync(int limit = 500)
        {
            var activities = new List<AdminActivityItem>();

            try
            {
                var itemsPerType = limit / 5;

                // Get all mood changes
                var moodChanges = await SupabaseService.GetAsync<UserMoodTracking>("UserMoodTracking",
                    $"order=Timestamp.desc&limit={itemsPerType}");

                foreach (var item in moodChanges)
                {
                    activities.Add(new AdminActivityItem
                    {
                        Id = item.Id,
                        Type = "Mood Change",
                        UserPhone = item.UserPhone,
                        Title = "Mood Updated",
                        Description = $"Changed from '{item.OldMood}' to '{item.NewMood}'",
                        Icon = "😊",
                        Timestamp = item.Timestamp
                    });
                }

                // Get all profile changes
                var profileChanges = await SupabaseService.GetAsync<UserProfileTracking>("UserProfileTracking",
                    $"order=Timestamp.desc&limit={itemsPerType}");

                foreach (var item in profileChanges)
                {
                    activities.Add(new AdminActivityItem
                    {
                        Id = item.Id,
                        Type = "Profile Change",
                        UserPhone = item.UserPhone,
                        UserName = item.UserName,
                        Title = $"{item.FieldName} Updated",
                        Description = $"Changed from '{item.OldValue}' to '{item.NewValue}'",
                        Icon = "✏️",
                        Timestamp = item.Timestamp
                    });
                }

                // Get all logins
                var logins = await SupabaseService.GetAsync<UserLoginTracking>("UserLoginTracking",
                    $"order=LoginTime.desc&limit={itemsPerType}");

                foreach (var item in logins)
                {
                    activities.Add(new AdminActivityItem
                    {
                        Id = item.Id,
                        Type = "Login",
                        UserPhone = item.UserPhone,
                        Title = "User Logged In",
                        Description = $"Logged in from device {item.DeviceId?[..Math.Min(8, item.DeviceId?.Length ?? 0)]}...",
                        Icon = "🔑",
                        Timestamp = item.LoginTime
                    });
                }

                // Get all group activities
                var groupActivities = await SupabaseService.GetAsync<GroupTracking>("GroupTracking",
                    $"order=Timestamp.desc&limit={itemsPerType}");

                foreach (var item in groupActivities)
                {
                    activities.Add(new AdminActivityItem
                    {
                        Id = item.Id,
                        Type = $"Group {item.Action}",
                        UserPhone = item.ActorPhone,
                        Title = $"Group {item.Action}",
                        Description = item.Details,
                        Icon = "👥",
                        Timestamp = item.Timestamp
                    });
                }

                // Get all post activities
                var postActivities = await SupabaseService.GetAsync<PostTracking>("PostTracking",
                    $"order=Timestamp.desc&limit={itemsPerType}");

                foreach (var item in postActivities)
                {
                    activities.Add(new AdminActivityItem
                    {
                        Id = item.Id,
                        Type = $"Post {item.Action}",
                        UserPhone = item.AuthorPhone,
                        Title = $"Post {item.Action}",
                        Description = item.Content,
                        Icon = "📝",
                        Timestamp = item.Timestamp
                    });
                }

                // Sort all activities by timestamp
                activities = activities.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAdminActivityFeedAsync error: {ex}");
            }

            return activities;
        }

        #endregion

        #region Helper Methods

        private string TruncateValue(string value, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
        }

        #endregion
    }

    #region Tracking Models (Updated - No SQLite Attributes)

    public class UserMoodTracking
    {
        public string Id { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string OldMood { get; set; } = string.Empty;
        public string NewMood { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // "profile", "post", "group"
        public DateTime Timestamp { get; set; }
    }

    public class UserProfileTracking
    {
        public string Id { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class UserLoginTracking
    {
        public string Id { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
    }

    public class PostTracking
    {
        public string Id { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string AuthorPhone { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Created, Edited, Deleted
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class GroupTracking
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Created, Joined, Left, MemberAdded, MemberRemoved, Updated
        public string ActorPhone { get; set; } = string.Empty;
        public string? TargetPhone { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class UserTrackingData
    {
        public string UserPhone { get; set; } = string.Empty;
        public List<UserMoodTracking> MoodHistory { get; set; } = new();
        public List<UserProfileTracking> ProfileChanges { get; set; } = new();
        public List<UserLoginTracking> LoginHistory { get; set; } = new();
        public List<PostTracking> PostHistory { get; set; } = new();
    }

    public class AdminActivityItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string BackgroundColor => "#FFFFFF";
    }

    #endregion
}