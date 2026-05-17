using Lock.Data.Post;
using Lock.Models;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class SavePostService
    {
        // ── Storage keys ──────────────────────────────────────────────────────
        private const string SavedPostsWithFoldersKeyPrefix = "saved_posts_folders_v2_";

        private static string GetFolderKey(string currentUserPhone)
            => $"{SavedPostsWithFoldersKeyPrefix}{currentUserPhone}";

        // ── Lightweight record stored in Preferences ──────────────────────────
        // Stores ONLY id + folder + timestamp — NOT the full Post object.
        // This keeps the JSON tiny and avoids the Preferences size limit.
        private class SavedRecord
        {
            public int PostId { get; set; }
            public string Folder { get; set; } = "Saved";
            public DateTime At { get; set; } = DateTime.UtcNow;
        }

        // ── Read / write records ──────────────────────────────────────────────

        private static List<SavedRecord> ReadRecords(string currentUserPhone)
        {
            try
            {
                var json = Preferences.Get(GetFolderKey(currentUserPhone), string.Empty);
                if (string.IsNullOrEmpty(json)) return new List<SavedRecord>();

                var list = JsonSerializer.Deserialize<List<SavedRecord>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var result = list ?? new List<SavedRecord>();
                Debug.WriteLine($"[SavePostService] ReadRecords: {result.Count} records for {currentUserPhone}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] ReadRecords error: {ex.Message}");
                return new List<SavedRecord>();
            }
        }

        private static void WriteRecords(string currentUserPhone, List<SavedRecord> records)
        {
            try
            {
                var json = JsonSerializer.Serialize(records,
                    new JsonSerializerOptions { WriteIndented = false });

                Debug.WriteLine($"[SavePostService] WriteRecords: {records.Count} records, JSON length={json.Length}");
                Preferences.Set(GetFolderKey(currentUserPhone), json);

                // Verify the write landed correctly
                var verify = Preferences.Get(GetFolderKey(currentUserPhone), string.Empty);
                Debug.WriteLine($"[SavePostService] WriteRecords verify length={verify.Length}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] WriteRecords error: {ex.Message}");
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public static bool IsPostSaved(int postId, string currentUserPhone)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                bool saved = records.Any(r => r.PostId == postId);
                Debug.WriteLine($"[SavePostService] IsPostSaved({postId}): {saved}");
                return saved;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] IsPostSaved error: {ex}");
                return false;
            }
        }

        // Save a post to a specific folder
        public static async Task<bool> SavePostAsync(int postId, string currentUserPhone,
            string folderName = "Saved")
        {
            try
            {
                if (string.IsNullOrEmpty(currentUserPhone)) return false;

                var folder = string.IsNullOrWhiteSpace(folderName) ? "Saved" : folderName.Trim();
                var records = ReadRecords(currentUserPhone);

                // Allow same post in different folders; block exact duplicate
                if (records.Any(r => r.PostId == postId &&
                    string.Equals(r.Folder, folder, StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine($"[SavePostService] Post {postId} already in folder '{folder}'");
                    return false;
                }

                // Verify the post actually exists before saving the record
                var post = await PostRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    Debug.WriteLine($"[SavePostService] Post {postId} not found");
                    return false;
                }

                records.Add(new SavedRecord { PostId = postId, Folder = folder, At = DateTime.UtcNow });
                WriteRecords(currentUserPhone, records);

                Debug.WriteLine($"[SavePostService] Saved post {postId} to '{folder}'. Total records: {records.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] SavePostAsync error: {ex}");
                return false;
            }
        }

        // Overload kept for compatibility
        public static Task<bool> SavePostAsync(int postId, string currentUserPhone)
            => SavePostAsync(postId, currentUserPhone, "Saved");

        // Remove ALL entries for a post (across all folders)
        public static Task<bool> UnsavePostAsync(int postId, string currentUserPhone)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                int removed = records.RemoveAll(r => r.PostId == postId);
                if (removed > 0)
                    WriteRecords(currentUserPhone, records);

                Debug.WriteLine($"[SavePostService] Unsaved post {postId}, removed {removed} record(s)");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] UnsavePostAsync error: {ex}");
                return Task.FromResult(false);
            }
        }

        // Move a post to a different folder
        public static Task<bool> MovePostToFolderAsync(int postId, string currentUserPhone,
            string newFolderName)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                var record = records.FirstOrDefault(r => r.PostId == postId);
                if (record == null) return Task.FromResult(false);

                record.Folder = string.IsNullOrWhiteSpace(newFolderName) ? "Saved" : newFolderName.Trim();
                WriteRecords(currentUserPhone, records);

                Debug.WriteLine($"[SavePostService] Moved post {postId} to '{record.Folder}'");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] MovePostToFolderAsync error: {ex}");
                return Task.FromResult(false);
            }
        }

        // Get all saved posts hydrated with full Post objects
        public static async Task<List<SavedPostItem>> GetSavedPostsWithFoldersAsync(string currentUserPhone)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                Debug.WriteLine($"[SavePostService] GetSavedPostsWithFoldersAsync: {records.Count} records");

                var result = new List<SavedPostItem>();

                foreach (var record in records.OrderByDescending(r => r.At))
                {
                    try
                    {
                        var post = await PostRepository.GetByIdAsync(record.PostId);
                        if (post == null)
                        {
                            Debug.WriteLine($"[SavePostService] Post {record.PostId} no longer exists, skipping");
                            continue;
                        }

                        result.Add(new SavedPostItem
                        {
                            Post = post,
                            FolderName = record.Folder,
                            SavedAt = record.At
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SavePostService] Error hydrating post {record.PostId}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[SavePostService] Returning {result.Count} hydrated items");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] GetSavedPostsWithFoldersAsync error: {ex}");
                return new List<SavedPostItem>();
            }
        }

        // Legacy - returns posts only
        public static async Task<List<Post>> GetSavedPostsAsync(string currentUserPhone)
        {
            var items = await GetSavedPostsWithFoldersAsync(currentUserPhone);
            return items.Select(s => s.Post).ToList();
        }

        // Folder stats
        public static Dictionary<string, int> GetFolderStats(string currentUserPhone)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                return records
                    .GroupBy(r => string.IsNullOrEmpty(r.Folder) ? "Saved" : r.Folder)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SavePostService] GetFolderStats error: {ex}");
                return new Dictionary<string, int>();
            }
        }

        public static async Task<List<SavedPostItem>> GetPostsByFolderAsync(string currentUserPhone,
            string folderName)
        {
            var all = await GetSavedPostsWithFoldersAsync(currentUserPhone);
            if (folderName == "All") return all;
            return all.Where(s => string.Equals(s.FolderName, folderName,
                StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static Task<bool> CreateFolderAsync(string currentUserPhone, string folderName)
            => Task.FromResult(true); // folders are implicit

        public static Task<bool> DeleteFolderAsync(string currentUserPhone, string folderName)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                foreach (var r in records.Where(r => string.Equals(r.Folder, folderName,
                    StringComparison.OrdinalIgnoreCase)))
                    r.Folder = "Saved";
                WriteRecords(currentUserPhone, records);
                return Task.FromResult(true);
            }
            catch { return Task.FromResult(false); }
        }

        public static Task<bool> RenameFolderAsync(string currentUserPhone, string oldName,
            string newName)
        {
            try
            {
                var records = ReadRecords(currentUserPhone);
                foreach (var r in records.Where(r => string.Equals(r.Folder, oldName,
                    StringComparison.OrdinalIgnoreCase)))
                    r.Folder = newName;
                WriteRecords(currentUserPhone, records);
                return Task.FromResult(true);
            }
            catch { return Task.FromResult(false); }
        }
    }

    // ── Model ─────────────────────────────────────────────────────────────────

    public class SavedPostItem
    {
        public Post Post { get; set; } = new Post();
        public string FolderName { get; set; } = "Saved";
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }
}