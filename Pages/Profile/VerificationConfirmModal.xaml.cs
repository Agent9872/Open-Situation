using Lock.Models;
using Lock.Chat.Services;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Pages.Profile
{
    public partial class VerificationConfirmModal : ContentPage
    {
        private User _profileUser;
        private string _extractedIdName;
        private string _extractedIdNumber;
        private string _extractedIdDob;
        private string _extractedIdAddress;
        private string _selectedIdType;
        private double _similarityScore;

        public event EventHandler<bool> VerificationCompleted;

        public VerificationConfirmModal(User profileUser,
                                        string extractedIdName,
                                        string extractedIdNumber,
                                        string extractedIdDob,
                                        string extractedIdAddress,
                                        string selectedIdType,
                                        double similarityScore)
        {
            InitializeComponent();

            _profileUser = profileUser;
            _extractedIdName = extractedIdName;
            _extractedIdNumber = extractedIdNumber;
            _extractedIdDob = extractedIdDob;
            _extractedIdAddress = extractedIdAddress;
            _selectedIdType = selectedIdType;
            _similarityScore = similarityScore;

            LoadData();
        }

        private void LoadData()
        {
            // Set match score
            MatchScoreLabel.Text = $"{_similarityScore:F0}%";

            // Set ID extracted data
            IdNameLabel.Text = string.IsNullOrEmpty(_extractedIdName) ? "Not detected" : _extractedIdName;
            IdNumberLabel.Text = string.IsNullOrEmpty(_extractedIdNumber) ? "Not detected" : _extractedIdNumber;
            IdDobLabel.Text = string.IsNullOrEmpty(_extractedIdDob) ? "Not detected" : _extractedIdDob;
            IdAddressLabel.Text = string.IsNullOrEmpty(_extractedIdAddress) ? "Not detected" : _extractedIdAddress;

            // Set profile data
            ProfileNameLabel.Text = _profileUser?.Name ?? "Unknown";
            ProfileDobLabel.Text = _profileUser?.DateOfBirth.ToString("dd/MM/yyyy") ?? "Unknown";

            // Set match icons based on similarity
            UpdateMatchIcons();
        }

        private void UpdateMatchIcons()
        {
            // Compare names (case-insensitive, ignoring order)
            bool nameMatch = IsNameMatch(_extractedIdName, _profileUser?.Name);
            NameMatchIcon.Text = nameMatch ? "?" : "??";
            NameMatchIcon.TextColor = nameMatch ? Color.FromArgb("#00B5B5") : Color.FromArgb("#FFA500");

            // Compare DOB
            bool dobMatch = _extractedIdDob == _profileUser?.DateOfBirth.ToString("dd/MM/yyyy");
            DobMatchIcon.Text = dobMatch ? "?" : "??";
            DobMatchIcon.TextColor = dobMatch ? Color.FromArgb("#00B5B5") : Color.FromArgb("#FFA500");

            // ID presence check
            bool idValid = !string.IsNullOrEmpty(_extractedIdNumber) &&
                          _extractedIdNumber != "Not detected" &&
                          _extractedIdNumber.Length >= 5;
            IdMatchIcon.Text = idValid ? "?" : "??";
            IdMatchIcon.TextColor = idValid ? Color.FromArgb("#00B5B5") : Color.FromArgb("#FFA500");
        }

        private bool IsNameMatch(string extractedName, string profileName)
        {
            if (string.IsNullOrEmpty(extractedName) || string.IsNullOrEmpty(profileName))
                return false;

            extractedName = extractedName.ToLower().Trim();
            profileName = profileName.ToLower().Trim();

            // Exact match
            if (extractedName == profileName)
                return true;

            // Check if words match regardless of order
            var extractedWords = extractedName.Split(' ');
            var profileWords = profileName.Split(' ');

            int matchCount = 0;
            foreach (var eWord in extractedWords)
            {
                foreach (var pWord in profileWords)
                {
                    if (eWord == pWord || eWord.Contains(pWord) || pWord.Contains(eWord))
                    {
                        matchCount++;
                        break;
                    }
                }
            }

            return matchCount >= Math.Min(extractedWords.Length, profileWords.Length) / 2;
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnSubmitClicked(object sender, EventArgs e)
        {
            try
            {
                // Show loading
                SubmitModalLoading.IsRunning = true;
                SubmitModalLoading.IsVisible = true;
                SubmitModalText.IsVisible = false;
                SubmitModalButton.IsEnabled = false;

                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();

                // Update user verification
                _profileUser.IsVerified = true;
                _profileUser.VerifiedAt = DateTime.UtcNow;
                _profileUser.VerificationIdNumber = _extractedIdNumber;
                _profileUser.VerificationIdType = _selectedIdType;
                _profileUser.VerificationSubmittedAt = DateTime.UtcNow;
                _profileUser.VerificationVerifiedAt = DateTime.UtcNow;
                _profileUser.VerificationStatus = "verified";
                _profileUser.VerificationScore = _similarityScore;

                // Replace with Supabase code:
                await SupabaseService.UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(_profileUser.PhoneNumber)}", _profileUser);

                // Send completion message
                VerificationCompleted?.Invoke(this, true);

                // Close modal and show success
                await Navigation.PopModalAsync();

                // Show success message on parent page
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Verification Successful! ?",
                        $"Congratulations {_profileUser.Name}!\n\n" +
                        $"Your account has been successfully verified with {_similarityScore:F0}% match confidence.\n\n" +
                        $"Verified on: {DateTime.UtcNow:MMMM dd, yyyy}\n\n" +
                        "Thank you for securing your account!",
                        "Continue");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Submit error: {ex}");
                await DisplayAlert("Error", "Failed to submit verification. Please try again.", "OK");
            }
            finally
            {
                SubmitModalLoading.IsRunning = false;
                SubmitModalLoading.IsVisible = false;
                SubmitModalText.IsVisible = true;
                SubmitModalButton.IsEnabled = true;
            }
        }
    }
}