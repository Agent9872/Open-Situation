using Lock.Models;
using Lock.Models.Chat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class SupabaseService
    {
        private static readonly HttpClient _http = new();
        private static bool _headersSet = false;

        // Serializer settings for PascalCase (default) and snake_case (fallback)
        private static readonly JsonSerializerSettings PascalSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver() // preserves PascalCase property names
        };

        private static readonly JsonSerializerSettings SnakeSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        private static void EnsureHeaders()
        {
            if (_headersSet) return;

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", SupabaseConfig.AnonKey);
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.Timeout = TimeSpan.FromSeconds(30);

            _headersSet = true;
        }

        private static string Url => $"{SupabaseConfig.Url}/rest/v1";

        // Read raw response while preserving status code for better diagnostics
        private static async Task<(bool Ok, string Body, int StatusCode)> GetRawAsync(string url)
        {
            try
            {
                var resp = await _http.GetAsync(url).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"[SUPABASE] GET {url} → {(int)resp.StatusCode}");
#if DEBUG
                var preview = body?.Length > 2000 ? body.Substring(0, 2000) + "..." : body;
                Debug.WriteLine($"[SUPABASE] Response preview: {preview}");
#endif
                return (resp.IsSuccessStatusCode, body ?? string.Empty, (int)resp.StatusCode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetRawAsync error for {url}: {ex}");
                return (false, string.Empty, 0);
            }
        }

        // Inspect JSON and pick serializer settings automatically (snake_case vs PascalCase)
        private static JsonSerializerSettings ChooseSettingsForResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return PascalSerializerSettings;

            try
            {
                var token = JToken.Parse(json);

                // If array, find first object
                JObject firstObj = null;
                if (token is JArray arr && arr.Count > 0 && arr[0] is JObject obj0)
                    firstObj = obj0;
                else if (token is JObject obj)
                    firstObj = obj;

                if (firstObj != null)
                {
                    foreach (var prop in firstObj.Properties())
                    {
                        var name = prop.Name;
                        // if any property contains an underscore, assume snake_case
                        if (name.Contains('_')) return SnakeSerializerSettings;
                        // if all-lowercase and contains letters, consider snake as well
                        if (name.Any(char.IsLetter) && name.All(c => char.IsLower(c) || c == '_')) return SnakeSerializerSettings;
                    }
                }
            }
            catch
            {
                // fall back quietly
            }

            return PascalSerializerSettings;
        }

        // ── CORE HELPERS ──────────────────────────────────────────────────────

        public static async Task<List<T>> GetAsync<T>(string table, string query = "")
        {
            try
            {
                EnsureHeaders();
                var url = $"{Url}/{table}{(string.IsNullOrEmpty(query) ? "" : "?" + query)}";
                var (ok, body, status) = await GetRawAsync(url).ConfigureAwait(false);
                if (!ok)
                {
                    Debug.WriteLine($"GetAsync<{typeof(T).Name}> failed status={status}");
                    return new List<T>();
                }

                var settings = ChooseSettingsForResponse(body);
                var list = JsonConvert.DeserializeObject<List<T>>(body, settings);
                return list ?? new List<T>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAsync<{typeof(T).Name}> error: {ex.Message}");
                return new List<T>();
            }
        }

        public static async Task<T?> GetOneAsync<T>(string table, string query)
        {
            // Ensure limit=1 is appended safely
            string finalQuery = query;
            if (!finalQuery.Contains("limit=", StringComparison.OrdinalIgnoreCase))
                finalQuery = string.IsNullOrEmpty(finalQuery) ? "limit=1" : finalQuery + "&limit=1";

            var list = await GetAsync<T>(table, finalQuery).ConfigureAwait(false);
            return list.FirstOrDefault();
        }

        public static async Task<bool> InsertAsync<T>(string table, T item)
        {
            try
            {
                EnsureHeaders();

                var json = JsonConvert.SerializeObject(item, PascalSerializerSettings);

                // Remove Id=0 so Supabase auto-generates it (if present)
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dict != null && dict.TryGetValue("Id", out var idVal) &&
                    (idVal?.ToString() == "0" || idVal?.ToString() == ""))
                {
                    dict.Remove("Id");
                    json = JsonConvert.SerializeObject(dict, PascalSerializerSettings);
                }

                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Debug.WriteLine($"[SUPABASE] INSERT {table} → {(int)response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                    Debug.WriteLine($"[SUPABASE] INSERT {table} FAILED: {body}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InsertAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<T?> InsertAndReturnAsync<T>(string table, T item)
        {
            try
            {
                EnsureHeaders();

                var json = JsonConvert.SerializeObject(item, PascalSerializerSettings);

                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dict != null && dict.TryGetValue("Id", out var idVal) &&
                    (idVal?.ToString() == "0" || idVal?.ToString() == ""))
                {
                    dict.Remove("Id");
                    json = JsonConvert.SerializeObject(dict, PascalSerializerSettings);
                }

                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}");
                request.Headers.Add("Prefer", "return=representation");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Debug.WriteLine($"[SUPABASE] INSERT {table} → {(int)response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[SUPABASE] Error: {body}");
                    return default;
                }

                // Choose settings by returned JSON
                var settings = ChooseSettingsForResponse(body);

                if (body.TrimStart().StartsWith("["))
                {
                    var list = JsonConvert.DeserializeObject<List<T>>(body, settings);
                    return list?.Count > 0 ? list[0] : default;
                }

                return JsonConvert.DeserializeObject<T>(body, settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InsertAndReturnAsync error: {ex.Message}");
                return default;
            }
        }

        public static async Task<T?> InsertPayloadAndReturnAsync<T>(string table, object payload)
        {
            try
            {
                EnsureHeaders();

                var json = JsonConvert.SerializeObject(payload, PascalSerializerSettings);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}");
                request.Headers.Add("Prefer", "return=representation");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Debug.WriteLine($"[SUPABASE] INSERT PAYLOAD {table} → {(int)response.StatusCode}");
                Debug.WriteLine($"[SUPABASE] Sent: {json}");
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[SUPABASE] Error: {body}");
                    return default;
                }

                var settings = ChooseSettingsForResponse(body);
                if (body.TrimStart().StartsWith("["))
                {
                    var list = JsonConvert.DeserializeObject<List<T>>(body, settings);
                    return list?.Count > 0 ? list[0] : default;
                }

                return JsonConvert.DeserializeObject<T>(body, settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InsertPayloadAndReturnAsync error: {ex.Message}");
                return default;
            }
        }

        public static async Task<bool> UpsertAsync<T>(string table, T item, string onConflict = "")
        {
            try
            {
                EnsureHeaders();
                var json = JsonConvert.SerializeObject(item, PascalSerializerSettings);
                var conflictParam = string.IsNullOrEmpty(onConflict) ? "" : $"?on_conflict={onConflict}";
                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}{conflictParam}");
                request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Debug.WriteLine($"[SUPABASE] UPSERT {table} → {(int)response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                    Debug.WriteLine($"[SUPABASE] UPSERT {table} FAILED: {body}");

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpsertAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// PATCH a row. Pass an anonymous object with ONLY the columns you want to update.
        /// Property names must match your Supabase column names exactly (PascalCase preserved by default).
        /// </summary>
        public static async Task<bool> UpdateAsync(string table, string query, object patch)
        {
            try
            {
                EnsureHeaders();

                var json = JsonConvert.SerializeObject(patch, PascalSerializerSettings);

                Debug.WriteLine($"[SUPABASE] PATCH {table}?{query}");
                Debug.WriteLine($"[SUPABASE] Payload: {json}");

                var request = new HttpRequestMessage(HttpMethod.Patch, $"{Url}/{table}?{query}");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[SUPABASE] PATCH FAILED {(int)response.StatusCode}: {body}");
                }
                else
                {
                    Debug.WriteLine($"[SUPABASE] PATCH OK {(int)response.StatusCode}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DeleteAsync(string table, string query)
        {
            try
            {
                EnsureHeaders();
                var response = await _http.DeleteAsync($"{Url}/{table}?{query}").ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"[SUPABASE] DELETE {table} → {(int)response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[SUPABASE] DELETE {table} FAILED: {body}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteAsync error: {ex.Message}");
                return false;
            }
        }

        // ── STORAGE ────────────────────────────────────────────────────────────

        public static async Task<string?> UploadFileAsync(
            string bucket, string localFilePath, string fileName)
        {
            try
            {
                if (!System.IO.File.Exists(localFilePath))
                {
                    Debug.WriteLine($"[STORAGE] File not found: {localFilePath}");
                    return null;
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(localFilePath).ConfigureAwait(false);
                var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                var mime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "application/octet-stream"
                };

                var objectPath = $"{bucket}/{fileName}";
                var uploadUrl = $"{SupabaseConfig.Url}/storage/v1/object/{objectPath}";

                var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Add("apikey", SupabaseConfig.AnonKey);
                request.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
                using var postContent = new ByteArrayContent(bytes);
                postContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                request.Content = postContent;

                var response = await _http.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[STORAGE] POST failed ({(int)response.StatusCode}), retrying with PUT upsert");

                    var putRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                    putRequest.Headers.Add("apikey", SupabaseConfig.AnonKey);
                    putRequest.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
                    putRequest.Headers.Add("x-upsert", "true");
                    using var putContent = new ByteArrayContent(bytes);
                    putContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                    putRequest.Content = putContent;

                    response = await _http.SendAsync(putRequest).ConfigureAwait(false);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Debug.WriteLine($"[STORAGE] Upload failed: {err}");
                    return null;
                }

                var publicUrl = $"{SupabaseConfig.Url}/storage/v1/object/public/{objectPath}";
                Debug.WriteLine($"[STORAGE] Uploaded successfully → {publicUrl}");
                return publicUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UploadFileAsync error: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> DeleteStorageFileAsync(string bucket, string fileName)
        {
            try
            {
                var deleteUrl = $"{SupabaseConfig.Url}/storage/v1/object/{bucket}/{fileName}";
                var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                request.Headers.Add("apikey", SupabaseConfig.AnonKey);
                request.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
                var response = await _http.SendAsync(request).ConfigureAwait(false);
                Debug.WriteLine($"[STORAGE] DELETE {bucket}/{fileName} → {(int)response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteStorageFileAsync error: {ex.Message}");
                return false;
            }
        }

        public static string? ExtractFileNameFromStorageUrl(string bucket, string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var marker = $"/storage/v1/object/public/{bucket}/";
            var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return url[(idx + marker.Length)..];
        }

        // ── TYPED HELPERS (USERS, POSTS, CHAT, GROUPS, COINS, ETC.) ───────────

        public static Task<User?> GetUserByPhoneAsync(string phone) =>
            GetOneAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}");

        public static Task<bool> UpsertUserAsync(User user) =>
            UpsertAsync("Users", user, "PhoneNumber");

        public static Task<bool> UpdateUserAsync(string phone, object patch) =>
            UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}", patch);

        public static Task<List<Post>> GetPostsAsync(int limit = 50, int offset = 0) =>
            GetAsync<Post>("Posts", $"order=CreatedAt.desc&limit={limit}&offset={offset}");

        public static Task<List<Post>> GetPostsByUserAsync(string phone) =>
            GetAsync<Post>("Posts", $"AuthorPhone=eq.{Uri.EscapeDataString(phone)}&order=CreatedAt.desc");

        public static Task<Post?> InsertPostAsync(Post post)
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
            return InsertPayloadAndReturnAsync<Post>("Posts", payload);
        }

        public static Task<bool> UpdatePostAsync(int postId, object patch) =>
            UpdateAsync("Posts", $"Id=eq.{postId}", patch);

        public static Task<bool> DeletePostAsync(int postId) =>
            DeleteAsync("Posts", $"Id=eq.{postId}");

        public static Task<List<Lock.Models.Comment>> GetCommentsAsync(int postId) =>
            GetAsync<Lock.Models.Comment>("Comments", $"PostId=eq.{postId}&order=CreatedAt.asc");

        public static Task<bool> InsertCommentAsync(Lock.Models.Comment comment) =>
            InsertAsync("Comments", new
            {
                PostId = comment.PostId,
                ParentCommentId = comment.ParentCommentId,
                AuthorPhone = comment.AuthorPhone,
                Content = comment.Content,
                LoveCount = comment.LoveCount,
                LovedByJson = comment.LovedByJson,
                CreatedAt = comment.CreatedAt
            });

        public static Task<bool> DeleteCommentAsync(int commentId) =>
            DeleteAsync("Comments", $"Id=eq.{commentId}");

        public static Task<List<ChatMessage>> GetMessagesAsync(string conversationId) =>
            GetAsync<ChatMessage>("ChatMessages", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&order=SentAt.asc");

        public static Task<bool> InsertMessageAsync(ChatMessage message) => InsertAsync("ChatMessages", message);

        public static Task<bool> UpdateMessageAsync(int messageId, object patch) =>
            UpdateAsync("ChatMessages", $"Id=eq.{messageId}", patch);

        public static Task<bool> DeleteMessageAsync(int messageId) =>
            DeleteAsync("ChatMessages", $"Id=eq.{messageId}");

        public static Task<List<Conversation>> GetConversationsAsync(string phone) =>
            GetAsync<Conversation>("Conversations", $"or=(ParticipantA.eq.{Uri.EscapeDataString(phone)},ParticipantB.eq.{Uri.EscapeDataString(phone)})&order=LastMessageAt.desc");

        public static Task<bool> UpsertConversationAsync(Conversation conv) => UpsertAsync("Conversations", conv, "ConversationId");

        public static Task<bool> UpdateConversationAsync(string conversationId, object patch) =>
            UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}", patch);

        public static Task<List<MessageRequest>> GetMessageRequestsAsync(string phone) =>
            GetAsync<MessageRequest>("MessageRequests", $"RecipientPhone=eq.{Uri.EscapeDataString(phone)}&IsAccepted=eq.false&IsDeclined=eq.false");

        public static Task<bool> InsertMessageRequestAsync(MessageRequest request) => InsertAsync("MessageRequests", request);

        public static Task<bool> AcceptMessageRequestAsync(int requestId) =>
            UpdateAsync("MessageRequests", $"Id=eq.{requestId}", new { IsAccepted = true, AcceptedAt = DateTime.UtcNow });

        public static Task<bool> DeclineMessageRequestAsync(int requestId) =>
            UpdateAsync("MessageRequests", $"Id=eq.{requestId}", new { IsDeclined = true, DeclinedAt = DateTime.UtcNow });

        public static Task<List<BlockedUser>> GetBlockedUsersAsync(string phone) =>
            GetAsync<BlockedUser>("BlockedUsers", $"UserPhone=eq.{Uri.EscapeDataString(phone)}");

        public static Task<bool> BlockUserAsync(string userPhone, string blockedPhone) =>
            InsertAsync("BlockedUsers", new { UserPhone = userPhone, BlockedPhone = blockedPhone, BlockedAt = DateTime.UtcNow });

        public static Task<bool> UnblockUserAsync(string userPhone, string blockedPhone) =>
            DeleteAsync("BlockedUsers", $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(blockedPhone)}");

        public static Task<bool> InsertGroupAsync(Group group) => InsertAsync("Groups", group);

        public static Task<bool> InsertGroupMemberAsync(GroupMember member) => InsertAsync("GroupMembers", member);

        public static Task<List<GroupMessage>> GetGroupMessagesAsync(string groupId) =>
            GetAsync<GroupMessage>("GroupMessages", $"GroupId=eq.{Uri.EscapeDataString(groupId)}&order=SentAt.asc");

        public static Task<bool> InsertGroupMessageAsync(GroupMessage message) => InsertAsync("GroupMessages", message);

        public static Task<List<GroupMember>> GetGroupMembersAsync(string groupId) =>
            GetAsync<GroupMember>("GroupMembers", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");

        public static async Task<int> GetCoinBalanceAsync(string phone)
        {
            var user = await GetUserByPhoneAsync(phone).ConfigureAwait(false);
            return user?.CoinBalance ?? 0;
        }

        public static Task<bool> UpdateCoinBalanceAsync(string phone, int newBalance) =>
            UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}", new { CoinBalance = newBalance });

        public static Task<bool> AddCoinTransactionAsync(string phone, int amount, string type, string reference = "", string description = "") =>
            InsertAsync("CoinTransactions", new { UserPhone = phone, Amount = amount, Type = type, Reference = reference, Description = description, CreatedAt = DateTime.UtcNow });

        public static async Task<bool> CreditCoinsAsync(string phone, int coins, string reference)
        {
            int current = await GetCoinBalanceAsync(phone).ConfigureAwait(false);
            bool ok = await UpdateCoinBalanceAsync(phone, current + coins).ConfigureAwait(false);
            if (!ok) return false;
            await AddCoinTransactionAsync(phone, coins, "deposit", reference, $"Deposited {coins} coins").ConfigureAwait(false);
            return true;
        }

        public static async Task<bool> DeductCoinsForGiftAsync(string senderPhone, string recipientPhone, string giftName, int cost)
        {
            int balance = await GetCoinBalanceAsync(senderPhone).ConfigureAwait(false);
            if (balance < cost) return false;
            await UpdateCoinBalanceAsync(senderPhone, balance - cost).ConfigureAwait(false);
            await AddCoinTransactionAsync(senderPhone, -cost, "gift_sent", "", $"Sent {giftName}").ConfigureAwait(false);
            await AddCoinTransactionAsync(recipientPhone, cost, "gift_received", "", $"Received {giftName}").ConfigureAwait(false);
            return true;
        }

        public static Task<List<UserPhoto>> GetUserPhotosAsync(int userId) => GetAsync<UserPhoto>("UserPhotos", $"UserId=eq.{userId}&order=Order.asc");

        public static Task<bool> InsertUserPhotoAsync(UserPhoto photo) => InsertAsync("UserPhotos", photo);

        public static Task<bool> DeleteUserPhotoAsync(int photoId) => DeleteAsync("UserPhotos", $"Id=eq.{photoId}");

        public static Task<List<UserPrompt>> GetUserPromptsAsync(int userId) => GetAsync<UserPrompt>("UserPrompts", $"UserId=eq.{userId}&order=Order.asc");

        public static Task<bool> UpsertUserPromptAsync(UserPrompt prompt) => UpsertAsync("UserPrompts", prompt, "Id");

        public static Task<bool> MarkPostSeenAsync(string phone, int postId) =>
            InsertAsync("SeenPosts", new { UserPhone = phone, PostId = postId, SeenAt = DateTime.UtcNow });

        public static Task<List<SeenPost>> GetSeenPostsAsync(string phone) =>
            GetAsync<SeenPost>("SeenPosts", $"UserPhone=eq.{Uri.EscapeDataString(phone)}");

        public static Task<List<EmergencyContact>> GetEmergencyContactsAsync(string phone) =>
            GetAsync<EmergencyContact>("EmergencyContacts", $"UserPhone=eq.{Uri.EscapeDataString(phone)}");

        public static Task<bool> InsertEmergencyContactAsync(EmergencyContact contact) => InsertAsync("EmergencyContacts", contact);

        public static Task<bool> DeleteEmergencyContactAsync(int id) => DeleteAsync("EmergencyContacts", $"Id=eq.{id}");

        public static Task<bool> TrackMoodChangeAsync(string phone, string oldMood, string newMood) =>
            InsertAsync("UserMoodTracking", new { UserPhone = phone, OldMood = oldMood, NewMood = newMood, Timestamp = DateTime.UtcNow });

        public static Task<bool> TrackLoginAsync(string phone, string ip = "", string device = "") =>
            InsertAsync("UserLoginTracking", new { UserPhone = phone, LoginTime = DateTime.UtcNow, IpAddress = ip, DeviceInfo = device });

        public static Task<bool> TrackProfileChangeAsync(string phone, string field, string oldVal, string newVal) =>
            InsertAsync("UserProfileTracking", new { UserPhone = phone, FieldChanged = field, OldValue = oldVal, NewValue = newVal, Timestamp = DateTime.UtcNow });
    }
}