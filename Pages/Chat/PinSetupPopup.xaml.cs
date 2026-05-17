using CommunityToolkit.Maui.Views;
using Lock.Services;

namespace Lock.Pages.Chat.Popups;

public partial class PinSetupPopup : Popup
{
    private readonly string _conversationId;
    private readonly Action<bool, string?> _callback;
    private bool _isConfirming = false;
    private string _tempPin = string.Empty;

    public PinSetupPopup(string conversationId, Action<bool, string?> callback)
    {
        InitializeComponent();
        _conversationId = conversationId;
        _callback = callback;
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (!_isConfirming)
        {
            // First step: validate PIN
            string pin = PinEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(pin))
            {
                ShowError("Please enter a PIN");
                return;
            }

            if (pin.Length < 4 || pin.Length > 6)
            {
                ShowError("PIN must be 4-6 digits");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(pin, @"^\d+$"))
            {
                ShowError("PIN must contain only numbers");
                return;
            }

            // Move to confirmation step
            _tempPin = pin;
            _isConfirming = true;

            PinEntry.IsVisible = false;
            ConfirmPinEntry.IsVisible = true;
            ConfirmPinEntry.Focus();

            var nextButton = sender as Button;
            if (nextButton != null)
                nextButton.Text = "Confirm";

            TitleLabel.Text = "Confirm PIN";
        }
        else
        {
            // Second step: confirm PIN
            string confirmPin = ConfirmPinEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(confirmPin))
            {
                ShowError("Please confirm your PIN");
                return;
            }

            if (_tempPin != confirmPin)
            {
                ShowError("PINs do not match. Please try again.");
                ConfirmPinEntry.Text = string.Empty;
                return;
            }

            // Save the PIN
            bool success = await ChatLockService.SetChatLockAsync(
                _conversationId,
                ChatLockService.LockType.Pin,
                _tempPin);

            _callback?.Invoke(success, _tempPin);
            await CloseAsync();
        }
    }

    private void ShowError(string message)
    {
        ErrorMessageLabel.Text = message;
        ErrorMessageLabel.IsVisible = true;

        // Auto-hide error after 2 seconds
        Device.StartTimer(TimeSpan.FromSeconds(2), () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ErrorMessageLabel.IsVisible = false;
            });
            return false;
        });
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _callback?.Invoke(false, null);
        await CloseAsync();
    }
}