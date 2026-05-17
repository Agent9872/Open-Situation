// ════════════════════════════════════════════════════
// FILE 5 — NEW FILE
// Path: Lock/Services/NavigationGuard.cs
// ════════════════════════════════════════════════════

using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class NavigationGuard
    {
        /// <summary>
        /// Returns true if the currently logged-in user is allowed to
        /// navigate to the given Shell route string.
        ///
        /// Admins always return true.
        /// Users are checked against their DeniedPages field.
        /// </summary>
        public static async Task<bool> CanNavigateAsync(string route)
        {
            try
            {
                // Admins bypass everything
                var role = Preferences.Get("current_user_role", "User");
                if (role == "Admin") return true;

                // Normalise: strip leading // or /
                var key = route.TrimStart('/');

                var phone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(phone)) return true; // not logged in — let auth handle it

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                                   .Where(u => u.PhoneNumber == phone)
                                   .FirstOrDefaultAsync();

                if (user == null) return true;

                return user.CanAccessPage(key);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigationGuard.CanNavigateAsync error: {ex.Message}");
                return true; // fail open — never hard-block on error
            }
        }
    }
}