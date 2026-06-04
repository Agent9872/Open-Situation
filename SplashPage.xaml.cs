using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Lock.Chat.Services;
using Lock.Models;
using System.Diagnostics;
using System;

namespace Lock.Pages
{
    public partial class SplashPage : ContentPage
    {
        private const string CurrentUserPhoneKey = "current_user_phone";

        public SplashPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            // Give a small delay to ensure splash screen is visible
            await Task.Delay(800);

            try
            {
                Debug.WriteLine("=== SPLASH PAGE: Checking login state ===");

                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();

                // Check if there's a saved phone number
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                Debug.WriteLine($"Saved phone: '{phone}'");

                if (!string.IsNullOrWhiteSpace(phone))
                {
                    // Verify user exists in Supabase
                    var users = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                    var user = users.FirstOrDefault();

                    if (user != null)
                    {
                        Debug.WriteLine("User found, navigating to PostPage");

                        // Create AppShell and navigate to post page
                        Application.Current.MainPage = new AppShell();

                        // Small delay for shell to initialize
                        await Task.Delay(100);

                        // Navigate to post page
                        await Shell.Current.GoToAsync("//post");

                        // Load profile data in background
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(200);
                            await ((AppShell)Application.Current.MainPage).LoadUserProfileAsync(phone);
                        });

                        return;
                    }
                    else
                    {
                        Debug.WriteLine("User not found in database, clearing preferences");
                        Preferences.Remove(CurrentUserPhoneKey);
                    }
                }

                Debug.WriteLine("No valid login, navigating to LoginPage");

                // Not logged in - go to login page
                Application.Current.MainPage = new AppShell();

                // Small delay for shell to initialize
                await Task.Delay(100);

                await Shell.Current.GoToAsync("//login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SPLASH PAGE ERROR: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Fallback - go to login
                try
                {
                    Application.Current.MainPage = new AppShell();
                    await Task.Delay(100);
                    await Shell.Current.GoToAsync("//login");
                }
                catch
                {
                    // Last resort - just show AppShell
                    Application.Current.MainPage = new AppShell();
                }
            }
        }
    }
}