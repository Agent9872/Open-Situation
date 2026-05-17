using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Lock.Models;
using SQLite;
using Microsoft.Maui.Storage;
using Lock.Chat.Services;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Lock.Services.Admin;

namespace Lock.Services
{
    // Enhanced auth service that works with JWT backend API
    public static class AuthService
    {
        private const string CurrentUserPhoneKey = "current_user_phone";
        private const string AuthTokenKey = "auth_token";
        private const string RefreshTokenKey = "refresh_token";
        private const string TokenExpiryKey = "token_expiry";

        private static readonly HttpClient _httpClient = new HttpClient();
        private static string? _currentToken;
        private static string? _currentRefreshToken;
        private static DateTime _tokenExpiry;

        // Very basic phone validation: digits, optional +, length 7-15
        private static readonly Regex PhoneRegex = new(@"^\+?\d{7,15}$", RegexOptions.Compiled);

        static AuthService()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.BaseAddress = new Uri(ApiConfig.BaseUrl);
        }

        // Normalize phone number by removing all non-digit characters except leading +
        private static string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            // Check if it starts with + (international format)
            bool hasPlus = phone.Trim().StartsWith("+");

            // Remove all non-digit characters
            var digits = new string(phone.Where(c => char.IsDigit(c)).ToArray());

            // Add back the + if it was present
            return hasPlus ? "+" + digits : digits;
        }

        // Load tokens from secure storage on app start
        public static async Task LoadStoredTokensAsync()
        {
            try
            {
                _currentToken = await SecureStorage.GetAsync(AuthTokenKey);
                _currentRefreshToken = await SecureStorage.GetAsync(RefreshTokenKey);
                var expiryStr = await SecureStorage.GetAsync(TokenExpiryKey);

                if (DateTime.TryParse(expiryStr, out var expiry))
                    _tokenExpiry = expiry;

                // ✅ Re-sync role from DB on app restart (in case Preferences was cleared)
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrEmpty(phone))
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var user = await db.Table<User>()
                                       .Where(u => u.PhoneNumber == phone)
                                       .FirstOrDefaultAsync();
                    if (user != null)
                        Preferences.Set("current_user_role", user.Role);
                }

                Debug.WriteLine($"Tokens loaded. Valid: {IsAuthenticated}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tokens: {ex.Message}");
            }
        }

        // Check if user is authenticated with valid token
        public static bool IsAuthenticated =>
            !string.IsNullOrEmpty(_currentToken) && _tokenExpiry > DateTime.UtcNow;

        // Get valid token (auto-refresh if needed)
        public static async Task<string?> GetValidTokenAsync()
        {
            // Check if token is still valid (with 5-minute buffer)
            if (!string.IsNullOrEmpty(_currentToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
            {
                return _currentToken;
            }

            // Try to refresh token if we have a refresh token
            if (!string.IsNullOrEmpty(_currentRefreshToken))
            {
                if (await RefreshTokenAsync())
                {
                    return _currentToken;
                }
            }

            return null;
        }

        // Refresh the JWT token
        private static async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(ApiConfig.Endpoints.Refresh, new
                {
                    refreshToken = _currentRefreshToken
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _currentToken = result.GetProperty("token").GetString();
                    _currentRefreshToken = result.GetProperty("refreshToken").GetString();
                    _tokenExpiry = result.GetProperty("expiresAt").GetDateTime();

                    // Store updated tokens
                    await SecureStorage.SetAsync(AuthTokenKey, _currentToken);
                    await SecureStorage.SetAsync(RefreshTokenKey, _currentRefreshToken);
                    await SecureStorage.SetAsync(TokenExpiryKey, _tokenExpiry.ToString("O"));

                    Debug.WriteLine("Token refreshed successfully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Token refresh failed: {ex.Message}");
            }

            return false;
        }

        // Async register that persists to local SQLite AND backend API
        public static async Task<(bool Success, string Error)> RegisterAsync(
       string name,
       string phone,
       string password,
       DateTime dob,
       string gender,
       string interest = "",
       string profileImagePath = "",
       string coverImagePath = "",
       string mood = "",
       string energyLevel = "",
       string country = "",
       string state = "",
       string bio = "",
       string interests = "",
       string drinks = "",
       bool smokes = false,
       bool hasPets = false,
       string religion = "",
       string politicalViews = "",
       string musicGenres = "",
       string favoriteArtists = "",
       string favoriteMovies = "",
       string favoriteBooks = "",
       string languages = "",
       string occupation = "",
       string education = "",
       string prompts = "",
       string dealbreakers = "",
       string topInterest = "",
       string topArtist = "",
       string topMovie = "",
       string sexualOrientation = "",
       bool isVerified = false,
       bool allowMoodSearch = true,
       bool ghostModeMoodShield = false,
       string ipAddress = "")
        {
            await DatabaseService.InitializeAsync();

            if (string.IsNullOrWhiteSpace(name))
                return (false, "Full name is required.");

            if (string.IsNullOrWhiteSpace(phone))
                return (false, "Phone number is required.");

            string normalizedPhone = NormalizePhoneNumber(phone);
            if (string.IsNullOrEmpty(normalizedPhone) || normalizedPhone.Length < 7)
                return (false, "Enter a valid phone number (at least 7 digits).");

            if (password.Length < 4)
                return (false, "Password must be at least 4 characters.");

            var age = DateTime.Today.Year - dob.Year;
            if (dob > DateTime.Today.AddYears(-age)) age--;
            if (age < 18)
                return (false, "You must be at least 18 years old to register.");

            if (string.IsNullOrWhiteSpace(gender))
                return (false, "Please select your gender.");

            var db = DatabaseService.GetConnection();

            // Check if user already exists locally
            var existing = await db.Table<User>()
                .Where(u => u.PhoneNumber == normalizedPhone)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // Block permanently banned phone numbers from re-registering
                if (existing.IsBanned && existing.BanType == "permanent")
                    return (false,
                        $"This phone number has been permanently banned and cannot be used to create an account.\nReason: {(string.IsNullOrEmpty(existing.BanReason) ? "Violation of terms of service" : existing.BanReason)}");

                return (false, "A user with this phone number already exists.");
            }

            // Create user object
            var user = new User(
                name: name,
                phoneNumber: normalizedPhone,
                password: password,
                dateOfBirth: dob,
                gender: gender,
                interest: interest,
                profileImagePath: profileImagePath,
                coverImagePath: coverImagePath,
                mood: mood,
                energyLevel: energyLevel,
                country: country,
                state: state,
                bio: bio,
                interests: interests,
                drinks: drinks,
                smokes: smokes,
                hasPets: hasPets,
                religion: religion,
                politicalViews: politicalViews,
                sexualOrientation: sexualOrientation
            );

            // Assign additional fields
            user.MusicGenres = musicGenres ?? string.Empty;
            user.FavoriteArtists = favoriteArtists ?? string.Empty;
            user.FavoriteMovies = favoriteMovies ?? string.Empty;
            user.FavoriteBooks = favoriteBooks ?? string.Empty;
            user.Languages = languages ?? string.Empty;
            user.Occupation = occupation ?? string.Empty;
            user.Education = education ?? string.Empty;
            user.Prompts = prompts ?? string.Empty;
            user.Dealbreakers = dealbreakers ?? string.Empty;
            user.TopInterest = topInterest ?? string.Empty;
            user.TopArtist = topArtist ?? string.Empty;
            user.TopMovie = topMovie ?? string.Empty;
            user.AllowMoodSearch = allowMoodSearch;
            user.GhostModeMoodShield = ghostModeMoodShield;
            user.IsVerified = isVerified;

            // Store captured IP address
            user.IpAddress = ipAddress ?? string.Empty;
            Debug.WriteLine($"[AUTH] Registering user: {normalizedPhone} | IP: {(string.IsNullOrEmpty(ipAddress) ? "unavailable" : ipAddress)}");

            // First user ever = Admin, everyone else = User
            var totalUsers = await db.Table<User>().CountAsync();
            user.Role = totalUsers == 0 ? "Admin" : "User";

            await db.InsertAsync(user);

            // Also register/login to backend API to get JWT token
            var loginResult = await LoginWithApiAsync(normalizedPhone, password);
            if (!loginResult.Success)
            {
                Debug.WriteLine($"Warning: User registered locally but API login failed: {loginResult.Error}");
            }

            return (true, string.Empty);
        }

        // Login with backend API (returns JWT token)
        private static async Task<(bool Success, string Error, User? User)> LoginWithApiAsync(string phone, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(ApiConfig.Endpoints.Login, new
                {
                    phoneNumber = phone,
                    deviceId = await GetDeviceId()
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _currentToken = result.GetProperty("token").GetString();
                    _currentRefreshToken = result.GetProperty("refreshToken").GetString();
                    _tokenExpiry = result.GetProperty("expiresAt").GetDateTime();

                    // Store tokens securely
                    await SecureStorage.SetAsync(AuthTokenKey, _currentToken);
                    await SecureStorage.SetAsync(RefreshTokenKey, _currentRefreshToken);
                    await SecureStorage.SetAsync(TokenExpiryKey, _tokenExpiry.ToString("O"));

                    // Get user info from response
                    var userInfo = result.GetProperty("user");
                    var user = await GetUserByPhoneAsync(phone);

                    return (true, string.Empty, user);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, $"API login failed: {error}", null);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}", null);
            }
        }

        // Async login that checks credentials against backend API and local SQLite
        public static async Task<(bool Success, string Error, User? User)> LoginAsync(string phone, string password)
        {
            await DatabaseService.InitializeAsync();

            if (string.IsNullOrWhiteSpace(phone))
                return (false, "Phone number is required.", null);

            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password is required.", null);

            string normalizedPhone = NormalizePhoneNumber(phone);

            var db = DatabaseService.GetConnection();
            var user = await db.Table<User>()
                .Where(u => u.PhoneNumber == normalizedPhone)
                .FirstOrDefaultAsync();

            if (user == null)
                return (false, "User not found. Please check your phone number.", null);

            if (user.Password != password)
                return (false, "Incorrect password.", null);

            // ── BAN CHECK ──────────────────────────────────────────────────────────
            // Auto-lift expired temporary ban first
            if (user.IsBanned && user.BanType == "temporary" && user.BanExpiresAt.HasValue
                && DateTime.UtcNow >= user.BanExpiresAt.Value)
            {
                await UserService.UnbanUserAsync(normalizedPhone);
                // Ban has expired — re-fetch user with cleared ban fields
                user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == normalizedPhone)
                    .FirstOrDefaultAsync();
            }
            else if (user.IsBanned && user.BanType == "permanent")
            {
                return (false,
                    $"This account has been permanently banned.\nReason: {(string.IsNullOrEmpty(user.BanReason) ? "Violation of community guidelines" : user.BanReason)}",
                    null);
            }
            else if (user.IsBanned && user.BanType == "temporary")
            {
                return (false,
                    $"This account is suspended until {user.BanExpiresAt:MMM dd, yyyy 'at' h:mm tt} UTC.\nReason: {(string.IsNullOrEmpty(user.BanReason) ? "Violation of community guidelines" : user.BanReason)}",
                    null);
            }
            // ───────────────────────────────────────────────────────────────────────

            // Authenticate with backend API to get JWT token
            var apiLoginResult = await LoginWithApiAsync(normalizedPhone, password);
            if (!apiLoginResult.Success)
            {
                Debug.WriteLine($"Warning: Local login successful but API auth failed: {apiLoginResult.Error}");
            }

            // Update last active timestamp
            user.LastActive = DateTime.UtcNow;
            await db.UpdateAsync(user);

            // Store current user phone + role in preferences
            Preferences.Set(CurrentUserPhoneKey, normalizedPhone);
            Preferences.Set("current_user_role", user.Role); // ✅ THIS was missing

            // ========== TRACK USER LOGIN ==========
            try
            {
                var deviceId = await GetDeviceId();
                await UserTrackingService.Instance.TrackUserLoginAsync(normalizedPhone, deviceId);
                Debug.WriteLine($"[TRACKING] User login tracked for: {normalizedPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to track login: {ex.Message}");
            }

            return (true, string.Empty, user);
        }

        // In AuthService.cs — replace MigrateExistingUserRolesAsync
        public static async Task MigrateExistingUserRolesAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var allUsers = await db.Table<User>()
                                       .OrderBy(u => u.JoinDate)
                                       .ToListAsync();

                if (allUsers.Count == 0) return;

                bool anyFixed = false;

                for (int i = 0; i < allUsers.Count; i++)
                {
                    var user = allUsers[i];
                    string correctRole = i == 0 ? "Admin" : (string.IsNullOrEmpty(user.Role) ? "User" : user.Role);

                    // Fix first user always (in case they were registered before role logic existed)
                    // Fix any user with empty/null role
                    if (user.Role != correctRole)
                    {
                        user.Role = correctRole;
                        await db.UpdateAsync(user);
                        anyFixed = true;
                        Debug.WriteLine($"[MIGRATION] Fixed {user.PhoneNumber} → {user.Role}");
                    }
                }

                // Always re-sync Preferences for the current logged-in user
                var currentPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrEmpty(currentPhone))
                {
                    var currentUser = allUsers.FirstOrDefault(u => u.PhoneNumber == currentPhone);
                    if (currentUser != null)
                    {
                        Preferences.Set("current_user_role", currentUser.Role);
                        Debug.WriteLine($"[MIGRATION] Synced Preferences → {currentUser.Role}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MIGRATION] Error: {ex.Message}");
            }
        }

        public static void Logout()
        {
            // Capture before clearing
            var refreshTokenToInvalidate = _currentRefreshToken;

            // ✅ Synchronous cleanup FIRST
            Preferences.Remove(CurrentUserPhoneKey);
            Preferences.Remove("current_user_role");
            _currentToken = null;
            _currentRefreshToken = null;
            _tokenExpiry = DateTime.MinValue;

            // Async cleanup in background
            _ = Task.Run(async () =>
            {
                try
                {
                    SecureStorage.Remove(AuthTokenKey);
                    SecureStorage.Remove(RefreshTokenKey);
                    SecureStorage.Remove(TokenExpiryKey);

                    if (!string.IsNullOrEmpty(refreshTokenToInvalidate))
                    {
                        await _httpClient.PostAsJsonAsync(ApiConfig.Endpoints.Logout, new
                        {
                            refreshToken = refreshTokenToInvalidate
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Logout async cleanup failed: {ex.Message}");
                }
            });
        }

        // Quick role checks — call these anywhere in the app
        public static string GetCurrentUserRole()
            => Preferences.Get("current_user_role", "User");

        public static bool IsCurrentUserAdmin()
            => GetCurrentUserRole() == "Admin";

        public static bool IsCurrentUserModerator()
            => GetCurrentUserRole() is "Moderator" or "Admin";

        public static async Task DiagnoseAdminAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var allUsers = await db.Table<User>()
                                       .OrderBy(u => u.JoinDate)
                                       .ToListAsync();

                Debug.WriteLine("========== ADMIN DIAGNOSIS ==========");
                Debug.WriteLine($"Total users in DB: {allUsers.Count}");

                foreach (var u in allUsers)
                    Debug.WriteLine($"  Phone: {u.PhoneNumber} | Role: '{u.Role}' | JoinDate: {u.JoinDate}");

                var currentPhone = Preferences.Get("current_user_phone", "NONE");
                var currentRole = Preferences.Get("current_user_role", "NONE");

                Debug.WriteLine($"Preferences → phone: {currentPhone}");
                Debug.WriteLine($"Preferences → role:  {currentRole}");
                Debug.WriteLine($"IsCurrentUserAdmin(): {IsCurrentUserAdmin()}");
                Debug.WriteLine("=====================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DIAGNOSIS] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks ban status for a phone number.
        /// Returns IsBanned, BanType, Reason, and ExpiresAt (null = permanent).
        /// Also auto-lifts expired temporary bans.
        /// </summary>
        public static async Task<(bool IsBanned, string BanType, string Reason, DateTime? ExpiresAt)> CheckBanStatusAsync(string phone)
        {
            try
            {
                if (string.IsNullOrEmpty(phone)) return (false, "", "", null);

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();

                if (user == null) return (false, "", "", null);

                // Auto-lift expired temporary ban
                if (user.IsBanned && user.BanType == "temporary" && user.BanExpiresAt.HasValue
                    && DateTime.UtcNow >= user.BanExpiresAt.Value)
                {
                    await UserService.UnbanUserAsync(phone);
                    return (false, "", "", null);
                }

                if (user.IsBanned)
                    return (
                        true,
                        user.BanType,
                        string.IsNullOrEmpty(user.BanReason) ? "Violation of community guidelines" : user.BanReason,
                        user.BanExpiresAt
                    );

                return (false, "", "", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckBanStatusAsync error: {ex.Message}");
                return (false, "", "", null);
            }
        }

      
        // Check if user is logged in (has valid token)
        public static bool IsUserLoggedIn()
        {
            return IsAuthenticated && !string.IsNullOrEmpty(Preferences.Get(CurrentUserPhoneKey, string.Empty));
        }

        // Get current user phone
        public static string GetCurrentUserPhone()
        {
            return Preferences.Get(CurrentUserPhoneKey, string.Empty);
        }

        // Get current user details from local DB
        public static async Task<User?> GetCurrentUserAsync()
        {
            var phone = GetCurrentUserPhone();
            if (string.IsNullOrEmpty(phone))
                return null;

            return await GetUserByPhoneAsync(phone);
        }

        // Get user by phone number
        private static async Task<User?> GetUserByPhoneAsync(string phone)
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            return await db.Table<User>()
                .Where(u => u.PhoneNumber == phone)
                .FirstOrDefaultAsync();
        }

        // Check if phone number is already registered
        public static async Task<bool> IsPhoneRegisteredAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            string normalizedPhone = NormalizePhoneNumber(phone);

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var user = await db.Table<User>()
                .Where(u => u.PhoneNumber == normalizedPhone)
                .FirstOrDefaultAsync();

            return user != null;
        }

        // Search for users by phone (useful for NewChatPage)
        public static async Task<User?> FindUserByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            string normalizedPhone = NormalizePhoneNumber(phone);

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            return await db.Table<User>()
                .Where(u => u.PhoneNumber == normalizedPhone)
                .FirstOrDefaultAsync();
        }

        // Search for users by partial phone or name
        public static async Task<List<User>> SearchUsersAsync(string searchText, string currentUserPhone)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return new List<User>();

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            string normalizedSearch = NormalizePhoneNumber(searchText);
            string searchLower = searchText.ToLowerInvariant().Trim();

            var allUsers = await db.Table<User>().ToListAsync();

            return allUsers
                .Where(u => u.PhoneNumber != currentUserPhone)
                .Where(u =>
                    (!string.IsNullOrEmpty(u.PhoneNumber) &&
                     NormalizePhoneNumber(u.PhoneNumber).Contains(normalizedSearch)) ||
                    (!string.IsNullOrEmpty(u.Name) &&
                     u.Name.ToLowerInvariant().Contains(searchLower))
                )
                .ToList();
        }

        // Add this method to your AuthService class
        public static async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var allUsers = await db.Table<User>()
                    .OrderByDescending(u => u.JoinDate)
                    .ToListAsync();

                return allUsers;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting all users: {ex.Message}");
                return new List<User>();
            }
        }

        // Get users with pagination
        public static async Task<List<User>> GetUsersPaginatedAsync(int pageNumber, int pageSize = 20)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var skip = (pageNumber - 1) * pageSize;
                var users = await db.Table<User>()
                    .OrderByDescending(u => u.JoinDate)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();

                return users;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting paginated users: {ex.Message}");
                return new List<User>();
            }
        }

        // Get user count
        public static async Task<int> GetUserCountAsync()
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                return await db.Table<User>().CountAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting user count: {ex.Message}");
                return 0;
            }
        }

        // Search users with filters
        public static async Task<List<User>> SearchUsersWithFiltersAsync(
            string searchText = "",
            string genderFilter = "",
            int? minAge = null,
            int? maxAge = null)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var query = db.Table<User>();

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLowerInvariant();
                    var allUsers = await query.ToListAsync();
                    return allUsers.Where(u =>
                        u.Name.ToLowerInvariant().Contains(searchLower) ||
                        u.PhoneNumber.Contains(searchText))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(genderFilter) && genderFilter != "All")
                {
                    var allUsers = await query.ToListAsync();
                    var filtered = allUsers.Where(u => u.Gender == genderFilter).ToList();

                    if (minAge.HasValue || maxAge.HasValue)
                    {
                        filtered = filtered.Where(u =>
                        {
                            var age = u.GetAge();
                            return (!minAge.HasValue || age >= minAge.Value) &&
                                   (!maxAge.HasValue || age <= maxAge.Value);
                        }).ToList();
                    }

                    return filtered.OrderByDescending(u => u.JoinDate).ToList();
                }

                var users = await query.ToListAsync();

                // Apply age filter (calculated in memory since Age is computed)
                if (minAge.HasValue || maxAge.HasValue)
                {
                    users = users.Where(u =>
                    {
                        var age = u.GetAge();
                        return (!minAge.HasValue || age >= minAge.Value) &&
                               (!maxAge.HasValue || age <= maxAge.Value);
                    }).ToList();
                }

                return users.OrderByDescending(u => u.JoinDate).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching users: {ex.Message}");
                return new List<User>();
            }
        }

        // Update user profile
        public static async Task<(bool Success, string Error)> UpdateUserAsync(User updatedUser)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var existing = await db.Table<User>()
                    .Where(u => u.PhoneNumber == updatedUser.PhoneNumber)
                    .FirstOrDefaultAsync();

                if (existing == null)
                    return (false, "User not found.");

                updatedUser.Id = existing.Id;
                await db.UpdateAsync(updatedUser);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Update failed: {ex.Message}");
            }
        }

        // Change password
        public static async Task<(bool Success, string Error)> ChangePasswordAsync(string phone, string oldPassword, string newPassword)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                string normalizedPhone = NormalizePhoneNumber(phone);
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == normalizedPhone)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return (false, "User not found.");

                if (user.Password != oldPassword)
                    return (false, "Current password is incorrect.");

                if (newPassword.Length < 4)
                    return (false, "New password must be at least 4 characters.");

                user.Password = newPassword;
                await db.UpdateAsync(user);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Password change failed: {ex.Message}");
            }
        }

        // Delete user account
        public static async Task<(bool Success, string Error)> DeleteAccountAsync(string phone, string password)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                string normalizedPhone = NormalizePhoneNumber(phone);
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == normalizedPhone)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return (false, "User not found.");

                if (user.Password != password)
                    return (false, "Incorrect password.");

                await db.DeleteAsync(user);

                if (GetCurrentUserPhone() == normalizedPhone)
                {
                    Logout();
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Account deletion failed: {ex.Message}");
            }
        }

      

        // Get device ID for API
        private static async Task<string> GetDeviceId()
        {
            var deviceId = await SecureStorage.GetAsync("device_id");
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                await SecureStorage.SetAsync("device_id", deviceId);
            }
            return deviceId;
        }

        // Get authorization header for API calls
        public static async Task<HttpClient> GetAuthenticatedHttpClientAsync()
        {
            var token = await GetValidTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            return _httpClient;
        }
    }
}