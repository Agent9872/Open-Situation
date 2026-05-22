using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class ExploreGroupsPage : ContentPage
    {
        // ?? State ????????????????????????????????????????????????????????????
        private string _currentTab = "All";
        private List<Group> _allResults = new();
        private string _searchQuery = string.Empty;
        private string _selectedMood = string.Empty;
        private string _currentUserPhone = string.Empty;
        private readonly Dictionary<string, string> _activeFilters = new();

        public ObservableCollection<GroupViewModel> Groups { get; } = new();

        // Tab mapping (like ConversationsPage)
        private Dictionary<Border, string> _tabMap = new();

        public ExploreGroupsPage()
        {
            InitializeComponent();
            BindingContext = this;
            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

            if (GroupsCollectionView != null)
                GroupsCollectionView.ItemsSource = Groups;

            // Build tabs dynamically
            BuildTabs();

            MessagingCenter.Subscribe<object>(this, "GroupsUpdated", (sender) =>
            {
                MainThread.BeginInvokeOnMainThread(async () => await LoadGroupsAsync());
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadGroupsAsync();
        }

        // ?? Dynamic Tab Building (same pattern as ConversationsPage) ??
        private void BuildTabs()
        {
            if (TabsContainer == null) return;
            TabsContainer.Children.Clear();
            _tabMap.Clear();

            // Define tabs with display text and filter type (NO EMOJIS)
            var tabs = new List<(string display, string filter, int? count)>
            {
                ("All", "All", null),
                ("Community", "Community", null),
                ("Dating", "Dating", null),
                ("Mood Room", "MoodRoom", null),
                ("Interest", "Interest", null),
                ("Support", "Support", null),
                ("Events", "Events", null)
            };

            foreach (var tab in tabs)
            {
                var tabBorder = CreateTab(tab.display, tab.filter);
                TabsContainer.Children.Add(tabBorder);
                _tabMap[tabBorder] = tab.filter;
            }

            // Set initial active tab
            UpdateTabVisuals();
        }
        private void UpdateTabVisuals()
        {
            if (TabsContainer == null) return;

            var activeBorderColor = Color.FromArgb("#008080");
            var activeTextColor = Color.FromArgb("#008080");
            var inactiveTextColor = Color.FromArgb("#A0A0A0");
            var inactiveBorderColor = Colors.Transparent;

            foreach (var child in TabsContainer.Children)
            {
                if (child is not Border border) continue;
                if (border.Content is not Grid contentGrid) continue;

                var textLabel = contentGrid.Children.OfType<Label>().FirstOrDefault();
                if (textLabel == null) continue;

                var filterType = border.BindingContext as string ?? string.Empty;
                bool isActive = filterType == _currentTab;

                if (isActive)
                {
                    textLabel.TextColor = activeTextColor;
                    border.Stroke = activeBorderColor;
                    border.BackgroundColor = Colors.Transparent;
                    textLabel.FontAttributes = FontAttributes.Bold;
                }
                else
                {
                    textLabel.TextColor = inactiveTextColor;
                    border.Stroke = inactiveBorderColor;
                    border.BackgroundColor = Colors.Transparent;
                    textLabel.FontAttributes = FontAttributes.None;
                }
            }
        }
        private Border CreateTab(string displayText, string filterType)
        {
            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                ColumnSpacing = 6
            };

            var textLabel = new Label
            {
                Text = displayText,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = Color.FromArgb("#A0A0A0"),
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(textLabel, 0);
            contentGrid.Children.Add(textLabel);

            var border = new Border
            {
                Content = contentGrid,
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(12, 6),
                StrokeThickness = 1.5,
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                HorizontalOptions = LayoutOptions.Start,
                MinimumWidthRequest = 60,
                BindingContext = filterType
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnTabTapped;
            border.GestureRecognizers.Add(tapGesture);

            return border;
        }

        private void OnTabTapped(object sender, EventArgs e)
        {
            if (sender is Border border && border.BindingContext is string filterType)
            {
                _currentTab = filterType;
                UpdateTabVisuals();
                ApplyFilters();
                UpdateCountLabel();
            }
        }


        // ?? Load ?????????????????????????????????????????????????????????????
        private async Task LoadGroupsAsync()
        {
            try
            {
                ShowSkeleton(true);

                await GroupDatabaseService.InitializeAsync();
                var groups = await GroupRepository.GetAllGroupsForExploreAsync(_currentUserPhone);

                _allResults = groups;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ApplyFilters();
                    ShowSkeleton(false);
                    UpdateCountLabel();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExploreGroupsPage.LoadGroupsAsync error: {ex}");
                ShowSkeleton(false);
                ShowEmpty("Something went wrong. Try refreshing.");
            }
        }

        // ?? Filtering ????????????????????????????????????????????????????????
        private void ApplyFilters()
        {
            var filtered = _allResults.AsEnumerable();

            // Tab filter
            if (_currentTab != "All")
            {
                var targetType = _currentTab switch
                {
                    "Community" => GroupType.CommunityCircle,
                    "Dating" => GroupType.SquadDating,
                    "MoodRoom" => GroupType.MoodRoom,
                    "Interest" => GroupType.InterestBased,
                    "Support" => GroupType.SupportCircle,
                    "Events" => GroupType.EventGroup,
                    _ => (GroupType?)null
                };
                if (targetType.HasValue)
                    filtered = filtered.Where(g => g.GroupType == targetType.Value);
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var q = _searchQuery.ToLower();
                filtered = filtered.Where(g =>
                    g.Name.ToLower().Contains(q) ||
                    g.Description.ToLower().Contains(q) ||
                    g.Category.ToLower().Contains(q) ||
                    (g.InterestTags != null && g.InterestTags.Any(t => t.ToLower().Contains(q))));
            }

            // ========== FIXED MOOD FILTER ==========
            // Filter by mood (for Mood Room groups)
            if (!string.IsNullOrEmpty(_selectedMood))
            {
                filtered = filtered.Where(g =>
                    g.GroupType == GroupType.MoodRoom &&
                    !string.IsNullOrEmpty(g.MoodFilter) &&
                    g.MoodFilter.Equals(_selectedMood, StringComparison.OrdinalIgnoreCase));
                Debug.WriteLine($"Filtering by mood: {_selectedMood}, found {filtered.Count()} groups");
            }

            // Sort
            var list = filtered.OrderByDescending(g => g.LastActiveAt).ToList();

            Groups.Clear();
            foreach (var g in list)
                Groups.Add(new GroupViewModel(g, _currentUserPhone, this));

            bool hasResults = Groups.Any();
            if (GroupsCollectionView != null) GroupsCollectionView.IsVisible = hasResults;
            if (NoGroupsLayout != null) NoGroupsLayout.IsVisible = !hasResults;

            UpdateCountLabel();
        }
        private void UpdateCountLabel()
        {
            if (GroupCountLabel != null)
            {
                GroupCountLabel.Text = Groups.Any()
                    ? $"{Groups.Count} group{(Groups.Count == 1 ? "" : "s")} found"
                    : "No groups for these filters";
            }
        }

        // ?? Search ???????????????????????????????????????????????????????????
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = e.NewTextValue ?? string.Empty;
            if (ClearSearchButton != null)
                ClearSearchButton.IsVisible = !string.IsNullOrEmpty(_searchQuery);

            if (!string.IsNullOrEmpty(_searchQuery))
                UpsertChip("Search", $"\"{_searchQuery}\"");
            else
                RemoveChip("Search");

            ApplyFilters();
            UpdateCountLabel();
        }

        private void OnClearSearch(object sender, EventArgs e)
        {
            if (SearchEntry != null) SearchEntry.Text = string.Empty;
            _searchQuery = string.Empty;
            if (ClearSearchButton != null) ClearSearchButton.IsVisible = false;
            RemoveChip("Search");
            ApplyFilters();
            UpdateCountLabel();
        }

        // ?? Mood filter ??????????????????????????????????????????????????????
        private void OnMoodFilterSelected(object sender, EventArgs e)
        {
            if (MoodFilterPicker == null || MoodFilterPicker.SelectedIndex < 0) return;
            var sel = MoodFilterPicker.Items[MoodFilterPicker.SelectedIndex];

            if (sel == "All Moods" || string.IsNullOrEmpty(sel))
            {
                _selectedMood = string.Empty;
                RemoveChip("Mood");
            }
            else
            {
                _selectedMood = sel;
                UpsertChip("Mood", sel);
            }

            ApplyFilters();
            UpdateCountLabel();
        }


        // ?? Filter chips ?????????????????????????????????????????????????????
        private void UpsertChip(string filterType, string displayValue)
        {
            _activeFilters[filterType] = displayValue;
            RebuildChips();
        }

        private void RemoveChip(string filterType)
        {
            _activeFilters.Remove(filterType);
            RebuildChips();
        }

        private void RebuildChips()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ActiveFiltersLayout == null || ActiveFiltersScrollView == null) return;

                ActiveFiltersLayout.Children.Clear();
                ActiveFiltersScrollView.IsVisible = _activeFilters.Any();

                foreach (var kv in _activeFilters)
                {
                    var capturedType = kv.Key;

                    var chipBorder = new Border
                    {
                        BackgroundColor = Color.FromArgb("#2A2A2A"),
                        StrokeThickness = 2,
                        Stroke = Color.FromArgb("#008080"),
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Padding = new Thickness(12, 6, 8, 6),
                        VerticalOptions = LayoutOptions.Center,
                        HeightRequest = 32
                    };

                    var grid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        VerticalOptions = LayoutOptions.Center
                    };

                    var label = new Label
                    {
                        Text = kv.Value,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#F0F0F0"),
                        VerticalOptions = LayoutOptions.Center,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    var closeFrame = new Frame
                    {
                        Content = new Label
                        {
                            Text = "X",
                            FontSize = 11,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        },
                        BackgroundColor = Color.FromArgb("#FF3B6F"),
                        CornerRadius = 10,
                        HasShadow = false,
                        Padding = 0,
                        WidthRequest = 20,
                        HeightRequest = 20,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        IsClippedToBounds = true
                    };

                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (_, _) => ClearFilterByType(capturedType);
                    closeFrame.GestureRecognizers.Add(tap);

                    Grid.SetColumn(closeFrame, 1);
                    grid.Children.Add(closeFrame);

                    chipBorder.Content = grid;
                    chipBorder.Margin = new Thickness(0, 0, 8, 0);
                    ActiveFiltersLayout.Children.Add(chipBorder);
                }
            });
        }

        private void ClearFilterByType(string filterType)
        {
            switch (filterType)
            {
                case "Search":
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (SearchEntry != null) SearchEntry.Text = string.Empty;
                        _searchQuery = string.Empty;
                        if (ClearSearchButton != null) ClearSearchButton.IsVisible = false;
                    });
                    break;

                case "Mood":
                    _selectedMood = string.Empty;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (MoodFilterPicker != null) MoodFilterPicker.SelectedIndex = 0;
                    });
                    break;
            }

            RemoveChip(filterType);
            ApplyFilters();
            UpdateCountLabel();
        }

        // ?? Refresh ??????????????????????????????????????????????????????????
        private async void OnRefreshTapped(object sender, TappedEventArgs e)
            => await LoadGroupsAsync();

        // ?? Group tap / join ?????????????????????????????????????????????????
        private async void OnGroupCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not GroupViewModel groupVm) return;
            await OnGroupCardTappedAsync(groupVm);
        }

        private async void OnGroupSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not GroupViewModel vm) return;
            GroupsCollectionView.SelectedItem = null;
            await OnGroupCardTappedAsync(vm);
        }

        private void RefreshGroupItem(GroupViewModel groupVm)
        {
            var index = Groups.IndexOf(groupVm);
            if (index < 0) return;                // item not found — nothing to do

            // Replacing the item at the same index raises CollectionChanged(Replace)
            // which forces CollectionView to rebuild that specific cell from scratch.
            Groups[index] = groupVm;
        }

        private async Task OnGroupCardTappedAsync(GroupViewModel groupVm)
        {
            // Already a member ? open the chat directly
            if (groupVm.IsMember)
            {
                await OpenGroupChat(groupVm.Id);
                return;
            }

            // Pending ? offer to cancel the request
            if (groupVm.IsPendingJoin)
            {
                await CancelJoinRequestAsync(groupVm);
                return;
            }

            // Not a member ? show join dialog
            bool join = await DisplayAlert(
                $"Join {groupVm.Name}?",
                $"{groupVm.GroupTypeIcon} {groupVm.GroupTypeDisplay}\n" +
                $"{groupVm.MemberCountDisplay}\n\n{groupVm.Description}",
                "Join Group", "Cancel");

            if (join)
                await JoinGroupAsync(groupVm);
        }
        private async void OnJoinButtonClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;

            // Walk up the visual tree to find the GroupViewModel binding context
            GroupViewModel? groupVm = null;

            if (btn.BindingContext is GroupViewModel vm)
                groupVm = vm;
            else if (btn.Parent?.BindingContext is GroupViewModel vm2)
                groupVm = vm2;
            else if (btn.Parent?.Parent?.BindingContext is GroupViewModel vm3)
                groupVm = vm3;

            if (groupVm == null) return;

            if (groupVm.IsPendingJoin)
                await CancelJoinRequestAsync(groupVm);
            else if (!groupVm.IsMember)
                await JoinGroupAsync(groupVm);
        }

        private async Task JoinGroupAsync(GroupViewModel groupVm)
        {
            try
            {
                ShowSkeleton(true);

                var (success, message) = await GroupRepository.JoinGroupAsync(
                    groupVm.Id, _currentUserPhone);

                if (success)
                {
                    bool isPending = message.Contains(
                        "waiting for admin approval", StringComparison.OrdinalIgnoreCase);

                    if (isPending)
                    {
                        // Reload to pull the new PendingJoinRequestId from the DB,
                        // then immediately refresh the cell.
                        await LoadGroupsAsync();

                        await DisplayAlert(
                            "Request Sent",
                            "Your join request has been sent.\nTap \"Pending…\" anytime to cancel it.",
                            "OK");
                    }
                    else
                    {
                        // Immediate join — update VM, refresh cell, then open chat
                        groupVm.IsMember = true;
                        MainThread.BeginInvokeOnMainThread(() => RefreshGroupItem(groupVm));

                        await DisplayAlert("You're in! ??", message, "Open Group");
                        await OpenGroupChat(groupVm.Id);

                        // Reload in background so member count updates
                        _ = LoadGroupsAsync();
                    }
                }
                else
                {
                    await DisplayAlert("Cannot Join", message, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JoinGroupAsync error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to join: {ex.Message}", "OK");
            }
            finally
            {
                ShowSkeleton(false);
            }
        }


        private async Task CancelJoinRequestAsync(GroupViewModel groupVm)
        {
            if (string.IsNullOrEmpty(groupVm.PendingJoinRequestId))
            {
                await DisplayAlert("Nothing to Cancel",
                    "No pending request found for this group.", "OK");
                return;
            }

            bool confirm = await DisplayAlert(
                "Cancel Request?",
                $"Withdraw your join request for \"{groupVm.Name}\"?\nYou can re-apply at any time.",
                "Yes, Cancel Request",
                "Keep Waiting");

            if (!confirm) return;

            try
            {
                // Show a quick activity indicator without hiding the list
                // (ShowSkeleton hides the whole list — too jarring for a single item update)
                var (success, message) = await GroupRepository.CancelJoinRequestAsync(
                    groupVm.PendingJoinRequestId, _currentUserPhone);

                if (success)
                {
                    // 1. Mutate the VM
                    groupVm.IsPendingJoin = false;
                    groupVm.PendingJoinRequestId = string.Empty;

                    // 2. Force CollectionView to redraw this cell immediately
                    MainThread.BeginInvokeOnMainThread(() => RefreshGroupItem(groupVm));

                    await DisplayAlert("Request Cancelled",
                        "Your join request has been withdrawn.", "OK");
                }
                else
                {
                    await DisplayAlert("Could Not Cancel", message, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CancelJoinRequestAsync error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to cancel: {ex.Message}", "OK");
            }
        }


        private async Task OpenGroupChat(string groupId)
        {
            var chatPage = new GroupChatPage();
            chatPage.GroupId = groupId;
            await Navigation.PushAsync(chatPage);
        }

        // ?? Navigation ???????????????????????????????????????????????????????
        private async void OnCreateGroupClicked(object sender, EventArgs e)
            => await Navigation.PushModalAsync(new CreateGroupPage());

        private async void OnCreateGroupTapped(object sender, TappedEventArgs e)
            => await Navigation.PushModalAsync(new CreateGroupPage());

        private async void OnBackClicked(object sender, EventArgs e)
            => await Navigation.PopAsync();

        // ?? Image preview ????????????????????????????????????????????????????
        private async void OnGroupImageTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not GroupViewModel vm) return;
            if (string.IsNullOrEmpty(vm.CoverImagePath)) return;

            var tapClose = new TapGestureRecognizer();
            tapClose.Tapped += async (s, ev) => await Navigation.PopModalAsync();

            var image = new Image
            {
                Source = vm.CoverImageSource,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            image.GestureRecognizers.Add(tapClose);

            await Navigation.PushModalAsync(new ContentPage
            {
                BackgroundColor = Colors.Black,
                Content = image
            });
        }

        // ?? Skeleton / empty helpers ?????????????????????????????????????????
        private void ShowSkeleton(bool show)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (SkeletonView != null) SkeletonView.IsVisible = show;
                if (show)
                {
                    if (GroupsCollectionView != null) GroupsCollectionView.IsVisible = false;
                    if (NoGroupsLayout != null) NoGroupsLayout.IsVisible = false;
                }
            });
        }

        private void ShowEmpty(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (SkeletonView != null) SkeletonView.IsVisible = false;
                if (GroupsCollectionView != null) GroupsCollectionView.IsVisible = false;
                if (NoGroupsLayout != null) NoGroupsLayout.IsVisible = true;
                if (EmptySubLabel != null) EmptySubLabel.Text = message;
                if (GroupCountLabel != null) GroupCountLabel.Text = "No groups found";
            });
        }

        public async Task RefreshGroupsAsync() => await LoadGroupsAsync();
    }

    // ?? ViewModel ????????????????????????????????????????????????????????????
    public class GroupViewModel : BindableObject
    {
        private readonly Group _group;
        private readonly string _currentUserPhone;
        private readonly ExploreGroupsPage _parentPage;

        // backing fields for mutable state
        private bool _isMember;
        private bool _isPendingJoin;
        private string _pendingJoinRequestId;

        public GroupViewModel(Group group, string currentUserPhone, ExploreGroupsPage parentPage)
        {
            _group = group;
            _currentUserPhone = currentUserPhone;
            _parentPage = parentPage;
            _isMember = group.IsMember;
            _isPendingJoin = group.IsPendingJoin;
            _pendingJoinRequestId = group.PendingJoinRequestId;
        }

        // ?? Immutable props ???????????????????????????????????????????????????
        public string Id => _group.Id;
        public string Name => _group.Name;
        public string Description => string.IsNullOrEmpty(_group.Description)
                                                     ? "No description"
                                                     : _group.Description;
        public bool HasDescription => !string.IsNullOrEmpty(_group.Description);
        public string CoverImagePath => _group.CoverImagePath;
        public string Category => _group.Category;
        public List<string> InterestTags => _group.InterestTags ?? new List<string>();
        public bool HasTags => InterestTags.Any();
        public int MemberCount => _group.MemberCount;
        public string MemberCountDisplay => $"{MemberCount} member{(MemberCount != 1 ? "s" : "")}";
        public string GroupTypeDisplay => _group.GroupTypeDisplay;
        public string GroupTypeIcon => _group.GroupTypeIcon;

        public ImageSource CoverImageSource =>
            string.IsNullOrWhiteSpace(_group.CoverImagePath) || !File.Exists(_group.CoverImagePath)
                ? ImageSource.FromFile("group_placeholder.png")
                : ImageSource.FromFile(_group.CoverImagePath);

        // ?? Mutable membership state ??????????????????????????????????????????

        /// <summary>True once the user is a confirmed member.</summary>
        public bool IsMember
        {
            get => _isMember;
            set
            {
                _isMember = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(JoinButtonText));
                OnPropertyChanged(nameof(JoinButtonColor));
                OnPropertyChanged(nameof(IsJoinable));
                OnPropertyChanged(nameof(IsPendingJoin));
            }
        }

        /// <summary>True while a join request is awaiting admin approval.</summary>
        public bool IsPendingJoin
        {
            get => _isPendingJoin;
            set
            {
                _isPendingJoin = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(JoinButtonText));
                OnPropertyChanged(nameof(JoinButtonColor));
                OnPropertyChanged(nameof(IsJoinable));
            }
        }

        /// <summary>
        /// The GroupJoinRequest.Id for the pending request.
        /// Set when IsPendingJoin = true; cleared when cancelled or approved.
        /// </summary>
        public string PendingJoinRequestId
        {
            get => _pendingJoinRequestId;
            set
            {
                _pendingJoinRequestId = value;
                OnPropertyChanged();
            }
        }

        // ?? Join button computed props ?????????????????????????????????????????

        /// <summary>
        /// "Joined ?"  ? confirmed member  (grey, inert)
        /// "Pending…"  ? awaiting approval (amber, tap = cancel dialog)
        /// "Join"      ? not a member yet  (teal, tap = send request)
        /// </summary>
        public string JoinButtonText =>
     IsMember ? "Joined" :
     IsPendingJoin ? "Pending…" :
                     "Join";

        public Color JoinButtonColor =>
            IsMember ? Color.FromArgb("#2A2A2A") :  // grey  — joined
            IsPendingJoin ? Color.FromArgb("#5C4A00") :  // amber — pending
                            Color.FromArgb("#008080");   // teal  — joinable

        /// <summary>False for joined/pending — disables the button at the XAML level.</summary>
        public bool IsJoinable => !IsMember && !IsPendingJoin;
    }
}