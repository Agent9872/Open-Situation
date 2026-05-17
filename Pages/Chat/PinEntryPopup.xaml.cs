using CommunityToolkit.Maui.Views;
using Lock.Services;
using System.Diagnostics;

namespace Lock.Pages.Chat.Popups;

public partial class PinEntryPopup : Popup
{
    private readonly string _conversationId;
    private readonly ChatLockService.LockType _lockType;
    private readonly Action<bool> _callback;

    public PinEntryPopup(string conversationId, ChatLockService.LockType lockType, Action<bool> callback)
    {
        InitializeComponent();
        _conversationId = conversationId;
        _lockType = lockType;
        _callback = callback;

        if (lockType == ChatLockService.LockType.Pin)
        {
            TitleLabel.Text = "Enter PIN to unlock";
            PinEntry.Placeholder = "Enter PIN (4-6 digits)";
            PinEntry.IsVisible = true;
        }
        else if (lockType == ChatLockService.LockType.Pattern)
        {
            TitleLabel.Text = "Enter Pattern to unlock";
            PinEntry.Placeholder = "Enter pattern code (1-9)";
            PinEntry.IsVisible = true;
        }
        else if (lockType == ChatLockService.LockType.Biometric)
        {
            TitleLabel.Text = "Biometric Unlock";
            PinEntry.IsVisible = false;

            // Auto-trigger biometric authentication
            Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool authenticated = await BiometricService.AuthenticateAsync("Unlock this chat");

                    if (authenticated)
                    {
                        _callback?.Invoke(true);
                        await CloseAsync();
                    }
                    else
                    {
                        ErrorMessageLabel.Text = "Biometric authentication failed. Please try again or use PIN/Pattern.";
                        ErrorMessageLabel.IsVisible = true;

                        // Show PIN entry as fallback
                        PinEntry.IsVisible = true;
                        TitleLabel.Text = "Enter PIN to unlock (fallback)";
                        PinEntry.Placeholder = "Enter PIN";
                    }
                });
                return false;
            });
        }
    }

    private void UpdateDots(int length)
    {
        var dots = new[] { Dot1, Dot2, Dot3, Dot4, Dot5, Dot6 };
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].Fill = i < length
                ? new SolidColorBrush(Color.FromArgb("#C667FF"))
                : new SolidColorBrush(Color.FromArgb("#2A2535"));
        }
    }
    private async Task TriggerBiometricUnlock()
    {
        try
        {
            bool authenticated = await BiometricService.AuthenticateAsync("Unlock this chat");

            if (authenticated)
            {
                _callback?.Invoke(true);
                await CloseAsync();
            }
            else
            {
                ErrorMessageLabel.Text = "Biometric authentication failed. Please try again or use PIN/Pattern.";
                ErrorMessageLabel.IsVisible = true;

                // Show PIN entry as fallback after biometric fails
                PinEntry.IsVisible = true;
                TitleLabel.Text = "Enter PIN to unlock (fallback)";
                PinEntry.Placeholder = "Enter PIN";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TriggerBiometricUnlock error: {ex}");
            ErrorMessageLabel.Text = $"Error: {ex.Message}";
            ErrorMessageLabel.IsVisible = true;
            PinEntry.IsVisible = true;
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        string input = PinEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(input) && _lockType != ChatLockService.LockType.Biometric)
        {
            ErrorMessageLabel.Text = "Please enter a value";
            ErrorMessageLabel.IsVisible = true;
            return;
        }

        bool verified = await ChatLockService.VerifyChatLockAsync(_conversationId, input, _lockType);

        if (verified)
        {
            _callback?.Invoke(true);
            await CloseAsync();
        }
        else
        {
            ErrorMessageLabel.Text = "Incorrect value. Please try again.";
            ErrorMessageLabel.IsVisible = true;
            PinEntry.Text = string.Empty;

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
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _callback?.Invoke(false);
        await CloseAsync();
    }
}