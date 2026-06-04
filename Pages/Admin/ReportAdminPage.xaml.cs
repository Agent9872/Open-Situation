using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
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
using System.Threading.Tasks;

namespace Lock.Pages.Admin
{
    public partial class ReportAdminPage : ContentPage, INotifyPropertyChanged
    {
        private ObservableCollection<ReportViewModel> _reports = new();
        private ObservableCollection<AppealViewModel> _appeals = new();
        private ObservableCollection<UserLocationViewModel> _userLocations = new();

        // All users cached for client-side filtering
        private List<Lock.Models.User> _allUsersCache = new();
        private List<string> _availableCountries = new();

        private string _currentFilter = "All";
        private string _currentUsersFilter = "All";
        private string _currentGenderFilter = "All";
        private string _currentCountryFilter = "All";
        private string _usersSearchQuery = "";
        private bool _isLoading = false;

        public new event PropertyChangedEventHandler? PropertyChanged;

        private bool _isBulkMode = false;
        private bool _isAllSelected = false;
        public ObservableCollection<ReportViewModel> Reports
        {
            get => _reports;
            set { _reports = value; OnPropertyChanged(); }
        }

        public ObservableCollection<AppealViewModel> Appeals
        {
            get => _appeals;
            set { _appeals = value; OnPropertyChanged(); }
        }

        public ObservableCollection<UserLocationViewModel> UserLocations
        {
            get => _userLocations;
            set { _userLocations = value; OnPropertyChanged(); }
        }

        public ReportAdminPage()
        {
            InitializeComponent();
            BindingContext = this;

            // Hide default navigation/header
            Shell.SetNavBarIsVisible(this, false);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Ensure navigation bar stays hidden
            Shell.SetNavBarIsVisible(this, false);

            await LoadReportsAsync();
        }

        // ??????????????????????????????????????????????????????????????
        // TAB SWITCHING
        // ??????????????????????????????????????????????????????????????

        private async void OnTabTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not string tab) return;

            // Exit bulk mode whenever tab changes
            ExitBulkMode();

            bool isReports = tab == "Reports";
            bool isAppeals = tab == "Appeals";
            bool isUsers = tab == "Users";

            TabReports.BackgroundColor = isReports ? Color.FromArgb("#00C9C9") : Color.FromArgb("#16161C");
            TabReports.Stroke = isReports ? Colors.Transparent : Color.FromArgb("#1E1E3A");

            TabAppeals.BackgroundColor = isAppeals ? Color.FromArgb("#1A0A14") : Color.FromArgb("#16161C");
            TabAppeals.Stroke = isAppeals ? Color.FromArgb("#C2455E") : Color.FromArgb("#1E1E3A");

            TabUsers.BackgroundColor = isUsers ? Color.FromArgb("#0A1A14") : Color.FromArgb("#16161C");
            TabUsers.Stroke = isUsers ? Color.FromArgb("#4CAF50") : Color.FromArgb("#1E1E3A");

            ReportsCollection.IsVisible = isReports;
            AppealsCollection.IsVisible = isAppeals;
            UsersCollection.IsVisible = isUsers;
            FilterScrollView.IsVisible = isReports;
            UsersSearchBar.IsVisible = isUsers;
            UsersControlsRow.IsVisible = isUsers;
            UsersStatsBar.IsVisible = isUsers;
            BulkToggleBtn.IsVisible = isUsers;   // ? show bulk toggle only on Users tab
            BulkSelectionBar.IsVisible = false;
            BulkActionBar.IsVisible = false;

            if (isReports) await LoadReportsAsync();
            else if (isAppeals) await LoadAppealsAsync();
            else await LoadUsersAsync();
        }

        private void OnBulkToggleTapped(object sender, TappedEventArgs e)
        {
            if (_isBulkMode)
                ExitBulkMode();
            else
                EnterBulkMode();
        }

        private void EnterBulkMode()
        {
            _isBulkMode = true;
            _isAllSelected = false;

            BulkToggleIcon.Text = "X";          // was "?" (broken emoji)
            BulkToggleBtn.BackgroundColor = Color.FromArgb("#1E1E3A");

            BulkSelectionBar.IsVisible = true;
            BulkActionBar.IsVisible = false;
            SelectAllCheckmark.IsVisible = false;

            foreach (var vm in UserLocations)
            {
                vm.IsBulkMode = true;
                vm.IsSelected = false;
            }

            UpdateBulkUI();
        }

        private void ExitBulkMode()
        {
            _isBulkMode = false;
            _isAllSelected = false;

            BulkToggleIcon.Text = "[ ]";        // was "?" (broken emoji)
            BulkToggleBtn.BackgroundColor = Color.FromArgb("#12122A");

            BulkSelectionBar.IsVisible = false;
            BulkActionBar.IsVisible = false;
            SelectAllCheckmark.IsVisible = false;

            foreach (var vm in UserLocations)
            {
                vm.IsBulkMode = false;
                vm.IsSelected = false;
            }
        }

        private void OnSelectAllTapped(object sender, TappedEventArgs e)
        {
            _isAllSelected = !_isAllSelected;

            foreach (var vm in UserLocations)
                vm.IsSelected = _isAllSelected;

            SelectAllCheckmark.IsVisible = _isAllSelected;
            UpdateBulkUI();
        }

        private async void OnUserRowTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not UserLocationViewModel vm) return;

            if (_isBulkMode)
            {
                vm.IsSelected = !vm.IsSelected;

                // Keep select-all checkbox in sync
                _isAllSelected = UserLocations.All(u => u.IsSelected);
                SelectAllCheckmark.IsVisible = _isAllSelected;

                UpdateBulkUI();
            }
            else
            {
                await ShowUserLocationActionsAsync(vm.User);
            }
        }

        private void UpdateBulkUI()
        {
            int count = UserLocations.Count(u => u.IsSelected);

            SelectedCountLabel.Text = $"{count} selected";
            SelectedCountBadge.IsVisible = count > 0;
            BulkActionBar.IsVisible = count > 0;

            BulkSelectionLabel.Text = _isAllSelected ? "All selected" : "Select All";
        }

        private List<UserLocationViewModel> GetSelectedUsers()
            => UserLocations.Where(u => u.IsSelected && u.User.Role != "Admin").ToList();

        // ??????????????????????????????????????????????????????????????????????
        //  BULK — Issue Warning
        // ??????????????????????????????????????????????????????????????????????

        private async void OnBulkWarnTapped(object sender, TappedEventArgs e)
        {
            var selected = GetSelectedUsers();
            if (!selected.Any()) return;

            string preset = await DisplayActionSheet(
                $"Warning reason for {selected.Count} user(s)", "Cancel", null,
                "Your recent behavior has violated our community guidelines. Please review them to avoid further action.",
                "We have received reports about your interactions. Continued violations may result in a suspension.",
                "Sending unsolicited or inappropriate content is not permitted on this platform.",
                "Harassment or disrespectful behavior toward other users will not be tolerated.");

            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Warning Message",
                $"This will be sent to {selected.Count} user(s). Edit if needed:",
                initialValue: preset);

            string warningText = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Bulk Warning",
                $"Issue this warning to {selected.Count} user(s)?", "Warn All", "Cancel");
            if (!confirm) return;

            int success = 0;
            foreach (var vm in selected)
            {
                bool warned = await UserService.IssueWarningAsync(vm.Phone, warningText);
                if (warned) success++;
            }

            await LoadUsersAsync();
            ExitBulkMode();
            await DisplayAlert("Done", $"Warning issued to {success}/{selected.Count} user(s).", "OK");
        }

        // ??????????????????????????????????????????????????????????????????????
        //  BULK — Temporary Ban
        // ??????????????????????????????????????????????????????????????????????

        private async void OnBulkTempBanTapped(object sender, TappedEventArgs e)
        {
            var selected = GetSelectedUsers();
            if (!selected.Any()) return;

            string durationCategory = await DisplayActionSheet("Select Ban Duration", "Cancel", null, "Hours", "Days");
            if (durationCategory == "Cancel" || string.IsNullOrEmpty(durationCategory)) return;

            string duration;
            DateTime expiresAt;

            if (durationCategory == "Hours")
            {
                duration = await DisplayActionSheet("Select Hours", "Cancel", null,
                    "1 hour", "6 hours", "12 hours", "24 hours");
                if (duration == "Cancel" || string.IsNullOrEmpty(duration)) return;
                expiresAt = duration switch
                {
                    "1 hour" => DateTime.UtcNow.AddHours(1),
                    "6 hours" => DateTime.UtcNow.AddHours(6),
                    "12 hours" => DateTime.UtcNow.AddHours(12),
                    _ => DateTime.UtcNow.AddHours(24)
                };
            }
            else
            {
                duration = await DisplayActionSheet("Select Days", "Cancel", null,
                    "2 days", "3 days", "7 days", "14 days", "30 days");
                if (duration == "Cancel" || string.IsNullOrEmpty(duration)) return;
                expiresAt = duration switch
                {
                    "2 days" => DateTime.UtcNow.AddDays(2),
                    "3 days" => DateTime.UtcNow.AddDays(3),
                    "7 days" => DateTime.UtcNow.AddDays(7),
                    "14 days" => DateTime.UtcNow.AddDays(14),
                    _ => DateTime.UtcNow.AddDays(30)
                };
            }

            string preset = await DisplayActionSheet("Select a Suspension Reason", "Cancel", null,
                "Repeated violations of our community guidelines.",
                "Sending spam, unsolicited messages, or inappropriate content.",
                "Harassment or threatening behavior toward another user.",
                "Sharing content that violates our terms of service.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Suspension Reason",
                $"Will apply to {selected.Count} user(s). Edit if needed:", initialValue: preset);
            string reason = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Bulk Temporary Ban",
                $"Suspend {selected.Count} user(s) for {duration}?\n" +
                $"Ends: {expiresAt:MMM dd, yyyy 'at' h:mm tt} UTC\nReason: {reason}",
                "Ban All", "Cancel");
            if (!confirm) return;

            int success = 0;
            foreach (var vm in selected)
            {
                bool banned = await UserService.BanUserAsync(vm.Phone, "temporary", reason, expiresAt);
                if (banned) success++;
            }

            await LoadUsersAsync();
            ExitBulkMode();
            await DisplayAlert("Done", $"Temporary ban applied to {success}/{selected.Count} user(s).", "OK");
        }

        // ??????????????????????????????????????????????????????????????????????
        //  BULK — Permanent Ban
        // ??????????????????????????????????????????????????????????????????????

        private async void OnBulkPermBanTapped(object sender, TappedEventArgs e)
        {
            var selected = GetSelectedUsers();
            if (!selected.Any()) return;

            string preset = await DisplayActionSheet("Select a Ban Reason", "Cancel", null,
                "Severe and repeated violations of our community guidelines.",
                "Distribution of illegal, explicit, or harmful content.",
                "Predatory, abusive, or threatening behavior toward other users.",
                "Creating a fake identity or impersonating another person.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Ban Reason",
                $"Will apply to {selected.Count} user(s). Edit if needed:", initialValue: preset);
            string reason = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Bulk Permanent Ban",
                $"PERMANENTLY ban {selected.Count} user(s)?\nReason: {reason}\n\nThis CANNOT be undone.",
                "Permanently Ban All", "Cancel");
            if (!confirm) return;

            int success = 0;
            foreach (var vm in selected)
            {
                bool banned = await UserService.BanUserAsync(vm.Phone, "permanent", reason, null);
                if (banned) success++;
            }

            await LoadUsersAsync();
            ExitBulkMode();
            await DisplayAlert("Done", $"Permanent ban applied to {success}/{selected.Count} user(s).", "OK");
        }


        // ??????????????????????????????????????????????????????????????
        // FILTER PILLS — REPORTS
        // ??????????????????????????????????????????????????????????????

        private async void OnFilterTapped(object sender, EventArgs e)
        {
            if (e is TappedEventArgs tapped && tapped.Parameter is string filter)
            {
                _currentFilter = filter;
                UpdateFilterPillStyles(filter);
                await LoadReportsAsync();
            }
        }

        private void UpdateFilterPillStyles(string active)
        {
            var allPills = new[]
            {
                ("All",         FilterAll),
                ("Pending",     FilterPending),
                ("UnderReview", FilterReview),
                ("Resolved",    FilterResolved),
                ("Dismissed",   FilterDismissed)
            };

            foreach (var (key, pill) in allPills)
            {
                bool isActive = key == active;
                pill.BackgroundColor = isActive ? Color.FromArgb("#00C9C9") : Color.FromArgb("#12122A");
                pill.Stroke = isActive ? Colors.Transparent : Color.FromArgb("#1E1E3A");
            }
        }

        private async void OnRefreshTapped(object sender, EventArgs e)
            => await LoadReportsAsync();

        // ??????????????????????????????????????????????????????????????
        // FILTER PILLS — USERS STATUS
        // ??????????????????????????????????????????????????????????????

        private async void OnUsersFilterTapped(object sender, EventArgs e)
        {
            if (e is TappedEventArgs tapped && tapped.Parameter is string filter)
            {
                _currentUsersFilter = filter;
                UpdateUsersFilterPillStyles(filter);
                ApplyUsersFilters();
            }
        }

        private void UpdateUsersFilterPillStyles(string active)
        {
            var allPills = new[]
            {
                ("All",    UsersFilterAll),
                ("Active", UsersFilterActive),
                ("Banned", UsersFilterBanned),
                ("Warned", UsersFilterWarned)
            };

            foreach (var (key, pill) in allPills)
            {
                bool isActive = key == active;
                pill.BackgroundColor = isActive ? Color.FromArgb("#4CAF50") : Color.FromArgb("#12122A");
                pill.Stroke = isActive ? Colors.Transparent : Color.FromArgb("#1E1E3A");
            }
        }

        // ??????????????????????????????????????????????????????????????
        // DROPDOWN FILTERS — GENDER & COUNTRY
        // ??????????????????????????????????????????????????????????????

        private void OnGenderPickerChanged(object sender, EventArgs e)
        {
            if (_suppressPickerEvents) return;   // ? guard
            if (GenderPicker.SelectedItem is string selected)
            {
                _currentGenderFilter = selected;
                ApplyUsersFilters();
            }
        }

        private void OnCountryPickerChanged(object sender, EventArgs e)
        {
            if (_suppressPickerEvents) return;   // ? guard
            if (CountryPicker.SelectedItem is string selected)
            {
                _currentCountryFilter = selected;
                ApplyUsersFilters();
            }
        }


        private void LoadCountryPicker()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CountryPicker.Items.Clear();
                CountryPicker.Items.Add("All");

                foreach (var country in _availableCountries.OrderBy(c => c))
                {
                    CountryPicker.Items.Add(country);
                }

                // Set selected index based on current filter
                if (_currentCountryFilter == "All")
                    CountryPicker.SelectedIndex = 0;
                else
                {
                    int idx = CountryPicker.Items.IndexOf(_currentCountryFilter);
                    CountryPicker.SelectedIndex = idx >= 0 ? idx : 0;
                }
            });
        }

        // ??????????????????????????????????????????????????????????????
        // SEARCH
        // ??????????????????????????????????????????????????????????????

        private void OnUsersSearchChanged(object sender, TextChangedEventArgs e)
        {
            _usersSearchQuery = e.NewTextValue?.Trim() ?? "";
            UsersSearchClear.IsVisible = !string.IsNullOrEmpty(_usersSearchQuery);
            ApplyUsersFilters();
        }

        private void OnUsersSearchClearTapped(object sender, EventArgs e)
        {
            UsersSearchEntry.Text = "";
            _usersSearchQuery = "";
            UsersSearchClear.IsVisible = false;
            ApplyUsersFilters();
        }

        // ??????????????????????????????????????????????????????????????
        // APPLY ALL USERS FILTERS (client-side)
        // ??????????????????????????????????????????????????????????????

        private void ApplyUsersFilters()
        {
            IEnumerable<Lock.Models.User> filtered = _allUsersCache;

            

            // Status filter
            filtered = _currentUsersFilter switch
            {
                "Active" => filtered.Where(u => !u.IsBanned),
                "Banned" => filtered.Where(u => u.IsBanned),
                "Warned" => filtered.Where(u => u.HasWarning && !u.WarningAcknowledged),
                _ => filtered
            };

            // Gender filter
            if (_currentGenderFilter != "All")
                filtered = filtered.Where(u =>
                    string.Equals(u.Gender, _currentGenderFilter, StringComparison.OrdinalIgnoreCase));

            // Country filter
            if (_currentCountryFilter != "All")
                filtered = filtered.Where(u =>
                    string.Equals(u.Country, _currentCountryFilter, StringComparison.OrdinalIgnoreCase));

            // Search
            if (!string.IsNullOrWhiteSpace(_usersSearchQuery))
            {
                string q = _usersSearchQuery.ToLowerInvariant();
                filtered = filtered.Where(u =>
                    (u.Name ?? "").ToLowerInvariant().Contains(q) ||
                    (u.Country ?? "").ToLowerInvariant().Contains(q) ||
                    (u.State ?? "").ToLowerInvariant().Contains(q));
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                UserLocations.Clear();
                foreach (var user in filtered)
                    UserLocations.Add(new UserLocationViewModel(user));

                EmptyState.IsVisible = !UserLocations.Any();
            });
        }
        // ??????????????????????????????????????????????????????????????
        // LOAD REPORTS
        // ??????????????????????????????????????????????????????????????

        private async Task LoadReportsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                LoadingState.IsVisible = true;
                EmptyState.IsVisible = false;

                ReportStatus? status = _currentFilter switch
                {
                    "Pending" => ReportStatus.Pending,
                    "UnderReview" => ReportStatus.UnderReview,
                    "Resolved" => ReportStatus.Resolved,
                    "Dismissed" => ReportStatus.Dismissed,
                    _ => null
                };

                // Replace this:
                // await DatabaseService.InitializeAsync();
                // var rawReports = await ReportService.GetAllReportsAsync(status);

                // With this (ReportService already uses Supabase, so just call it):
                var rawReports = await ReportService.GetAllReportsAsync(status);

                var vms = rawReports
                    .OrderByDescending(r => r.ReportedAt)
                    .Select(r => new ReportViewModel(r))
                    .ToList();

                Reports.Clear();
                foreach (var vm in vms)
                    Reports.Add(vm);

                await UpdateStatsAsync();
                EmptyState.IsVisible = !Reports.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadReportsAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to load reports: {ex.Message}", "OK");
            }
            finally
            {
                _isLoading = false;
                LoadingState.IsVisible = false;
            }
        }

        public async Task EmergencyUnbanAdminAsync()
        {
            try
            {
                // Replace this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // var adminUser = await db.Table<Lock.Models.User>()
                //     .Where(u => u.PhoneNumber == "08088206738")
                //     .FirstOrDefaultAsync();

                // With this Supabase code:
                var adminUsers = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    "PhoneNumber=eq.08088206738&limit=1");
                var adminUser = adminUsers.FirstOrDefault();

                if (adminUser != null)
                {
                    adminUser.IsBanned = false;
                    adminUser.BanType = null;
                    adminUser.BanReason = null;
                    adminUser.BanExpiresAt = null;
                    adminUser.AppealStatus = null;
                    adminUser.ModerationNote = null;

                    // Replace: await db.UpdateAsync(adminUser);
                    await SupabaseService.UpdateAsync("Users", "PhoneNumber=eq.08088206738", adminUser);

                    await DisplayAlert("Success", $"Admin user {adminUser.Name} has been unbanned!", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "User not found!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to unban: {ex.Message}", "OK");
            }
        }

        private async Task UpdateStatsAsync()
        {
            try
            {
                var all = await ReportService.GetAllReportsAsync(null);

                int total = all.Count;
                int pending = all.Count(r => r.Status == ReportStatus.Pending);
                int review = all.Count(r => r.Status == ReportStatus.UnderReview);
                int resolved = all.Count(r => r.Status == ReportStatus.Resolved);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalCountLabel.Text = total.ToString();
                    PendingCountLabel.Text = pending.ToString();
                    ReviewCountLabel.Text = review.ToString();
                    ResolvedCountLabel.Text = resolved.ToString();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateStatsAsync error: {ex}");
            }
        }

        // ??????????????????????????????????????????????????????????????
        // LOAD APPEALS
        // ??????????????????????????????????????????????????????????????

        private async Task LoadAppealsAsync()
        {
            try
            {
                LoadingState.IsVisible = true;
                EmptyState.IsVisible = false;

                // Replace this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // var usersWithAppeals = await db.Table<Lock.Models.User>()
                //     .Where(u => u.AppealStatus == "pending" ||
                //                 u.AppealStatus == "approved" ||
                //                 u.AppealStatus == "rejected")
                //     .OrderByDescending(u => u.AppealSubmittedAt)
                //     .ToListAsync();

                // With this Supabase code:
                var usersWithAppeals = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    "or(AppealStatus.eq.pending,AppealStatus.eq.approved,AppealStatus.eq.rejected)&order=AppealSubmittedAt.desc");

                Appeals.Clear();
                foreach (var user in usersWithAppeals)
                    Appeals.Add(new AppealViewModel(user));

                int pendingCount = usersWithAppeals.Count(u => u.AppealStatus == "pending");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AppealsCountLabel.Text = pendingCount.ToString();
                    AppealsBadge.IsVisible = pendingCount > 0;
                    AppealsBadgeLabel.Text = pendingCount.ToString();
                });

                EmptyState.IsVisible = !Appeals.Any();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAppealsAsync error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to load appeals: {ex.Message}", "OK");
            }
            finally
            {
                LoadingState.IsVisible = false;
            }
        }

        // ??????????????????????????????????????????????????????????????
        // LOAD USERS
        // ??????????????????????????????????????????????????????????????

        private bool _suppressPickerEvents = false;

        private async Task LoadUsersAsync()
        {
            try
            {
                LoadingState.IsVisible = true;
                EmptyState.IsVisible = false;

                // Replace this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // _allUsersCache = await db.Table<Lock.Models.User>()
                //     .OrderBy(u => u.Country)
                //     .ThenBy(u => u.State)
                //     .ToListAsync();

                // With this Supabase code:
                _allUsersCache = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    "order=Country.asc,State.asc");

                _availableCountries = _allUsersCache
                    .Where(u => !string.IsNullOrWhiteSpace(u.Country))
                    .Select(u => u.Country!)
                    .Distinct()
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _suppressPickerEvents = true;

                    LoadCountryPicker();
                    GenderPicker.SelectedIndex = 0;
                    CountryPicker.SelectedIndex = 0;

                    _currentGenderFilter = "All";
                    _currentCountryFilter = "All";

                    _suppressPickerEvents = false;

                    // Stats exclude admin
                    UsersTotalLabel.Text = _allUsersCache.Count(u => (u.Role ?? "User") != "Admin").ToString();
                    UsersBannedLabel.Text = _allUsersCache.Count(u => u.IsBanned && (u.Role ?? "User") != "Admin").ToString();
                    UsersActiveLabel.Text = _allUsersCache.Count(u => !u.IsBanned && (u.Role ?? "User") != "Admin").ToString();

                    ApplyUsersFilters();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadUsersAsync error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to load users: {ex.Message}", "OK");
            }
            finally
            {
                LoadingState.IsVisible = false;
            }
        }
        // ??????????????????????????????????????????????????????????????
        // ROW TAPPED
        // ??????????????????????????????????????????????????????????????

        private async void OnReportTapped(object sender, EventArgs e)
        {
            if (e is TappedEventArgs tapped)
            {
                if (tapped.Parameter is ReportViewModel reportVm)
                    await ShowReportDetailsAsync(reportVm.Report);
                else if (tapped.Parameter is AppealViewModel appealVm)
                    await ShowAppealDetailsAsync(appealVm.User);
                else if (tapped.Parameter is UserLocationViewModel userVm)
                    await ShowUserLocationActionsAsync(userVm.User);
            }
        }

        // ??????????????????????????????????????????????????????????????
        // REPORT DETAILS
        // ??????????????????????????????????????????????????????????????

        private async Task ShowReportDetailsAsync(Report report)
        {
            if (report == null) return;

            bool isClosed = report.Status == ReportStatus.Resolved
                         || report.Status == ReportStatus.Dismissed
                         || report.Status == ReportStatus.ActionTaken;

            if (isClosed)
            {
                await Navigation.PushAsync(new FullReportDetailsPage(report, this));
                return;
            }

            string action = await DisplayActionSheet(
                $"Report - {report.ReportedUserName}",
                "Close", null,
                "View Full Report",
                "Mark as Resolved",
                "Mark as Under Review",
                "Dismiss Report",
                "Issue Warning",
                "Temporary Ban",
                "Permanent Ban"
            );

            switch (action)
            {
                case "View Full Report":
                    await Navigation.PushAsync(new FullReportDetailsPage(report, this));
                    break;

                case "Mark as Resolved":
                    {
                        string preset = await DisplayActionSheet("Select a Resolve Reason", "Cancel", null,
                            "We reviewed your account and found no further issues.",
                            "The reported content has been removed and the issue is now resolved.",
                            "Our team has investigated and taken the necessary corrective action.",
                            "This matter has been resolved in line with our community guidelines.");
                        if (preset == "Cancel" || string.IsNullOrEmpty(preset)) break;
                        string note = await DisplayPromptAsync("Resolve Note", "Edit if needed, or tap OK:", initialValue: preset);
                        string msg = string.IsNullOrWhiteSpace(note) ? preset : note;
                        await UserService.ResolveReportAsync(report.ReportedUserPhone, msg);
                        await UpdateStatusAsync(report, ReportStatus.Resolved, $"Resolved — Note: {msg}");
                        break;
                    }

                case "Mark as Under Review":
                    {
                        string preset = await DisplayActionSheet("Select an Under Review Reason", "Cancel", null,
                            "Your account is currently under review by our moderation team.",
                            "We have received a report and are actively investigating your account activity.",
                            "Our team is reviewing your recent interactions to ensure guideline compliance.",
                            "A review has been initiated. We will notify you once it is complete.");
                        if (preset == "Cancel" || string.IsNullOrEmpty(preset)) break;
                        string note = await DisplayPromptAsync("Under Review Note", "Edit if needed, or tap OK:", initialValue: preset);
                        string msg = string.IsNullOrWhiteSpace(note) ? preset : note;
                        await UserService.ResolveReportAsync(report.ReportedUserPhone, msg);
                        await UpdateStatusAsync(report, ReportStatus.UnderReview, $"Under review — Note: {msg}");
                        break;
                    }

                case "Dismiss Report":
                    {
                        bool confirm = await DisplayAlert("Dismiss Report",
                            "No action will be taken against the reported user.", "Dismiss", "Cancel");
                        if (!confirm) break;
                        string preset = await DisplayActionSheet("Select a Dismiss Reason", "Cancel", null,
                            "After review, no violation of our community guidelines was found.",
                            "This report did not meet the threshold for a policy violation.",
                            "The reported content was reviewed and found to be within acceptable use.",
                            "Our team found insufficient evidence to support this report.");
                        if (preset == "Cancel" || string.IsNullOrEmpty(preset)) break;
                        string note = await DisplayPromptAsync("Dismiss Note", "Edit if needed, or tap OK:", initialValue: preset);
                        string msg = string.IsNullOrWhiteSpace(note) ? preset : note;
                        await UserService.DismissReportAsync(report.ReportedUserPhone);
                        await UserService.ResolveReportAsync(report.ReportedUserPhone, msg);
                        await UpdateStatusAsync(report, ReportStatus.Dismissed, $"Dismissed — Note: {msg}");
                        break;
                    }

                case "Issue Warning":
                    {
                        string preset = await DisplayActionSheet("Select a Warning Reason", "Cancel", null,
                            "Your recent behavior has violated our community guidelines. Please review them to avoid further action.",
                            "We have received reports about your interactions. Continued violations may result in a suspension.",
                            "Sending unsolicited or inappropriate content is not permitted on this platform.",
                            "Harassment or disrespectful behavior toward other users will not be tolerated.");
                        if (preset == "Cancel" || string.IsNullOrEmpty(preset)) break;
                        string note = await DisplayPromptAsync("Warning Message", "Edit if needed, or tap OK:", initialValue: preset);
                        string warningText = string.IsNullOrWhiteSpace(note) ? preset : note;
                        bool warned = await UserService.IssueWarningAsync(report.ReportedUserPhone, warningText);
                        if (warned)
                        {
                            await UpdateStatusAsync(report, ReportStatus.ActionTaken, $"Warning issued — Message: {warningText}");
                            await DisplayAlert("Warning Issued", $"Warning sent to {report.ReportedUserName}.", "OK");
                        }
                        else
                            await DisplayAlert("Error", $"Could not issue warning. Phone [{report.ReportedUserPhone}] not found.", "OK");
                        break;
                    }

                case "Temporary Ban":
                    await ApplyTemporaryBanAsync(report.ReportedUserPhone, report.ReportedUserName,
                        async (status, notes) => await UpdateStatusAsync(report, status, notes));
                    break;

                case "Permanent Ban":
                    await ApplyPermanentBanAsync(report.ReportedUserPhone, report.ReportedUserName,
                        async (status, notes) => await UpdateStatusAsync(report, status, notes));
                    break;
            }
        }

        // ??????????????????????????????????????????????????????????????
        // APPEAL DETAILS
        // ??????????????????????????????????????????????????????????????

        private async Task ShowAppealDetailsAsync(Lock.Models.User user)
        {
            bool isClosed = user.AppealStatus == "approved" || user.AppealStatus == "rejected";

            if (isClosed)
            {
                await DisplayAlert($"Appeal - {user.Name}",
                    $"This appeal has already been {user.AppealStatus}.\n\n" +
                    $"Name: {user.Name}\nPhone: {user.PhoneNumber}\n" +
                    $"Ban Type: {user.BanType}\nBan Reason: {user.BanReason}\n" +
                    $"Appeal Status: {user.AppealStatus?.ToUpper()}\n" +
                    $"Reviewed At: {user.AppealReviewedAt:MMM dd, yyyy h:mm tt}\n\n" +
                    $"Admin Response:\n{user.AppealAdminResponse}",
                    "Close");
                return;
            }

            string action = await DisplayActionSheet(
                $"Appeal - {user.Name}", "Close", null,
                "Approve Appeal - Unban User",
                "Reject Appeal - Keep Ban",
                "View Full Appeal");

            switch (action)
            {
                case "Approve Appeal - Unban User":
                    {
                        string approveNote = await DisplayPromptAsync("Approval Note",
                            "Add a message to send to the user:",
                            initialValue: "Your appeal has been reviewed and approved. Your account has been reinstated. Please ensure you follow our community guidelines going forward.");

                        bool confirmApprove = await DisplayAlert("Confirm Approval",
                            $"Unban {user.Name} and approve their appeal?", "Approve", "Cancel");
                        if (!confirmApprove) break;

                        await UserService.UnbanUserAsync(user.PhoneNumber);

                        // FIXED: Use Supabase instead of SQLite
                        var dbUsers = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(user.PhoneNumber)}&limit=1");
                        var dbUser = dbUsers.FirstOrDefault();

                        if (dbUser != null)
                        {
                            dbUser.AppealStatus = "approved";
                            dbUser.AppealAdminResponse = approveNote ?? string.Empty;
                            dbUser.AppealReviewedAt = DateTime.UtcNow;
                            dbUser.ModerationNote = $"Your appeal was approved. {approveNote}";
                            dbUser.ModerationUpdatedAt = DateTime.UtcNow;

                            await SupabaseService.UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(user.PhoneNumber)}", dbUser);
                        }

                        await LoadAppealsAsync();
                        await DisplayAlert("Approved", $"{user.Name}'s appeal has been approved and their account restored.", "OK");
                        break;
                    }

                case "Reject Appeal - Keep Ban":
                    {
                        string rejectNote = await DisplayPromptAsync("Rejection Reason",
                            "Add a message to send to the user:",
                            initialValue: "After careful review, your appeal has been rejected. The original moderation decision stands. If you have new information, you may submit another appeal after 30 days.");

                        bool confirmReject = await DisplayAlert("Confirm Rejection",
                            $"Reject {user.Name}'s appeal and keep the ban?", "Reject", "Cancel");
                        if (!confirmReject) break;

                        // FIXED: Use Supabase instead of SQLite
                        var dbUsers = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(user.PhoneNumber)}&limit=1");
                        var dbUser = dbUsers.FirstOrDefault();

                        if (dbUser != null)
                        {
                            dbUser.AppealStatus = "rejected";
                            dbUser.AppealAdminResponse = rejectNote ?? string.Empty;
                            dbUser.AppealReviewedAt = DateTime.UtcNow;

                            await SupabaseService.UpdateAsync("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(user.PhoneNumber)}", dbUser);
                        }

                        await LoadAppealsAsync();
                        await DisplayAlert("Rejected", $"{user.Name}'s appeal has been rejected.", "OK");
                        break;
                    }

                case "View Full Appeal":
                    await DisplayAlert($"Appeal - {user.Name}",
                        $"Name: {user.Name}\nPhone: {user.PhoneNumber}\n" +
                        $"Ban Type: {user.BanType}\nBan Reason: {user.BanReason}\n" +
                        $"Submitted: {user.AppealSubmittedAt:MMM dd, yyyy h:mm tt}\n\nAppeal:\n{user.AppealText}",
                        "Close");
                    break;
            }
        }


        // ??????????????????????????????????????????????????????????????
        // USER LOCATION ACTIONS
        // ??????????????????????????????????????????????????????????????

        private async Task ShowUserLocationActionsAsync(Lock.Models.User user)
        {
            if (user == null) return;

            string statusLine = user.IsBanned
                ? $"Status: BANNED ({user.BanType}) — {user.BanReason}"
                : user.HasWarning && !user.WarningAcknowledged
                    ? "Status: WARNED"
                    : "Status: Active";

            string action = await DisplayActionSheet(
                $"{user.Name} — {user.Country}, {user.State}",
                "Close", null,
                "View Full Profile",
                user.IsBanned ? "Unban User" : "Issue Warning",
                "Temporary Ban",
                "Permanent Ban"
            );

            switch (action)
            {
                case "View Full Profile":
                    await DisplayAlert($"User Profile — {user.Name}",
                        $"Name: {user.Name}\nPhone: {user.PhoneNumber}\n" +
                        $"Gender: {user.Gender}\nAge: {user.GetAge()}\n" +
                        $"Country: {user.Country}\nState: {user.State}\n" +
                        $"Joined: {user.JoinDate:MMM dd, yyyy}\n" +
                        $"Last Active: {user.LastActive:MMM dd, yyyy h:mm tt}\n\n" +
                        $"{statusLine}\n" +
                        (user.IsBanned && user.BanExpiresAt.HasValue
                            ? $"Ban Expires: {user.BanExpiresAt:MMM dd, yyyy HH:mm} UTC\n" : "") +
                        (!string.IsNullOrEmpty(user.ModerationNote)
                            ? $"\nModeration Note:\n{user.ModerationNote}" : ""),
                        "Close");
                    break;

                case "Unban User":
                    if (user.IsBanned)
                    {
                        bool confirmUnban = await DisplayAlert("Unban User",
                            $"Remove ban from {user.Name}?", "Unban", "Cancel");
                        if (!confirmUnban) break;
                        await UserService.UnbanUserAsync(user.PhoneNumber);
                        await LoadUsersAsync();
                        await DisplayAlert("Unbanned", $"{user.Name} has been unbanned.", "OK");
                    }
                    break;

                case "Issue Warning":
                    {
                        string preset = await DisplayActionSheet("Select a Warning Reason", "Cancel", null,
                            "Your recent behavior has violated our community guidelines. Please review them to avoid further action.",
                            "We have received reports about your interactions. Continued violations may result in a suspension.",
                            "Sending unsolicited or inappropriate content is not permitted on this platform.",
                            "Harassment or disrespectful behavior toward other users will not be tolerated.");
                        if (preset == "Cancel" || string.IsNullOrEmpty(preset)) break;
                        string note = await DisplayPromptAsync("Warning Message", "Edit if needed, or tap OK:", initialValue: preset);
                        string warningText = string.IsNullOrWhiteSpace(note) ? preset : note;
                        bool warned = await UserService.IssueWarningAsync(user.PhoneNumber, warningText);
                        if (warned)
                        {
                            await LoadUsersAsync();
                            await DisplayAlert("Warning Issued", $"Warning sent to {user.Name}.", "OK");
                        }
                        else
                            await DisplayAlert("Error", $"Could not issue warning to {user.Name}.", "OK");
                        break;
                    }

                case "Temporary Ban":
                    await ApplyTemporaryBanAsync(user.PhoneNumber, user.Name,
                        async (_, __) => await LoadUsersAsync());
                    break;

                case "Permanent Ban":
                    await ApplyPermanentBanAsync(user.PhoneNumber, user.Name,
                        async (_, __) => await LoadUsersAsync());
                    break;
            }
        }

        // ??????????????????????????????????????????????????????????????
        // SHARED BAN HELPERS
        // ??????????????????????????????????????????????????????????????

        private async Task ApplyTemporaryBanAsync(
            string phone, string name,
            Func<ReportStatus, string, Task> onSuccess)
        {
            string durationCategory = await DisplayActionSheet("Select Ban Duration", "Cancel", null, "Hours", "Days");
            if (durationCategory == "Cancel" || string.IsNullOrEmpty(durationCategory)) return;

            string duration;
            DateTime expiresAt;

            if (durationCategory == "Hours")
            {
                duration = await DisplayActionSheet("Select Hours", "Cancel", null,
                    "1 hour", "6 hours", "12 hours", "24 hours");
                if (duration == "Cancel" || string.IsNullOrEmpty(duration)) return;
                expiresAt = duration switch
                {
                    "1 hour" => DateTime.UtcNow.AddHours(1),
                    "6 hours" => DateTime.UtcNow.AddHours(6),
                    "12 hours" => DateTime.UtcNow.AddHours(12),
                    _ => DateTime.UtcNow.AddHours(24)
                };
            }
            else
            {
                duration = await DisplayActionSheet("Select Days", "Cancel", null,
                    "2 days", "3 days", "7 days", "14 days", "30 days");
                if (duration == "Cancel" || string.IsNullOrEmpty(duration)) return;
                expiresAt = duration switch
                {
                    "2 days" => DateTime.UtcNow.AddDays(2),
                    "3 days" => DateTime.UtcNow.AddDays(3),
                    "7 days" => DateTime.UtcNow.AddDays(7),
                    "14 days" => DateTime.UtcNow.AddDays(14),
                    _ => DateTime.UtcNow.AddDays(30)
                };
            }

            string preset = await DisplayActionSheet("Select a Suspension Reason", "Cancel", null,
                "Repeated violations of our community guidelines.",
                "Sending spam, unsolicited messages, or inappropriate content.",
                "Harassment or threatening behavior toward another user.",
                "Sharing content that violates our terms of service.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Suspension Reason", "Edit if needed, or tap OK:", initialValue: preset);
            string reason = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Temporary Ban",
                $"Suspend {name} for {duration}?\n" +
                $"Ends: {expiresAt:MMM dd, yyyy 'at' h:mm tt} UTC\nReason: {reason}",
                "Ban", "Cancel");
            if (!confirm) return;

            bool banned = await UserService.BanUserAsync(phone, "temporary", reason, expiresAt);
            if (banned)
            {
                await onSuccess(ReportStatus.ActionTaken,
                    $"Temp ban for {duration} until {expiresAt:MMM dd, yyyy HH:mm} UTC — Reason: {reason}");
                await DisplayAlert("Ban Applied", $"{name} suspended for {duration}.", "OK");
            }
            else
                await DisplayAlert("Error", $"Could not ban user. Phone [{phone}] not found.", "OK");
        }

        private async Task ApplyPermanentBanAsync(
            string phone, string name,
            Func<ReportStatus, string, Task> onSuccess)
        {
            string preset = await DisplayActionSheet("Select a Ban Reason", "Cancel", null,
                "Severe and repeated violations of our community guidelines.",
                "Distribution of illegal, explicit, or harmful content.",
                "Predatory, abusive, or threatening behavior toward other users.",
                "Creating a fake identity or impersonating another person.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Ban Reason", "Edit if needed, or tap OK:", initialValue: preset);
            string reason = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Permanent Ban",
                $"PERMANENTLY ban {name}?\n" +
                $"Phone: {phone}\nReason: {reason}\n\nThis CANNOT be undone.",
                "Permanently Ban", "Cancel");
            if (!confirm) return;

            bool banned = await UserService.BanUserAsync(phone, "permanent", reason, null);
            if (banned)
            {
                await onSuccess(ReportStatus.ActionTaken, $"Permanent ban — Reason: {reason}");
                await DisplayAlert("Ban Applied", $"{name} has been permanently banned.", "OK");
            }
            else
                await DisplayAlert("Error", $"Could not ban user. Phone [{phone}] not found.", "OK");
        }

        // ??????????????????????????????????????????????????????????????
        // UPDATE STATUS
        // ??????????????????????????????????????????????????????????????

        /// <summary>Called by FullReportDetailsPage after an action is taken.</summary>
        public async Task UpdateStatusAsync(Report report, ReportStatus newStatus, string notes)
        {
            try
            {
                await ReportService.UpdateReportStatusAsync(report.Id, newStatus, notes);
                await LoadReportsAsync();
                await DisplayAlert("Updated", $"Report status updated to: {newStatus}", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateStatusAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to update status: {ex.Message}", "OK");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ??????????????????????????????????????????????????????????????
    // REPORT VIEW MODEL
    // ??????????????????????????????????????????????????????????????

    public class ReportViewModel : INotifyPropertyChanged
    {
        public Report Report { get; }

        public ReportViewModel(Report report)
        {
            Report = report;
            LoadAvatarsAsync();
        }

        public string ReportedUserName => Report.ReportedUserName ?? "Unknown";
        public string ReportedUserPhone => Report.ReportedUserPhone ?? "";
        public string ReporterName => Report.ReporterName ?? "Anonymous";
        public string ReporterPhone => Report.ReporterPhone ?? "";
        public string Category => Report.Category ?? "Uncategorized";
        public string Description => Report.Description ?? "";
        public string AdminNotes => Report.AdminNotes ?? "";
        public string ReportedMessageContent => Report.ReportedMessageContent ?? "";
        public DateTime ReportedAt => Report.ReportedAt;
        public ReportStatus Status => Report.Status;
        public List<ReportImage> Images => Report.Images ?? new();

        public bool HasImages => Images.Count > 0;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public bool HasAdminNotes => !string.IsNullOrWhiteSpace(AdminNotes);
        public bool HasReportedMessage => !string.IsNullOrWhiteSpace(ReportedMessageContent);

        public bool IsClosed =>
            Report.Status == ReportStatus.Resolved ||
            Report.Status == ReportStatus.Dismissed ||
            Report.Status == ReportStatus.ActionTaken;

        public string ImagesCountLabel => $"EVIDENCE  ({Images.Count} image{(Images.Count == 1 ? "" : "s")})";
        public string ImagesCountBadge => $"[{Images.Count}]";

        public string StatusLabel => Report.Status switch
        {
            ReportStatus.Pending => "PENDING",
            ReportStatus.UnderReview => "REVIEWING",
            ReportStatus.Resolved => "RESOLVED",
            ReportStatus.Dismissed => "DISMISSED",
            ReportStatus.ActionTaken => "ACTION TAKEN",
            _ => "PENDING"
        };

        public string StatusColor => Report.Status switch
        {
            ReportStatus.Pending => "#FF9800",
            ReportStatus.UnderReview => "#2196F3",
            ReportStatus.Resolved => "#4CAF50",
            ReportStatus.Dismissed => "#9E9E9E",
            ReportStatus.ActionTaken => "#FF6B6B",
            _ => "#FF9800"
        };

        public double RowOpacity => IsClosed ? 0.55 : 1.0;

        private string _reportedUserAvatar = "default_avatar.png";
        private string _reporterAvatar = "default_avatar.png";

        public string ReportedUserAvatar
        {
            get => _reportedUserAvatar;
            private set { _reportedUserAvatar = value; OnPropertyChanged(); }
        }

        public string ReporterAvatar
        {
            get => _reporterAvatar;
            private set { _reporterAvatar = value; OnPropertyChanged(); }
        }

        private async void LoadAvatarsAsync()
        {
            try
            {
                // Replace this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();

                if (!string.IsNullOrEmpty(Report.ReportedUserPhone))
                {
                    // Replace: var reportedUser = await db.Table<Lock.Models.User>()
                    //     .Where(u => u.PhoneNumber == Report.ReportedUserPhone)
                    //     .FirstOrDefaultAsync();

                    var reportedUsers = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(Report.ReportedUserPhone)}&limit=1");
                    var reportedUser = reportedUsers.FirstOrDefault();

                    ReportedUserAvatar = reportedUser != null
                        && !string.IsNullOrEmpty(reportedUser.ProfileImagePath)
                        && File.Exists(reportedUser.ProfileImagePath)
                            ? reportedUser.ProfileImagePath
                            : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(Report.ReportedUserName ?? "U")}&background=FF3B6F&color=FFFFFF&size=128";
                }

                if (!string.IsNullOrEmpty(Report.ReporterPhone))
                {
                    // Replace: var reporter = await db.Table<Lock.Models.User>()
                    //     .Where(u => u.PhoneNumber == Report.ReporterPhone)
                    //     .FirstOrDefaultAsync();

                    var reporters = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(Report.ReporterPhone)}&limit=1");
                    var reporter = reporters.FirstOrDefault();

                    ReporterAvatar = reporter != null
                        && !string.IsNullOrEmpty(reporter.ProfileImagePath)
                        && File.Exists(reporter.ProfileImagePath)
                            ? reporter.ProfileImagePath
                            : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(Report.ReporterName ?? "U")}&background=2A2A38&color=AAAAAA&size=128";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAvatarsAsync error: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ??????????????????????????????????????????????????????????????
    // APPEAL VIEW MODEL
    // ??????????????????????????????????????????????????????????????

    public class AppealViewModel : INotifyPropertyChanged
    {
        public Lock.Models.User User { get; }

        public AppealViewModel(Lock.Models.User user) { User = user; }

        public string ReportedUserName => User.Name ?? "Unknown";
        public string ReportedUserPhone => User.PhoneNumber ?? "";
        public string ReporterName => "Self";
        public string ReporterPhone => User.PhoneNumber ?? "";
        public string Category => User.BanType == "permanent" ? "Permanent Ban Appeal" : "Suspension Appeal";
        public string Description => User.AppealText ?? "";
        public string AdminNotes => User.AppealAdminResponse ?? "";
        public DateTime ReportedAt => User.AppealSubmittedAt ?? DateTime.UtcNow;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public bool HasAdminNotes => !string.IsNullOrWhiteSpace(AdminNotes);
        public bool HasReportedMessage => false;
        public bool HasImages => false;
        public string ReportedMessageContent => string.Empty;
        public string ImagesCountLabel => string.Empty;
        public string ImagesCountBadge => string.Empty;
        public List<ReportImage> Images => new();

        public bool IsClosed => User.AppealStatus == "approved" || User.AppealStatus == "rejected";
        public double RowOpacity => IsClosed ? 0.55 : 1.0;

        public string ReportedUserAvatar
        {
            get
            {
                if (!string.IsNullOrEmpty(User.ProfileImagePath) && File.Exists(User.ProfileImagePath))
                    return User.ProfileImagePath;
                return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(User.Name ?? "U")}&background=FF9800&color=FFFFFF&size=128";
            }
        }

        public string ReporterAvatar => ReportedUserAvatar;

        public string StatusLabel => User.AppealStatus switch
        {
            "pending" => "PENDING",
            "approved" => "APPROVED",
            "rejected" => "REJECTED",
            _ => "PENDING"
        };

        public string StatusColor => User.AppealStatus switch
        {
            "pending" => "#FF9800",
            "approved" => "#4CAF50",
            "rejected" => "#FF3B6F",
            _ => "#FF9800"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ??????????????????????????????????????????????????????????????
    // USER LOCATION VIEW MODEL
    // ??????????????????????????????????????????????????????????????

    public class UserLocationViewModel : INotifyPropertyChanged
    {
        public Lock.Models.User User { get; }

        public UserLocationViewModel(Lock.Models.User user) { User = user; }

        public string Name => User.Name ?? "Unknown";
        public string Phone => User.PhoneNumber ?? "";
        public string Country => string.IsNullOrWhiteSpace(User.Country) ? "—" : User.Country;
        public string State => string.IsNullOrWhiteSpace(User.State) ? "—" : User.State;
        public string Gender => User.Gender ?? "—";
        public int Age => User.GetAge();
        public string AgeLabel => $"{User.GetAge()}";
        public string LastActive => User.LastActive.ToString("MMM dd, yyyy");
        public bool IsBanned => User.IsBanned;
        public bool IsWarned => User.HasWarning && !User.WarningAcknowledged;

        private bool _isBulkMode;
        private bool _isSelected;

        public bool IsBulkMode
        {
            get => _isBulkMode;
            set
            {
                if (_isBulkMode == value) return;
                _isBulkMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RowBackground));
                OnPropertyChanged(nameof(RowStroke));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CheckboxBackground));
                OnPropertyChanged(nameof(CheckboxStroke));
                OnPropertyChanged(nameof(RowBackground));
                OnPropertyChanged(nameof(RowStroke));
            }
        }


        /// <summary>Venus/Mars symbol coloured by gender.</summary>
        public string GenderIcon => (User.Gender ?? "").ToLowerInvariant() switch
        {
            "female" => "?",
            "male" => "?",
            _ => "?"
        };

        public string Avatar
        {
            get
            {
                if (!string.IsNullOrEmpty(User.ProfileImagePath) && File.Exists(User.ProfileImagePath))
                    return User.ProfileImagePath;
                string bg = User.IsBanned ? "FF3B6F" : "00C9C9";
                return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(User.Name ?? "U")}&background={bg}&color=FFFFFF&size=64";
            }
        }

        public string StatusLabel => User.IsBanned
            ? $"BANNED ({User.BanType?.ToUpper()})"
            : User.HasWarning && !User.WarningAcknowledged
                ? "WARNED"
                : "Active";

        public string StatusColor => User.IsBanned
            ? "#FF3B6F"
            : User.HasWarning && !User.WarningAcknowledged
                ? "#FF9800"
                : "#4CAF50";

        public string LocationDisplay =>
            (string.IsNullOrWhiteSpace(User.State) ? "" : $"{User.State}, ") +
            (string.IsNullOrWhiteSpace(User.Country) ? "Unknown" : User.Country);

        public event PropertyChangedEventHandler? PropertyChanged;

        // Checkbox visuals
        public string CheckboxBackground => IsSelected ? "#00C9C9" : "Transparent";
        public string CheckboxStroke => IsSelected ? "#00C9C9" : "#555566";

        // Row highlight when selected
        public string RowBackground => IsSelected ? "#0E2A2A" : "#12122A";
        public string RowStroke => IsSelected ? "#00C9C9" : StatusColor;

        // Wire up INotifyPropertyChanged (already there; ensure OnPropertyChanged is implemented)
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}