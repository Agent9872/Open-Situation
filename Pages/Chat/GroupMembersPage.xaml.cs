using Lock.Chat.Services;
using Lock.Converter.Chat;
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
    public partial class GroupMembersPage : ContentPage
    {
        // ?? State ??????????????????????????????????????????????????????????????
        private readonly string _groupId;
        private readonly string _currentUserPhone;
        private bool _isAdmin = false;
        private string _currentTab = "Members"; // "Members" | "Pending"

        // Members tab
        private ObservableCollection<ExtendedGroupMember> _members = new();
        private List<ExtendedGroupMember> _allMembers = new();

        // Pending tab
        private ObservableCollection<PendingRequestViewModel> _pendingRequests = new();
        private List<PendingRequestViewModel> _allPendingRequests = new();

        // Tab border references for visual update
        private readonly Dictionary<Border, string> _tabMap = new();

        public GroupMembersPage(string groupId, string currentUserPhone)
        {
            InitializeComponent();
            _groupId = groupId;
            _currentUserPhone = currentUserPhone;
            Shell.SetNavBarIsVisible(this, false);

            MembersCollectionView.ItemsSource = _members;
            PendingCollectionView.ItemsSource = _pendingRequests;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Check admin status first so we know whether to show the Pending tab
            var currentMember = await GroupRepository.GetMemberAsync(_groupId, _currentUserPhone);
            _isAdmin = currentMember?.IsPrivileged ?? false;

            BuildTabs();
            await LoadAllAsync();
        }

        // ??????????????????????????????????????????????????????????????????????
        // TAB BUILDING
        // ??????????????????????????????????????????????????????????????????????

        private void BuildTabs()
        {
            if (TabsContainer == null) return;
            TabsContainer.Children.Clear();
            _tabMap.Clear();

            var tabs = new List<(string display, string key)>
            {
                ("Members", "Members")
            };

            // Only admins / creators see the Pending tab
            if (_isAdmin)
                tabs.Add(("Requests", "Pending"));

            foreach (var (display, key) in tabs)
            {
                var border = CreateTab(display, key);
                TabsContainer.Children.Add(border);
                _tabMap[border] = key;
            }

            UpdateTabVisuals();
        }

        private Border CreateTab(string displayText, string key)
        {
            var label = new Label
            {
                Text = displayText,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#A0A0A0"),
                VerticalOptions = LayoutOptions.Center
            };

            // Badge label — only shown on the Pending tab when count > 0
            var badge = new Border
            {
                BackgroundColor = Color.FromArgb("#E8593C"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(6, 2),
                IsVisible = false,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var grid = new Grid
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                ColumnSpacing = 5,
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Auto }
        }
            };
            grid.Add(label, 0, 0);
            grid.Add(badge, 1, 0);

            var border = new Border
            {
                Content = grid,
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(14, 6),
                StrokeThickness = 1.5,
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                HorizontalOptions = LayoutOptions.Start,
                MinimumWidthRequest = 70,
                BindingContext = key
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnTabTapped;
            border.GestureRecognizers.Add(tap);

            return border;
        }
        private void UpdateTabVisuals()
        {
            foreach (var child in TabsContainer.Children)
            {
                if (child is not Border border) continue;
                var label = (border.Content as Grid)?.Children.OfType<Label>().FirstOrDefault();
                if (label == null) continue;

                bool isActive = (border.BindingContext as string) == _currentTab;
                label.TextColor = isActive ? Color.FromArgb("#008080") : Color.FromArgb("#A0A0A0");
                border.Stroke = isActive ? Color.FromArgb("#008080") : Colors.Transparent;
                label.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
            }
        }

        private void OnTabTapped(object sender, EventArgs e)
        {
            if (sender is Border border && border.BindingContext is string key)
            {
                _currentTab = key;
                UpdateTabVisuals();
                SwitchTab();
            }
        }

        private void SwitchTab()
        {
            bool showMembers = _currentTab == "Members";
            bool showPending = _currentTab == "Pending";

            MembersCollectionView.IsVisible = showMembers;
            EmptyState.IsVisible = showMembers && _members.Count == 0;

            PendingCollectionView.IsVisible = showPending;
            EmptyPendingState.IsVisible = showPending && _pendingRequests.Count == 0;

            // Update the count label
            if (showMembers)
                MemberCountLabel.Text = $"{_members.Count} member{(_members.Count == 1 ? "" : "s")}";
            else
                MemberCountLabel.Text = $"{_pendingRequests.Count} pending request{(_pendingRequests.Count == 1 ? "" : "s")}";

            // Hide search bar on pending tab (not useful there)
            // Keep it visible on members tab
        }

        // ??????????????????????????????????????????????????????????????????????
        // LOAD
        // ??????????????????????????????????????????????????????????????????????

        private async Task LoadAllAsync()
        {
            await Task.WhenAll(LoadMembersAsync(), LoadPendingRequestsAsync());
            SwitchTab();
        }

        private async Task LoadMembersAsync()
        {
            try
            {
                var members = await GroupRepository.GetMembersAsync(_groupId);

                _allMembers = new List<ExtendedGroupMember>();

                foreach (var member in members.OrderBy(m => (int)m.Role).ThenBy(m => m.UserName))
                {
                    var ext = await CreateExtendedMemberAsync(member);
                    _allMembers.Add(ext);
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _members.Clear();
                    foreach (var m in _allMembers)
                        _members.Add(m);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadMembersAsync error: {ex}");
            }
        }

        private async Task LoadPendingRequestsAsync()
        {
            if (!_isAdmin) return;

            try
            {
                var requests = await GroupRepository.GetPendingJoinRequestsAsync(_groupId);

                var enriched = new List<PendingRequestViewModel>();
                foreach (var r in requests)
                {
                    var vm = new PendingRequestViewModel(r);
                    await EnrichPendingRequestAsync(vm);
                    enriched.Add(vm);
                }

                _allPendingRequests = enriched;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _pendingRequests.Clear();
                    foreach (var r in _allPendingRequests)
                        _pendingRequests.Add(r);

                    UpdatePendingTabBadge(); // <-- add this line
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPendingRequestsAsync error: {ex}");
            }
        }

        private async Task EnrichPendingRequestAsync(PendingRequestViewModel vm)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(vm.UserPhone)}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    vm.UserBio = user.Bio ?? string.Empty;
                    vm.UserMood = user.Mood ?? string.Empty;

                    var span = DateTime.UtcNow - user.LastActive;
                    if (span.TotalMinutes < 5)
                    {
                        vm.OnlineStatusText = "Online";
                        vm.OnlineStatusColor = "#10B981";
                    }
                    else if (span.TotalHours < 24)
                    {
                        vm.OnlineStatusText = $"Active {(int)span.TotalHours}h ago";
                        vm.OnlineStatusColor = "#888888";
                    }
                    else
                    {
                        vm.OnlineStatusText = $"Active {(int)span.TotalDays}d ago";
                        vm.OnlineStatusColor = "#555555";
                    }
                }

                vm.CompatibilityWithCurrentUser =
                    await CalculateCompatibilityAsync(vm.UserPhone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnrichPendingRequest error: {ex}");
            }
        }



        // ??????????????????????????????????????????????????????????????????????
        // SEARCH  (Members tab only)
        // ??????????????????????????????????????????????????????????????????????

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var q = e.NewTextValue?.Trim() ?? string.Empty;
            if (ClearSearchButton != null)
                ClearSearchButton.IsVisible = !string.IsNullOrEmpty(q);

            var filtered = string.IsNullOrEmpty(q)
                ? _allMembers
                : _allMembers.Where(m =>
                    m.DisplayName.ToLower().Contains(q.ToLower()) ||
                    (m.UserMood?.ToLower().Contains(q.ToLower()) ?? false)).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _members.Clear();
                foreach (var m in filtered)
                    _members.Add(m);

                MemberCountLabel.Text = $"{_members.Count} member{(_members.Count == 1 ? "" : "s")}";
                EmptyState.IsVisible = _members.Count == 0 && _currentTab == "Members";
            });
        }

        private void OnClearSearchTapped(object sender, EventArgs e)
        {
            if (SearchEntry != null)
            {
                SearchEntry.Text = string.Empty;
                SearchEntry.Focus();
            }
        }

        // ??????????????????????????????????????????????????????????????????????
        // MEMBER TAPPED  (view profile / promote)
        // ??????????????????????????????????????????????????????????????????????

        private async void OnMemberTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not ExtendedGroupMember member) return;

                bool isSelf = member.UserPhone == _currentUserPhone;
                bool targetIsCreator = member.Role == GroupMemberRole.Creator;
                bool canPromote = _isAdmin && !isSelf && !targetIsCreator &&
                                      member.Role == GroupMemberRole.Member;

                var options = new List<string> { "View Profile" };
                if (canPromote) options.Add("Promote to Admin");

                var action = await DisplayActionSheet(
                    member.DisplayName, "Cancel", null, options.ToArray());

                if (action == "View Profile")
                {
                    var profilePage = new Lock.Pages.Profile.ProfilePage();
                    profilePage.Phone = member.UserPhone;
                    profilePage.ViewOnlyString = "true";
                    await Navigation.PushAsync(profilePage);
                }
                else if (action == "Promote to Admin")
                {
                    bool confirm = await DisplayAlert(
                        "Promote to Admin",
                        $"Make {member.DisplayName} an admin?",
                        "Promote", "Cancel");

                    if (!confirm) return;

                    bool ok = await GroupRepository.PromoteMemberAsync(
                        _groupId, _currentUserPhone,
                        member.UserPhone, GroupMemberRole.Admin);

                    await DisplayAlert(ok ? "Done" : "Error",
                        ok ? $"{member.DisplayName} is now an admin."
                           : "Could not promote member.", "OK");

                    if (ok) await LoadMembersAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnMemberTapped error: {ex}");
                await DisplayAlert("Error", "Could not perform action.", "OK");
            }
        }

        // ??????????????????????????????????????????????????????????????????????
        // REMOVE MEMBER  (inline Remove button on each card)
        // ??????????????????????????????????????????????????????????????????????

        private async void OnRemoveMemberTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not ExtendedGroupMember member) return;

                bool confirm = await DisplayAlert(
                    "Remove Member",
                    $"Remove {member.DisplayName} from the group? They will need to request again to rejoin.",
                    "Remove", "Cancel");

                if (!confirm) return;

                bool ok = await GroupRepository.RemoveMemberAsync(
                    _groupId, _currentUserPhone, member.UserPhone);

                if (ok)
                {
                    // Remove from local list immediately — no full reload needed
                    var item = _members.FirstOrDefault(m => m.UserPhone == member.UserPhone);
                    if (item != null)
                    {
                        _members.Remove(item);
                        _allMembers.Remove(item);
                    }

                    MemberCountLabel.Text =
                        $"{_members.Count} member{(_members.Count == 1 ? "" : "s")}";
                    EmptyState.IsVisible = _members.Count == 0;

                    await DisplayAlert("Removed", $"{member.DisplayName} has been removed.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Could not remove member.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnRemoveMemberTapped error: {ex}");
                await DisplayAlert("Error", "Could not remove member.", "OK");
            }
        }

        // ??????????????????????????????????????????????????????????????????????
        // APPROVE / REJECT PENDING REQUEST
        // ??????????????????????????????????????????????????????????????????????

        private async void OnApproveRequestTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PendingRequestViewModel req) return;

                bool confirm = await DisplayAlert(
                    "Approve Request",
                    $"Let {req.UserName} join the group?",
                    "Approve", "Cancel");

                if (!confirm) return;

                bool ok = await GroupRepository.ApproveJoinRequestAsync(
                    req.Id, _currentUserPhone);

                if (ok)
                {
                    // Reload members FIRST so the new member appears in the list
                    await LoadMembersAsync();

                    // Then remove from pending UI
                    RemovePendingItem(req);

                    await DisplayAlert("Approved", $"{req.UserName} is now a member.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Could not approve request.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnApproveRequestTapped error: {ex}");
                await DisplayAlert("Error", "Could not approve request.", "OK");
            }
        }
        private async void OnRejectRequestTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not PendingRequestViewModel req) return;

                bool confirm = await DisplayAlert(
                    "Reject Request",
                    $"Decline {req.UserName}'s request to join?",
                    "Reject", "Cancel");

                if (!confirm) return;

                bool ok = await GroupRepository.RejectJoinRequestAsync(
                    req.Id, _currentUserPhone);

                if (ok)
                {
                    RemovePendingItem(req);
                    await DisplayAlert("Rejected", $"{req.UserName}'s request has been declined.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Could not reject request.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnRejectRequestTapped error: {ex}");
                await DisplayAlert("Error", "Could not reject request.", "OK");
            }
        }

        /// <summary>Removes a pending request from both lists and updates the UI count.</summary>
        private void RemovePendingItem(PendingRequestViewModel req)
        {
            var item = _pendingRequests.FirstOrDefault(r => r.Id == req.Id);
            if (item != null)
            {
                _pendingRequests.Remove(item);
                _allPendingRequests.Remove(item);
            }

            MemberCountLabel.Text =
                $"{_pendingRequests.Count} pending request{(_pendingRequests.Count == 1 ? "" : "s")}";
            EmptyPendingState.IsVisible = _pendingRequests.Count == 0;

            // Update the Requests tab badge if visible
            UpdatePendingTabBadge();
        }

        /// <summary>Appends a request count badge to the Requests tab label.</summary>
        private void UpdatePendingTabBadge()
        {
            foreach (var child in TabsContainer.Children)
            {
                if (child is not Border border) continue;
                if ((border.BindingContext as string) != "Pending") continue;
                if (border.Content is not Grid grid) continue;

                // The badge is the second column child — a Border wrapping a Label
                var badgeBorder = grid.Children
                    .OfType<Border>()
                    .FirstOrDefault();

                if (badgeBorder == null) break;

                bool hasPending = _pendingRequests.Count > 0;
                badgeBorder.IsVisible = hasPending;

                if (hasPending && badgeBorder.Content is Label badgeLabel)
                    badgeLabel.Text = _pendingRequests.Count.ToString();

                break;
            }
        }
        // ??????????????????????????????????????????????????????????????????????
        // EXTENDED MEMBER CREATION
        // ??????????????????????????????????????????????????????????????????????

        private async Task<ExtendedGroupMember> CreateExtendedMemberAsync(GroupMember member)
        {
            var extended = new ExtendedGroupMember
            {
                Id = member.Id,
                GroupId = member.GroupId,
                UserPhone = member.UserPhone,
                UserName = member.UserName,
                UserProfileImagePath = member.UserProfileImagePath,
                AnonymousAlias = member.AnonymousAlias,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                IsAnonymous = member.IsAnonymous,
                IsMuted = member.IsMuted,

                // Admins can remove anyone who is not the creator and not themselves
                CanBeRemoved = _isAdmin &&
                               member.UserPhone != _currentUserPhone &&
                               member.Role != GroupMemberRole.Creator
            };

            var users = await SupabaseService.GetAsync<User>("Users",
                $"PhoneNumber=eq.{Uri.EscapeDataString(member.UserPhone)}&limit=1");
            var user = users.FirstOrDefault();

            if (user != null)
            {
                extended.UserBio = user.Bio ?? string.Empty;
                extended.UserMood = user.Mood ?? string.Empty;
                if (!string.IsNullOrEmpty(user.ProfileImagePath))
                    extended.UserProfileImagePath = user.ProfileImagePath;
                extended.JoinDate = user.JoinDate;
                extended.LastActive = user.LastActive;

                var span = DateTime.UtcNow - user.LastActive;
                if (span.TotalMinutes < 5)
                {
                    extended.OnlineStatusText = "Online";
                    extended.OnlineStatusColor = "#10B981";
                }
                else if (span.TotalHours < 24)
                {
                    extended.OnlineStatusText = $"Active {span.TotalHours:F0}h ago";
                    extended.OnlineStatusColor = "#888888";
                }
                else
                {
                    extended.OnlineStatusText = $"Active {span.TotalDays:F0}d ago";
                    extended.OnlineStatusColor = "#555555";
                }
            }

            extended.CompatibilityWithCurrentUser = member.UserPhone == _currentUserPhone
                ? "You"
                : await CalculateCompatibilityAsync(member.UserPhone);

            return extended;
        }

        private async Task<string> CalculateCompatibilityAsync(string targetPhone)
        {
            try
            {
                var meUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(_currentUserPhone)}&limit=1");
                var me = meUsers.FirstOrDefault();

                var targetUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(targetPhone)}&limit=1");
                var target = targetUsers.FirstOrDefault();

                if (me == null || target == null) return "0%";

                var score = await CompatibilityService.CalculateCompatibilityScoreAsync(me, target);
                return $"{score}%";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalculateCompatibility error: {ex}");
                return "0%";
            }
        }

        // ??????????????????????????????????????????????????????????????????????
        // NAVIGATION
        // ??????????????????????????????????????????????????????????????????????

        private async void OnBackTapped(object sender, EventArgs e)
            => await Navigation.PopAsync();
    }

    // ??????????????????????????????????????????????????????????????????????????
    // EXTENDED GROUP MEMBER  (Members tab VM)
    // ??????????????????????????????????????????????????????????????????????????

    public class ExtendedGroupMember : GroupMember
    {
        public string UserBio { get; set; } = string.Empty;
        public string UserMood { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
        public DateTime LastActive { get; set; }
        public string OnlineStatusText { get; set; } = "Offline";
        public string OnlineStatusColor { get; set; } = "#888888";
        public string CompatibilityWithCurrentUser { get; set; } = "0%";

        /// <summary>
        /// True when the current user is an admin and this member can be kicked.
        /// Set in CreateExtendedMemberAsync.
        /// </summary>
        public bool CanBeRemoved { get; set; } = false;

        public bool HasMood => !string.IsNullOrEmpty(UserMood);
        public bool HasBio => !string.IsNullOrEmpty(UserBio);

        public string UserBioPreview => HasBio
            ? (UserBio.Length > 50 ? UserBio[..47] + "..." : UserBio)
            : string.Empty;

        public new string RoleDisplay => Role switch
        {
            GroupMemberRole.Creator => "Creator",
            GroupMemberRole.Admin => "Admin",
            GroupMemberRole.Moderator => "Mod",
            _ => string.Empty
        };

        public new bool IsPrivileged =>
            Role is GroupMemberRole.Creator or GroupMemberRole.Admin or GroupMemberRole.Moderator;

        public new string RoleBadgeColor => Role switch
        {
            GroupMemberRole.Creator => "#D4AF37",
            GroupMemberRole.Admin => "#008080",
            GroupMemberRole.Moderator => "#7F77DD",
            _ => "Transparent"
        };

        public new string DisplayName =>
            IsAnonymous && !string.IsNullOrEmpty(AnonymousAlias) ? AnonymousAlias : UserName;
    }

    // ??????????????????????????????????????????????????????????????????????????
    // PENDING REQUEST VIEW MODEL  (Pending tab)
    // ??????????????????????????????????????????????????????????????????????????

    public class PendingRequestViewModel
    {
        private readonly GroupJoinRequest _request;

        public PendingRequestViewModel(GroupJoinRequest request)
        {
            _request = request;
        }

        public string Id => _request.Id;
        public string GroupId => _request.GroupId;
        public string UserPhone => _request.UserPhone;
        public string UserName => _request.UserName;
        public string UserProfileImage => _request.UserProfileImage;
        public string Message => _request.Message;
        public bool HasMessage => !string.IsNullOrEmpty(_request.Message);

        // ?? Enriched from User table ??????????????????????????????????????
        public string UserBio { get; set; } = string.Empty;
        public string UserMood { get; set; } = string.Empty;
        public string OnlineStatusText { get; set; } = "Offline";
        public string OnlineStatusColor { get; set; } = "#555555";
        public string CompatibilityWithCurrentUser { get; set; } = "0%";

        public bool HasMood => !string.IsNullOrEmpty(UserMood);
        public bool HasBio => !string.IsNullOrEmpty(UserBio);
        public string UserBioPreview => HasBio
            ? (UserBio.Length > 50 ? UserBio[..47] + "..." : UserBio)
            : string.Empty;

        public string RequestedAtDisplay
        {
            get
            {
                var span = DateTime.UtcNow - _request.RequestedAt;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                return $"{(int)span.TotalDays}d ago";
            }
        }
    }
}