using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class UserService
    {
        private const string CurrentUserPhoneKey = "current_user_phone";

        public static async Task<bool> BanUserAsync(
            string phone,
            string banType,
            string reason = "",
            DateTime? expiresAt = null)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null)
                {
                    Debug.WriteLine($"BanUserAsync: user not found for phone {phone}");
                    return false;
                }

                user.IsBanned = true;
                user.BanType = banType;
                user.BanReason = reason;
                user.BannedAt = DateTime.UtcNow;
                user.BanExpiresAt = expiresAt;
                user.ModerationStatus = banType == "permanent" ? "perm_banned" : "temp_banned";
                user.ModerationNote = banType == "permanent"
                    ? $"Your account has been permanently banned.\nReason: {reason}"
                    : $"Your account has been temporarily suspended until {expiresAt:MMM dd, yyyy 'at' h:mm tt}.\nReason: {reason}";
                user.ModerationUpdatedAt = DateTime.UtcNow;

                var success = await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}", user);

                if (success)
                {
                    Debug.WriteLine($"BanUserAsync: {phone} banned ({banType}) expires={expiresAt}");
                }

                // Force logout if currently logged in
                var currentPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (currentPhone == phone)
                {
                    Preferences.Remove(CurrentUserPhoneKey);
                    MessagingCenter.Send<object, string>(
                        new object(), "UserForcedLogout", user.ModerationNote);
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BanUserAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> IssueWarningAsync(string phone, string warningMessage)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null) return false;

                user.HasWarning = true;
                user.WarningMessage = warningMessage;
                user.WarnedAt = DateTime.UtcNow;
                user.WarningAcknowledged = false;
                user.ModerationStatus = "warned";
                user.ModerationNote = $"You have received a warning from the moderation team.\n\n{warningMessage}";
                user.ModerationUpdatedAt = DateTime.UtcNow;

                var success = await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}", user);

                // If user is online, notify them immediately
                var currentPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (currentPhone == phone)
                {
                    MessagingCenter.Send<object, string>(
                        new object(), "UserWarningIssued", user.ModerationNote);
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IssueWarningAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> ResolveReportAsync(string phone, string note)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null) return false;

                user.ModerationStatus = "resolved";
                user.ModerationNote = string.IsNullOrEmpty(note)
                    ? "A report against your account was reviewed and resolved. No action was required."
                    : note;
                user.ModerationUpdatedAt = DateTime.UtcNow;

                return await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}", user);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResolveReportAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DismissReportAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null) return false;

                user.ModerationStatus = "dismissed";
                user.ModerationNote = "A report against your account was reviewed. No violation was found and no action was taken.";
                user.ModerationUpdatedAt = DateTime.UtcNow;

                return await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}", user);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DismissReportAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> UnbanUserAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null) return false;

                user.IsBanned = false;
                user.BanType = string.Empty;
                user.BanReason = string.Empty;
                user.BannedAt = null;
                user.BanExpiresAt = null;
                user.ModerationStatus = string.Empty;
                user.ModerationNote = string.Empty;
                user.ModerationUpdatedAt = DateTime.UtcNow;

                var success = await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}", user);

                if (success)
                {
                    Debug.WriteLine($"UnbanUserAsync: {phone} unbanned");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnbanUserAsync error: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> AcknowledgeWarningAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null) return false;

                user.WarningAcknowledged = true;

                return await SupabaseService.UpdateAsync("Users", $"Id=eq.{user.Id}",
                    new { WarningAcknowledged = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AcknowledgeWarningAsync error: {ex.Message}");
                return false;
            }
        }

        // Check if a temporary ban has expired and auto-lift it
        public static async Task<bool> CheckAndLiftExpiredBanAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user == null) return false;
                if (!user.IsBanned || user.BanType != "temporary") return false;
                if (user.BanExpiresAt == null) return false;

                if (DateTime.UtcNow >= user.BanExpiresAt.Value)
                {
                    await UnbanUserAsync(phone);
                    Debug.WriteLine($"Auto-lifted expired temp ban for {phone}");
                    return true; // ban was lifted
                }

                return false; // still banned
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckAndLiftExpiredBanAsync error: {ex.Message}");
                return false;
            }
        }
    }
}