using Microsoft.Maui.Controls;

namespace Lock.Pages
{
    public partial class TermsPage : ContentPage
    {
        public TermsPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
        }

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnDeclineClicked(object sender, EventArgs e)
        {
            bool decline = await DisplayAlert(
                "Decline Terms",
                "You must accept the Terms and Conditions to use Lock. Would you like to review them again?",
                "Review Terms",
                "Close");

            if (!decline)
            {
                await Navigation.PopModalAsync();
            }
        }
    }
}