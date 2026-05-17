// ????????????????????????????????????????????????????
// FILE — FULL REPLACEMENT
// Path: Lock/Pages/Admin/AdminRolePage.xaml.cs
// ????????????????????????????????????????????????????

using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Lock.Services.Admin;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lock.Pages.Admin
{
    // ?????????????????????????????????????????????????????????????
    // UserRoleViewModel
    // ?????????????????????????????????????????????????????????????
    public class UserRoleViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
        public DateTime LastActive { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropChanged(); }
        }

        private string _role = "User";
        public string Role
        {
            get => _role;
            set
            {
                _role = value;
                OnPropChanged();
                OnPropChanged(nameof(IsAdmin));
                OnPropChanged(nameof(RoleDisplay));
                OnPropChanged(nameof(RoleColor));
                OnPropChanged(nameof(ActionLabel));
                OnPropChanged(nameof(ActionBg));
                OnPropChanged(nameof(ActionBorder));
                OnPropChanged(nameof(ActionTextColor));
                OnPropChanged(nameof(CardStroke));
                OnPropChanged(nameof(AvatarRingColor));
                OnPropChanged(nameof(PermissionSummary));
                // NOTE: PermissionsButtonVisible is now ALWAYS true
                // Admins can also have page permissions managed
                OnPropChanged(nameof(PermissionsButtonVisible));
            }
        }

        // ?? Page Permissions ??????????????????????????????????????
        private string _deniedPages = string.Empty;
        public string DeniedPages
        {
            get => _deniedPages;
            set
            {
                _deniedPages = value;
                OnPropChanged();
                OnPropChanged(nameof(DeniedPageSet));
                OnPropChanged(nameof(PermissionSummary));
            }
        }

        public HashSet<string> DeniedPageSet =>
            string.IsNullOrWhiteSpace(_deniedPages)
                ? new HashSet<string>()
                : new HashSet<string>(_deniedPages.Split(',',
                    StringSplitOptions.RemoveEmptyEntries));

        public bool CanAccessPage(string pageKey)
        {
            // ? FIX: Admins now also respect DeniedPages
            // Previously admins always bypassed — now they can be restricted too
            return !DeniedPageSet.Contains(pageKey);
        }

        public string PermissionSummary
        {
            get
            {
                var denied = DeniedPageSet.Count;
                var allowed = PagePermissionDefinitions.Groups
                                .SelectMany(g => g.Pages).Count() - denied;
                if (denied == 0) return "? All pages allowed";
                return $"? {allowed} allowed  ·  ? {denied} blocked";
            }
        }

        // ? FIX: Always true — both admins AND users can have permissions managed
        public bool PermissionsButtonVisible => true;

        public bool IsAdmin => Role == "Admin";
        public string RoleDisplay => Role.ToUpper();
        public Color RoleColor => IsAdmin ? Color.FromArgb("#4A9EFF") : Color.FromArgb("#3A4555");
        public Color CardStroke => IsAdmin ? Color.FromArgb("#1A4A80") : Color.FromArgb("#161628");
        public Color AvatarRingColor => IsAdmin
            ? Color.FromArgb("#00C9C9") : Color.FromArgb("#1E1E3A");
        public string ActionLabel => IsAdmin ? "REVOKE" : "MAKE ADMIN";
        public Color ActionBg => IsAdmin ? Color.FromArgb("#2D0A0A") : Color.FromArgb("#0D2845");
        public Color ActionBorder => IsAdmin ? Color.FromArgb("#5A1515") : Color.FromArgb("#1A4A80");
        public Color ActionTextColor => IsAdmin ? Color.FromArgb("#FF6B6B") : Color.FromArgb("#4A9EFF");

        private ImageSource? _profileImageSource;
        public ImageSource? ProfileImageSource
        {
            get => _profileImageSource;
            set
            {
                _profileImageSource = value;
                OnPropChanged();
                OnPropChanged(nameof(HasImage));
                OnPropChanged(nameof(HasNoImage));
            }
        }
        public bool HasImage => ProfileImageSource != null;
        public bool HasNoImage => !HasImage;

        public bool IsOnline => DateTime.UtcNow - LastActive < TimeSpan.FromMinutes(5);
        public Color OnlineDotColor => IsOnline
            ? Color.FromArgb("#00E676") : Color.FromArgb("#2A2A45");

        private string _location = string.Empty;
        public string Location
        {
            get => _location;
            set { _location = value; OnPropChanged(); OnPropChanged(nameof(HasLocation)); }
        }
        public bool HasLocation => !string.IsNullOrWhiteSpace(_location);

        private string _mood = string.Empty;
        public string Mood
        {
            get => _mood;
            set
            {
                _mood = value;
                OnPropChanged();
                OnPropChanged(nameof(HasMood));
                OnPropChanged(nameof(MoodDisplay));
                OnPropChanged(nameof(MoodColor));
                OnPropChanged(nameof(MoodBg));
                OnPropChanged(nameof(MoodBorder));
            }
        }
        public bool HasMood => !string.IsNullOrWhiteSpace(_mood);
        public string MoodDisplay => string.IsNullOrWhiteSpace(_mood) ? string.Empty : _mood.ToUpper();

        public Color MoodColor => _mood?.ToLower() switch
        {
            "happy" or "excited" => Color.FromArgb("#FFD700"),
            "chill" or "relaxed" => Color.FromArgb("#00C9C9"),
            "sad" or "lonely" => Color.FromArgb("#6B9EFF"),
            "angry" or "frustrated" => Color.FromArgb("#FF6B6B"),
            "romantic" or "flirty" => Color.FromArgb("#FF69B4"),
            "bored" => Color.FromArgb("#9B59B6"),
            "anxious" or "stressed" => Color.FromArgb("#FF8C00"),
            _ => Color.FromArgb("#6B7280"),
        };
        public Color MoodBg => MoodColor.WithAlpha(0.12f);
        public Color MoodBorder => MoodColor.WithAlpha(0.35f);

        public string JoinedText => $"Joined {JoinDate:MMM yyyy}";
    }

    // ?????????????????????????????????????????????????????????????
    // AdminRolePage
    // ?????????????????????????????????????????????????????????????
    public partial class AdminRolePage : ContentPage
    {
        private const string CurrentUserPhoneKey = "current_user_phone";

        private List<UserRoleViewModel> _allItems = new();
        private ObservableCollection<UserRoleViewModel> _displayed = new();
        private CancellationTokenSource? _searchCts;
        private string _currentSearch = string.Empty;

        public AdminRolePage()
        {
            InitializeComponent();
            UsersCollection.ItemsSource = _displayed;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUsersAsync();
        }

        // ?? Load ??????????????????????????????????????????????????
        private async Task LoadUsersAsync()
        {
            ShowLoading(true);
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var users = await db.Table<User>().OrderBy(u => u.JoinDate).ToListAsync();

                _allItems.Clear();

                foreach (var u in users)
                {
                    var vm = new UserRoleViewModel
                    {
                        PhoneNumber = u.PhoneNumber,
                        Name = string.IsNullOrWhiteSpace(u.Name) ? u.PhoneNumber : u.Name,
                        Role = string.IsNullOrWhiteSpace(u.Role) ? "User" : u.Role,
                        JoinDate = u.JoinDate,
                        LastActive = u.LastActive,
                        Mood = u.Mood ?? string.Empty,
                        Location = BuildLocation(u),
                        DeniedPages = u.DeniedPages ?? string.Empty,
                    };

                    if (!string.IsNullOrWhiteSpace(u.ProfileImagePath) &&
                        File.Exists(u.ProfileImagePath))
                    {
                        try
                        {
                            var bytes = await File.ReadAllBytesAsync(u.ProfileImagePath);
                            if (bytes.Length > 0)
                            {
                                var ms = new MemoryStream(bytes);
                                vm.ProfileImageSource = ImageSource.FromStream(() => ms);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Image load error: {ex.Message}");
                        }
                    }

                    _allItems.Add(vm);
                }

                ApplyFilter(_currentSearch);
                UpdateSubtitle();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadUsersAsync error: {ex.Message}");
                await DisplayAlert("Error", "Could not load users. Please try again.", "OK");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private static string BuildLocation(User u)
        {
            var parts = new[] { u.State, u.Country }
                            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            return string.Join(", ", parts);
        }

        // ?? Filter ????????????????????????????????????????????????
        private void ApplyFilter(string query)
        {
            var trimmed = query?.Trim() ?? string.Empty;
            IEnumerable<UserRoleViewModel> filtered = _allItems;

            if (!string.IsNullOrEmpty(trimmed))
            {
                var lower = trimmed.ToLowerInvariant();
                filtered = _allItems.Where(vm =>
                    vm.Name.ToLowerInvariant().Contains(lower) ||
                    vm.PhoneNumber.Contains(trimmed));
            }

            var sorted = filtered
                .OrderByDescending(vm => vm.IsAdmin)
                .ThenBy(vm => vm.JoinDate)
                .ToList();

            _displayed.Clear();
            foreach (var vm in sorted) _displayed.Add(vm);

            EmptyView.IsVisible = _displayed.Count == 0;
            UsersCollection.IsVisible = _displayed.Count > 0;
            UpdateSubtitle();
        }

        private void UpdateSubtitle()
        {
            var adminCount = _allItems.Count(v => v.IsAdmin);
            SubtitleLabel.Text =
                $"{_allItems.Count} USERS · {adminCount} ADMIN{(adminCount == 1 ? "" : "S")}";
        }

        // ?? Search ????????????????????????????????????????????????
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            _currentSearch = e.NewTextValue ?? string.Empty;
            ClearIconGrid.IsVisible = !string.IsNullOrEmpty(_currentSearch);

            Task.Run(async () =>
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                    await MainThread.InvokeOnMainThreadAsync(() => ApplyFilter(_currentSearch));
            }, token);
        }

        private void OnClearSearch(object sender, EventArgs e)
        {
            SearchEntry.Text = string.Empty;
            _currentSearch = string.Empty;
            ClearIconGrid.IsVisible = false;
            ApplyFilter(string.Empty);
        }

        // ?? Role toggle ???????????????????????????????????????????
        private async void OnRoleToggleTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not UserRoleViewModel vm) return;

            var selfPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
            if (vm.IsAdmin && vm.PhoneNumber == selfPhone)
            {
                await DisplayAlert("Not Allowed",
                    "You cannot remove your own admin privileges.", "OK");
                return;
            }

            string newRole = vm.IsAdmin ? "User" : "Admin";
            string action = vm.IsAdmin ? "revoke admin from" : "grant admin to";

            bool confirm = await DisplayAlert(
                vm.IsAdmin ? "Revoke Admin" : "Grant Admin",
                $"Are you sure you want to {action} {vm.Name}?",
                "Yes", "Cancel");
            if (!confirm) return;

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                                   .Where(u => u.PhoneNumber == vm.PhoneNumber)
                                   .FirstOrDefaultAsync();
                if (user == null)
                {
                    await DisplayAlert("Error", "User not found.", "OK");
                    return;
                }

                user.Role = newRole;
                // ? Do NOT clear DeniedPages when promoting to Admin
                // Admins can also have individual page restrictions

                await db.UpdateAsync(user);

                vm.Role = newRole;

                if (vm.PhoneNumber == selfPhone)
                    Preferences.Set("current_user_role", newRole);

                UpdateSubtitle();

                var toast = vm.IsAdmin
                    ? $"?? {vm.Name} is now an Admin"
                    : $"? {vm.Name}'s admin access has been revoked";
                await AppShell.DisplayToastAsync(toast);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Role toggle error: {ex.Message}");
                await DisplayAlert("Error", $"Could not update role: {ex.Message}", "OK");
            }
        }

        // ?? Page permissions ??????????????????????????????????????
        private async void OnManagePermissionsTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not UserRoleViewModel vm) return;
            // ? FIX: No more "Admins can't have restrictions" gate
            // Everyone can have their page access managed now
            await ShowPermissionsSheetAsync(vm);
        }

        private async Task ShowPermissionsSheetAsync(UserRoleViewModel vm)
        {
            var toggles = new List<PagePermissionToggleVm>();
            foreach (var group in PagePermissionDefinitions.Groups)
            {
                foreach (var page in group.Pages)
                {
                    toggles.Add(new PagePermissionToggleVm
                    {
                        Key = page.Key,
                        DisplayName = page.DisplayName,
                        Description = page.Description,
                        Icon = page.Icon,
                        GroupName = group.GroupName,
                        AccentColor = group.AccentColor,
                        IsAllowed = vm.CanAccessPage(page.Key),
                    });
                }
            }

            var sheet = new PagePermissionsSheet(vm.Name, toggles);

            sheet.OnSaved += async (savedToggles) =>
            {
                var deniedStr = string.Join(",",
                    savedToggles.Where(t => !t.IsAllowed).Select(t => t.Key));

                try
                {
                    await DatabaseService.InitializeAsync();
                    var db = DatabaseService.GetConnection();
                    var user = await db.Table<User>()
                                       .Where(u => u.PhoneNumber == vm.PhoneNumber)
                                       .FirstOrDefaultAsync();
                    if (user != null)
                    {
                        user.DeniedPages = deniedStr;
                        await db.UpdateAsync(user);

                        // ? Update vm — this triggers PermissionSummary to refresh in the list
                        vm.DeniedPages = deniedStr;

                        await AppShell.DisplayToastAsync(
                            $"? Permissions updated for {vm.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Permission save error: {ex.Message}");
                    await DisplayAlert("Error", $"Could not save: {ex.Message}", "OK");
                }
            };

            await Navigation.PushModalAsync(sheet);
        }

        // ?? Navigation ????????????????????????????????????????????
        private async void OnBackTapped(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");

        private async void OnRefreshTapped(object sender, EventArgs e)
            => await LoadUsersAsync();

        private void ShowLoading(bool loading)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingView.IsVisible = loading;
                UsersCollection.IsVisible = !loading && _displayed.Count > 0;
                EmptyView.IsVisible = !loading && _displayed.Count == 0;
            });
        }
    }
}