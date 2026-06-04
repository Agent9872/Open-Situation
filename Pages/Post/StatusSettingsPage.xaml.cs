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

        // In StatusSettingsPage.xaml.cs
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
                        CleanupExpiredStatuses();

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

        private async void CleanupExpiredStatuses()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var duration = Preferences.Get($"status_duration_{currentUserPhone}", "24 hours");
                var expirationHours = GetExpirationHours(duration);

                // Get all status posts from Supabase
                var allStatuses = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                    $"AuthorPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&not.StatusImagePath=is.null");

                var now = DateTime.UtcNow;
                var expiredStatuses = new List<Lock.Models.Post>();

                foreach (var status in allStatuses)
                {
                    var age = now - status.CreatedAt;
                    if (age.TotalHours >= expirationHours)
                    {
                        expiredStatuses.Add(status);
                    }
                }

                foreach (var expired in expiredStatuses)
                {
                    // Delete from Supabase
                    await SupabaseService.DeleteAsync("Posts", $"Id=eq.{Uri.EscapeDataString(expired.Id.ToString())}");

                    // Delete the actual image file
                    if (!string.IsNullOrEmpty(expired.StatusImagePath) && System.IO.File.Exists(expired.StatusImagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(expired.StatusImagePath);
                        }
                        catch { }
                    }
                }

                if (expiredStatuses.Any())
                {
                    Debug.WriteLine($"Cleaned up {expiredStatuses.Count} expired statuses");
                    MessagingCenter.Send(this, "StatusCleanedUp");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up expired statuses: {ex}");
            }
        }

        private int GetExpirationHours(string duration)
        {
            return duration switch
            {
                "24 hours" => 24,
                "48 hours" => 48,
                _ => 24  // Default to 24 hours
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

        // Add this method to StatusSettingsPage.xaml.cs
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

                if (confirm)
                {
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                    if (!string.IsNullOrEmpty(currentUserPhone))
                    {
                        // Get all status posts from Supabase
                        var statusPosts = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                            $"AuthorPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&not.StatusImagePath=is.null");

                        foreach (var post in statusPosts)
                        {
                            // Delete from Supabase
                            await SupabaseService.DeleteAsync("Posts", $"Id=eq.{Uri.EscapeDataString(post.Id.ToString())}");

                            // Delete the actual image file if it exists
                            if (!string.IsNullOrEmpty(post.StatusImagePath) && System.IO.File.Exists(post.StatusImagePath))
                            {
                                try
                                {
                                    System.IO.File.Delete(post.StatusImagePath);
                                }
                                catch { }
                            }
                        }

                        await DisplayAlert("Success", "All your statuses have been cleared", "OK");

                        // Notify that status was cleared
                        MessagingCenter.Send(this, "StatusCleared");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing statuses: {ex}");
                await DisplayAlert("Error", "Could not clear statuses", "OK");
            }
        }
    }
}