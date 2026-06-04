using Lock.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lock.Services;

namespace Lock.Services
{
    public static class ProfileDataService
    {
        // ========== USER PHOTOS ==========
        public static async Task<List<UserPhoto>> GetUserPhotosAsync(int userId)
        {
            try
            {
                return await SupabaseService.GetAsync<UserPhoto>("UserPhotos",
                    $"UserId=eq.{userId}&order=Order.asc");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserPhotosAsync error: {ex}");
                return new List<UserPhoto>();
            }
        }

        public static async Task<UserPhoto?> AddUserPhotoAsync(int userId, string imagePath, string category = "Profile", string caption = "")
        {
            try
            {
                // Get current max order
                var existing = await SupabaseService.GetAsync<UserPhoto>("UserPhotos",
                    $"UserId=eq.{userId}&order=Order.desc&limit=1");

                int newOrder = (existing.FirstOrDefault()?.Order ?? -1) + 1;

                var photo = new UserPhoto
                {
                    UserId = userId,
                    ImagePath = imagePath,
                    Category = category,
                    Caption = caption,
                    Order = newOrder,
                    IsPrimary = newOrder == 0, // First photo is primary
                    UploadedAt = DateTime.UtcNow
                };

                var inserted = await SupabaseService.InsertAndReturnAsync<UserPhoto>("UserPhotos", photo);
                return inserted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddUserPhotoAsync error: {ex}");
                return null;
            }
        }

        public static async Task SetPrimaryPhotoAsync(int photoId, int userId)
        {
            try
            {
                // Remove primary from all user photos
                var userPhotos = await SupabaseService.GetAsync<UserPhoto>("UserPhotos",
                    $"UserId=eq.{userId}");

                foreach (var photo in userPhotos)
                {
                    if (photo.IsPrimary)
                    {
                        await SupabaseService.UpdateAsync("UserPhotos", $"Id=eq.{photo.Id}",
                            new { IsPrimary = false });
                    }
                }

                // Set new primary
                await SupabaseService.UpdateAsync("UserPhotos", $"Id=eq.{photoId}",
                    new { IsPrimary = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetPrimaryPhotoAsync error: {ex}");
            }
        }

        public static async Task DeleteUserPhotoAsync(int photoId)
        {
            try
            {
                var photos = await SupabaseService.GetAsync<UserPhoto>("UserPhotos",
                    $"Id=eq.{photoId}&limit=1");
                var photo = photos.FirstOrDefault();

                if (photo != null)
                {
                    // Delete file
                    try
                    {
                        if (File.Exists(photo.ImagePath))
                            File.Delete(photo.ImagePath);
                    }
                    catch { }

                    await SupabaseService.DeleteAsync("UserPhotos", $"Id=eq.{photoId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteUserPhotoAsync error: {ex}");
            }
        }

        // ========== PROMPTS ==========
        public static async Task<List<UserPrompt>> GetUserPromptsAsync(int userId)
        {
            try
            {
                return await SupabaseService.GetAsync<UserPrompt>("UserPrompts",
                    $"UserId=eq.{userId}&order=Order.asc");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserPromptsAsync error: {ex}");
                return new List<UserPrompt>();
            }
        }

        public static async Task DeleteDateIdeaAsync(int ideaId)
        {
            try
            {
                await SupabaseService.DeleteAsync("DateIdeas", $"Id=eq.{ideaId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteDateIdeaAsync error: {ex}");
            }
        }

        public static async Task<UserPrompt?> AddUserPromptAsync(int userId, string question, string answer)
        {
            try
            {
                // Get current max order
                var existing = await SupabaseService.GetAsync<UserPrompt>("UserPrompts",
                    $"UserId=eq.{userId}&order=Order.desc&limit=1");

                int newOrder = (existing.FirstOrDefault()?.Order ?? -1) + 1;

                var prompt = new UserPrompt
                {
                    UserId = userId,
                    Question = question,
                    Answer = answer,
                    Order = newOrder,
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await SupabaseService.InsertAndReturnAsync<UserPrompt>("UserPrompts", prompt);
                return inserted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddUserPromptAsync error: {ex}");
                return null;
            }
        }

        public static async Task UpdateUserPromptAsync(int promptId, string answer)
        {
            try
            {
                await SupabaseService.UpdateAsync("UserPrompts", $"Id=eq.{promptId}",
                    new { Answer = answer });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUserPromptAsync error: {ex}");
            }
        }

        public static async Task DeleteUserPromptAsync(int promptId)
        {
            try
            {
                await SupabaseService.DeleteAsync("UserPrompts", $"Id=eq.{promptId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteUserPromptAsync error: {ex}");
            }
        }

        // ========== DATE IDEAS ==========
        public static async Task<List<DateIdea>> GetUserDateIdeasAsync(int userId)
        {
            try
            {
                return await SupabaseService.GetAsync<DateIdea>("DateIdeas",
                    $"UserId=eq.{userId}&order=CreatedAt.desc");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserDateIdeasAsync error: {ex}");
                return new List<DateIdea>();
            }
        }

        public static async Task<DateIdea?> AddDateIdeaAsync(int userId, string title, string description, string location, string category, bool isPublic = true)
        {
            try
            {
                var idea = new DateIdea
                {
                    UserId = userId,
                    Title = title,
                    Description = description,
                    Location = location,
                    Category = category,
                    IsPublic = isPublic,
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await SupabaseService.InsertAndReturnAsync<DateIdea>("DateIdeas", idea);
                return inserted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddDateIdeaAsync error: {ex}");
                return null;
            }
        }

        // ========== EVENTS ==========
        public static async Task<List<UserEvent>> GetUserEventsAsync(int userId, string filter = "Upcoming")
        {
            try
            {
                var now = DateTime.UtcNow;

                switch (filter)
                {
                    case "Upcoming":
                        return await SupabaseService.GetAsync<UserEvent>("UserEvents",
                            $"UserId=eq.{userId}&EventDate=gt.{now:yyyy-MM-ddTHH:mm:ssZ}&order=EventDate.asc");

                    case "Past":
                        return await SupabaseService.GetAsync<UserEvent>("UserEvents",
                            $"UserId=eq.{userId}&EventDate=lte.{now:yyyy-MM-ddTHH:mm:ssZ}&order=EventDate.desc");

                    case "Hosting":
                    default:
                        return await SupabaseService.GetAsync<UserEvent>("UserEvents",
                            $"UserId=eq.{userId}&order=EventDate.desc");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserEventsAsync error: {ex}");
                return new List<UserEvent>();
            }
        }

        public static async Task<UserEvent?> CreateEventAsync(int userId, string eventName, string description,
            string location, DateTime eventDate, string category, int maxAttendees = 0, bool isPublic = true)
        {
            try
            {
                var evt = new UserEvent
                {
                    UserId = userId,
                    EventName = eventName,
                    Description = description,
                    Location = location,
                    EventDate = eventDate,
                    Category = category,
                    MaxAttendees = maxAttendees,
                    IsPublic = isPublic,
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await SupabaseService.InsertAndReturnAsync<UserEvent>("UserEvents", evt);

                if (inserted != null)
                {
                    // Creator automatically attends
                    var attendance = new EventAttendance
                    {
                        EventId = inserted.Id,
                        UserId = userId,
                        Status = "Going",
                        CreatedAt = DateTime.UtcNow
                    };
                    await SupabaseService.InsertAsync("EventAttendance", attendance);
                }

                return inserted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateEventAsync error: {ex}");
                return null;
            }
        }

        public static async Task<List<User>> GetEventAttendeesAsync(int eventId)
        {
            try
            {
                // Get all attendance records for this event with status "Going"
                var attendances = await SupabaseService.GetAsync<EventAttendance>("EventAttendance",
                    $"EventId=eq.{eventId}&Status=eq.Going");

                var attendees = new List<User>();
                foreach (var attendance in attendances)
                {
                    var users = await SupabaseService.GetAsync<User>("Users",
                        $"Id=eq.{attendance.UserId}&limit=1");
                    var user = users.FirstOrDefault();
                    if (user != null)
                        attendees.Add(user);
                }

                return attendees;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEventAttendeesAsync error: {ex}");
                return new List<User>();
            }
        }

        public static async Task<bool> JoinEventAsync(int eventId, int userId)
        {
            try
            {
                // Check if already attending
                var existing = await SupabaseService.GetAsync<EventAttendance>("EventAttendance",
                    $"EventId=eq.{eventId}&UserId=eq.{userId}&limit=1");

                if (existing.Any())
                    return false;

                var attendance = new EventAttendance
                {
                    EventId = eventId,
                    UserId = userId,
                    Status = "Going",
                    CreatedAt = DateTime.UtcNow
                };

                await SupabaseService.InsertAsync("EventAttendance", attendance);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JoinEventAsync error: {ex}");
                return false;
            }
        }

        public static async Task LeaveEventAsync(int eventId, int userId)
        {
            try
            {
                await SupabaseService.DeleteAsync("EventAttendance",
                    $"EventId=eq.{eventId}&UserId=eq.{userId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LeaveEventAsync error: {ex}");
            }
        }

        // ========== STATS ==========
        public static async Task<int> GetProfileViewsCountAsync(int userId)
        {
            try
            {
                // Get profile views count from ProfileViews table
                var views = await SupabaseService.GetAsync<ProfileView>("ProfileViews",
                    $"ViewedUserId=eq.{userId}");
                return views.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProfileViewsCountAsync error: {ex}");
                return new Random().Next(10, 100); // Fallback
            }
        }

        public static async Task<int> GetMatchesCountAsync(int userId)
        {
            try
            {
                // This would come from a Matches table
                // For now, return sample data
                // TODO: Implement actual matches count from Supabase
                return new Random().Next(1, 20);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMatchesCountAsync error: {ex}");
                return 0;
            }
        }

        public static async Task<double> GetResponseRateAsync(int userId)
        {
            try
            {
                // This would calculate from message data
                // TODO: Implement actual response rate calculation
                return new Random().Next(40, 95);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetResponseRateAsync error: {ex}");
                return 0;
            }
        }
    }
}