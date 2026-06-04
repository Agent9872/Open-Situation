using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Microsoft.Maui.Controls.Shapes;
using Lock.Chat.Services;
using Path = System.IO.Path;

namespace Lock.Pages.Admin
{
    public partial class UsersListPage : ContentPage
    {
        private List<User> _allUsers;
        private List<User> _filteredUsers;
        private bool _isFilterVisible = false;
        private string _currentTab = "Users";

        // Activity tracking
        private List<UserActivity> _userActivities;
        private List<ProfileChange> _profileChanges;

        public UsersListPage()
        {
            InitializeComponent();

            // Hide BOTH Shell and NavigationPage bars
            Shell.SetNavBarIsVisible(this, false);
            NavigationPage.SetHasNavigationBar(this, false);

            _allUsers = new List<User>();
            _filteredUsers = new List<User>();
            _userActivities = new List<UserActivity>();
            _profileChanges = new List<ProfileChange>();

            InitializePickers();
            LoadUsers();
            LoadActivities();
            LoadProfileChanges();

            // HIDE SHELL NAVIGATION BAR - Just this one line
            Shell.SetNavBarIsVisible(this, false);

            // Subscribe to profile update messages to track changes
            MessagingCenter.Subscribe<object, ProfileUpdatedMessage>(this, "ProfileUpdated", OnProfileUpdated);
            MessagingCenter.Subscribe<object, PostCreatedMessage>(this, "PostCreated", OnPostCreated);
            MessagingCenter.Subscribe<object, LoveToggledMessage>(this, "LoveToggled", OnLoveToggled);
            MessagingCenter.Subscribe<object, SparkToggledMessage>(this, "SparkToggled", OnSparkToggled);
        }

        private void InitializePickers()
        {
            if (GenderFilterPicker != null)
            {
                GenderFilterPicker.Items.Add("All Genders");
                GenderFilterPicker.Items.Add("Male");
                GenderFilterPicker.Items.Add("Female");
                GenderFilterPicker.Items.Add("Other");
                GenderFilterPicker.SelectedIndex = 0;
            }

            if (InterestFilterPicker != null)
            {
                InterestFilterPicker.Items.Add("All Interests");
                InterestFilterPicker.Items.Add("Friendship");
                InterestFilterPicker.Items.Add("Dating");
                InterestFilterPicker.Items.Add("Networking");
                InterestFilterPicker.Items.Add("Casual");
                InterestFilterPicker.Items.Add("Serious Relationship");
                InterestFilterPicker.SelectedIndex = 0;
            }

            // Location picker is populated dynamically after users load
            if (LocationFilterPicker != null)
            {
                LocationFilterPicker.Items.Add("All Locations");
                LocationFilterPicker.SelectedIndex = 0;
            }

            if (ReportTypePicker != null)
            {
                ReportTypePicker.Items.Add("User Summary");
                ReportTypePicker.Items.Add("Activity Report");
                ReportTypePicker.Items.Add("Profile Changes Report");
                ReportTypePicker.Items.Add("Verification Status");
                ReportTypePicker.SelectedIndex = 0;
            }
        }


        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Ensure nav bar stays hidden every time the page appears
            Shell.SetNavBarIsVisible(this, false);

            LoadUsers();
            LoadActivities();
            LoadProfileChanges();
        }

        // Add this helper method to your class
        private string GetValidProfileImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            // Try the path as-is first
            if (File.Exists(path))
                return path;

            // Try just the filename in app's local data directory
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
            {
                var localPath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                if (File.Exists(localPath))
                    return localPath;

                var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                if (File.Exists(cachePath))
                    return cachePath;
            }

            return string.Empty;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<object, ProfileUpdatedMessage>(this, "ProfileUpdated");
            MessagingCenter.Unsubscribe<object, PostCreatedMessage>(this, "PostCreated");
            MessagingCenter.Unsubscribe<object, LoveToggledMessage>(this, "LoveToggled");
            MessagingCenter.Unsubscribe<object, SparkToggledMessage>(this, "SparkToggled");
        }

        #region Tab Navigation
        private void OnUsersTabTapped(object sender, EventArgs e)
        {
            SwitchTab("Users");
        }

        private void OnActivitiesTabTapped(object sender, EventArgs e)
        {
            SwitchTab("Activities");
            LoadActivities();
        }

        private void OnChangesTabTapped(object sender, EventArgs e)
        {
            SwitchTab("Changes");
            LoadProfileChanges();
        }

        private void OnReportsTabTapped(object sender, EventArgs e)
        {
            SwitchTab("Reports");
            GenerateReport();
        }

        private void SwitchTab(string tabName)
        {
            _currentTab = tabName;

            // Reset all indicators
            UsersTabIndicator.WidthRequest = 0;
            ActivitiesTabIndicator.WidthRequest = 0;
            ChangesTabIndicator.WidthRequest = 0;
            ReportsTabIndicator.WidthRequest = 0;

            // Reset tab colors
            SetTabLabelColor(UsersTab, "#666666");
            SetTabLabelColor(ActivitiesTab, "#666666");
            SetTabLabelColor(ChangesTab, "#666666");
            SetTabLabelColor(ReportsTab, "#666666");

            // Hide all content
            UsersContent.IsVisible = false;
            ActivitiesContent.IsVisible = false;
            ChangesContent.IsVisible = false;
            ReportsContent.IsVisible = false;

            switch (tabName)
            {
                case "Users":
                    UsersContent.IsVisible = true;
                    UsersTabIndicator.WidthRequest = 40;
                    SetTabLabelColor(UsersTab, "#2196F3");
                    break;
                case "Activities":
                    ActivitiesContent.IsVisible = true;
                    ActivitiesTabIndicator.WidthRequest = 40;
                    SetTabLabelColor(ActivitiesTab, "#2196F3");
                    break;
                case "Changes":
                    ChangesContent.IsVisible = true;
                    ChangesTabIndicator.WidthRequest = 40;
                    SetTabLabelColor(ChangesTab, "#2196F3");
                    break;
                case "Reports":
                    ReportsContent.IsVisible = true;
                    ReportsTabIndicator.WidthRequest = 40;
                    SetTabLabelColor(ReportsTab, "#2196F3");
                    break;
            }
        }

        private void SetTabLabelColor(Grid tab, string colorHex)
        {
            var stack = tab.Children.OfType<VerticalStackLayout>().FirstOrDefault();
            if (stack != null)
            {
                var label = stack.Children.OfType<Label>().Skip(1).FirstOrDefault();
                if (label != null)
                {
                    label.TextColor = Color.FromArgb(colorHex);
                }
            }
        }
        #endregion

        #region Users Tab Methods
        private async void LoadUsers()
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                EmptyStateGrid.IsVisible = false;

                _allUsers = await AuthService.GetAllUsersAsync();

                // Populate location picker dynamically from actual user data
                PopulateLocationPicker();

                ApplyFilters();
                UpdateStats();

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                await DisplayAlert("Error", $"Failed to load users: {ex.Message}", "OK");
            }
        }

        private void PopulateLocationPicker()
        {
            if (LocationFilterPicker == null) return;

            var currentIndex = LocationFilterPicker.SelectedIndex;
            var currentLocation = currentIndex > 0
                ? LocationFilterPicker.Items[currentIndex]
                : null;

            LocationFilterPicker.Items.Clear();
            LocationFilterPicker.Items.Add("All Locations");

            var locations = _allUsers
                .Select(u => GetUserLocation(u))
                .Where(loc => !string.IsNullOrWhiteSpace(loc))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l)
                .ToList();

            foreach (var loc in locations)
                LocationFilterPicker.Items.Add(loc);

            if (currentLocation != null)
            {
                var idx = LocationFilterPicker.Items.IndexOf(currentLocation);
                LocationFilterPicker.SelectedIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                LocationFilterPicker.SelectedIndex = 0;
            }
        }

        private void ApplyFilters()
        {
            var filtered = _allUsers.AsEnumerable();

            // Search
            if (!string.IsNullOrWhiteSpace(SearchBarControl?.Text))
            {
                var searchLower = SearchBarControl.Text.ToLowerInvariant();
                filtered = filtered.Where(u =>
                    u.Name.ToLowerInvariant().Contains(searchLower) ||
                    u.PhoneNumber.Contains(searchLower));
            }

            // Gender
            if (GenderFilterPicker?.SelectedIndex > 0)
            {
                var selected = GenderFilterPicker.SelectedItem as string;
                if (!string.IsNullOrEmpty(selected) && selected != "All Genders")
                    filtered = filtered.Where(u => u.Gender == selected);
            }

            // Interest
            if (InterestFilterPicker?.SelectedIndex > 0)
            {
                var selected = InterestFilterPicker.SelectedItem as string;
                if (!string.IsNullOrEmpty(selected) && selected != "All Interests")
                    filtered = filtered.Where(u =>
                        !string.IsNullOrEmpty(u.Interest) &&
                        u.Interest.Equals(selected, StringComparison.OrdinalIgnoreCase));
            }

            // Location
            if (LocationFilterPicker?.SelectedIndex > 0)
            {
                var selected = LocationFilterPicker.SelectedItem as string;
                if (!string.IsNullOrEmpty(selected) && selected != "All Locations")
                    filtered = filtered.Where(u =>
                        GetUserLocation(u).Equals(selected, StringComparison.OrdinalIgnoreCase));
            }

            _filteredUsers = filtered.OrderByDescending(u => u.JoinDate).ToList();
            RenderUsersTable();
            UpdateStats();
            UpdateActiveFiltersLabel();

            EmptyStateGrid.IsVisible = !_filteredUsers.Any();
        }

        private string GetUserLocation(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.Country) && !string.IsNullOrWhiteSpace(user.State))
                return $"{user.State}, {user.Country}";
            if (!string.IsNullOrWhiteSpace(user.State))
                return user.State;
            if (!string.IsNullOrWhiteSpace(user.Country))
                return user.Country;
            return string.Empty;
        }

        private void UpdateActiveFiltersLabel()
        {
            var activeFiltersLabel = this.FindByName<Label>("ActiveFiltersLabel");
            if (activeFiltersLabel == null) return;

            var parts = new List<string>();

            if (GenderFilterPicker?.SelectedIndex > 0)
                parts.Add(GenderFilterPicker.SelectedItem as string ?? "");
            if (InterestFilterPicker?.SelectedIndex > 0)
                parts.Add(InterestFilterPicker.SelectedItem as string ?? "");
            if (LocationFilterPicker?.SelectedIndex > 0)
                parts.Add(LocationFilterPicker.SelectedItem as string ?? "");

            activeFiltersLabel.Text = parts.Any()
                ? $"? {string.Join("  ·  ", parts)}"
                : "";
        }


        private void RenderUsersTable()
        {
            UsersContainer.Children.Clear();
            if (!_filteredUsers.Any()) return;

            for (int i = 0; i < _filteredUsers.Count; i++)
            {
                var user = _filteredUsers[i];
                var rowBg = i % 2 == 0
                    ? Color.FromArgb("#0A0A0F")
                    : Color.FromArgb("#0D0D14");

                var row = new Grid
                {
                    Padding = new Thickness(16, 11),
                    BackgroundColor = rowBg,
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(0.18, GridUnitType.Star) }, // Name
                new ColumnDefinition { Width = new GridLength(0.15, GridUnitType.Star) }, // Phone
                new ColumnDefinition { Width = new GridLength(0.06, GridUnitType.Star) }, // Age
                new ColumnDefinition { Width = new GridLength(0.09, GridUnitType.Star) }, // Gender
                new ColumnDefinition { Width = new GridLength(0.12, GridUnitType.Star) }, // Interest
                new ColumnDefinition { Width = new GridLength(0.14, GridUnitType.Star) }, // Location
                new ColumnDefinition { Width = new GridLength(0.16, GridUnitType.Star) }, // IP Address
                new ColumnDefinition { Width = new GridLength(0.10, GridUnitType.Star) }, // Joined
            }
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) => await ShowUserDetails(user);
                row.GestureRecognizers.Add(tap);

                // Col 0: Avatar + Name
                var nameStack = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
                var avatarFrame = new Frame
                {
                    WidthRequest = 28,
                    HeightRequest = 28,
                    CornerRadius = 14,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#16161C"),
                    BorderColor = Color.FromArgb("#2A2A38")
                };

                if (!string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                {
                    avatarFrame.Content = new Image
                    {
                        Source = ImageSource.FromFile(user.ProfileImagePath),
                        Aspect = Aspect.AspectFill,
                        WidthRequest = 28,
                        HeightRequest = 28
                    };
                }
                else
                {
                    avatarFrame.Content = new Label
                    {
                        Text = user.Name?.Length > 0 ? user.Name[0].ToString().ToUpper() : "?",
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    };
                }

                nameStack.Children.Add(avatarFrame);
                nameStack.Children.Add(new Label
                {
                    Text = user.Name,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    VerticalOptions = LayoutOptions.Center
                });
                row.Add(nameStack, 0, 0);

                // Col 1: Phone
                row.Add(new Label
                {
                    Text = user.PhoneNumber,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#7A7A8C"),
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);

                // Col 2: Age
                row.Add(new Label
                {
                    Text = user.GetAge().ToString(),
                    FontSize = 11,
                    TextColor = Color.FromArgb("#7A7A8C"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }, 2, 0);

                // Col 3: Gender
                row.Add(new Label
                {
                    Text = user.Gender ?? "—",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#7A7A8C"),
                    VerticalOptions = LayoutOptions.Center
                }, 3, 0);

                // Col 4: Interest
                row.Add(new Label
                {
                    Text = !string.IsNullOrEmpty(user.Interest) ? user.Interest : "—",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    VerticalOptions = LayoutOptions.Center
                }, 4, 0);

                // Col 5: Location (always shown from user profile data)
                var locationText = GetUserLocation(user);
                row.Add(new Label
                {
                    Text = !string.IsNullOrEmpty(locationText) ? locationText : "—",
                    FontSize = 10,
                    TextColor = !string.IsNullOrEmpty(locationText)
                        ? Color.FromArgb("#A0A0B0")
                        : Color.FromArgb("#3A3A4A"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    VerticalOptions = LayoutOptions.Center
                }, 5, 0);

                // Col 6: IP Address (copyable, teal if present)
                var ipText = !string.IsNullOrEmpty(user.IpAddress) ? user.IpAddress : "—";
                var ipLabel = new Label
                {
                    Text = ipText,
                    FontSize = 10,
                    TextColor = !string.IsNullOrEmpty(user.IpAddress)
                        ? Color.FromArgb("#00B5B5")
                        : Color.FromArgb("#3A3A4A"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    VerticalOptions = LayoutOptions.Center,
                    TextDecorations = !string.IsNullOrEmpty(user.IpAddress)
                        ? TextDecorations.Underline
                        : TextDecorations.None
                };

                if (!string.IsNullOrEmpty(user.IpAddress))
                {
                    var ipTap = new TapGestureRecognizer();
                    var capturedIp = user.IpAddress;
                    ipTap.Tapped += async (s, e) =>
                    {
                        await Clipboard.Default.SetTextAsync(capturedIp);
                        var lbl = s as Label;
                        if (lbl != null)
                        {
                            var originalColor = lbl.TextColor;
                            var originalText = lbl.Text;
                            lbl.TextColor = Color.FromArgb("#4CAF50");
                            lbl.Text = "Copied!";
                            await Task.Delay(1200);
                            lbl.Text = originalText;
                            lbl.TextColor = originalColor;
                        }
                    };
                    ipLabel.GestureRecognizers.Add(ipTap);
                }
                row.Add(ipLabel, 6, 0);

                // Col 7: Joined
                row.Add(new Label
                {
                    Text = user.JoinDate.ToString("MMM dd"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A"),
                    VerticalOptions = LayoutOptions.Center
                }, 7, 0);

                UsersContainer.Children.Add(row);
                UsersContainer.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#1C1C25")
                });
            }
        }
        private async Task ShowUserDetails(User user)
        {
            try
            {
                // Create the detail page
                var detailPage = new UserDetailPage();

                // Set the user phone property directly
                detailPage.UserPhone = user.PhoneNumber;

                // Use PushAsync for normal page navigation (not modal)
                await Navigation.PushAsync(detailPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex}");
                await DisplayAlert("Error", "Could not open user details", "OK");
            }
        }

        private void UpdateStats()
        {
            var totalUsers = _allUsers.Count;
            var showingUsers = _filteredUsers.Count;
            StatsLabel.Text = $"?? Total: {totalUsers} | Showing: {showingUsers}";
        }

        private void RefreshButton_Clicked(object sender, EventArgs e) => LoadUsers();
        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void GenderFilter_SelectedIndexChanged(object sender, EventArgs e) => ApplyFilters();

        private void ClearFiltersButton_Clicked(object sender, EventArgs e)
        {
            if (SearchBarControl != null) SearchBarControl.Text = string.Empty;
            if (GenderFilterPicker != null) GenderFilterPicker.SelectedIndex = 0;
            if (InterestFilterPicker != null) InterestFilterPicker.SelectedIndex = 0;
            if (LocationFilterPicker != null) LocationFilterPicker.SelectedIndex = 0;
            ApplyFilters();
        }
        #endregion

        #region Activities Tab Methods
        private async void LoadActivities()
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                _userActivities = await LoadUserActivitiesFromDatabase();

                ActivitiesStatsLabel.Text = $"?? Recent Activities ({_userActivities.Count})";
                RenderActivitiesTable();

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                EmptyStateGrid.IsVisible = !_userActivities.Any();
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                Debug.WriteLine($"LoadActivities error: {ex}");
                await DisplayAlert("Error", $"Failed to load activities: {ex.Message}", "OK");
            }
        }

        private async Task<List<UserActivity>> LoadUserActivitiesFromDatabase()
        {
            var activities = new List<UserActivity>();
            try
            {
                // Get recent posts from Supabase
                var posts = await SupabaseService.GetAsync<Lock.Models.Post>("Posts",
                    "order=CreatedAt.desc&limit=50");

                foreach (var post in posts)
                {
                    // Get the author's user info - changed variable name to 'authorUsers'
                    var authorUsers = await SupabaseService.GetAsync<User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(post.AuthorPhone)}&limit=1");
                    var author = authorUsers.FirstOrDefault();

                    activities.Add(new UserActivity
                    {
                        Id = $"post_{post.Id}",
                        UserId = post.AuthorPhone,
                        UserName = author?.Name ?? post.AuthorPhone,
                        ProfileImage = GetValidProfileImagePath(author?.ProfileImagePath),
                        ActivityType = "Post Created",
                        Title = "New Post",
                        Description = post.Content?.Length > 100
                            ? post.Content.Substring(0, 100) + "..."
                            : post.Content ?? "Shared a post",
                        Icon = "M160-200v-80h80v-560h400l120 120v440h80v80H160Zm300-160q17 0 28.5-11.5T500-400q0-17-11.5-28.5T460-440q-17 0-28.5 11.5T420-400q0 17 11.5 28.5T460-360Zm0-160q17 0 28.5-11.5T500-560v-160q0-17-11.5-28.5T460-760q-17 0-28.5 11.5T420-720v160q0 17 11.5 28.5T460-520Z",
                        Timestamp = post.CreatedAt
                    });
                }

                // Get recent user joins from Supabase - changed variable name to 'recentUsers'
                var recentUsers = await SupabaseService.GetAsync<User>("Users",
                    "order=JoinDate.desc&limit=50");

                foreach (var user in recentUsers)
                {
                    activities.Add(new UserActivity
                    {
                        Id = $"user_{user.Id}_{user.JoinDate.Ticks}",
                        UserId = user.PhoneNumber,
                        UserName = user.Name,
                        ProfileImage = GetValidProfileImagePath(user.ProfileImagePath),
                        ActivityType = "User Joined",
                        Title = "New User Registered",
                        Description = $"{user.Name} joined Lock",
                        Icon = "M480-480q-66 0-113-47t-47-113q0-66 47-113t113-47q66 0 113 47t47 113q0 66-47 113t-113 47ZM160-160v-112q0-34 17.5-62.5T224-378q62-31 126-46.5T480-440q66 0 130 15.5T736-378q29 15 46.5 43.5T800-272v112H160Z",
                        Timestamp = user.JoinDate
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadUserActivitiesFromDatabase error: {ex}");
            }
            return activities;
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            Shell.SetNavBarIsVisible(this, false);
        }
        private void RenderActivitiesTable()
        {
            ActivitiesContainer.Children.Clear();
            if (!_userActivities.Any()) return;

            var sorted = _userActivities.OrderByDescending(a => a.Timestamp).ToList();

            foreach (var activity in sorted)
            {
                var row = new Grid
                {
                    Padding = new Thickness(16, 12),
                    ColumnSpacing = 14,
                    BackgroundColor = Color.FromArgb("#0A0A0F"),
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(44) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
                };

                // ?? AVATAR ??
                var avatarFrame = new Frame
                {
                    WidthRequest = 40,
                    HeightRequest = 40,
                    CornerRadius = 20,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#16161C"),
                    BorderColor = Color.FromArgb("#2A2A38"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start
                };

                var validPath = GetValidProfileImagePath(activity.ProfileImage);
                if (!string.IsNullOrEmpty(validPath))
                {
                    avatarFrame.Content = new Image
                    {
                        Source = ImageSource.FromFile(validPath),
                        Aspect = Aspect.AspectFill,
                        WidthRequest = 40,
                        HeightRequest = 40
                    };
                }
                else
                {
                    avatarFrame.Content = new Label
                    {
                        Text = activity.UserName?.Length > 0 ? activity.UserName[0].ToString().ToUpper() : "?",
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    };
                }

                // ?? STATUS DOT overlay on avatar ??
                var avatarGrid = new Grid
                {
                    WidthRequest = 44,
                    HeightRequest = 44,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start
                };
                avatarGrid.Children.Add(avatarFrame);

                var statusDot = new Ellipse
                {
                    WidthRequest = 12,
                    HeightRequest = 12,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        activity.Timestamp > DateTime.UtcNow.AddHours(-24) ? "#4CAF50" : "#555555")),
                    Stroke = new SolidColorBrush(Color.FromArgb("#0A0A0F")),
                    StrokeThickness = 2,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.End
                };
                avatarGrid.Children.Add(statusDot);

                row.Add(avatarGrid, 0, 0);

                // ?? TEXT ??
                var activityTypeBadge = new Border
                {
                    BackgroundColor = Color.FromArgb("#1A2A1A"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 5 },
                    Padding = new Thickness(5, 2),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = activity.ActivityType,
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#22C55E")
                    }
                };

                var nameRow = new HorizontalStackLayout { Spacing = 6 };
                nameRow.Children.Add(new Label
                {
                    Text = activity.UserName,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    VerticalOptions = LayoutOptions.Center
                });
                nameRow.Children.Add(activityTypeBadge);

                var textStack = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
                textStack.Children.Add(nameRow);

                if (!string.IsNullOrEmpty(activity.Description))
                {
                    textStack.Children.Add(new Label
                    {
                        Text = activity.Description,
                        FontSize = 11,
                        TextColor = Color.FromArgb("#7A7A8C"),
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 2
                    });
                }

                textStack.Children.Add(new Label
                {
                    Text = activity.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A")
                });

                row.Add(textStack, 1, 0);

                // ?? RIGHT DOT ??
                row.Add(new Ellipse
                {
                    WidthRequest = 8,
                    HeightRequest = 8,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        activity.Timestamp > DateTime.UtcNow.AddHours(-24) ? "#4CAF50" : "#555555")),
                    VerticalOptions = LayoutOptions.Center
                }, 2, 0);

                ActivitiesContainer.Children.Add(row);
                ActivitiesContainer.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#1C1C25"),
                    Margin = new Thickness(16, 0)
                });
            }
        }

        private async void LoadProfileChanges()
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                _profileChanges = await LoadProfileChangesFromDatabase();

                ChangesStatsLabel.Text = $"?? Recent Profile Changes ({_profileChanges.Count})";
                RenderChangesTable();

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                EmptyStateGrid.IsVisible = !_profileChanges.Any();
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                Debug.WriteLine($"LoadProfileChanges error: {ex}");
            }
        }

        private async void OnRoleManagerClicked(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Role Manager button clicked");

                // TODO: Replace with your actual Role Manager page
                await Navigation.PushAsync(new AdminRolePage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to Role Manager: {ex}");
                await DisplayAlert("Error", "Could not open Role Manager", "OK");
            }
        }
        private void RenderChangesTable()
        {
            ChangesContainer.Children.Clear();
            if (!_profileChanges.Any()) return;

            var sorted = _profileChanges.OrderByDescending(c => c.Timestamp).ToList();

            foreach (var change in sorted)
            {
                var row = new Grid
                {
                    Padding = new Thickness(16, 12),
                    ColumnSpacing = 14,
                    BackgroundColor = Color.FromArgb("#0A0A0F"),
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(44) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
                };

                // ?? AVATAR ??
                var avatarFrame = new Frame
                {
                    WidthRequest = 40,
                    HeightRequest = 40,
                    CornerRadius = 20,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#16161C"),
                    BorderColor = Color.FromArgb("#2A2A38"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start
                };

                var validPath = GetValidProfileImagePath(change.ProfileImage);
                if (!string.IsNullOrEmpty(validPath))
                {
                    avatarFrame.Content = new Image
                    {
                        Source = ImageSource.FromFile(validPath),
                        Aspect = Aspect.AspectFill,
                        WidthRequest = 40,
                        HeightRequest = 40
                    };
                }
                else
                {
                    avatarFrame.Content = new Label
                    {
                        Text = change.UserName?.Length > 0 ? change.UserName[0].ToString().ToUpper() : "?",
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    };
                }

                // ?? ICON BADGE on avatar ??
                var avatarGrid = new Grid
                {
                    WidthRequest = 44,
                    HeightRequest = 44,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start
                };
                avatarGrid.Children.Add(avatarFrame);

                var iconBadge = new Frame
                {
                    WidthRequest = 18,
                    HeightRequest = 18,
                    CornerRadius = 9,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#0D1F1F"),
                    BorderColor = Color.FromArgb("#00B5B5"),
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.End,
                    Content = new Label
                    {
                        Text = change.Icon,
                        FontSize = 8,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                };
                avatarGrid.Children.Add(iconBadge);

                row.Add(avatarGrid, 0, 0);

                // ?? TEXT ??
                var fieldBadge = new Border
                {
                    BackgroundColor = Color.FromArgb("#0D1F1F"),
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5")),
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    Padding = new Thickness(6, 2),
                    Content = new Label
                    {
                        Text = change.FieldChanged,
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5")
                    }
                };

                var fieldRow = new HorizontalStackLayout { Spacing = 6 };
                fieldRow.Children.Add(fieldBadge);

                var textStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
                textStack.Children.Add(new Label
                {
                    Text = change.UserName,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8")
                });
                textStack.Children.Add(fieldRow);

                if (!string.IsNullOrEmpty(change.ChangeDetails))
                {
                    textStack.Children.Add(new Label
                    {
                        Text = change.ChangeDetails,
                        FontSize = 11,
                        TextColor = Color.FromArgb("#7A7A8C"),
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 1
                    });
                }

                textStack.Children.Add(new Label
                {
                    Text = change.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A")
                });

                row.Add(textStack, 1, 0);

                // ?? EDIT ICON ??
                row.Add(new Label
                {
                    Text = "?",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#3A3A4A"),
                    VerticalOptions = LayoutOptions.Center
                }, 2, 0);

                ChangesContainer.Children.Add(row);
                ChangesContainer.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#1C1C25"),
                    Margin = new Thickness(16, 0)
                });
            }
        }
        private async Task<List<ProfileChange>> LoadProfileChangesFromDatabase()
        {
            var changes = new List<ProfileChange>();
            try
            {
                // FIXED: Use Supabase instead of SQLite
                var users = await SupabaseService.GetAsync<User>("Users", "");

                foreach (var user in users)
                {
                    var resolvedImage = GetValidProfileImagePath(user.ProfileImagePath);

                    if (user.MoodLastUpdated > DateTime.UtcNow.AddDays(-7))
                    {
                        changes.Add(new ProfileChange
                        {
                            Id = $"mood_{user.Id}_{user.MoodLastUpdated.Ticks}",
                            UserId = user.PhoneNumber,
                            UserName = user.Name,
                            ProfileImage = resolvedImage,
                            FieldChanged = "Mood / Looking For",
                            OldValue = "",
                            NewValue = user.Mood,
                            ChangeDetails = $"Updated looking for: {user.Mood}",
                            Icon = "M480-80q-83 0-141.5-58.5T280-280q0-48 18-90.5T352-446l128-128 128 128q36 36 54 78.5t18 90.5q0 83-58.5 141.5T480-80Zm0-80q50 0 85-35t35-85q0-29-10.5-54T560-381l-80-80-80 80q-19 19-29.5 44T360-280q0 50 35 85t85 35Zm0-160ZM360-600l-84-84q-11-11-11-28t11-28l196-196q11-11 28-11t28 11l196 196q11 11 11 28t-11 28l-84 84-80-80-80 80-120-120 80-80-80 80 120 120Z",
                            Timestamp = user.MoodLastUpdated
                        });
                    }

                    if (user.JoinDate.AddDays(1) < user.LastActive && !string.IsNullOrEmpty(user.Bio))
                    {
                        changes.Add(new ProfileChange
                        {
                            Id = $"bio_{user.Id}_{user.LastActive.Ticks}",
                            UserId = user.PhoneNumber,
                            UserName = user.Name,
                            ProfileImage = resolvedImage,
                            FieldChanged = "Bio",
                            OldValue = "",
                            NewValue = user.Bio?.Length > 50 ? user.Bio.Substring(0, 50) + "..." : user.Bio,
                            ChangeDetails = "Updated profile bio",
                            Icon = "M200-200h57l391-391-57-57-391 391v57Zm-80 80v-170l528-527q12-11 26.5-17t30.5-6q16 0 31 6t26 18l55 56q12 11 17.5 26t5.5 30q0 16-5.5 30.5T817-647L290-120H120Zm640-584-56-56 56 56Zm-141 85-28-29 57 57-29-28Z",
                            Timestamp = user.LastActive
                        });
                    }

                    if (!string.IsNullOrEmpty(user.ProfileImagePath))
                    {
                        changes.Add(new ProfileChange
                        {
                            Id = $"photo_{user.Id}_{user.LastActive.Ticks}",
                            UserId = user.PhoneNumber,
                            UserName = user.Name,
                            ProfileImage = resolvedImage,
                            FieldChanged = "Profile Photo",
                            OldValue = "",
                            NewValue = user.ProfileImagePath,
                            ChangeDetails = "Updated profile photo",
                            Icon = "M480-260q75 0 127.5-52.5T660-440q0-75-52.5-127.5T480-620q-75 0-127.5 52.5T300-440q0 75 52.5 127.5T480-260Zm0-80q-42 0-71-29t-29-71q0-42 29-71t71-29q42 0 71 29t29 71q0 42-29 71t-71 29ZM160-120q-33 0-56.5-23.5T80-200v-480q0-33 23.5-56.5T160-760h126l74-80h240l74 80h126q33 0 56.5 23.5T880-680v480q0 33-23.5 56.5T800-120H160Z",
                            Timestamp = user.LastActive
                        });
                    }

                    if (!string.IsNullOrEmpty(user.Country) || !string.IsNullOrEmpty(user.State))
                    {
                        changes.Add(new ProfileChange
                        {
                            Id = $"location_{user.Id}_{user.LastActive.Ticks}",
                            UserId = user.PhoneNumber,
                            UserName = user.Name,
                            ProfileImage = resolvedImage,
                            FieldChanged = "Location",
                            OldValue = "",
                            NewValue = $"{user.State}, {user.Country}".Trim(',', ' '),
                            ChangeDetails = $"Location set to: {user.State}, {user.Country}".Trim(',', ' '),
                            Icon = "M480-480q33 0 56.5-23.5T560-560q0-33-23.5-56.5T480-640q-33 0-56.5 23.5T400-560q0 33 23.5 56.5T480-480Zm0 294q122-112 181-203.5T720-552q0-109-69.5-178.5T480-800q-101 0-170.5 69.5T240-552q0 71 59 162.5T480-186Z",
                            Timestamp = user.LastActive
                        });
                    }

                    if (!string.IsNullOrEmpty(user.Interest))
                    {
                        changes.Add(new ProfileChange
                        {
                            Id = $"interest_{user.Id}_{user.LastActive.Ticks}",
                            UserId = user.PhoneNumber,
                            UserName = user.Name,
                            ProfileImage = resolvedImage,
                            FieldChanged = "Interest",
                            OldValue = "",
                            NewValue = user.Interest,
                            ChangeDetails = $"Looking for: {user.Interest}",
                            Icon = "m480-120-58-52q-101-91-167-157T150-447q-79-95-104.5-167T20-756q0-130 91-217t229-87q75 0 141 34t99 96q33-62 99-96t141-34q138 0 229 87t91 217q0 73-25.5 145T835-447q-44 56-110 122.5T538-172l-58 52Z",
                            Timestamp = user.LastActive
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfileChangesFromDatabase error: {ex}");
            }
            return changes;
        }
        // ?? Activities search ??
        private void ActivitiesSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ActivitiesSearchClear.IsVisible = !string.IsNullOrEmpty(e.NewTextValue);
            RenderActivitiesFiltered(e.NewTextValue?.Trim() ?? "");
        }

        private void ActivitiesSearchClear_Tapped(object sender, EventArgs e)
        {
            ActivitiesSearchEntry.Text = "";
            ActivitiesSearchClear.IsVisible = false;
            RenderActivitiesFiltered("");
        }

        private void RenderActivitiesFiltered(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // reuse existing render — no filter
                RenderActivitiesTable();
                return;
            }

            var q = query.ToLowerInvariant();
            var backup = _userActivities;
            _userActivities = _userActivities
                .Where(a =>
                    (a.UserName ?? "").ToLowerInvariant().Contains(q) ||
                    (a.ActivityType ?? "").ToLowerInvariant().Contains(q) ||
                    (a.Description ?? "").ToLowerInvariant().Contains(q))
                .ToList();

            RenderActivitiesTable();
            _userActivities = backup;  // restore full list after render
        }

        // ?? Changes search ??
        private void ChangesSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ChangesSearchClear.IsVisible = !string.IsNullOrEmpty(e.NewTextValue);
            RenderChangesFiltered(e.NewTextValue?.Trim() ?? "");
        }

        private void ChangesSearchClear_Tapped(object sender, EventArgs e)
        {
            ChangesSearchEntry.Text = "";
            ChangesSearchClear.IsVisible = false;
            RenderChangesFiltered("");
        }

        private void RenderChangesFiltered(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                RenderChangesTable();
                return;
            }

            var q = query.ToLowerInvariant();
            var backup = _profileChanges;
            _profileChanges = _profileChanges
                .Where(c =>
                    (c.UserName ?? "").ToLowerInvariant().Contains(q) ||
                    (c.FieldChanged ?? "").ToLowerInvariant().Contains(q) ||
                    (c.ChangeDetails ?? "").ToLowerInvariant().Contains(q))
                .ToList();

            RenderChangesTable();
            _profileChanges = backup;
        }

        #endregion

        #region Profile Changes Tab Methods


        // Message handlers for real-time updates
        private void OnProfileUpdated(object sender, ProfileUpdatedMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _profileChanges.Insert(0, new ProfileChange
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = message.UserPhone,
                    UserName = message.UserName,
                    ProfileImage = string.Empty,
                    FieldChanged = message.FieldChanged,
                    OldValue = message.OldValue,
                    NewValue = message.NewValue,
                    ChangeDetails = message.Description,
                    Icon = GetFieldIcon(message.FieldChanged),
                    Timestamp = DateTime.UtcNow
                });

                if (_currentTab == "Changes")
                    RenderChangesTable();
            });
        }

        private void OnPostCreated(object sender, PostCreatedMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _userActivities.Insert(0, new UserActivity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = message.UserPhone,
                    UserName = message.UserName,
                    ProfileImage = string.Empty,
                    ActivityType = "Post Created",
                    Title = "New Post",
                    Description = message.Content?.Length > 100
                        ? message.Content.Substring(0, 100) + "..."
                        : message.Content ?? "Shared a post",
                    Icon = "??",
                    Timestamp = DateTime.UtcNow
                });

                if (_currentTab == "Activities")
                    RenderActivitiesTable();
            });
        }

        private void OnLoveToggled(object sender, LoveToggledMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _userActivities.Insert(0, new UserActivity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = message.UserPhone,
                    UserName = message.UserName,
                    ActivityType = message.IsLoved ? "Loved" : "Unloved",
                    Title = message.IsLoved ? "?? Loved a Post" : "?? Unloved a Post",
                    Description = $"{(message.IsLoved ? "Loved" : "Unloved")} a post by {message.PostAuthorName}",
                    Icon = message.IsLoved ? "??" : "??",
                    Timestamp = DateTime.UtcNow
                });
            });
        }

        private void OnSparkToggled(object sender, SparkToggledMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _userActivities.Insert(0, new UserActivity
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = message.UserPhone,
                    UserName = message.UserName,
                    ActivityType = message.IsSparked ? "Sparked" : "Unsparked",
                    Title = message.IsSparked ? "? Sparked a Post" : "? Removed Spark",
                    Description = $"{(message.IsSparked ? "Sparked" : "Removed spark from")} a post by {message.PostAuthorName}",
                    Icon = "?",
                    Timestamp = DateTime.UtcNow
                });
            });
        }

        private string GetFieldIcon(string fieldName)
        {
            return fieldName?.ToLower() switch
            {
                var s when s != null && s.Contains("mood") => "M480-80q-83 0-141.5-58.5T280-280q0-48 18-90.5T352-446l128-128 128 128q36 36 54 78.5t18 90.5q0 83-58.5 141.5T480-80Zm0-80q50 0 85-35t35-85q0-29-10.5-54T560-381l-80-80-80 80q-19 19-29.5 44T360-280q0 50 35 85t85 35Zm0-160ZM360-600l-84-84q-11-11-11-28t11-28l196-196q11-11 28-11t28 11l196 196q11 11 11 28t-11 28l-84 84-80-80-80 80-120-120 80-80-80 80 120 120Z",
                var s when s != null && s.Contains("bio") => "M200-200h57l391-391-57-57-391 391v57Zm-80 80v-170l528-527q12-11 26.5-17t30.5-6q16 0 31 6t26 18l55 56q12 11 17.5 26t5.5 30q0 16-5.5 30.5T817-647L290-120H120Zm640-584-56-56 56 56Zm-141 85-28-29 57 57-29-28Z",
                var s when s != null && (s.Contains("photo") || s.Contains("image")) => "M480-260q75 0 127.5-52.5T660-440q0-75-52.5-127.5T480-620q-75 0-127.5 52.5T300-440q0 75 52.5 127.5T480-260Zm0-80q-42 0-71-29t-29-71q0-42 29-71t71-29q42 0 71 29t29 71q0 42-29 71t-71 29ZM160-120q-33 0-56.5-23.5T80-200v-480q0-33 23.5-56.5T160-760h126l74-80h240l74 80h126q33 0 56.5 23.5T880-680v480q0 33-23.5 56.5T800-120H160Z",
                var s when s != null && s.Contains("interest") => "m480-120-58-52q-101-91-167-157T150-447q-79-95-104.5-167T20-756q0-130 91-217t229-87q75 0 141 34t99 96q33-62 99-96t141-34q138 0 229 87t91 217q0 73-25.5 145T835-447q-44 56-110 122.5T538-172l-58 52Z",
                var s when s != null && s.Contains("location") => "M480-480q33 0 56.5-23.5T560-560q0-33-23.5-56.5T480-640q-33 0-56.5 23.5T400-560q0 33 23.5 56.5T480-480Zm0 294q122-112 181-203.5T720-552q0-109-69.5-178.5T480-800q-101 0-170.5 69.5T240-552q0 71 59 162.5T480-186Z",
                _ => "M440-280h80v-240h-80v240Zm40-320q17 0 28.5-11.5T520-640q0-17-11.5-28.5T480-680q-17 0-28.5 11.5T440-640q0 17 11.5 28.5T480-600Zm0 520q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Z"
            };
        }

        #endregion

        #region Reports Tab Methods
        private void ReportTypePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            GenerateReport();
        }

        private async void GenerateReportButton_Clicked(object sender, EventArgs e)
        {
            await GenerateReport();
        }

        private async Task GenerateReport()
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                ReportContainer.Children.Clear();

                var reportType = ReportTypePicker?.SelectedItem as string ?? "User Summary";
                var selectedDate = ReportDatePicker?.Date ?? DateTime.Today;

                switch (reportType)
                {
                    case "User Summary":
                        await GenerateUserSummaryReport();
                        break;
                    case "Activity Report":
                        await GenerateActivityReport(selectedDate);
                        break;
                    case "Profile Changes Report":
                        await GenerateChangesReport(selectedDate);
                        break;
                    case "Verification Status":
                        await GenerateVerificationReport();
                        break;
                }

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                await DisplayAlert("Error", $"Failed to generate report: {ex.Message}", "OK");
            }
        }

        // ?? Shared: avatar frame builder ??
        private Frame BuildReportAvatar(string profileImagePath, string name, int size = 36)
        {
            var frame = new Frame
            {
                WidthRequest = size,
                HeightRequest = size,
                CornerRadius = size / 2,
                Padding = 0,
                IsClippedToBounds = true,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#1A2A2A"),
                BorderColor = Color.FromArgb("#00B5B5")
            };

            var validPath = GetValidProfileImagePath(profileImagePath);
            if (!string.IsNullOrEmpty(validPath))
            {
                frame.Content = new Image
                {
                    Source = ImageSource.FromFile(validPath),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = size,
                    HeightRequest = size
                };
            }
            else
            {
                frame.Content = new Label
                {
                    Text = name?.Length > 0 ? name[0].ToString().ToUpper() : "?",
                    FontSize = size * 0.38,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00B5B5"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
            }
            return frame;
        }

        private async void OnViewReportsClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PushAsync(new ReportAdminPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to ReportAdminPage: {ex}");
                await DisplayAlert("Error", "Could not open Reports", "OK");
            }
        }


        // ?? Shared: section header ??
        private View BuildSectionHeader(string title, string subtitle = "")
        {
            var stack = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 0, 0, 10) };
            stack.Children.Add(new Label
            {
                Text = title,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#00B5B5"),
                CharacterSpacing = 1.2
            });
            if (!string.IsNullOrEmpty(subtitle))
                stack.Children.Add(new Label
                {
                    Text = subtitle,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#5A5A6A")
                });
            return stack;
        }

        // ?? Shared: stat row ??
        private View BuildStatRow(string label, string value, string valueColor = "#2196F3")
        {
            var grid = new Grid
            {
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        },
                Margin = new Thickness(0, 3)
            };
            grid.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#888888") }, 0, 0);
            grid.Add(new Label
            {
                Text = value,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(valueColor),
                HorizontalOptions = LayoutOptions.End
            }, 1, 0);
            return grid;
        }

        // ?? Shared: user row with avatar ??
        private View BuildUserRow(string profileImage, string name, string detail, string badge = "", string badgeColor = "#00B5B5", string badgeBg = "#0D1F1F")
        {
            var grid = new Grid
            {
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(44) },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        },
                ColumnSpacing = 10,
                Margin = new Thickness(0, 4)
            };

            grid.Add(BuildReportAvatar(profileImage, name, 36), 0, 0);

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Children.Add(new Label
            {
                Text = name,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F0EDE8"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
            if (!string.IsNullOrEmpty(detail))
                textStack.Children.Add(new Label
                {
                    Text = detail,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#7A7A8C"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                });
            grid.Add(textStack, 1, 0);

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeBorder = new Border
                {
                    BackgroundColor = Color.FromArgb(badgeBg),
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Color.FromArgb(badgeColor)),
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    Padding = new Thickness(8, 3),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = badge,
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb(badgeColor)
                    }
                };
                grid.Add(badgeBorder, 2, 0);
            }

            return grid;
        }

        // ?? Shared: card wrapper ??
        private Border WrapInCard(View content, string headerTitle = "", string headerIcon = "")
        {
            var cardStack = new VerticalStackLayout { Spacing = 0 };

            if (!string.IsNullOrEmpty(headerTitle))
            {
                var header = new Grid
                {
                    Padding = new Thickness(16, 12),
                    BackgroundColor = Color.FromArgb("#12121A"),
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
                    ColumnSpacing = 8
                };
                if (!string.IsNullOrEmpty(headerIcon))
                    header.Add(new Label
                    {
                        Text = headerIcon,
                        FontSize = 14,
                        VerticalOptions = LayoutOptions.Center
                    }, 0, 0);

                header.Add(new Label
                {
                    Text = headerTitle,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    VerticalOptions = LayoutOptions.Center
                }, 1, 0);

                cardStack.Children.Add(header);
                cardStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }

            var contentWrapper = new VerticalStackLayout { Padding = new Thickness(16, 12), Spacing = 4 };
            contentWrapper.Children.Add(content);
            cardStack.Children.Add(contentWrapper);

            return new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D14"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1C1C25")),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = cardStack,
                Margin = new Thickness(0, 0, 0, 14)
            };
        }

        // ?? REPORT 1: USER SUMMARY ??
        private async Task GenerateUserSummaryReport()
        {
            if (!_allUsers.Any())
            {
                ReportContainer.Children.Add(WrapInCard(
                    new Label { Text = "No users found.", TextColor = Color.FromArgb("#5A5A6A"), FontSize = 13 },
                    "?? User Summary", ""));
                return;
            }

            // ?? Stats card ??
            var statsStack = new VerticalStackLayout { Spacing = 2 };
            statsStack.Children.Add(BuildStatRow("Total Users", _allUsers.Count.ToString(), "#F0EDE8"));
            statsStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(0, 6) });
            statsStack.Children.Add(BuildStatRow("Male", _allUsers.Count(u => u.Gender == "Male").ToString(), "#2196F3"));
            statsStack.Children.Add(BuildStatRow("Female", _allUsers.Count(u => u.Gender == "Female").ToString(), "#E91E8C"));
            statsStack.Children.Add(BuildStatRow("Other / Not Specified", _allUsers.Count(u => u.Gender == "Other" || string.IsNullOrEmpty(u.Gender)).ToString(), "#9C27B0"));
            statsStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(0, 6) });

            var usersWithAge = _allUsers.Where(u => u.DateOfBirth != DateTime.MinValue).ToList();
            var avgAge = usersWithAge.Any() ? usersWithAge.Average(u => u.GetAge()) : 0;
            statsStack.Children.Add(BuildStatRow("Average Age", avgAge > 0 ? $"{avgAge:F1} yrs" : "N/A", "#00B5B5"));
            statsStack.Children.Add(BuildStatRow("Verified", _allUsers.Count(u => u.IsVerified).ToString(), "#4CAF50"));

            ReportContainer.Children.Add(WrapInCard(statsStack, "?? Overview", ""));

            // ?? Age distribution ??
            var ageStack = new VerticalStackLayout { Spacing = 2 };
            var ageBands = new[] { ("18–24", 18, 24), ("25–34", 25, 34), ("35–44", 35, 44), ("45+", 45, 999) };
            foreach (var (label, min, max) in ageBands)
            {
                var count = _allUsers.Count(u => { try { var a = u.GetAge(); return a >= min && a <= max; } catch { return false; } });
                ageStack.Children.Add(BuildStatRow(label, count.ToString(), "#00B5B5"));
            }
            ReportContainer.Children.Add(WrapInCard(ageStack, "?? Age Distribution", ""));

            // ?? All users list ??
            var usersListStack = new VerticalStackLayout { Spacing = 0 };
            foreach (var user in _allUsers.OrderByDescending(u => u.JoinDate))
            {
                usersListStack.Children.Add(BuildUserRow(
                    user.ProfileImagePath,
                    user.Name,
                    $"{user.PhoneNumber}  •  {user.Gender ?? "—"}  •  Age {user.GetAge()}",
                    user.IsVerified ? "? Verified" : "Unverified",
                    user.IsVerified ? "#4CAF50" : "#666666",
                    user.IsVerified ? "#0A2A0A" : "#1A1A1A"
                ));
                usersListStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }
            ReportContainer.Children.Add(WrapInCard(usersListStack, $"?? All Users ({_allUsers.Count})", ""));
        }

        // ?? REPORT 2: ACTIVITY REPORT ??
        private async Task GenerateActivityReport(DateTime date)
        {
            var activities = _userActivities.Where(a => a.Timestamp.Date == date.Date).ToList();

            // ?? Stats summary ??
            var statsStack = new VerticalStackLayout { Spacing = 2 };
            statsStack.Children.Add(BuildStatRow("Total Activities", activities.Count.ToString(), "#F0EDE8"));
            statsStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(0, 6) });
            statsStack.Children.Add(BuildStatRow("Posts Created", activities.Count(a => a.ActivityType == "Post Created").ToString(), "#2196F3"));
            statsStack.Children.Add(BuildStatRow("Loves Given", activities.Count(a => a.ActivityType == "Loved").ToString(), "#E91E63"));
            statsStack.Children.Add(BuildStatRow("Sparks Given", activities.Count(a => a.ActivityType == "Sparked").ToString(), "#FF9800"));
            statsStack.Children.Add(BuildStatRow("Unique Users Active", activities.Select(a => a.UserId).Distinct().Count().ToString(), "#00B5B5"));
            ReportContainer.Children.Add(WrapInCard(statsStack, $"?? Summary — {date:MMM dd, yyyy}", ""));

            if (!activities.Any())
            {
                ReportContainer.Children.Add(WrapInCard(
                    new Label { Text = "No activities recorded for this date.", TextColor = Color.FromArgb("#5A5A6A"), FontSize = 13 }, "", ""));
                return;
            }

            // ?? Most active users ??
            var topUsers = activities
                .GroupBy(a => new { a.UserId, a.UserName, a.ProfileImage })
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            var topStack = new VerticalStackLayout { Spacing = 0 };
            int rank = 1;
            foreach (var g in topUsers)
            {
                topStack.Children.Add(BuildUserRow(
                    g.Key.ProfileImage,
                    g.Key.UserName,
                    $"{g.Count()} activities  •  Posts: {g.Count(a => a.ActivityType == "Post Created")}",
                    $"#{rank}",
                    rank == 1 ? "#FFD700" : rank == 2 ? "#C0C0C0" : rank == 3 ? "#CD7F32" : "#555555",
                    "#1A1A1A"
                ));
                topStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                rank++;
            }
            ReportContainer.Children.Add(WrapInCard(topStack, "?? Most Active Users", ""));

            // ?? Full activity feed ??
            var feedStack = new VerticalStackLayout { Spacing = 0 };
            foreach (var act in activities.OrderByDescending(a => a.Timestamp))
            {
                feedStack.Children.Add(BuildUserRow(
                    act.ProfileImage,
                    act.UserName,
                    act.Description?.Length > 60 ? act.Description.Substring(0, 60) + "…" : act.Description ?? "",
                    act.ActivityType,
                    act.ActivityType == "Post Created" ? "#22C55E" :
                    act.ActivityType == "Loved" ? "#E91E63" :
                    act.ActivityType == "Sparked" ? "#FF9800" : "#00B5B5",
                    "#0A0A0F"
                ));
                // Timestamp sub-row
                feedStack.Children.Add(new Label
                {
                    Text = "    " + act.Timestamp.ToString("HH:mm:ss"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A"),
                    Margin = new Thickness(54, 0, 0, 4)
                });
                feedStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }
            ReportContainer.Children.Add(WrapInCard(feedStack, $"?? All Activities ({activities.Count})", ""));
        }

        // ?? REPORT 3: PROFILE CHANGES ??
        private async Task GenerateChangesReport(DateTime date)
        {
            var changes = _profileChanges.Where(c => c.Timestamp.Date == date.Date).ToList();

            // ?? Stats ??
            var statsStack = new VerticalStackLayout { Spacing = 2 };
            statsStack.Children.Add(BuildStatRow("Total Changes", changes.Count.ToString(), "#F0EDE8"));
            statsStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(0, 6) });
            statsStack.Children.Add(BuildStatRow("Mood Updates", changes.Count(c => c.FieldChanged.Contains("Mood")).ToString(), "#9C27B0"));
            statsStack.Children.Add(BuildStatRow("Bio Updates", changes.Count(c => c.FieldChanged == "Bio").ToString(), "#2196F3"));
            statsStack.Children.Add(BuildStatRow("Unique Users", changes.Select(c => c.UserId).Distinct().Count().ToString(), "#00B5B5"));
            ReportContainer.Children.Add(WrapInCard(statsStack, $"?? Summary — {date:MMM dd, yyyy}", ""));

            if (!changes.Any())
            {
                ReportContainer.Children.Add(WrapInCard(
                    new Label { Text = "No profile changes recorded for this date.", TextColor = Color.FromArgb("#5A5A6A"), FontSize = 13 }, "", ""));
                return;
            }

            // ?? Changes by field ??
            var byField = changes.GroupBy(c => c.FieldChanged).OrderByDescending(g => g.Count());
            var fieldStack = new VerticalStackLayout { Spacing = 2 };
            foreach (var g in byField)
                fieldStack.Children.Add(BuildStatRow(g.Key, g.Count().ToString(), "#00B5B5"));
            ReportContainer.Children.Add(WrapInCard(fieldStack, "?? Changes by Field", ""));

            // ?? Full changes feed ??
            var feedStack = new VerticalStackLayout { Spacing = 0 };
            foreach (var change in changes.OrderByDescending(c => c.Timestamp))
            {
                feedStack.Children.Add(BuildUserRow(
                    change.ProfileImage,
                    change.UserName,
                    change.ChangeDetails,
                    change.FieldChanged,
                    "#00B5B5",
                    "#0D1F1F"
                ));
                feedStack.Children.Add(new Label
                {
                    Text = "    " + change.Timestamp.ToString("HH:mm:ss"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A"),
                    Margin = new Thickness(54, 0, 0, 4)
                });
                feedStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }
            ReportContainer.Children.Add(WrapInCard(feedStack, $"?? All Changes ({changes.Count})", ""));
        }

        // ?? REPORT 4: VERIFICATION ??
        private async Task GenerateVerificationReport()
        {
            if (!_allUsers.Any())
            {
                ReportContainer.Children.Add(WrapInCard(
                    new Label { Text = "No users found.", TextColor = Color.FromArgb("#5A5A6A"), FontSize = 13 },
                    "?? Verification Status", ""));
                return;
            }

            var verified = _allUsers.Where(u => u.IsVerified).ToList();
            var pending = _allUsers.Where(u => u.VerificationStatus == "pending").ToList();
            var rejected = _allUsers.Where(u => u.VerificationStatus == "rejected").ToList();
            var notVerified = _allUsers.Where(u => !u.IsVerified && u.VerificationStatus != "pending" && u.VerificationStatus != "rejected").ToList();
            var total = _allUsers.Count;

            // ?? Stats ??
            var statsStack = new VerticalStackLayout { Spacing = 2 };
            statsStack.Children.Add(BuildStatRow("? Verified", verified.Count.ToString(), "#4CAF50"));
            statsStack.Children.Add(BuildStatRow("? Pending", pending.Count.ToString(), "#FF9800"));
            statsStack.Children.Add(BuildStatRow("? Not Verified", notVerified.Count.ToString(), "#666666"));
            statsStack.Children.Add(BuildStatRow("?? Rejected", rejected.Count.ToString(), "#F44336"));
            statsStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(0, 6) });
            statsStack.Children.Add(BuildStatRow("Verification Rate", $"{(double)verified.Count / total * 100:F1}%", "#00B5B5"));
            ReportContainer.Children.Add(WrapInCard(statsStack, "?? Overview", ""));

            // ?? Verified users ??
            if (verified.Any())
            {
                var verStack = new VerticalStackLayout { Spacing = 0 };
                foreach (var u in verified)
                {
                    verStack.Children.Add(BuildUserRow(u.ProfileImagePath, u.Name, u.PhoneNumber, "? Verified", "#4CAF50", "#0A2A0A"));
                    verStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                ReportContainer.Children.Add(WrapInCard(verStack, $"? Verified Users ({verified.Count})", ""));
            }

            // ?? Pending users ??
            if (pending.Any())
            {
                var penStack = new VerticalStackLayout { Spacing = 0 };
                foreach (var u in pending)
                {
                    penStack.Children.Add(BuildUserRow(u.ProfileImagePath, u.Name, u.PhoneNumber, "? Pending", "#FF9800", "#2A1A00"));
                    penStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                ReportContainer.Children.Add(WrapInCard(penStack, $"? Pending Verification ({pending.Count})", ""));
            }

            // ?? Rejected users ??
            if (rejected.Any())
            {
                var rejStack = new VerticalStackLayout { Spacing = 0 };
                foreach (var u in rejected)
                {
                    rejStack.Children.Add(BuildUserRow(u.ProfileImagePath, u.Name, u.PhoneNumber, "?? Rejected", "#F44336", "#2A0A0A"));
                    rejStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                ReportContainer.Children.Add(WrapInCard(rejStack, $"?? Rejected ({rejected.Count})", ""));
            }

            // ?? Not verified ??
            if (notVerified.Any())
            {
                var nvStack = new VerticalStackLayout { Spacing = 0 };
                foreach (var u in notVerified)
                {
                    nvStack.Children.Add(BuildUserRow(u.ProfileImagePath, u.Name, u.PhoneNumber, "Unverified", "#555555", "#1A1A1A"));
                    nvStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                ReportContainer.Children.Add(WrapInCard(nvStack, $"? Not Yet Verified ({notVerified.Count})", ""));
            }
        }
        #endregion



        // Add this method to navigate to the full Admin Dashboard
        private async void OnAdminDashboardClicked(object sender, EventArgs e)
        {
            try
            {
                Debug.WriteLine("Admin Dashboard button clicked - navigating to full dashboard");

                // Navigate to the AdminDashboardPage
                await Navigation.PushAsync(new AdminDashboardPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to Admin Dashboard: {ex}");
                await DisplayAlert("Error", "Could not open Admin Dashboard", "OK");
            }
        }

        private Border CreateStatCard(string title, Dictionary<string, string> stats)
        {
            var stackLayout = new VerticalStackLayout { Spacing = 8 };

            stackLayout.Children.Add(new Label
            {
                Text = title,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333333"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            foreach (var stat in stats)
            {
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) }, new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) } } };
                row.Add(new Label { Text = stat.Key, FontSize = 14, TextColor = Color.FromArgb("#666666") }, 0, 0);
                row.Add(new Label { Text = stat.Value, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2196F3"), HorizontalOptions = LayoutOptions.End }, 1, 0);
                stackLayout.Children.Add(row);
            }

            return new Border
            {
                Padding = new Thickness(15),
                BackgroundColor = Colors.White,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = stackLayout,
                Margin = new Thickness(0, 0, 0, 15)
            };
        }

        private async void ExportReportButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                var reportType = ReportTypePicker?.SelectedItem as string ?? "User Summary";
                var csv = new StringBuilder();

                switch (reportType)
                {
                    case "User Summary":
                        csv.AppendLine("User Summary Report");
                        csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        csv.AppendLine("");
                        csv.AppendLine("Name,Phone,Age,Gender,Interest,Verified,Join Date");

                        if (_allUsers.Any())
                        {
                            foreach (var user in _allUsers)
                            {
                                var age = user.GetAge();
                                csv.AppendLine($"\"{user.Name}\",{user.PhoneNumber},{age},{user.Gender},{user.Interest},{user.IsVerified},{user.JoinDate:yyyy-MM-dd}");
                            }
                        }
                        else
                        {
                            csv.AppendLine("No users found");
                        }
                        break;

                    case "Activity Report":
                        csv.AppendLine("Activity Report");
                        csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        csv.AppendLine("");
                        csv.AppendLine("User,Activity Type,Description,Timestamp");

                        if (_userActivities.Any())
                        {
                            foreach (var activity in _userActivities.Take(500))
                            {
                                csv.AppendLine($"\"{activity.UserName}\",{activity.ActivityType},\"{activity.Description}\",{activity.Timestamp:yyyy-MM-dd HH:mm:ss}");
                            }
                        }
                        else
                        {
                            csv.AppendLine("No activities found");
                        }
                        break;
                }

                var fileName = $"{reportType.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Export {reportType}",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                await DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
            }
        }

        private void MenuButton_Clicked(object sender, EventArgs e)
        {
            _isFilterVisible = !_isFilterVisible;
            FilterBar.IsVisible = _isFilterVisible;
            var toggleLabel = this.FindByName<Label>("FilterToggleLabel");
            if (toggleLabel != null)
                toggleLabel.Text = _isFilterVisible ? "Filters  ?" : "Filters  ?";
        }

        // ?? Add new filter event handlers ??
        private void InterestFilter_SelectedIndexChanged(object sender, EventArgs e) => ApplyFilters();
        private void LocationFilter_SelectedIndexChanged(object sender, EventArgs e) => ApplyFilters();

    }

    #region Models for Activity Tracking
    public class UserActivity
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty; // Add this
        public string ActivityType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string BackgroundColor => "#FFFFFF";
        public string StatusIcon => "?";
        public string StatusColor => Timestamp > DateTime.UtcNow.AddHours(-24) ? "#4CAF50" : "#999999";
    }

    public class ProfileChange
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty; // Add this
        public string FieldChanged { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string ChangeDetails { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string BackgroundColor => "#FFFFFF";
    }
    // Message models for real-time updates
    public class ProfileUpdatedMessage
    {
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FieldChanged { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class PostCreatedMessage
    {
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class LoveToggledMessage
    {
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string PostAuthorName { get; set; } = string.Empty;
        public bool IsLoved { get; set; }
    }

    public class SparkToggledMessage
    {
        public string UserPhone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int PostId { get; set; }
        public string PostAuthorName { get; set; } = string.Empty;
        public bool IsSparked { get; set; }
    }
    #endregion
}