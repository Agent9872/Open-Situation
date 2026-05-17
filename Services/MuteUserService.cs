using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class MuteUserService
    {
        private const string MutedUsersKeyPrefix = "muted_users_";

        private static string GetKey(string currentUserPhone)
            => $"{MutedUsersKeyPrefix}{currentUserPhone}";

        public static List<string> GetMutedPhones(string currentUserPhone)
        {
            try
            {
                var json = Preferences.Get(GetKey(currentUserPhone), string.Empty);
                if (string.IsNullOrEmpty(json)) return new List<string>();
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MuteUserService.GetMutedPhones error: {ex}");
                return new List<string>();
            }
        }

        public static bool IsUserMuted(string authorPhone, string currentUserPhone)
        {
            if (string.IsNullOrEmpty(authorPhone) || string.IsNullOrEmpty(currentUserPhone))
                return false;

            var muted = GetMutedPhones(currentUserPhone);
            return muted.Any(p => string.Equals(p.Trim(), authorPhone.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static Task<bool> MuteUserAsync(string authorPhone, string currentUserPhone)
        {
            try
            {
                if (string.IsNullOrEmpty(authorPhone) || string.IsNullOrEmpty(currentUserPhone))
                    return Task.FromResult(false);

                var muted = GetMutedPhones(currentUserPhone);

                if (!muted.Any(p => string.Equals(p.Trim(), authorPhone.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    muted.Add(authorPhone.Trim());
                    Save(currentUserPhone, muted);
                }

                Debug.WriteLine($"Muted user: {authorPhone} for {currentUserPhone}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MuteUserService.MuteUserAsync error: {ex}");
                return Task.FromResult(false);
            }
        }

        public static Task<bool> UnmuteUserAsync(string authorPhone, string currentUserPhone)
        {
            try
            {
                var muted = GetMutedPhones(currentUserPhone);
                var removed = muted.RemoveAll(p =>
                    string.Equals(p.Trim(), authorPhone.Trim(), StringComparison.OrdinalIgnoreCase));

                if (removed > 0)
                    Save(currentUserPhone, muted);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MuteUserService.UnmuteUserAsync error: {ex}");
                return Task.FromResult(false);
            }
        }

        public static Task<List<string>> GetMutedPhonesAsync(string currentUserPhone)
            => Task.FromResult(GetMutedPhones(currentUserPhone));

        private static void Save(string currentUserPhone, List<string> muted)
        {
            Preferences.Set(GetKey(currentUserPhone), JsonSerializer.Serialize(muted));
        }
    }
}