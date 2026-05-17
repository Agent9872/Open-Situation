using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Pages.Profile
{
    public partial class SafetyTipsPage : ContentPage
    {
        private string _currentUserPhone;

        public SafetyTipsPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, true);
            Shell.SetNavBarIsVisible(this, true);

            // Get current user phone
            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
        }

        // Navigate to Emergency Contacts Page
        private async Task OnEmergencyContactsTappedAsync(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentUserPhone))
                {
                    await DisplayAlert("Error", "User not found", "OK");
                    return;
                }

                var contactsPage = new EmergencyContactsPage(_currentUserPhone);
                await Navigation.PushAsync(contactsPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnEmergencyContactsTapped error: {ex}");
                await DisplayAlert("Error", "Could not open emergency contacts", "OK");
            }
        }

        // Share Date Details
        private async void OnShareDateDetailsTapped(object sender, EventArgs e)
        {
            try
            {
                await DisplayAlert("Share Date Details",
                    "This feature allows you to share your date location and details with trusted friends.\n\n" +
                    "You can share:\n" +
                    "• Date location\n" +
                    "• Time and duration\n" +
                    "• Who you're meeting\n" +
                    "• Live location tracking\n\n" +
                    "Your friends will receive a notification with all the details.",
                    "Got it");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnShareDateDetailsTapped error: {ex}");
            }
        }

        // Button handler for setting up emergency contacts
        private async void OnEmergencyContactsButtonClicked(object sender, EventArgs e)
        {
            await OnEmergencyContactsTappedAsync(sender, e);
        }

        // Event handler for the Emergency Contacts button tap (Border tap)
        private void OnEmergencyContactsTapped(object sender, EventArgs e)
        {
            _ = OnEmergencyContactsTappedAsync(sender, e);
        }
    }
}