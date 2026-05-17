using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Lock.Pages
{
    public partial class LoginPage : ContentPage
    {
        private const string CurrentUserPhoneKey = "current_user_phone";
        private const string SavedPhoneKey = "saved_login_phone";
        private const string SavedPasswordKey = "saved_login_password";
        private const string RememberMeKey = "remember_me";

        private bool _isPasswordVisible = false;
        private CancellationTokenSource? _carouselCts;
        private int _currentBgIndex = 0;
        private Image[] _bgImages = Array.Empty<Image>();
        private string _bannedPhone = string.Empty;

        public LoginPage()
        {
            InitializeComponent();
            _bgImages = new[] { BgImage1, BgImage2, BgImage3, BgImage4, BgImage5 };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Auto-navigate if already logged in
            var existingPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
            if (!string.IsNullOrEmpty(existingPhone))
            {
                await UserService.CheckAndLiftExpiredBanAsync(existingPhone);
                var (isBanned, banType, reason, expiresAt) = await AuthService.CheckBanStatusAsync(existingPhone);

                if (isBanned)
                {
                    Preferences.Remove(CurrentUserPhoneKey);
                    string title = banType == "permanent"
                        ? "Account Permanently Banned"
                        : "Account Temporarily Suspended";
                    string message = banType == "permanent"
                        ? $"Your account has been permanently banned.\n\nReason: {reason}\n\nUse the Appeal button below if you believe this is a mistake."
                        : $"Your account has been suspended until:\n{expiresAt:MMM dd, yyyy 'at' h:mm tt} UTC\n\nReason: {reason}";

                    await DisplayAlert(title, message, "OK");
                }
                else
                {
                    await Shell.Current.GoToAsync("//post");
                    return;
                }
            }

            // Restore saved credentials if Remember Me was checked
            var rememberMe = Preferences.Get(RememberMeKey, false);
            RememberMeCheckBox.IsChecked = rememberMe;

            if (rememberMe)
            {
                try
                {
                    var savedPhone = await SecureStorage.GetAsync(SavedPhoneKey);
                    var savedPassword = await SecureStorage.GetAsync(SavedPasswordKey);

                    if (!string.IsNullOrEmpty(savedPhone))
                        PhoneEntry.Text = savedPhone;

                    if (!string.IsNullOrEmpty(savedPassword))
                        PasswordEntry.Text = savedPassword;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LOGIN] Failed to restore saved credentials: {ex.Message}");
                }
            }
            else
            {
                PhoneEntry.Text = string.Empty;
                PasswordEntry.Text = string.Empty;
            }

            MessageLabel.IsVisible = false;
            AppealButton.IsVisible = false;
            _currentBgIndex = 0;
            StartBackgroundCarousel();
        }

        protected override void OnDisappearing()
        {
            _carouselCts?.Cancel();
            base.OnDisappearing();
        }

        // ?? Remember Me ???????????????????????????????????????????????????????

        private void RememberMe_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            Preferences.Set(RememberMeKey, e.Value);

            // If unchecked, wipe saved credentials immediately
            if (!e.Value)
            {
                SecureStorage.Remove(SavedPhoneKey);
                SecureStorage.Remove(SavedPasswordKey);
                Debug.WriteLine("[LOGIN] Saved credentials cleared.");
            }
        }

        // ?? Carousel ??????????????????????????????????????????????????????????

        private void StartBackgroundCarousel()
        {
            _carouselCts?.Cancel();
            _carouselCts = new CancellationTokenSource();
            var token = _carouselCts.Token;

            for (int i = 0; i < _bgImages.Length; i++)
                _bgImages[i].Opacity = (i == 0) ? 0.85 : 0;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token);
                        if (token.IsCancellationRequested) break;

                        int next = (_currentBgIndex + 1) % _bgImages.Length;
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (!token.IsCancellationRequested)
                                await Task.WhenAll(
                                    _bgImages[next].FadeTo(0.85, 1200, Easing.CubicInOut),
                                    _bgImages[_currentBgIndex].FadeTo(0, 1200, Easing.CubicInOut));
                        });
                        _currentBgIndex = next;
                    }
                    catch (TaskCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Carousel error: {ex.Message}");
                        await Task.Delay(1000, token);
                    }
                }
            }, token);
        }

        // ?? Handlers ??????????????????????????????????????????????????????????

        private void TogglePasswordVisibility(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            PasswordEntry.IsPassword = !_isPasswordVisible;
            EyeOpenIcon.IsVisible = !_isPasswordVisible;
            EyeClosedIcon.IsVisible = _isPasswordVisible;
        }

        private async void SignInButton_Clicked(object sender, EventArgs e)
        {
            MessageLabel.IsVisible = false;
            AppealButton.IsVisible = false;

            var phone = PhoneEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            {
                ShowMessage("Phone and password are required.");
                return;
            }

            // Show loading state
            SetLoadingState(true);

            try
            {
                (bool success, string error, Lock.Models.User? user) =
                    await AuthService.LoginAsync(phone, password);

                if (!success)
                {
                    ShowMessage(error ?? "Login failed");

                    if (error != null &&
                        (error.Contains("permanently banned") || error.Contains("suspended until")))
                    {
                        _bannedPhone = phone;
                        AppealButton.IsVisible = true;
                    }
                    return;
                }

                // Force role into Preferences from the returned user object
                if (user != null)
                    Preferences.Set("current_user_role", user.Role);

                Preferences.Set(CurrentUserPhoneKey, phone);

                // Save credentials if Remember Me is checked
                if (RememberMeCheckBox.IsChecked)
                {
                    try
                    {
                        await SecureStorage.SetAsync(SavedPhoneKey, phone);
                        await SecureStorage.SetAsync(SavedPasswordKey, password);
                        Debug.WriteLine("[LOGIN] Credentials saved securely.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LOGIN] Failed to save credentials: {ex.Message}");
                    }
                }

                MessagingCenter.Send(this, "UserLoggedIn", phone);
                await Shell.Current.GoToAsync("//post");
            }
            catch (Exception ex)
            {
                ShowMessage($"Login error: {ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            SignInButton.IsVisible = !isLoading;
            LoadingOverlay.IsVisible = isLoading;
            PhoneEntry.IsEnabled = !isLoading;
            PasswordEntry.IsEnabled = !isLoading;
            RememberMeCheckBox.IsEnabled = !isLoading;
        }

        private async void AppealButton_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_bannedPhone)) return;
            await Navigation.PushModalAsync(new Admin.AppealPage(_bannedPhone));
        }

        private async void RegisterButton_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new RegisterPage());

        private void ShowMessage(string text)
        {
            MessageLabel.Text = text;
            MessageLabel.IsVisible = true;
        }
    }
}