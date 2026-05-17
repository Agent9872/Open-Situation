using Lock.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lock.Chat.Services;
using System.Linq;

namespace Lock.Services
{
    public static class ProfileDataService
    {
        // ========== USER PHOTOS ==========
        public static async Task<List<UserPhoto>> GetUserPhotosAsync(int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<UserPhoto>()
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.Order)
                .ToListAsync();
        }

        public static async Task<UserPhoto> AddUserPhotoAsync(int userId, string imagePath, string category = "Profile", string caption = "")
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Get current max order
            var existing = await db.Table<UserPhoto>()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.Order)
                .FirstOrDefaultAsync();

            int newOrder = (existing?.Order ?? -1) + 1;

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

            await db.InsertAsync(photo);
            return photo;
        }

        public static async Task SetPrimaryPhotoAsync(int photoId, int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Remove primary from all user photos
            var userPhotos = await db.Table<UserPhoto>()
                .Where(p => p.UserId == userId)
                .ToListAsync();

            foreach (var photo in userPhotos)
            {
                photo.IsPrimary = false;
                await db.UpdateAsync(photo);
            }

            // Set new primary
            var newPrimary = await db.Table<UserPhoto>()
                .Where(p => p.Id == photoId)
                .FirstOrDefaultAsync();

            if (newPrimary != null)
            {
                newPrimary.IsPrimary = true;
                await db.UpdateAsync(newPrimary);
            }
        }

        public static async Task DeleteUserPhotoAsync(int photoId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var photo = await db.Table<UserPhoto>()
                .Where(p => p.Id == photoId)
                .FirstOrDefaultAsync();

            if (photo != null)
            {
                // Delete file
                try
                {
                    if (File.Exists(photo.ImagePath))
                        File.Delete(photo.ImagePath);
                }
                catch { }

                await db.DeleteAsync(photo);
            }
        }

        // ========== PROMPTS ==========
        public static async Task<List<UserPrompt>> GetUserPromptsAsync(int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<UserPrompt>()
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.Order)
                .ToListAsync();
        }

        public static async Task DeleteDateIdeaAsync(int ideaId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            await db.DeleteAsync<DateIdea>(ideaId);
        }

        public static async Task<UserPrompt> AddUserPromptAsync(int userId, string question, string answer)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Get current max order
            var existing = await db.Table<UserPrompt>()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.Order)
                .FirstOrDefaultAsync();

            int newOrder = (existing?.Order ?? -1) + 1;

            var prompt = new UserPrompt
            {
                UserId = userId,
                Question = question,
                Answer = answer,
                Order = newOrder,
                CreatedAt = DateTime.UtcNow
            };

            await db.InsertAsync(prompt);
            return prompt;
        }

        public static async Task UpdateUserPromptAsync(int promptId, string answer)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var prompt = await db.Table<UserPrompt>()
                .Where(p => p.Id == promptId)
                .FirstOrDefaultAsync();

            if (prompt != null)
            {
                prompt.Answer = answer;
                await db.UpdateAsync(prompt);
            }
        }

        public static async Task DeleteUserPromptAsync(int promptId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            await db.DeleteAsync<UserPrompt>(promptId);
        }

        // ========== DATE IDEAS ==========
        public static async Task<List<DateIdea>> GetUserDateIdeasAsync(int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<DateIdea>()
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public static async Task<DateIdea> AddDateIdeaAsync(int userId, string title, string description, string location, string category, bool isPublic = true)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

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

            await db.InsertAsync(idea);
            return idea;
        }


        // ========== EVENTS ==========
        public static async Task<List<UserEvent>> GetUserEventsAsync(int userId, string filter = "Upcoming")
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            var now = DateTime.UtcNow;

            switch (filter)
            {
                case "Upcoming":
                    return await db.Table<UserEvent>()
                        .Where(e => e.UserId == userId && e.EventDate > now)
                        .OrderBy(e => e.EventDate)
                        .ToListAsync();

                case "Past":
                    return await db.Table<UserEvent>()
                        .Where(e => e.UserId == userId && e.EventDate <= now)
                        .OrderByDescending(e => e.EventDate)
                        .ToListAsync();

                case "Hosting":
                default:
                    return await db.Table<UserEvent>()
                        .Where(e => e.UserId == userId)
                        .OrderByDescending(e => e.EventDate)
                        .ToListAsync();
            }
        }

        public static async Task<UserEvent> CreateEventAsync(int userId, string eventName, string description,
            string location, DateTime eventDate, string category, int maxAttendees = 0, bool isPublic = true)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

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

            await db.InsertAsync(evt);

            // Creator automatically attends
            var attendance = new EventAttendance
            {
                EventId = evt.Id,
                UserId = userId,
                Status = "Going",
                CreatedAt = DateTime.UtcNow
            };
            await db.InsertAsync(attendance);

            return evt;
        }

        public static async Task<List<User>> GetEventAttendeesAsync(int eventId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Get all attendance records for this event
            var attendances = await db.Table<EventAttendance>()
                .Where(a => a.EventId == eventId && a.Status == "Going")
                .ToListAsync();

            var attendees = new List<User>();
            foreach (var attendance in attendances)
            {
                var user = await db.Table<User>().Where(u => u.Id == attendance.UserId).FirstOrDefaultAsync();
                if (user != null)
                    attendees.Add(user);
            }

            return attendees;
        }

        public static async Task<bool> JoinEventAsync(int eventId, int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Check if already attending
            var existing = await db.Table<EventAttendance>()
                .Where(a => a.EventId == eventId && a.UserId == userId)
                .FirstOrDefaultAsync();

            if (existing != null)
                return false;

            var attendance = new EventAttendance
            {
                EventId = eventId,
                UserId = userId,
                Status = "Going",
                CreatedAt = DateTime.UtcNow
            };

            await db.InsertAsync(attendance);
            return true;
        }

        public static async Task LeaveEventAsync(int eventId, int userId)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var attendance = await db.Table<EventAttendance>()
                .Where(a => a.EventId == eventId && a.UserId == userId)
                .FirstOrDefaultAsync();

            if (attendance != null)
                await db.DeleteAsync(attendance);
        }

        // ========== STATS ==========
        public static async Task<int> GetProfileViewsCountAsync(int userId)
        {
            // This would ideally come from a ProfileViews table
            // For now, return sample data
            return new Random().Next(10, 100);
        }

        public static async Task<int> GetMatchesCountAsync(int userId)
        {
            // This would come from a Matches table
            return new Random().Next(1, 20);
        }

        public static async Task<double> GetResponseRateAsync(int userId)
        {
            // This would calculate from message data
            return new Random().Next(40, 95);
        }
    }
}