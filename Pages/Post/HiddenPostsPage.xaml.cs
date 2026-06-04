using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Post
{
    public partial class HiddenPostsPage : ContentPage
    {
        private ObservableCollection<Lock.Models.Post> _hiddenPosts = new();
        // Master list — always holds ALL saved items regardless of active category
        private List<Lock.Services.SavedPostItem> _allSavedPosts = new();
        private ObservableCollection<MutedUserInfo> _mutedUsers = new();

        private string _currentTab = "Hidden";
        private string _currentCategory = "All";
        private bool _isNavigating;

        public HiddenPostsPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAllData();
        }

        // Load all data
        private async Task LoadAllData()
        {
            await LoadHiddenPosts();
            await LoadSavedPosts();   // populates _allSavedPosts + rebuilds category tabs
            await LoadMutedUsers();
            UpdateVisibleContent();
        }

        // Saved posts
        private async Task LoadSavedPosts()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var list = await SavePostService.GetSavedPostsWithFoldersAsync(currentUserPhone);

                _allSavedPosts.Clear();
                foreach (var item in list)
                {
                    await ResolveAuthorAsync(item.Post);
                    item.Post.UpdateDisplayContent(100);
                    _allSavedPosts.Add(item);
                }

                // Rebuild category tabs from the freshly loaded master list
                RebuildCategoryTabs();

                // Apply whichever category is currently active
                ApplyCategoryFilter();

                Debug.WriteLine($"LoadSavedPosts: {_allSavedPosts.Count} total items, {GetDistinctCategories().Count} categories");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSavedPosts error: {ex}");
            }
        }

        // Returns distinct, non-empty category names from the master list
        private List<string> GetDistinctCategories()
        {
            return _allSavedPosts
                .Select(s => string.IsNullOrEmpty(s.FolderName) ? "Uncategorized" : s.FolderName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        // Rebuilds the horizontal category tab strip from _allSavedPosts
        private void RebuildCategoryTabs()
        {
            var container = this.FindByName<HorizontalStackLayout>("CategoryTabsLayout");
            if (container == null) return;

            // Remove everything except the fixed "All" button at index 0
            while (container.Children.Count > 1)
                container.Children.RemoveAt(1);

            // Style the "All" button
            if (container.Children[0] is Button allBtn)
            {
                allBtn.CommandParameter = "All";          // ensure parameter is set
                allBtn.BackgroundColor = _currentCategory == "All"
                    ? Color.FromArgb("#FF3B6F") : Color.FromArgb("#2A2A2A");
                allBtn.TextColor = _currentCategory == "All"
                    ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#888888");
            }

            // Add one button per distinct category
            foreach (var categoryName in GetDistinctCategories())
            {
                int count = _allSavedPosts.Count(s =>
                    (string.IsNullOrEmpty(s.FolderName) ? "Uncategorized" : s.FolderName) == categoryName);

                var btn = new Button
                {
                    Text = $"{categoryName} ({count})",
                    BackgroundColor = _currentCategory == categoryName
                        ? Color.FromArgb("#FF3B6F") : Color.FromArgb("#2A2A2A"),
                    TextColor = _currentCategory == categoryName
                        ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#888888"),
                    CornerRadius = 20,
                    Padding = new Thickness(12, 6),
                    FontSize = 13,
                    CommandParameter = categoryName
                };
                btn.Clicked += OnCategoryTabClicked;
                container.Children.Add(btn);
            }
        }

        // Filters _allSavedPosts by _currentCategory and pushes the result
        // into SavedPostsCollectionView.ItemsSource
        private void ApplyCategoryFilter()
        {
            IEnumerable<Lock.Services.SavedPostItem> filtered;

            if (_currentCategory == "All")
            {
                filtered = _allSavedPosts;
            }
            else
            {
                filtered = _allSavedPosts.Where(s =>
                    (string.IsNullOrEmpty(s.FolderName) ? "Uncategorized" : s.FolderName) == _currentCategory);
            }

            // Assign a new list so the CollectionView sees the change
            SavedPostsCollectionView.ItemsSource = filtered.Select(s => s.Post).ToList();
        }

        private void UpdateCategoryButtonStyles()
        {
            var container = this.FindByName<HorizontalStackLayout>("CategoryTabsLayout");
            if (container == null) return;

            foreach (var child in container.Children)
            {
                if (child is Button btn)
                {
                    bool active = (btn.CommandParameter?.ToString() ?? "All") == _currentCategory;
                    btn.BackgroundColor = active ? Color.FromArgb("#FF3B6F") : Color.FromArgb("#2A2A2A");
                    btn.TextColor = active ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#888888");
                }
            }
        }

        private void OnCategoryTabClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            _currentCategory = btn.CommandParameter?.ToString() ?? "All";
            UpdateCategoryButtonStyles();
            ApplyCategoryFilter();
            UpdateVisibleContent();
        }

        // Tab switching
        private void OnTabClicked(object sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not string tab) return;

            _currentTab = tab;

            HiddenTabButton.TextColor = tab == "Hidden" ? Color.FromArgb("#FF3B6F") : Color.FromArgb("#888888");
            SavedTabButton.TextColor = tab == "Saved" ? Color.FromArgb("#FFD24D") : Color.FromArgb("#888888");
            MutedTabButton.TextColor = tab == "Muted" ? Color.FromArgb("#FF3B6F") : Color.FromArgb("#888888");

            UpdateVisibleContent();
        }

        // Visible content
        private void UpdateVisibleContent()
        {
            HiddenPostsCollectionView.IsVisible = false;
            SavedContent.IsVisible = false;
            MutedUsersCollectionView.IsVisible = false;
            EmptyStateGrid.IsVisible = false;

            switch (_currentTab)
            {
                case "Hidden":
                    if (_hiddenPosts.Count == 0)
                        ShowEmptyState("??", "No hidden posts", "Posts you hide will appear here");
                    else
                        HiddenPostsCollectionView.IsVisible = true;
                    break;

                case "Saved":
                    if (_allSavedPosts.Count == 0)
                    {
                        ShowEmptyState("??", "No saved posts", "Bookmark posts to find them here");
                    }
                    else
                    {
                        SavedContent.IsVisible = true;
                    }
                    break;

                case "Muted":
                    if (_mutedUsers.Count == 0)
                        ShowEmptyState("??", "No muted users", "Users you mute will appear here");
                    else
                        MutedUsersCollectionView.IsVisible = true;
                    break;
            }
        }

        private void ShowEmptyState(string icon, string title, string message)
        {
            EmptyStateIcon.Text = icon;
            EmptyStateTitle.Text = title;
            EmptyStateMessage.Text = message;
            EmptyStateGrid.IsVisible = true;
        }

        // Load hidden posts
        private async Task LoadHiddenPosts()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var list = await HidePostService.GetHiddenPostsAsync(currentUserPhone);

                _hiddenPosts.Clear();
                foreach (var post in list)
                {
                    await ResolveAuthorAsync(post);
                    post.UpdateDisplayContent(100);
                    _hiddenPosts.Add(post);
                }

                HiddenPostsCollectionView.ItemsSource = _hiddenPosts;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadHiddenPosts error: {ex}");
            }
        }

        // Load muted users
        private async Task LoadMutedUsers()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var mutedPhones = MuteUserService.GetMutedPhones(currentUserPhone);
                _mutedUsers.Clear();

                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();

                foreach (var phone in mutedPhones)
                {
                    try
                    {
                        // Replace with Supabase code:
                        var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                            $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                        var user = users.FirstOrDefault();

                        _mutedUsers.Add(new MutedUserInfo
                        {
                            PhoneNumber = phone,
                            DisplayName = user?.Name ?? phone,
                            ProfileImagePath = user?.ProfileImagePath ?? string.Empty
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading muted user {phone}: {ex}");
                        _mutedUsers.Add(new MutedUserInfo { PhoneNumber = phone, DisplayName = phone });
                    }
                }

                MutedUsersCollectionView.ItemsSource = _mutedUsers;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadMutedUsers error: {ex}");
            }
        }

        // Resolve author
        private async Task ResolveAuthorAsync(Lock.Models.Post post)
        {
            try
            {
                if (string.IsNullOrEmpty(post.AuthorPhone)) return;

                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // var user = await db.Table<Lock.Models.User>()
                //     .Where(u => u.PhoneNumber == post.AuthorPhone)
                //     .FirstOrDefaultAsync();

                // Replace with Supabase code:
                var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(post.AuthorPhone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    post.AuthorDisplayName = string.IsNullOrWhiteSpace(user.Name) ? post.AuthorPhone : user.Name;
                    post.AuthorProfileImagePath = user.ProfileImagePath ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResolveAuthorAsync error: {ex}");
            }
        }

        // Replace the OnPostTapped method
        private async void OnPostTapped(object sender, EventArgs e)
        {
            if (_isNavigating) return;

            try
            {
                Lock.Models.Post? post = null;

                // Try to get post from different sources
                if (sender is TapGestureRecognizer tapGesture)
                {
                    post = tapGesture.CommandParameter as Lock.Models.Post;
                }
                else if (sender is Grid grid)
                {
                    post = grid.BindingContext as Lock.Models.Post;
                }
                else if (sender is VisualElement visualElement)
                {
                    post = visualElement.BindingContext as Lock.Models.Post;
                }

                if (post == null || post.Id <= 0) return;

                _isNavigating = true;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var commentsPage = new CommentsPage(post.Id, currentUserPhone);

                // Check if this page was presented modally or pushed
                if (Navigation.ModalStack.Contains(this))
                {
                    // If modal, pop the modal first then navigate
                    await Navigation.PopModalAsync(false);
                    await Task.Delay(100);
                    await Navigation.PushAsync(commentsPage);
                }
                else
                {
                    // If not modal, just push the CommentsPage
                    await Navigation.PushAsync(commentsPage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPostTapped error: {ex}");

                // Fallback: try direct navigation
                try
                {
                    if (sender is TapGestureRecognizer tapGesture)
                    {
                        var post = tapGesture.CommandParameter as Lock.Models.Post;
                        if (post != null && post.Id > 0)
                        {
                            var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                            var commentsPage = new CommentsPage(post.Id, currentUserPhone);
                            await Navigation.PushAsync(commentsPage);
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"Fallback navigation error: {fallbackEx}");
                    await DisplayAlert("Error", "Could not open post. Please try again.", "OK");
                }
            }
            finally
            {
                _isNavigating = false;
            }
        }
        // Replace the NavigateToPost method
        private async Task NavigateToPost(Lock.Models.Post post)
        {
            if (_isNavigating) return;
            _isNavigating = true;
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                // Create CommentsPage directly (this already shows the post header + comments)
                var commentsPage = new CommentsPage(post.Id, currentUserPhone);

                // Check if this page was presented modally or pushed
                if (Navigation.ModalStack.Contains(this))
                {
                    // If modal, pop the modal first then navigate
                    await Navigation.PopModalAsync(false);
                    await Task.Delay(100);
                    await Navigation.PushAsync(commentsPage);
                }
                else
                {
                    // If not modal, just push the CommentsPage
                    await Navigation.PushAsync(commentsPage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigateToPost error: {ex}");

                // Fallback: try direct push without popping modal
                try
                {
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    var commentsPage = new CommentsPage(post.Id, currentUserPhone);
                    await Navigation.PushAsync(commentsPage);
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"Fallback navigation error: {fallbackEx}");
                    await DisplayAlert("Error", "Could not open post. Please try again.", "OK");
                }
            }
            finally
            {
                _isNavigating = false;
            }
        }
        // Also update the OnPostMenuClicked to use the same navigation logic
        private async void OnPostMenuClicked(object sender, EventArgs e)
        {
            try
            {
                Lock.Models.Post? post = null;

                // Handle different sender types
                if (sender is Button button)
                {
                    post = button.CommandParameter as Lock.Models.Post;
                }
                else if (sender is Grid grid)
                {
                    post = grid.BindingContext as Lock.Models.Post;
                }
                else if (sender is TapGestureRecognizer tapGesture)
                {
                    post = tapGesture.CommandParameter as Lock.Models.Post;
                }
                else if (sender is VisualElement visualElement)
                {
                    post = visualElement.BindingContext as Lock.Models.Post;

                    // If still null, try to traverse up to find the parent Grid's binding context
                    if (post == null)
                    {
                        var parent = visualElement.Parent;
                        while (parent != null && post == null)
                        {
                            if (parent is Grid parentGrid)
                            {
                                post = parentGrid.BindingContext as Lock.Models.Post;
                            }
                            else if (parent is VerticalStackLayout vstack && vstack.Parent is Grid gridParent)
                            {
                                post = gridParent.BindingContext as Lock.Models.Post;
                            }
                            parent = parent.Parent;
                        }
                    }
                }

                if (post == null)
                {
                    Debug.WriteLine("OnPostMenuClicked: Could not find post");
                    await DisplayAlert("Error", "Could not find post", "OK");
                    return;
                }

                if (_currentTab == "Hidden")
                {
                    var action = await DisplayActionSheet("Post Options", "Cancel", null, "Unhide", "View Post");
                    if (action == "Unhide")
                        await UnhidePost(post);
                    else if (action == "View Post")
                        await NavigateToPost(post);  // This now goes to CommentsPage
                }
                else if (_currentTab == "Saved")
                {
                    var action = await DisplayActionSheet("Post Options", "Cancel", null, "Unsave", "Move to Category", "View Post");
                    if (action == "Unsave")
                        await UnsavePost(post);
                    else if (action == "Move to Category")
                        await MovePostToFolder(post);
                    else if (action == "View Post")
                        await NavigateToPost(post);  // This now goes to CommentsPage
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPostMenuClicked error: {ex}");
                await DisplayAlert("Error", "Could not open post menu", "OK");
            }
        }

        // Actions
        private async Task MovePostToFolder(Lock.Models.Post post)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var folderName = await DisplayPromptAsync(
                    "Move to Category", "Enter category name:",
                    maxLength: 30, keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(folderName)) return;

                await SavePostService.MovePostToFolderAsync(post.Id, currentUserPhone, folderName);

                // Update master list in-memory so no full reload is needed
                var item = _allSavedPosts.FirstOrDefault(s => s.Post.Id == post.Id);
                if (item != null) item.FolderName = folderName;

                RebuildCategoryTabs();
                ApplyCategoryFilter();
                await DisplayAlert("Success", $"Post moved to '{folderName}'", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MovePostToFolder error: {ex}");
                await DisplayAlert("Error", "Could not move post to category", "OK");
            }
        }

        private async Task UnhidePost(Lock.Models.Post post)
        {
            try
            {
                var confirm = await DisplayAlert("Unhide Post",
                    "Unhide this post? It will appear in your feed again.", "Unhide", "Cancel");
                if (!confirm) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                bool ok = await HidePostService.UnhidePostAsync(post.Id, currentUserPhone);

                if (ok)
                {
                    _hiddenPosts.Remove(post);
                    MessagingCenter.Send(this, "PostUnhidden", post.Id);
                    await DisplayAlert("Unhidden", "Post will appear in your feed again.", "OK");
                    UpdateVisibleContent();
                }
                else
                {
                    await DisplayAlert("Error", "Could not unhide post.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnhidePost error: {ex}");
                await DisplayAlert("Error", "Could not unhide post.", "OK");
            }
        }

        private async Task UnsavePost(Lock.Models.Post post)
        {
            try
            {
                var confirm = await DisplayAlert("Remove Bookmark",
                    "Remove this post from your saved posts?", "Remove", "Cancel");
                if (!confirm) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                bool ok = await SavePostService.UnsavePostAsync(post.Id, currentUserPhone);

                if (ok)
                {
                    // 1. Remove from master list immediately
                    var item = _allSavedPosts.FirstOrDefault(s => s.Post.Id == post.Id);
                    if (item != null) _allSavedPosts.Remove(item);

                    // 2. Rebuild category tabs (counts change)
                    RebuildCategoryTabs();

                    // 3. If the removed post's category is now empty, fall back to "All"
                    bool categoryStillExists = _allSavedPosts.Any(s =>
                        (string.IsNullOrEmpty(s.FolderName) ? "Uncategorized" : s.FolderName) == _currentCategory);
                    if (!categoryStillExists && _currentCategory != "All")
                        _currentCategory = "All";

                    // 4. Refresh the list — works for both "All" and specific category tabs
                    ApplyCategoryFilter();

                    MessagingCenter.Send(this, "PostUnsaved", post.Id);
                    await DisplayAlert("Removed", "Post removed from your bookmarks.", "OK");
                    UpdateVisibleContent();
                }
                else
                {
                    await DisplayAlert("Error", "Could not remove bookmark.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnsavePost error: {ex}");
                await DisplayAlert("Error", "Could not remove bookmark.", "OK");
            }
        }

        private async void OnUnmuteUserClicked(object sender, EventArgs e)
        {
            try
            {
                var mutedUser = (sender as Button)?.CommandParameter as MutedUserInfo;
                if (mutedUser == null) return;

                var confirm = await DisplayAlert("Unmute User",
                    $"Unmute {mutedUser.DisplayName}? They will be able to post in your feed again.",
                    "Unmute", "Cancel");
                if (!confirm) return;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                await MuteUserService.UnmuteUserAsync(mutedUser.PhoneNumber, currentUserPhone);

                _mutedUsers.Remove(mutedUser);
                MessagingCenter.Send(this, "UserUnmuted", mutedUser.PhoneNumber);
                await DisplayAlert("Unmuted", $"You'll now see posts from {mutedUser.DisplayName} again.", "OK");
                UpdateVisibleContent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnUnmuteUserClicked error: {ex}");
                await DisplayAlert("Error", "Could not unmute user.", "OK");
            }
        }
    }

    public class FolderInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class MutedUserInfo
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProfileImagePath { get; set; } = string.Empty;
    }
}