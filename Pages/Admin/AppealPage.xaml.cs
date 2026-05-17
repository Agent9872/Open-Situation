using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Pages.Admin
{
    public partial class AppealPage : ContentPage
    {
        private readonly string _phone;
        private bool _isSubmitting = false;
        private int _selectedPresetIndex = -1;

        private static readonly string[] _presetTemplates = new[]
        {
            "I believe my account was banned by mistake. This phone number belongs to me and I have not violated any community guidelines. I respectfully request a review of this decision.",
            "I was falsely reported by another user. The report against me was not accurate and I did not engage in the behavior described. I would like the moderation team to review the evidence.",
            "I believe there was a misunderstanding regarding my account activity. I did not intend to violate any rules and I am willing to clarify any concerns the team may have.",
            "I acknowledge that my past behavior may have been inappropriate, but I have genuinely changed and I am committed to following all community guidelines going forward. I respectfully ask for a second chance."
        };

        private static readonly Border[] _reasonButtons;

        public AppealPage(string phone)
        {
            InitializeComponent();
            _phone = phone;
            LoadBanInfo();
        }

        private async void LoadBanInfo()
        {
            try
            {
                if (PhoneDisplayLabel != null)
                    PhoneDisplayLabel.Text = _phone;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == _phone)
                    .FirstOrDefaultAsync();

                if (user == null || BanStatusLabel == null) return;

                if (user.BanType == "permanent")
                {
                    BanStatusLabel.Text = $"Status: Permanently banned\nReason: {user.BanReason}";
                }
                else if (user.BanType == "temporary" && user.BanExpiresAt.HasValue)
                {
                    BanStatusLabel.Text = $"Status: Suspended until {user.BanExpiresAt:MMM dd, yyyy 'at' h:mm tt} UTC\nReason: {user.BanReason}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadBanInfo error: {ex.Message}");
            }
        }

        private void OnReasonPresetTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not string indexStr || !int.TryParse(indexStr, out int index)) return;
            if (index < 0 || index >= _presetTemplates.Length) return;

            _selectedPresetIndex = index;

            // Update button visuals
            var allButtons = new[] { ReasonBtn1, ReasonBtn2, ReasonBtn3, ReasonBtn4 };
            for (int i = 0; i < allButtons.Length; i++)
            {
                if (allButtons[i] == null) continue;
                bool isActive = i == index;
                allButtons[i].BackgroundColor = isActive
                    ? Color.FromArgb("#1A1000")
                    : Color.FromArgb("#16161C");
                allButtons[i].Stroke = isActive
                    ? Color.FromArgb("#FF9800")
                    : Color.FromArgb("#2A2A35");

                var label = allButtons[i].Content as Label;
                if (label != null)
                    label.TextColor = isActive
                        ? Color.FromArgb("#FF9800")
                        : Color.FromArgb("#7A7A8C");
            }

            // Fill editor with template
            if (AppealReasonEditor != null)
            {
                AppealReasonEditor.Text = _presetTemplates[index];
                if (AppealCharLabel != null)
                    AppealCharLabel.Text = $"{_presetTemplates[index].Length} / 1500";
            }
        }

        private void OnAppealTextChanged(object sender, TextChangedEventArgs e)
        {
            if (AppealCharLabel != null)
                AppealCharLabel.Text = $"{e.NewTextValue?.Length ?? 0} / 1500";
        }

        private async void OnSubmitAppealClicked(object sender, TappedEventArgs e)
        {
            if (_isSubmitting) return;

            string appealText = AppealReasonEditor?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(appealText))
            {
                await DisplayAlert("Required", "Please explain the reason for your appeal.", "OK");
                return;
            }

            if (appealText.Length < 30)
            {
                await DisplayAlert("Too Short", "Please provide more detail in your appeal (at least 30 characters).", "OK");
                return;
            }

            if (HonestCheckBox == null || !HonestCheckBox.IsChecked)
            {
                await DisplayAlert("Confirmation Required",
                    "Please confirm that the information in your appeal is truthful.", "OK");
                return;
            }

            _isSubmitting = true;
            if (SubmitAppealLabel != null) SubmitAppealLabel.Text = "Submitting...";

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == _phone)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    await DisplayAlert("Error", "Account not found.", "OK");
                    return;
                }

                // Check if already appealed recently
                if (!string.IsNullOrEmpty(user.AppealStatus) && user.AppealStatus == "pending")
                {
                    await DisplayAlert("Already Submitted",
                        "You already have a pending appeal. Please wait for the moderation team to respond.", "OK");
                    return;
                }

                string additionalContext = AdditionalContextEditor?.Text?.Trim() ?? string.Empty;
                string fullAppeal = string.IsNullOrEmpty(additionalContext)
                    ? appealText
                    : $"{appealText}\n\nAdditional Context:\n{additionalContext}";

                // Save appeal to user record
                user.AppealText = fullAppeal;
                user.AppealStatus = "pending";
                user.AppealSubmittedAt = DateTime.UtcNow;

                await db.UpdateAsync(user);

                await DisplayAlert(
                    "Appeal Submitted",
                    "Your appeal has been submitted and will be reviewed by our moderation team within 48-72 hours.\n\nYou will be notified of the outcome.",
                    "OK");

                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSubmitAppealClicked error: {ex.Message}");
                await DisplayAlert("Error", $"Could not submit appeal: {ex.Message}", "OK");
            }
            finally
            {
                _isSubmitting = false;
                if (SubmitAppealLabel != null) SubmitAppealLabel.Text = "Submit Appeal";
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}