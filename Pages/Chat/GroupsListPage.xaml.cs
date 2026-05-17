using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class GroupsListPage : ContentPage
    {
        private string _currentUserPhone = string.Empty;
        private ObservableCollection<Group> _myGroups = new();
        private ObservableCollection<Group> _publicGroups = new();
        private bool _showingMyGroups = true;

        public GroupsListPage()
        {
            InitializeComponent();
            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadGroupsAsync();
        }

        private async Task LoadGroupsAsync()
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();

                var myGroups = await GroupRepository.GetMyGroupsAsync(_currentUserPhone);
                _myGroups.Clear();
                foreach (var g in myGroups) _myGroups.Add(g);

                var publicGroups = await GroupRepository.GetPublicGroupsAsync(_currentUserPhone);
                _publicGroups.Clear();
                foreach (var g in publicGroups) _publicGroups.Add(g);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MyGroupsCollectionView.ItemsSource = _myGroups;
                    DiscoverGroupsCollectionView.ItemsSource = _publicGroups;
                    MyGroupsEmpty.IsVisible = !_myGroups.Any();
                    DiscoverGroupsEmpty.IsVisible = !_publicGroups.Any();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGroups error: {ex}");
            }
        }

        private void OnMyGroupsTabTapped(object sender, EventArgs e)
        {
            _showingMyGroups = true;
            MyGroupsCollectionView.IsVisible = true;
            DiscoverGroupsCollectionView.IsVisible = false;
            MyGroupsSection.IsVisible = true;
            DiscoverSection.IsVisible = false;
            MyGroupsTabLine.IsVisible = true;
            DiscoverTabLine.IsVisible = false;
            MyGroupsTabLabel.TextColor = Color.FromArgb("#008080");
            DiscoverTabLabel.TextColor = Color.FromArgb("#666666");
        }

        private void OnDiscoverTabTapped(object sender, EventArgs e)
        {
            _showingMyGroups = false;
            MyGroupsCollectionView.IsVisible = false;
            DiscoverGroupsCollectionView.IsVisible = true;
            MyGroupsSection.IsVisible = false;
            DiscoverSection.IsVisible = true;
            MyGroupsTabLine.IsVisible = false;
            DiscoverTabLine.IsVisible = true;
            MyGroupsTabLabel.TextColor = Color.FromArgb("#666666");
            DiscoverTabLabel.TextColor = Color.FromArgb("#008080");
        }

        private async void OnCreateGroupTapped(object sender, EventArgs e)
        {
            var page = new CreateGroupPage();
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async void OnGroupTapped(object sender, EventArgs e)
        {
            try
            {
                if (sender is VisualElement ve && ve.BindingContext is Group group)
                {
                    var chatPage = new GroupChatPage();
                    chatPage.GroupId = group.Id;
                    await Navigation.PushAsync(chatPage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnGroupTapped error: {ex}");
            }
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = e.NewTextValue?.Trim() ?? string.Empty;
                var results = await GroupRepository.GetPublicGroupsAsync(_currentUserPhone, searchQuery: query);

                _publicGroups.Clear();
                foreach (var g in results) _publicGroups.Add(g);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Search error: {ex}");
            }
        }

        private async void OnJoinByCodeTapped(object sender, EventArgs e)
        {
            var code = await DisplayPromptAsync(
                "Join by Code",
                "Enter the invite code:",
                maxLength: 10,
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(code)) return;

            var (success, message, group) = await GroupRepository
                .JoinByInviteCodeAsync(code.Trim().ToUpper(), _currentUserPhone);

            if (success && group != null)
            {
                await DisplayAlert("Joined!", $"You joined '{group.Name}'", "OK");
                await LoadGroupsAsync();

                var chatPage = new GroupChatPage();
                chatPage.GroupId = group.Id;
                await Navigation.PushAsync(chatPage);
            }
            else
            {
                await DisplayAlert("Error", message, "OK");
            }
        }
    }
}