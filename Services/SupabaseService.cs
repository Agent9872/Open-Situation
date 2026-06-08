using Lock.Models;
using Lock.Models.Chat;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace Lock.Services
{
    public static class SupabaseService
    {
        private static readonly HttpClient _http = new();
        private static bool _headersSet = false;

        private static void EnsureHeaders()
        {
            if (_headersSet) return;
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", SupabaseConfig.AnonKey);
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
            _headersSet = true;
        }

        private static string Url => $"{SupabaseConfig.Url}/rest/v1";

        // ── CORE HELPERS ──────────────────────────────────────────────────────

        public static async Task<List<T>> GetAsync<T>(string table, string query = "")
        {
            try
            {
                EnsureHeaders();
                var url = $"{Url}/{table}{(string.IsNullOrEmpty(query) ? "" : "?" + query)}";
                var response = await _http.GetStringAsync(url);
                return JsonConvert.DeserializeObject<List<T>>(response) ?? new();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAsync<{typeof(T).Name}> error: {ex.Message}");
                return new();
            }
        }

        public static async Task<T?> GetOneAsync<T>(string table, string query)
        {
            var list = await GetAsync<T>(table, query + "&limit=1");
            return list.FirstOrDefault();
        }

        public static async Task<bool> InsertAsync<T>(string table, T item)
        {
            try
            {
                EnsureHeaders();
                var json = JsonConvert.SerializeObject(item, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
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

                // NOTE: DefaultValueHandling.Ignore has been intentionally removed.
                // It was stripping valid false/0 fields (e.g. IsBanned, Smokes, CoinBalance)
                // and causing Supabase to reject inserts due to missing required data.
                var json = JsonConvert.SerializeObject(item, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}");
                request.Headers.Add("Prefer", "return=representation");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[SUPABASE] INSERT {table} → {(int)response.StatusCode}");
                Debug.WriteLine($"[SUPABASE] Body: {body}");

                if (!response.IsSuccessStatusCode)
                    return default;

                // Supabase returns array on success
                if (body.TrimStart().StartsWith("["))
                {
                    var list = JsonConvert.DeserializeObject<List<T>>(body);
                    return list != null && list.Count > 0 ? list[0] : default;
                }

                return default;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InsertAndReturnAsync error: {ex.Message}");
                return default;
            }
        }

        public static async Task<bool> UpsertAsync<T>(string table, T item, string onConflict = "")
        {
            try
            {
                EnsureHeaders();
                var json = JsonConvert.SerializeObject(item, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var conflictParam = string.IsNullOrEmpty(onConflict) ? "" : $"?on_conflict={onConflict}";
                var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/{table}{conflictParam}");
                request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpsertAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> UpdateAsync(string table, string query, object patch)
        {
            try
            {
                EnsureHeaders();
                var json = JsonConvert.SerializeObject(patch);
                var request = new HttpRequestMessage(HttpMethod.Patch, $"{Url}/{table}?{query}");
                request.Headers.Add("Prefer", "return=minimal");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.SendAsync(request);
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
                var response = await _http.DeleteAsync($"{Url}/{table}?{query}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteAsync error: {ex.Message}");
                return false;
            }
        }

        // ── USERS ──────────────────────────────────────────────────────────────

        public static Task<User?> GetUserByPhoneAsync(string phone) =>
            GetOneAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}");

        public static Task<bool> UpsertUserAsync(User user) =>
            UpsertAsync("Users", user, "PhoneNumber");

        public static Task<bool> UpdateUserAsync(string phone, object patch) =>
            UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}", patch);

        // ── POSTS ──────────────────────────────────────────────────────────────

        public static Task<List<Post>> GetPostsAsync(int limit = 50, int offset = 0) =>
            GetAsync<Post>("Posts", $"order=CreatedAt.desc&limit={limit}&offset={offset}");

        public static Task<List<Post>> GetPostsByUserAsync(string phone) =>
            GetAsync<Post>("Posts", $"AuthorPhone=eq.{Uri.EscapeDataString(phone)}&order=CreatedAt.desc");

        public static Task<Post?> InsertPostAsync(Post post) =>
            InsertAndReturnAsync("Posts", post);

        public static Task<bool> UpdatePostAsync(int postId, object patch) =>
            UpdateAsync("Posts", $"Id=eq.{postId}", patch);

        public static Task<bool> DeletePostAsync(int postId) =>
            DeleteAsync("Posts", $"Id=eq.{postId}");

        // ── COMMENTS ──────────────────────────────────────────────────────────

        public static Task<List<Lock.Models.Comment>> GetCommentsAsync(int postId) =>
            GetAsync<Lock.Models.Comment>("Comments",
                $"PostId=eq.{postId}&order=CreatedAt.asc");

        public static Task<bool> InsertCommentAsync(Lock.Models.Comment comment) =>
            InsertAsync("Comments", comment);

        public static Task<bool> DeleteCommentAsync(int commentId) =>
            DeleteAsync("Comments", $"Id=eq.{commentId}");

        // ── CHAT MESSAGES ──────────────────────────────────────────────────────

        public static Task<List<ChatMessage>> GetMessagesAsync(string conversationId) =>
            GetAsync<ChatMessage>("ChatMessages",
                $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&order=SentAt.asc");

        public static Task<bool> InsertMessageAsync(ChatMessage message) =>
            InsertAsync("ChatMessages", message);

        public static Task<bool> UpdateMessageAsync(int messageId, object patch) =>
            UpdateAsync("ChatMessages", $"Id=eq.{messageId}", patch);

        public static Task<bool> DeleteMessageAsync(int messageId) =>
            DeleteAsync("ChatMessages", $"Id=eq.{messageId}");

        // ── CONVERSATIONS ──────────────────────────────────────────────────────

        public static Task<List<Conversation>> GetConversationsAsync(string phone) =>
            GetAsync<Conversation>("Conversations",
                $"or=(ParticipantA.eq.{Uri.EscapeDataString(phone)},ParticipantB.eq.{Uri.EscapeDataString(phone)})&order=LastMessageAt.desc");

        public static Task<bool> UpsertConversationAsync(Conversation conv) =>
            UpsertAsync("Conversations", conv, "ConversationId");

        public static Task<bool> UpdateConversationAsync(string conversationId, object patch) =>
            UpdateAsync("Conversations",
                $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}", patch);

        // ── MESSAGE REQUESTS ───────────────────────────────────────────────────

        public static Task<List<MessageRequest>> GetMessageRequestsAsync(string phone) =>
            GetAsync<MessageRequest>("MessageRequests",
                $"RecipientPhone=eq.{Uri.EscapeDataString(phone)}&IsAccepted=eq.false&IsDeclined=eq.false");

        public static Task<bool> InsertMessageRequestAsync(MessageRequest request) =>
            InsertAsync("MessageRequests", request);

        public static Task<bool> AcceptMessageRequestAsync(int requestId) =>
            UpdateAsync("MessageRequests", $"Id=eq.{requestId}",
                new { IsAccepted = true, AcceptedAt = DateTime.UtcNow });

        public static Task<bool> DeclineMessageRequestAsync(int requestId) =>
            UpdateAsync("MessageRequests", $"Id=eq.{requestId}",
                new { IsDeclined = true, DeclinedAt = DateTime.UtcNow });

        // ── BLOCKED USERS ──────────────────────────────────────────────────────

        public static Task<List<BlockedUser>> GetBlockedUsersAsync(string phone) =>
            GetAsync<BlockedUser>("BlockedUsers",
                $"UserPhone=eq.{Uri.EscapeDataString(phone)}");

        public static Task<bool> BlockUserAsync(string userPhone, string blockedPhone) =>
            InsertAsync("BlockedUsers", new
            {
                UserPhone = userPhone,
                BlockedPhone = blockedPhone,
                BlockedAt = DateTime.UtcNow
            });

        public static Task<bool> UnblockUserAsync(string userPhone, string blockedPhone) =>
            DeleteAsync("BlockedUsers",
                $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&BlockedPhone=eq.{Uri.EscapeDataString(blockedPhone)}");

        // ── GROUPS ─────────────────────────────────────────────────────────────

        public static Task<bool> InsertGroupAsync(Group group) =>
            InsertAsync("Groups", group);

        public static Task<bool> InsertGroupMemberAsync(GroupMember member) =>
            InsertAsync("GroupMembers", member);

        public static Task<List<GroupMessage>> GetGroupMessagesAsync(string groupId) =>
            GetAsync<GroupMessage>("GroupMessages",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&order=SentAt.asc");

        public static Task<bool> InsertGroupMessageAsync(GroupMessage message) =>
            InsertAsync("GroupMessages", message);

        public static Task<List<GroupMember>> GetGroupMembersAsync(string groupId) =>
            GetAsync<GroupMember>("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}");

        // ── COINS ──────────────────────────────────────────────────────────────

        public static async Task<int> GetCoinBalanceAsync(string phone)
        {
            var user = await GetUserByPhoneAsync(phone);
            return user?.CoinBalance ?? 0;
        }

        public static Task<bool> UpdateCoinBalanceAsync(string phone, int newBalance) =>
            UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}",
                new { CoinBalance = newBalance });

        public static Task<bool> AddCoinTransactionAsync(
            string phone, int amount, string type,
            string reference = "", string description = "") =>
            InsertAsync("CoinTransactions", new
            {
                UserPhone = phone,
                Amount = amount,
                Type = type,
                Reference = reference,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });

        public static async Task<bool> CreditCoinsAsync(
            string phone, int coins, string reference)
        {
            int current = await GetCoinBalanceAsync(phone);
            bool ok = await UpdateCoinBalanceAsync(phone, current + coins);
            if (!ok) return false;
            await AddCoinTransactionAsync(phone, coins, "deposit",
                reference, $"Deposited {coins} coins");
            return true;
        }

        public static async Task<bool> DeductCoinsForGiftAsync(
            string senderPhone, string recipientPhone, string giftName, int cost)
        {
            int balance = await GetCoinBalanceAsync(senderPhone);
            if (balance < cost) return false;
            await UpdateCoinBalanceAsync(senderPhone, balance - cost);
            await AddCoinTransactionAsync(senderPhone, -cost, "gift_sent",
                description: $"Sent {giftName}");
            await AddCoinTransactionAsync(recipientPhone, cost, "gift_received",
                description: $"Received {giftName}");
            return true;
        }

        // ── USER PHOTOS ────────────────────────────────────────────────────────

        public static Task<List<UserPhoto>> GetUserPhotosAsync(int userId) =>
            GetAsync<UserPhoto>("UserPhotos", $"UserId=eq.{userId}&order=Order.asc");

        public static Task<bool> InsertUserPhotoAsync(UserPhoto photo) =>
            InsertAsync("UserPhotos", photo);

        public static Task<bool> DeleteUserPhotoAsync(int photoId) =>
            DeleteAsync("UserPhotos", $"Id=eq.{photoId}");

        // ── USER PROMPTS ───────────────────────────────────────────────────────

        public static Task<List<UserPrompt>> GetUserPromptsAsync(int userId) =>
            GetAsync<UserPrompt>("UserPrompts", $"UserId=eq.{userId}&order=Order.asc");

        public static Task<bool> UpsertUserPromptAsync(UserPrompt prompt) =>
            UpsertAsync("UserPrompts", prompt, "Id");

        // ── SEEN POSTS ─────────────────────────────────────────────────────────

        public static Task<bool> MarkPostSeenAsync(string phone, int postId) =>
            InsertAsync("SeenPosts", new
            {
                UserPhone = phone,
                PostId = postId,
                SeenAt = DateTime.UtcNow
            });

        public static Task<List<SeenPost>> GetSeenPostsAsync(string phone) =>
            GetAsync<SeenPost>("SeenPosts",
                $"UserPhone=eq.{Uri.EscapeDataString(phone)}");

        // ── EMERGENCY CONTACTS ─────────────────────────────────────────────────

        public static Task<List<EmergencyContact>> GetEmergencyContactsAsync(string phone) =>
            GetAsync<EmergencyContact>("EmergencyContacts",
                $"UserPhone=eq.{Uri.EscapeDataString(phone)}");

        public static Task<bool> InsertEmergencyContactAsync(EmergencyContact contact) =>
            InsertAsync("EmergencyContacts", contact);

        public static Task<bool> DeleteEmergencyContactAsync(int id) =>
            DeleteAsync("EmergencyContacts", $"Id=eq.{id}");

        // ── ADMIN TRACKING ─────────────────────────────────────────────────────

        public static Task<bool> TrackMoodChangeAsync(
            string phone, string oldMood, string newMood) =>
            InsertAsync("UserMoodTracking", new
            {
                UserPhone = phone,
                OldMood = oldMood,
                NewMood = newMood,
                Timestamp = DateTime.UtcNow
            });

        public static Task<bool> TrackLoginAsync(
            string phone, string ip = "", string device = "") =>
            InsertAsync("UserLoginTracking", new
            {
                UserPhone = phone,
                LoginTime = DateTime.UtcNow,
                IpAddress = ip,
                DeviceInfo = device
            });

        public static Task<bool> TrackProfileChangeAsync(
            string phone, string field, string oldVal, string newVal) =>
            InsertAsync("UserProfileTracking", new
            {
                UserPhone = phone,
                FieldChanged = field,
                OldValue = oldVal,
                NewValue = newVal,
                Timestamp = DateTime.UtcNow
            });
    }
}