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
        private View[]? _bgImages;
        private string _bannedPhone = string.Empty;
        private bool _carouselInitialized = false;

        public LoginPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await Task.Delay(500);

            // Existing startup logic (ban/auto-login, restore credentials, UI state)
            try
            {
                // Check ban / auto-login
                var existingPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (!string.IsNullOrEmpty(existingPhone))
                {
                    try
                    {
                        await UserService.CheckAndLiftExpiredBanAsync(existingPhone);
                        var (isBanned, banType, reason, expiresAt) =
                            await AuthService.CheckBanStatusAsync(existingPhone);

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
                            await Shell.Current.GoToAsync("//post", false);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LOGIN] Ban check error: {ex.Message}");
                    }
                }

                // Restore saved credentials
                var rememberMe = Preferences.Get(RememberMeKey, false);
                RememberMeCheckBox.IsChecked = rememberMe;

                if (rememberMe)
                {
                    try
                    {
                        var savedPhone = await SecureStorage.GetAsync(SavedPhoneKey);
                        var savedPassword = await SecureStorage.GetAsync(SavedPasswordKey);
                        if (!string.IsNullOrEmpty(savedPhone)) PhoneEntry.Text = savedPhone;
                        if (!string.IsNullOrEmpty(savedPassword)) PasswordEntry.Text = savedPassword;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LOGIN] Failed to restore credentials: {ex.Message}");
                    }
                }
                else
                {
                    PhoneEntry.Text = string.Empty;
                    PasswordEntry.Text = string.Empty;
                }

                MessageLabel.IsVisible = false;
                AppealButton.IsVisible = false;

                // Only init carousel once — prevents duplicate Task.Run loops
                // Wrap carousel init/start in try/catch to capture problems (missing images, OOM, etc.)
                try
                {
                    if (!_carouselInitialized)
                    {
                        InitCarouselImages();
                        _carouselInitialized = true;
                    }

                    StartBackgroundCarousel();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LOGIN] Carousel init/start error: {ex.Message}\n{ex.StackTrace}");
                    // Fail-safe: ensure no running timer
                    StopCarousel();
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected startup exception so app does not crash silently
                Debug.WriteLine($"[LOGIN] Unexpected OnAppearing error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        protected override void OnDisappearing()
        {
            StopCarousel();
            base.OnDisappearing();
        }

        // ?? Carousel ????????????????????????????????????????????????????????????

        private void InitCarouselImages()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var img1 = this.FindByName<FFImageLoading.Maui.CachedImage>("BgImage1");
                var img2 = this.FindByName<FFImageLoading.Maui.CachedImage>("BgImage2");
                var img3 = this.FindByName<FFImageLoading.Maui.CachedImage>("BgImage3");
                var img4 = this.FindByName<FFImageLoading.Maui.CachedImage>("BgImage4");
                var img5 = this.FindByName<FFImageLoading.Maui.CachedImage>("BgImage5");

                var validFF = new[] { img1, img2, img3, img4, img5 }
                    .Where(i => i != null).ToArray();

                // Store as View[] since both Image and CachedImage inherit from View
                _bgImages = validFF.Select(i => (Image)(object)i).ToArray();

                if (_bgImages == null)
                {
                    Debug.WriteLine("[LOGIN] No carousel images found");
                    return;
                }

                for (int i = 0; i < _bgImages.Length; i++)
                    _bgImages[i].Opacity = i == 0 ? 0.85 : 0;

                _currentBgIndex = 0;
                Debug.WriteLine($"[LOGIN] Carousel initialized with {_bgImages.Length} images");
            });
        }
        private void StartBackgroundCarousel()
        {
            if (_bgImages == null || _bgImages.Length < 2)
            {
                Debug.WriteLine("[LOGIN] Carousel skipped — not enough images");
                return;
            }

            StopCarousel();
            _carouselCts = new CancellationTokenSource();
            var token = _carouselCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token);
                        if (token.IsCancellationRequested) break;

                        // Re-check in case page was torn down
                        if (_bgImages == null || _bgImages.Length < 2) break;

                        int next = (_currentBgIndex + 1) % _bgImages.Length;
                        var current = _bgImages[_currentBgIndex];
                        var nextImg = _bgImages[next];

                        if (current == null || nextImg == null)
                        {
                            _currentBgIndex = next;
                            continue;
                        }

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (!token.IsCancellationRequested)
                                await Task.WhenAll(
                                    nextImg.FadeTo(0.85, 1200, Easing.CubicInOut),
                                    current.FadeTo(0, 1200, Easing.CubicInOut)
                                );
                        });

                        _currentBgIndex = next;
                    }
                    catch (TaskCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LOGIN] Carousel error: {ex.Message}");
                        try { await Task.Delay(1000, token); }
                        catch (TaskCanceledException) { break; }
                    }
                }
                Debug.WriteLine("[LOGIN] Carousel stopped.");
            }, token);
        }

        private void StopCarousel()
        {
            _carouselCts?.Cancel();
            _carouselCts?.Dispose();
            _carouselCts = null;
        }

        // ?? Password Toggle ??????????????????????????????????????????????????????

        private void TogglePasswordVisibility(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            PasswordEntry.IsPassword = !_isPasswordVisible;
            EyeOpenIcon.IsVisible = !_isPasswordVisible;
            EyeClosedIcon.IsVisible = _isPasswordVisible;
        }

        // ?? Remember Me ??????????????????????????????????????????????????????????

        private void RememberMe_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            Preferences.Set(RememberMeKey, e.Value);
            if (!e.Value)
            {
                SecureStorage.Remove(SavedPhoneKey);
                SecureStorage.Remove(SavedPasswordKey);
                Debug.WriteLine("[LOGIN] Saved credentials cleared.");
            }
        }

        // ?? Sign In ??????????????????????????????????????????????????????????????

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

                if (user != null)
                    Preferences.Set("current_user_role", user.Role);

                Preferences.Set(CurrentUserPhoneKey, phone);

                if (RememberMeCheckBox.IsChecked)
                {
                    try
                    {
                        await SecureStorage.SetAsync(SavedPhoneKey, phone);
                        await SecureStorage.SetAsync(SavedPasswordKey, password);
                        Debug.WriteLine("[LOGIN] Credentials saved.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LOGIN] Failed to save credentials: {ex.Message}");
                    }
                }

                MessagingCenter.Send(this, "UserLoggedIn", phone);
                await Shell.Current.GoToAsync("//post", false);
            }
            catch (Exception ex)
            {
                ShowMessage($"Login error: {ex.Message}");
                Debug.WriteLine($"[LOGIN] Exception: {ex}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        // ?? Loading State ????????????????????????????????????????????????????????

        private void SetLoadingState(bool isLoading)
        {
            SignInButton.IsVisible = !isLoading;
            LoadingOverlay.IsVisible = isLoading;
            PhoneEntry.IsEnabled = !isLoading;
            PasswordEntry.IsEnabled = !isLoading;
            RememberMeCheckBox.IsEnabled = !isLoading;
        }

        // ?? Appeal ???????????????????????????????????????????????????????????????

        private async void AppealButton_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_bannedPhone)) return;
            await Navigation.PushModalAsync(new Admin.AppealPage(_bannedPhone));
        }

        // ?? Register ?????????????????????????????????????????????????????????????

        private async void RegisterButton_Clicked(object sender, EventArgs e)
            => await Navigation.PushAsync(new RegisterPage());

        // ?? Helpers ??????????????????????????????????????????????????????????????

        private void ShowMessage(string text)
        {
            MessageLabel.Text = text;
            MessageLabel.IsVisible = true;
        }
    }
}