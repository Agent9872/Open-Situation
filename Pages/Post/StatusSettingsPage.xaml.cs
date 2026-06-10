using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Post
{
    public partial class StatusSettingsPage : ContentPage
    {
        public StatusSettingsPage()
        {
            InitializeComponent();
            Shell.SetNavBarIsVisible(this, false);  // Remove default shell navigation bar
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // Load saved privacy setting
                var privacy = Preferences.Get($"status_privacy_{currentUserPhone}", "Everyone");
                int privacyIndex = PrivacyPicker.Items.IndexOf(privacy);
                if (privacyIndex >= 0)
                {
                    PrivacyPicker.SelectedIndex = privacyIndex;
                    CustomPrivacyOptions.IsVisible = privacy == "Custom";
                }

                // Load saved duration setting (default to 24 hours)
                var duration = Preferences.Get($"status_duration_{currentUserPhone}", "24 hours");
                int durationIndex = DurationPicker.Items.IndexOf(duration);
                if (durationIndex >= 0)
                    DurationPicker.SelectedIndex = durationIndex;
                else
                    DurationPicker.SelectedIndex = 0; // Default to 24 hours
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading status settings: {ex}");
            }
        }

        private void OnPrivacyChanged(object sender, EventArgs e)
        {
            try
            {
                if (PrivacyPicker.SelectedItem != null)
                {
                    string selected = PrivacyPicker.SelectedItem.ToString();
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    if (!string.IsNullOrEmpty(currentUserPhone))
                    {
                        Preferences.Set($"status_privacy_{currentUserPhone}", selected);
                        CustomPrivacyOptions.IsVisible = selected == "Custom";
                        Debug.WriteLine($"Status privacy changed to: {selected}");

                        // Notify that status settings changed
                        MessagingCenter.Send(this, "StatusSettingsChanged");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving privacy setting: {ex}");
            }
        }

        private void OnDurationChanged(object sender, EventArgs e)
        {
            try
            {
                if (DurationPicker.SelectedItem != null)
                {
                    string selected = DurationPicker.SelectedItem.ToString();
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    if (!string.IsNullOrEmpty(currentUserPhone))
                    {
                        Preferences.Set($"status_duration_{currentUserPhone}", selected);
                        Debug.WriteLine($"Status duration changed to: {selected}");

                        // Clean up expired statuses when duration changes
                        _ = CleanupExpiredStatusesAsync();

                        // Notify that status settings changed
                        MessagingCenter.Send(this, "StatusSettingsChanged");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving duration setting: {ex}");
            }
        }

        /// <summary>
        /// Queries Supabase for the current user's status posts,
        /// deletes expired ones from both the DB and Supabase Storage.
        /// </summary>
        private async Task CleanupExpiredStatusesAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var duration = Preferences.Get($"status_duration_{currentUserPhone}", "24 hours");
                var expirationHours = GetExpirationHours(duration);

                // Correct PostgREST filter: StatusImagePath=not.is.null
                var allStatuses = await SupabaseService.GetAsync<Lock.Models.Post>(
                    "Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&StatusImagePath=not.is.null");

                var now = DateTime.UtcNow;
                var expired = allStatuses
                    .Where(s => (now - s.CreatedAt).TotalHours >= expirationHours)
                    .ToList();

                foreach (var status in expired)
                {
                    // Delete DB row + storage file via the new helper
                    await SupabaseService.DeleteStatusPostAsync(status.Id, status.StatusImagePath);

                    // Also remove local cached file if it exists
                    if (!string.IsNullOrEmpty(status.StatusImagePath) &&
                        System.IO.File.Exists(status.StatusImagePath))
                    {
                        try { System.IO.File.Delete(status.StatusImagePath); }
                        catch { /* ignore local deletion failures */ }
                    }
                }

                if (expired.Any())
                {
                    Debug.WriteLine($"[STATUS] Cleaned up {expired.Count} expired status(es)");
                    MessagingCenter.Send(this, "StatusCleanedUp");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupExpiredStatusesAsync error: {ex}");
            }
        }

        // Keep the old sync signature so any existing call sites still compile
        private void CleanupExpiredStatuses() => _ = CleanupExpiredStatusesAsync();

        private int GetExpirationHours(string duration)
        {
            return duration switch
            {
                "24 hours" => 24,
                "48 hours" => 48,
                "7 days" => 168,
                _ => 24   // Default to 24 hours
            };
        }

        private async void OnAllowedContactsClicked(object sender, EventArgs e)
        {
            try
            {
                // Open page to select allowed contacts
                await Navigation.PushAsync(new StatusPrivacyContactsPage(StatusPrivacyContactsPage.PrivacyType.Allowed));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening allowed contacts: {ex}");
                await DisplayAlert("Error", "Could not open contacts selection", "OK");
            }
        }

        private async void OnBlockedContactsClicked(object sender, EventArgs e)
        {
            try
            {
                // Open page to select blocked contacts
                await Navigation.PushAsync(new StatusPrivacyContactsPage(StatusPrivacyContactsPage.PrivacyType.Blocked));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening blocked contacts: {ex}");
                await DisplayAlert("Error", "Could not open contacts selection", "OK");
            }
        }

        private async void OnViewHistoryClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new StatusHistoryPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening status history: {ex}");
                await DisplayAlert("Error", "Could not open status history", "OK");
            }
        }

        private async void CloseButton_Clicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnClearAllClicked(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert(
                    "Clear All Statuses",
                    "Are you sure you want to delete all your status images? This action cannot be undone.",
                    "Clear All",
                    "Cancel");

                if (!confirm) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // Correct PostgREST filter: StatusImagePath=not.is.null
                var statusPosts = await SupabaseService.GetAsync<Lock.Models.Post>(
                    "Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&StatusImagePath=not.is.null");

                foreach (var post in statusPosts)
                {
                    // Delete DB row + Supabase Storage file via the new helper
                    await SupabaseService.DeleteStatusPostAsync(post.Id, post.StatusImagePath);

                    // Also remove local cached file if it exists
                    if (!string.IsNullOrEmpty(post.StatusImagePath) &&
                        System.IO.File.Exists(post.StatusImagePath))
                    {
                        try { System.IO.File.Delete(post.StatusImagePath); }
                        catch { /* ignore */ }
                    }
                }

                await DisplayAlert("Success", "All your statuses have been cleared", "OK");

                // Notify that status was cleared
                MessagingCenter.Send(this, "StatusCleared");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing statuses: {ex}");
                await DisplayAlert("Error", "Could not clear statuses", "OK");
            }
        }
    }
}