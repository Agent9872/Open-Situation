using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using SQLite;
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var tracking = new UserMoodTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    UserPhone = userPhone,
                    OldMood = oldMood ?? string.Empty,
                    NewMood = newMood ?? string.Empty,
                    Source = source,
                    Timestamp = DateTime.UtcNow
                };

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<UserMoodTracking>()
                    .Where(t => t.UserPhone == userPhone)
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<UserMoodTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
                Debug.WriteLine($"[TRACKING] Profile change for {userPhone}: {fieldName} changed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackProfileChangeAsync error: {ex}");
            }
        }

        // Track all profile changes at once
        public async Task TrackAllProfileChangesAsync(User oldUser, User newUser)
        {
            if (oldUser == null || newUser == null) return;

            // Compare all fields
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Name", oldUser.Name, newUser.Name, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "DisplayName", oldUser.DisplayName, newUser.DisplayName, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Bio", oldUser.Bio, newUser.Bio, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Gender", oldUser.Gender, newUser.Gender, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Interest", oldUser.Interest, newUser.Interest, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Country", oldUser.Country, newUser.Country, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "State", oldUser.State, newUser.State, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Occupation", oldUser.Occupation, newUser.Occupation, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Education", oldUser.Education, newUser.Education, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Height", oldUser.HeightCm?.ToString() ?? "", newUser.HeightCm?.ToString() ?? "", newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "BodyType", oldUser.BodyType, newUser.BodyType, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Ethnicity", oldUser.Ethnicity, newUser.Ethnicity, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Tribe", oldUser.Tribe, newUser.Tribe, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Religion", oldUser.Religion, newUser.Religion, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "PoliticalViews", oldUser.PoliticalViews, newUser.PoliticalViews, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Drinks", oldUser.Drinks, newUser.Drinks, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Smokes", oldUser.Smokes.ToString(), newUser.Smokes.ToString(), newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "HasPets", oldUser.HasPets.ToString(), newUser.HasPets.ToString(), newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "MusicGenres", oldUser.MusicGenres, newUser.MusicGenres, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "FavoriteArtists", oldUser.FavoriteArtists, newUser.FavoriteArtists, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "FavoriteMovies", oldUser.FavoriteMovies, newUser.FavoriteMovies, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "FavoriteBooks", oldUser.FavoriteBooks, newUser.FavoriteBooks, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Languages", oldUser.Languages, newUser.Languages, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Interests", oldUser.Interests, newUser.Interests, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "PersonalityType", oldUser.PersonalityType, newUser.PersonalityType, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "LoveLanguage", oldUser.LoveLanguage, newUser.LoveLanguage, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "SexualOrientation", oldUser.SexualOrientation, newUser.SexualOrientation, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "TopInterest", oldUser.TopInterest, newUser.TopInterest, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "TopArtist", oldUser.TopArtist, newUser.TopArtist, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "TopMovie", oldUser.TopMovie, newUser.TopMovie, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "ProfileImage", oldUser.ProfileImagePath, newUser.ProfileImagePath, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "CoverImage", oldUser.CoverImagePath, newUser.CoverImagePath, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "Mood", oldUser.Mood, newUser.Mood, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "EnergyLevel", oldUser.EnergyLevel, newUser.EnergyLevel, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "DietaryPreference", oldUser.DietaryPreference, newUser.DietaryPreference, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "ExerciseFrequency", oldUser.ExerciseFrequency, newUser.ExerciseFrequency, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "KidsPreference", oldUser.KidsPreference, newUser.KidsPreference, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "HasChildren", oldUser.HasChildren, newUser.HasChildren, newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "AllowMoodSearch", oldUser.AllowMoodSearch.ToString(), newUser.AllowMoodSearch.ToString(), newUser.Name);
            await TrackProfileChangeAsync(newUser.PhoneNumber, "GhostModeMoodShield", oldUser.GhostModeMoodShield.ToString(), newUser.GhostModeMoodShield.ToString(), newUser.Name);
        }

        // Get profile change history for a user
        public async Task<List<UserProfileTracking>> GetProfileChangeHistoryAsync(string userPhone, int limit = 100)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<UserProfileTracking>()
                    .Where(t => t.UserPhone == userPhone)
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<UserProfileTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var tracking = new UserLoginTracking
                {
                    Id = Guid.NewGuid().ToString(),
                    UserPhone = userPhone,
                    DeviceId = deviceId,
                    LoginTime = DateTime.UtcNow
                };

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Update the last login record with logout time
                var lastLogin = await db.Table<UserLoginTracking>()
                    .Where(t => t.UserPhone == userPhone && t.LogoutTime == null)
                    .OrderByDescending(t => t.LoginTime)
                    .FirstOrDefaultAsync();

                if (lastLogin != null)
                {
                    lastLogin.LogoutTime = DateTime.UtcNow;
                    await db.UpdateAsync(lastLogin);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<UserLoginTracking>()
                    .Where(t => t.UserPhone == userPhone)
                    .OrderByDescending(t => t.LoginTime)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<UserLoginTracking>()
                    .OrderByDescending(t => t.LoginTime)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<PostTracking>()
                    .Where(t => t.AuthorPhone == userPhone)
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<PostTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

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

                await db.InsertAsync(tracking);
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<GroupTracking>()
                    .Where(t => t.GroupId == groupId)
                    .OrderByDescending(t => t.Timestamp)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                return await db.Table<GroupTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit)
                    .ToListAsync();
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
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Get all mood changes
                var moodChanges = await db.Table<UserMoodTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit / 4)
                    .ToListAsync();

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
                var profileChanges = await db.Table<UserProfileTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit / 4)
                    .ToListAsync();

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
                var logins = await db.Table<UserLoginTracking>()
                    .OrderByDescending(t => t.LoginTime)
                    .Take(limit / 4)
                    .ToListAsync();

                foreach (var item in logins)
                {
                    activities.Add(new AdminActivityItem
                    {
                        Id = item.Id,
                        Type = "Login",
                        UserPhone = item.UserPhone,
                        Title = "User Logged In",
                        Description = $"Logged in from device {item.DeviceId?[..8]}...",
                        Icon = "🔑",
                        Timestamp = item.LoginTime
                    });
                }

                // Get all group activities
                var groupActivities = await db.Table<GroupTracking>()
                    .OrderByDescending(t => t.Timestamp)
                    .Take(limit / 4)
                    .ToListAsync();

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

        // Get user name by phone
        private async Task<string> GetUserNameAsync(string userPhone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>().Where(u => u.PhoneNumber == userPhone).FirstOrDefaultAsync();
                return user?.Name ?? userPhone;
            }
            catch
            {
                return userPhone;
            }
        }

        #endregion
    }

    #region Tracking Models

    [Table("UserMoodTracking")]
    public class UserMoodTracking
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string OldMood { get; set; } = string.Empty;
        public string NewMood { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // "profile", "post", "group"
        public DateTime Timestamp { get; set; }
    }

    [Table("UserProfileTracking")]
    public class UserProfileTracking
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    [Table("UserLoginTracking")]
    public class UserLoginTracking
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
    }

    [Table("PostTracking")]
    public class PostTracking
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string AuthorPhone { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Created, Edited, Deleted
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    [Table("GroupTracking")]
    public class GroupTracking
    {
        [PrimaryKey]
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