using Lock.Chat.Services;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Lock.Pages
{
    public partial class RegisterPage : ContentPage
    {
        private bool _isPasswordVisible = false;
        private CancellationTokenSource _carouselCts;
        private int _currentBgIndex = 0;
        private Image[] _bgImages;

        public RegisterPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            _bgImages = new[]
            {
                this.FindByName<Image>("BgImage1"),
                this.FindByName<Image>("BgImage2"),
                this.FindByName<Image>("BgImage3")
            };

            var dobPicker = this.FindByName<DatePicker>("DobPicker");
            if (dobPicker != null)
            {
                dobPicker.MaximumDate = DateTime.Today;
                dobPicker.Date = DateTime.Today.AddYears(-18);
                dobPicker.DateSelected += DobPicker_DateSelected;
                UpdateAge(dobPicker.Date);
            }

            var genderPicker = this.FindByName<Picker>("GenderPicker");
            if (genderPicker != null)
                genderPicker.ItemsSource = new[] { "Male", "Female", "Other" };

            var interestPicker = this.FindByName<Picker>("InterestPicker");
            if (interestPicker != null)
                interestPicker.ItemsSource = new[] { "Women", "Men", "Everyone" };

            var eyeToggleIcon = this.FindByName<Grid>("EyeToggleIcon");
            if (eyeToggleIcon != null)
            {
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += TogglePasswordVisibility;
                eyeToggleIcon.GestureRecognizers.Add(tapGesture);
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartBackgroundCarousel();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _carouselCts?.Cancel();
        }

        private void StartBackgroundCarousel()
        {
            _carouselCts?.Cancel();
            _carouselCts = new CancellationTokenSource();
            var token = _carouselCts.Token;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                for (int i = 0; i < _bgImages.Length; i++)
                {
                    if (_bgImages[i] != null)
                    {
                        _bgImages[i].Opacity = i == 0 ? 0.85 : 0;
                        var imageName = i == 0 ? "login.png" : $"login{i}.png";
                        _bgImages[i].Source = ImageSource.FromFile(imageName);
                        Debug.WriteLine($"Forced reload of {imageName}");
                    }
                }
            });

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(4000, token);
                        if (token.IsCancellationRequested) break;

                        var nextIndex = (_currentBgIndex + 1) % _bgImages.Length;

                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            var current = _bgImages[_currentBgIndex];
                            var next = _bgImages[nextIndex];

                            if (current != null && next != null)
                            {
                                await Task.WhenAll(
                                    next.FadeTo(0.85, 1000),
                                    current.FadeTo(0, 1000)
                                );
                            }
                        });

                        _currentBgIndex = nextIndex;
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Carousel error: {ex.Message}");
                    }
                }
            }, token);
        }

        private void TogglePasswordVisibility(object sender, EventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            var passwordEntry = this.FindByName<Entry>("PasswordEntry");
            var eyeOpenIcon = this.FindByName<Path>("EyeOpenIcon");
            var eyeClosedIcon = this.FindByName<Path>("EyeClosedIcon");

            if (passwordEntry != null)
                passwordEntry.IsPassword = !_isPasswordVisible;

            if (eyeOpenIcon != null && eyeClosedIcon != null)
            {
                eyeOpenIcon.IsVisible = !_isPasswordVisible;
                eyeClosedIcon.IsVisible = _isPasswordVisible;
            }
        }

        private void DobPicker_DateSelected(object? sender, DateChangedEventArgs e)
        {
            UpdateAge(e.NewDate);
        }

        private void UpdateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;

            var ageLabel = this.FindByName<Label>("AgeLabel");
            if (ageLabel != null)
                ageLabel.Text = $"Age: {age}";
        }

        private void SetLoadingState(bool isLoading)
        {
            var registerButton = this.FindByName<Button>("RegisterButton");
            var loadingOverlay = this.FindByName<Grid>("RegisterLoadingOverlay");
            var nameEntry = this.FindByName<Entry>("NameEntry");
            var phoneEntry = this.FindByName<Entry>("PhoneEntry");
            var passwordEntry = this.FindByName<Entry>("PasswordEntry");
            var genderPicker = this.FindByName<Picker>("GenderPicker");
            var interestPicker = this.FindByName<Picker>("InterestPicker");
            var dobPicker = this.FindByName<DatePicker>("DobPicker");
            var termsCheckBox = this.FindByName<CheckBox>("TermsCheckBox");

            if (registerButton != null) registerButton.IsVisible = !isLoading;
            if (loadingOverlay != null) loadingOverlay.IsVisible = isLoading;
            if (nameEntry != null) nameEntry.IsEnabled = !isLoading;
            if (phoneEntry != null) phoneEntry.IsEnabled = !isLoading;
            if (passwordEntry != null) passwordEntry.IsEnabled = !isLoading;
            if (genderPicker != null) genderPicker.IsEnabled = !isLoading;
            if (interestPicker != null) interestPicker.IsEnabled = !isLoading;
            if (dobPicker != null) dobPicker.IsEnabled = !isLoading;
            if (termsCheckBox != null) termsCheckBox.IsEnabled = !isLoading;
        }

        private async void RegisterButton_Clicked(object sender, EventArgs e)
        {
            var messageLabel = this.FindByName<Label>("MessageLabel");
            if (messageLabel != null) messageLabel.IsVisible = false;

            var nameEntry = this.FindByName<Entry>("NameEntry");
            var phoneEntry = this.FindByName<Entry>("PhoneEntry");
            var passwordEntry = this.FindByName<Entry>("PasswordEntry");
            var genderPicker = this.FindByName<Picker>("GenderPicker");
            var interestPicker = this.FindByName<Picker>("InterestPicker");
            var dobPicker = this.FindByName<DatePicker>("DobPicker");
            var termsCheckBox = this.FindByName<CheckBox>("TermsCheckBox");

            var name = nameEntry?.Text?.Trim() ?? string.Empty;
            var phone = phoneEntry?.Text?.Trim() ?? string.Empty;
            var password = passwordEntry?.Text ?? string.Empty;
            var gender = genderPicker?.SelectedItem as string ?? string.Empty;
            var interest = interestPicker?.SelectedItem as string ?? string.Empty;
            var dob = dobPicker?.Date ?? DateTime.MinValue;

            // ?? Validation ????????????????????????????????????????????????????
            if (termsCheckBox == null || !termsCheckBox.IsChecked)
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

            if (dob == DateTime.MinValue || dob > DateTime.Today)
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

            // ?? Show loading ??????????????????????????????????????????????????
            SetLoadingState(true);

            try
            {
                // Capture IP + location from ipinfo.io (non-fatal)
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
                    phoneEntry?.Focus();
                    return;
                }

                // Fetch the newly created user and store role in Preferences
                // Critical for the first user (Admin) to see the Admin Panel
                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();

                    bool hasPlus = phone.StartsWith("+");
                    var digits = new string(phone.Where(c => char.IsDigit(c)).ToArray());
                    var normalizedPhone = hasPlus ? "+" + digits : digits;

                    var newUser = await db.Table<Lock.Models.User>()
                                         .Where(u => u.PhoneNumber == normalizedPhone)
                                         .FirstOrDefaultAsync();

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
            }
            finally
            {
                SetLoadingState(false);
            }
        }

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

        private void ShowMessage(string text)
        {
            var messageLabel = this.FindByName<Label>("MessageLabel");
            if (messageLabel != null)
            {
                messageLabel.Text = text;
                messageLabel.IsVisible = true;
            }
        }
    }
}