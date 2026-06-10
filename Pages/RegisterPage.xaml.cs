using Lock.Chat.Services;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Lock.Pages
{
    public partial class RegisterPage : ContentPage
    {
        private bool _isPasswordVisible = false;
        private CancellationTokenSource? _carouselCts;
        private int _currentBgIndex = 0;

        // ? FIX: Nullable — assigned in OnAppearing() where XAML tree is ready,
        //         NOT in the constructor where FindByName returns null on Android.
        private Image[]? _bgImages;

        public RegisterPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            // ?? Pickers: safe to configure in constructor via x:Name ??????????
            // (Picker/DatePicker data-binding doesn't need the visual tree attached)

            // GenderPicker — x:Name="GenderPicker" in XAML
            GenderPicker.ItemsSource = new[] { "Male", "Female", "Other" };

            // InterestPicker — x:Name="InterestPicker" in XAML
            InterestPicker.ItemsSource = new[] { "Women", "Men", "Everyone" };

            // DobPicker — x:Name="DobPicker" in XAML
            // DateSelected is already wired in XAML (DateSelected="DobPicker_DateSelected"),
            // so we only set the bounds and initial value here.
            DobPicker.MaximumDate = DateTime.Today;
            DobPicker.Date = DateTime.Today.AddYears(-18);
            UpdateAge(DobPicker.Date);

            // ?? EyeToggleIcon ????????????????????????????????????????????????
            // The password eye-toggle Grid in RegisterPage.xaml does NOT have an
            // x:Name, so we cannot use FindByName for it. The TapGestureRecognizer
            // is wired directly in XAML:
            //   <TapGestureRecognizer Tapped="TogglePasswordVisibility" />
            // Nothing extra needed here.
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // ? FIX: Initialise _bgImages HERE — XAML elements are fully ready.
            // RegisterPage.xaml has BgImage1, BgImage2, BgImage3 (3 images).
            _bgImages = new[]
            {
                this.FindByName<Image>("BgImage1"),
                this.FindByName<Image>("BgImage2"),
                this.FindByName<Image>("BgImage3")
            };

            for (int i = 0; i < _bgImages.Length; i++)
            {
                if (_bgImages[i] == null)
                    Debug.WriteLine($"[REGISTER] WARNING: BgImage{i + 1} is null — check x:Name in RegisterPage.xaml");
            }

            StartBackgroundCarousel();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _carouselCts?.Cancel();
            _carouselCts = null;
        }

        // ?? Carousel ??????????????????????????????????????????????????????????

        private void StartBackgroundCarousel()
        {
            _carouselCts?.Cancel();
            _carouselCts = new CancellationTokenSource();
            var token = _carouselCts.Token;

            // Set initial opacities safely
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_bgImages == null) return;
                for (int i = 0; i < _bgImages.Length; i++)
                {
                    if (_bgImages[i] == null) continue;
                    _bgImages[i].Opacity = i == 0 ? 0.85 : 0;
                }
            });

            _currentBgIndex = 0;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(4000, token);
                        if (token.IsCancellationRequested) break;

                        if (_bgImages == null || _bgImages.Length == 0) break;

                        var nextIndex = (_currentBgIndex + 1) % _bgImages.Length;
                        var current = _bgImages[_currentBgIndex];
                        var next = _bgImages[nextIndex];

                        // ? FIX: Skip frame instead of crashing when image is null
                        if (current == null || next == null)
                        {
                            Debug.WriteLine($"[REGISTER] Skipping carousel frame — null at index {_currentBgIndex} or {nextIndex}");
                            _currentBgIndex = nextIndex;
                            continue;
                        }

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (!token.IsCancellationRequested)
                                await Task.WhenAll(
                                    next.FadeTo(0.85, 1000, Easing.CubicInOut),
                                    current.FadeTo(0, 1000, Easing.CubicInOut)
                                );
                        });

                        _currentBgIndex = nextIndex;
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[REGISTER] Carousel error: {ex.Message}");
                        if (!token.IsCancellationRequested)
                            await Task.Delay(1000, token).ContinueWith(_ => { });
                    }
                }

                Debug.WriteLine("[REGISTER] Carousel stopped.");
            }, token);
        }

        // ?? Password Toggle ???????????????????????????????????????????????????
        // Wired via XAML: <TapGestureRecognizer Tapped="TogglePasswordVisibility" />
        // EyeOpenIcon / EyeClosedIcon are x:Name'd in RegisterPage.xaml.

        private void TogglePasswordVisibility(object? sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            // x:Name="PasswordEntry" — direct field access, no FindByName needed
            PasswordEntry.IsPassword = !_isPasswordVisible;

            // x:Name="EyeOpenIcon" / x:Name="EyeClosedIcon"
            EyeOpenIcon.IsVisible = !_isPasswordVisible;
            EyeClosedIcon.IsVisible = _isPasswordVisible;
        }

        // ?? Date of Birth ?????????????????????????????????????????????????????
        // Wired via XAML: DateSelected="DobPicker_DateSelected"

        private void DobPicker_DateSelected(object? sender, DateChangedEventArgs e)
        {
            UpdateAge(e.NewDate);
        }

        private void UpdateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;

            // x:Name="AgeLabel"
            AgeLabel.Text = $"Age: {age}";
        }

        // ?? Loading State ?????????????????????????????????????????????????????
        // NOTE: RegisterPage.xaml does NOT have a "RegisterLoadingOverlay" Grid with
        // an x:Name. If you want a loading overlay add one to the XAML and give it
        // x:Name="RegisterLoadingOverlay". Until then we just toggle the button.

        private void SetLoadingState(bool isLoading)
        {
            // x:Name="RegisterButton"
            RegisterButton.IsEnabled = !isLoading;
            RegisterButton.Text = isLoading ? "Please wait…" : "CREATE ACCOUNT";

            // Individual field disable
            NameEntry.IsEnabled = !isLoading;
            PhoneEntry.IsEnabled = !isLoading;
            PasswordEntry.IsEnabled = !isLoading;
            GenderPicker.IsEnabled = !isLoading;
            InterestPicker.IsEnabled = !isLoading;
            DobPicker.IsEnabled = !isLoading;
            TermsCheckBox.IsEnabled = !isLoading;
        }

        // ?? Register Button ???????????????????????????????????????????????????
        // Wired via XAML: Clicked="RegisterButton_Clicked"

        private async void RegisterButton_Clicked(object sender, EventArgs e)
        {
            // x:Name="MessageLabel"
            MessageLabel.IsVisible = false;

            var name = NameEntry.Text?.Trim() ?? string.Empty;
            var phone = PhoneEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;
            var gender = GenderPicker.SelectedItem as string ?? string.Empty;
            var interest = InterestPicker.SelectedItem as string ?? string.Empty;
            var dob = DobPicker.Date;

            // ?? Validation ????????????????????????????????????????????????????

            if (!TermsCheckBox.IsChecked)
            {
                ShowMessage("You must accept the Terms and Conditions to register.");
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("Full name is required.");
                return;
            }

            if (string.IsNullOrEmpty(phone))
            {
                ShowMessage("Phone number is required.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowMessage("Password is required.");
                return;
            }

            if (password.Length < 6)
            {
                ShowMessage("Password must be at least 6 characters.");
                return;
            }

            if (string.IsNullOrEmpty(gender))
            {
                ShowMessage("Please select your gender.");
                return;
            }

            if (dob > DateTime.Today)
            {
                ShowMessage("Date of birth cannot be in the future.");
                return;
            }

            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            if (age < 18)
            {
                ShowMessage("You must be at least 18 years old to register.");
                return;
            }

            // ?? Submit ????????????????????????????????????????????????????????

            SetLoadingState(true);

            try
            {
                var ipInfo = await IpService.GetIpInfoAsync();
                var ipAddress = ipInfo.Ip;
                var ipCountry = ipInfo.Country;
                var ipRegion = ipInfo.Region;

                Debug.WriteLine($"[REGISTER] IP: {(string.IsNullOrEmpty(ipAddress) ? "unavailable" : ipAddress)} | Location: {ipInfo.Location}");

                var result = await AuthService.RegisterAsync(
                    name: name,
                    phone: phone,
                    password: password,
                    dob: dob,
                    gender: gender,
                    interest: interest,
                    country: ipCountry,
                    state: ipRegion,
                    ipAddress: ipAddress
                );

                if (!result.Success)
                {
                    ShowMessage(result.Error);
                    PhoneEntry.Focus();
                    return;
                }

                // Fetch newly created user and store role
                try
                {
                    bool hasPlus = phone.StartsWith("+");
                    var digits = new string(phone.Where(c => char.IsDigit(c)).ToArray());
                    var normalizedPhone = new string(phone.Where(char.IsDigit).ToArray());
                    var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(normalizedPhone)}&limit=1");
                    var newUser = users.FirstOrDefault();

                    if (newUser != null)
                    {
                        Preferences.Set("current_user_role", newUser.Role);
                        Debug.WriteLine($"[REGISTER] Role stored: {newUser.Role} for {normalizedPhone}");
                    }
                    else
                    {
                        Debug.WriteLine($"[REGISTER] Could not find user after registration: {normalizedPhone}");
                    }
                }
                catch (Exception roleEx)
                {
                    Debug.WriteLine($"[REGISTER] Role fetch failed (non-fatal): {roleEx.Message}");
                }

                await DisplayAlert("Success", "Registration completed. You can now sign in.", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                ShowMessage("Registration failed. " + ex.Message);
                Debug.WriteLine($"[REGISTER] Exception: {ex}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        // ?? Navigation ????????????????????????????????????????????????????????
        // Wired via XAML: Tapped="ViewTerms_Clicked" / Tapped="SignInButton_Clicked"

        private async void ViewTerms_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new TermsPage());
        }

        private async void SignInButton_Clicked(object sender, EventArgs e)
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
            else
                await Shell.Current.GoToAsync("//LoginPage");
        }

        // ?? Helpers ???????????????????????????????????????????????????????????

        private void ShowMessage(string text)
        {
            MessageLabel.Text = text;
            MessageLabel.IsVisible = true;
        }
    }
}