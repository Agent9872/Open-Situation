using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Communication = Microsoft.Maui.ApplicationModel.Communication;
using System.Diagnostics;

namespace Lock.Pages.Chat
{
    // SearchResult class to handle both registered and unregistered users
    public class SearchResult : INotifyPropertyChanged
    {
        private User _user;
        public User User { get => _user; set { _user = value; OnPropertyChanged(); OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(PhoneNumber)); OnPropertyChanged(nameof(Mood)); OnPropertyChanged(nameof(ProfileImagePath)); } }
        private bool _isRegistered;
        public bool IsRegistered { get => _isRegistered; set { _isRegistered = value; OnPropertyChanged(); } }

        public string Name => User?.Name ?? "Unknown";

        public bool HidePhoneNumber => User?.HidePhoneNumber ?? false;

        public string PhoneNumber => User?.PhoneNumber ?? "";
        public string Mood => User?.Mood ?? string.Empty;
        public string ProfileImagePath => User?.ProfileImagePath ?? "https://ui-avatars.com/api/?name=User&background=333&color=fff";
        public bool AllowMoodSearch => User?.AllowMoodSearch ?? false;
        public bool GhostModeMoodShield => User?.GhostModeMoodShield ?? false;

        // Match data properties
        public string InterestedIn { get; set; } = string.Empty;
        public bool HasInterestedIn => !string.IsNullOrEmpty(InterestedIn);

        public int MatchPercent { get; set; } = 0;
        public bool HasMatch => MatchPercent > 0;
        public string MatchDisplay => $"{MatchPercent}%";

        public string MatchColor => MatchPercent >= 80 ? "#10B981"
                                  : MatchPercent >= 60 ? "#008080"
                                  : MatchPercent >= 40 ? "#F59E0B"
                                                       : "#888888";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class NewChatPage : ContentPage, INotifyPropertyChanged
    {
        private const string CurrentUserPhoneKey = "current_user_phone";
        private readonly ObservableCollection<SearchResult> _suggestions = new();
        private CancellationTokenSource? _searchCts;
        private SearchResult? _selectedSearchResult;
        private string? _selectedMoodFilter = null;
        private string? _selectedLocationFilter = null;
        private string? _myOwnMood = null;
        private bool _selectedUserIsRegistered;


        // Tab state properties
        private bool _isSearchTabActive = true;
        public bool IsSearchTabActive
        {
            get => _isSearchTabActive;
            set
            {
                _isSearchTabActive = value;
                OnPropertyChanged();
                UpdateTabContent();
            }
        }

        private bool _isContactsTabActive;
        private bool _isRecentTabActive = true;

        public INavigation? ParentNavigation { get; set; }

        public bool IsContactsTabActive
        {
            get => _isContactsTabActive;
            set
            {
                _isContactsTabActive = value;
                OnPropertyChanged();
                UpdateTabContent();
            }
        }

        public bool IsRecentTabActive
        {
            get => _isRecentTabActive;
            set
            {
                _isRecentTabActive = value;
                OnPropertyChanged();
                UpdateTabContent();
            }
        }

        // Recent chats properties
        private ObservableCollection<SearchResult> _allRecentChats = new();
        public ObservableCollection<SearchResult> AllRecentChats
        {
            get => _allRecentChats;
            set
            {
                _allRecentChats = value;
                OnPropertyChanged(nameof(AllRecentChats));
                FilterRecentChats();
            }
        }

        private ObservableCollection<SearchResult> _filteredRecentChats = new();
        public ObservableCollection<SearchResult> FilteredRecentChats
        {
            get => _filteredRecentChats;
            set
            {
                _filteredRecentChats = value;
                OnPropertyChanged(nameof(FilteredRecentChats));
                OnPropertyChanged(nameof(HasRecentChats));
            }
        }

        public bool HasRecentChats => FilteredRecentChats?.Any() == true;

        // UI Control properties
        private Entry PhoneEntryControl
        {
            get
            {
                try { return this.FindByName<Entry>("PhoneEntry")!; }
                catch { return null; }
            }
        }

        // Contact item class for displaying phone contacts
        public class ContactItem : INotifyPropertyChanged
        {
            private string _displayName;
            private string _phoneNumber;
            private string _profileImage;
            private bool _isRegistered;
            private User _registeredUser;

            public string DisplayName
            {
                get => _displayName;
                set { _displayName = value; OnPropertyChanged(); }
            }

            public string PhoneNumber
            {
                get => _phoneNumber;
                set { _phoneNumber = value; OnPropertyChanged(); }
            }

            public string ProfileImage
            {
                get => _profileImage;
                set { _profileImage = value; OnPropertyChanged(); }
            }

            public bool IsRegistered
            {
                get => _isRegistered;
                set { _isRegistered = value; OnPropertyChanged(); }
            }

            public User RegisteredUser
            {
                get => _registeredUser;
                set { _registeredUser = value; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private CollectionView SuggestionsCollectionViewControl
        {
            get
            {
                try { return this.FindByName<CollectionView>("SuggestionsCollectionView")!; }
                catch { return null; }
            }
        }

        private Picker MoodPickerControl
        {
            get
            {
                try { return this.FindByName<Picker>("MoodPicker")!; }
                catch { return null; }
            }
        }

        private Picker LocationPickerControl
        {
            get
            {
                try { return this.FindByName<Picker>("LocationPicker")!; }
                catch { return null; }
            }
        }

        private Label ResultCountLabelControl
        {
            get
            {
                try { return this.FindByName<Label>("ResultCountLabel")!; }
                catch { return null; }
            }
        }

        private CollectionView FriendsCollectionViewControl
        {
            get
            {
                try { return this.FindByName<CollectionView>("FriendsCollectionView")!; }
                catch { return null; }
            }
        }

        private CollectionView RecentChatsCollectionViewControl
        {
            get
            {
                try { return this.FindByName<CollectionView>("RecentChatsCollectionView")!; }
                catch { return null; }
            }
        }

        public bool SelectedUserIsRegistered
        {
            get => _selectedUserIsRegistered;
            set
            {
                _selectedUserIsRegistered = value;
                OnPropertyChanged(nameof(SelectedUserIsRegistered));
            }
        }

        // Imported unregistered contacts
        private List<string> _importedUnregisteredContacts = new List<string>();

        // Contacts list properties
        private ObservableCollection<ContactItem> _allContacts = new();
        public ObservableCollection<ContactItem> AllContacts
        {
            get => _allContacts;
            set
            {
                _allContacts = value;
                OnPropertyChanged(nameof(AllContacts));
                OnPropertyChanged(nameof(HasContacts));
            }
        }

        public bool HasContacts => AllContacts?.Any() == true;

        private ObservableCollection<IGrouping<string, SearchResult>> _groupedFriends;
        public ObservableCollection<IGrouping<string, SearchResult>> GroupedFriends
        {
            get => _groupedFriends;
            set
            {
                _groupedFriends = value;
                OnPropertyChanged(nameof(GroupedFriends));
            }
        }

        private int _friendsCount;
        public int FriendsCount
        {
            get => _friendsCount;
            set
            {
                _friendsCount = value;
                OnPropertyChanged(nameof(FriendsCount));
            }
        }

        // Constructor
        public NewChatPage()
        {
            try
            {
                InitializeComponent();
                BindingContext = this;
                this.Loaded += OnPageLoaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Constructor error: {ex}");
            }
        }

        // Tab click handlers
        private void SearchTab_Clicked(object sender, EventArgs e)
        {
            IsRecentTabActive = false;
            IsSearchTabActive = true;

            if (PhoneEntryControl != null)
            {
                _ = PerformSearchAsync(PhoneEntryControl.Text?.Trim() ?? "");
            }
        }

        private void RecentTab_Clicked(object sender, EventArgs e)
        {
            IsRecentTabActive = true;
            IsSearchTabActive = false;
            FilterRecentChats();
        }

        private void UpdateTabContent()
        {
            if (RecentTabButton != null)
                RecentTabButton.IsVisible = true;
            if (SearchTabButton != null)
                SearchTabButton.IsVisible = true;

            if (RecentTabContent != null)
                RecentTabContent.IsVisible = IsRecentTabActive;
            if (SearchTabContent != null)
                SearchTabContent.IsVisible = IsSearchTabActive;

            System.Diagnostics.Debug.WriteLine($"Tab updated - Recent: {IsRecentTabActive}, Search: {IsSearchTabActive}");
        }

        // Orientation compatibility check
        private bool AreOrientationCompatible(User me, User other, out string otherInterestedInLabel)
        {
            otherInterestedInLabel = string.Empty;

            static string OrientationGenderKey(User u)
            {
                var o = (u.SexualOrientation ?? string.Empty).ToLower();
                var g = (u.Gender ?? string.Empty).ToLower();

                if (o.Contains("gay") || o.Contains("lesbian"))
                    return g;
                if (o.Contains("straight") || o.Contains("hetero"))
                    return g.Contains("male") && !g.Contains("female") ? "female" : "male";
                if (o.Contains("bi") || o.Contains("pan") || o.Contains("queer"))
                    return "any";
                return "any";
            }

            var myGenderKey = (me.Gender ?? string.Empty).ToLower().Trim();
            var theirGenderKey = (other.Gender ?? string.Empty).ToLower().Trim();

            var myInterestKey = OrientationGenderKey(me);
            var theirInterestKey = OrientationGenderKey(other);

            if (!string.IsNullOrEmpty(other.SexualOrientation) &&
                !other.SexualOrientation.Contains("not to say", StringComparison.OrdinalIgnoreCase))
            {
                otherInterestedInLabel = other.SexualOrientation;
            }
            else if (!string.IsNullOrEmpty(other.Interest))
            {
                otherInterestedInLabel = other.Interest;
            }

            if (myInterestKey == "any" || theirInterestKey == "any") return true;

            bool theyWantMe = theirInterestKey == "any" ||
                              myGenderKey.Contains(theirInterestKey) ||
                              theirInterestKey.Contains(myGenderKey);

            bool iWantThem = myInterestKey == "any" ||
                             theirGenderKey.Contains(myInterestKey) ||
                             myInterestKey.Contains(theirGenderKey);

            return theyWantMe && iWantThem;
        }

        private async Task<(int score, string interestedIn, bool compatible)>
            GetMatchDataAsync(User me, User other)
        {
            try
            {
                bool compatible = AreOrientationCompatible(me, other, out string interestedIn);
                if (!compatible) return (0, interestedIn, false);

                int score = await CompatibilityService.CalculateCompatibilityScoreAsync(me, other);
                return (score, interestedIn, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetMatchDataAsync error: {ex}");
                return (0, string.Empty, true);
            }
        }

        // Deduplicate search results against recent chats
        private List<SearchResult> DeduplicateSearchResults(List<SearchResult> searchResults)
        {
            if (searchResults == null || !searchResults.Any())
                return new List<SearchResult>();

            if (FilteredRecentChats == null || !FilteredRecentChats.Any())
                return searchResults;

            var recentChatPhoneNumbers = new HashSet<string>(
                FilteredRecentChats
                    .Where(r => r.IsRegistered && !string.IsNullOrWhiteSpace(r.PhoneNumber))
                    .Select(r => NormalizePhoneNumber(r.PhoneNumber)),
                StringComparer.OrdinalIgnoreCase
            );

            var deduplicatedResults = searchResults
                .Where(result =>
                {
                    if (!result.IsRegistered)
                        return true;

                    var normalizedPhone = NormalizePhoneNumber(result.PhoneNumber);
                    return !recentChatPhoneNumbers.Contains(normalizedPhone);
                })
                .ToList();

            var removedCount = searchResults.Count - deduplicatedResults.Count;
            if (removedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Deduplicated: Removed {removedCount} users already in recent chats");
            }

            return deduplicatedResults;
        }

        // Main search method with enhancements
        private async Task PerformSearchAsync(string searchText)
        {
            try
            {
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;

                if (SuggestionsCollectionViewControl == null || ResultCountLabelControl == null)
                    return;

                if (string.IsNullOrWhiteSpace(searchText) &&
                    string.IsNullOrEmpty(_selectedMoodFilter) &&
                    string.IsNullOrEmpty(_selectedLocationFilter))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        _suggestions.Clear();
                        SuggestionsCollectionViewControl.IsVisible = false;
                        ResultCountLabelControl.IsVisible = false;
                    });
                    return;
                }

                await Task.Delay(300, token);

                // Get all users from Supabase
                var allUsers = await SupabaseService.GetAsync<User>("Users", "");
                var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);

                // Load ghosted phones
                var ghostedPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    ghostedPhones = allUsers
                        .Where(u => u.GhostModeMoodShield)
                        .Select(u => (u.PhoneNumber ?? "").Trim())
                        .Where(p => !string.IsNullOrEmpty(p) &&
                                    !string.Equals(p, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Search ghost filter error: {ex.Message}");
                }

                // Get all registered users (excluding current user AND ghosted users)
                var registeredUsers = allUsers
                    .Where(u => u.PhoneNumber != currentUserPhone &&
                                !string.IsNullOrWhiteSpace(u.PhoneNumber) &&
                                !ghostedPhones.Contains(u.PhoneNumber.Trim()))
                    .ToList();

                var matches = new List<SearchResult>();

                // Apply mood filter if selected
                if (!string.IsNullOrEmpty(_selectedMoodFilter) && _selectedMoodFilter != "All moods")
                {
                    registeredUsers = registeredUsers
                        .Where(u => string.Equals(u.Mood?.Trim(), _selectedMoodFilter, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Apply location filter if selected
                if (!string.IsNullOrEmpty(_selectedLocationFilter) && _selectedLocationFilter != "All locations")
                {
                    registeredUsers = registeredUsers
                        .Where(u =>
                        {
                            if (string.IsNullOrEmpty(u.Country) && string.IsNullOrEmpty(u.State))
                                return false;

                            var location = $"{u.Country ?? ""}, {u.State ?? ""}".Trim(',', ' ').ToLowerInvariant();
                            var filterLower = _selectedLocationFilter.ToLowerInvariant();

                            return location.Contains(filterLower) ||
                                   (u.Country?.ToLowerInvariant() == filterLower) ||
                                   (u.State?.ToLowerInvariant() == filterLower);
                        })
                        .ToList();
                }

                // Always search by partial matching
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var normalizedSearch = new string(searchText.Where(c => char.IsDigit(c)).ToArray());
                    var searchLower = searchText.ToLowerInvariant().Trim();

                    // 1. Exact phone number match - ONLY if user doesn't have HidePhoneNumber enabled
                    var exactPhoneMatches = registeredUsers
                        .Where(u => !string.IsNullOrWhiteSpace(u.PhoneNumber))
                        .Where(u => !u.HidePhoneNumber)
                        .Where(u =>
                        {
                            var normalizedUserPhone = new string(u.PhoneNumber.Where(c => char.IsDigit(c)).ToArray());
                            return normalizedUserPhone == normalizedSearch && !string.IsNullOrEmpty(normalizedSearch);
                        })
                        .Select(u => new SearchResult { User = u, IsRegistered = true });

                    matches.AddRange(exactPhoneMatches);

                    // 2. Partial phone number match
                    if (!exactPhoneMatches.Any() && !string.IsNullOrEmpty(normalizedSearch))
                    {
                        var registeredByPhone = registeredUsers
                            .Where(u => !string.IsNullOrWhiteSpace(u.PhoneNumber))
                            .Where(u => !u.HidePhoneNumber)
                            .Where(u =>
                            {
                                var normalizedUserPhone = new string(u.PhoneNumber.Where(c => char.IsDigit(c)).ToArray());
                                return normalizedUserPhone.Contains(normalizedSearch);
                            })
                            .Select(u => new SearchResult { User = u, IsRegistered = true });

                        matches.AddRange(registeredByPhone);
                    }

                    // 3. Partial name match
                    var registeredByName = registeredUsers
                        .Where(u => !string.IsNullOrWhiteSpace(u.Name) &&
                                   u.Name.ToLowerInvariant().Contains(searchLower))
                        .Select(u => new SearchResult { User = u, IsRegistered = true });

                    matches.AddRange(registeredByName);

                    // 4. Mood match
                    if (string.IsNullOrEmpty(_selectedMoodFilter))
                    {
                        var registeredByMood = registeredUsers
                            .Where(u => !string.IsNullOrWhiteSpace(u.Mood) &&
                                       u.Mood.ToLowerInvariant().Contains(searchLower))
                            .Select(u => new SearchResult { User = u, IsRegistered = true });

                        matches.AddRange(registeredByMood);
                    }

                    // 5. Location match
                    if (string.IsNullOrEmpty(_selectedLocationFilter))
                    {
                        var registeredByLocation = registeredUsers
                            .Where(u =>
                            {
                                var location = $"{u.Country ?? ""} {u.State ?? ""}".ToLowerInvariant();
                                return location.Contains(searchLower);
                            })
                            .Select(u => new SearchResult { User = u, IsRegistered = true });

                        matches.AddRange(registeredByLocation);
                    }

                    // 6. Unregistered contacts
                    if (IsPhoneNumber(searchText))
                    {
                        var unregisteredContacts = GetImportedUnregisteredContacts();
                        if (unregisteredContacts.Any())
                        {
                            var unregisteredMatches = unregisteredContacts
                                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                                .Where(phone =>
                                {
                                    var normalizedPhone = new string(phone.Where(c => char.IsDigit(c)).ToArray());
                                    return normalizedPhone.Contains(normalizedSearch) && !string.IsNullOrEmpty(normalizedSearch);
                                })
                                .Select(phone => new SearchResult
                                {
                                    User = new User
                                    {
                                        PhoneNumber = phone,
                                        Name = FormatContactName(phone),
                                        ProfileImagePath = "unregistered_icon.png"
                                    },
                                    IsRegistered = false
                                });

                            matches.AddRange(unregisteredMatches);
                        }
                    }
                }
                else
                {
                    // No search text — show all users matching mood/location filters
                    matches = registeredUsers
                        .Select(u => new SearchResult { User = u, IsRegistered = true })
                        .ToList();
                }

                // Remove duplicates by phone number (prefer registered over unregistered)
                matches = matches
                    .GroupBy(r => NormalizePhoneNumber(r.PhoneNumber))
                    .Select(g => g.OrderByDescending(r => r.IsRegistered).First())
                    .ToList();

                // Enrich with match data and interested-in
                try
                {
                    var meUsers = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                    var meUser = meUsers.FirstOrDefault();

                    if (meUser != null)
                    {
                        foreach (var result in matches.Where(r => r.IsRegistered && r.User != null))
                        {
                            var matchData = await GetMatchDataAsync(meUser, result.User);
                            result.MatchPercent = matchData.compatible ? matchData.score : 0;
                            result.InterestedIn = matchData.interestedIn;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PerformSearchAsync enrich error: {ex}");
                }

                // Deduplicate against recent chats
                matches = DeduplicateSearchResults(matches);

                // Take top 20
                matches = matches.Take(20).ToList();

                token.ThrowIfCancellationRequested();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _suggestions.Clear();
                    foreach (var result in matches)
                        _suggestions.Add(result);

                    SuggestionsCollectionViewControl.IsVisible = matches.Count > 0;
                    ResultCountLabelControl.IsVisible = true;

                    if (matches.Count > 0)
                    {
                        var registeredCount = matches.Count(r => r.IsRegistered);
                        var unregisteredCount = matches.Count(r => !r.IsRegistered);

                        var descParts = new List<string>();
                        if (!string.IsNullOrEmpty(_selectedMoodFilter) && _selectedMoodFilter != "All moods")
                            descParts.Add($"mood «{_selectedMoodFilter}»");
                        if (!string.IsNullOrEmpty(_selectedLocationFilter) && _selectedLocationFilter != "All locations")
                            descParts.Add($"location «{_selectedLocationFilter}»");
                        if (!string.IsNullOrWhiteSpace(searchText))
                            descParts.Add($"\"{searchText}\"");

                        var desc = descParts.Any() ? $" for {string.Join(" + ", descParts)}" : "";

                        var ghostedCount = allUsers.Count(u => u.GhostModeMoodShield && u.PhoneNumber != currentUserPhone);
                        var ghostInfo = ghostedCount > 0 ? $" (🔒 {ghostedCount} hidden by ghost mode)" : "";

                        var recentChatsCount = FilteredRecentChats?.Count ?? 0;
                        var dedupInfo = recentChatsCount > 0 && registeredCount > 0 ? $" • {registeredCount} in search" : "";

                        // Count users hidden by HidePhoneNumber privacy
                        var hiddenPhoneCount = 0;
                        var privacyInfo = "";
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            var normSearch = new string(searchText.Where(c => char.IsDigit(c)).ToArray());
                            hiddenPhoneCount = registeredUsers.Count(u => u.HidePhoneNumber &&
                                !string.IsNullOrWhiteSpace(u.PhoneNumber) &&
                                new string(u.PhoneNumber.Where(c => char.IsDigit(c)).ToArray()).Contains(normSearch));
                            privacyInfo = hiddenPhoneCount > 0 ? $" • 🔒 {hiddenPhoneCount} hidden (privacy)" : "";
                        }

                        if (registeredCount > 0)
                        {
                            ResultCountLabelControl.Text = $"{registeredCount} registered user{(registeredCount == 1 ? "" : "s")} found{desc}{ghostInfo}{dedupInfo}{privacyInfo}";
                            ResultCountLabelControl.TextColor = Color.FromArgb("#4CAF50");
                        }
                        else if (unregisteredCount > 0)
                        {
                            ResultCountLabelControl.Text = $"📱 {unregisteredCount} unregistered number{(unregisteredCount == 1 ? "" : "s")} found{desc}";
                            ResultCountLabelControl.TextColor = Color.FromArgb("#FFA500");
                        }
                        else
                        {
                            ResultCountLabelControl.Text = $"No matching users found{desc}{ghostInfo}";
                            ResultCountLabelControl.TextColor = Color.FromArgb("#FF6B6B");
                        }
                    }
                    else
                    {
                        var normalizedSearchForGhost = new string(searchText.Where(c => char.IsDigit(c)).ToArray());
                        var ghostedMatchesCount = allUsers.Count(u =>
                            u.GhostModeMoodShield &&
                            u.PhoneNumber != currentUserPhone &&
                            (!string.IsNullOrWhiteSpace(searchText) &&
                             (u.Name?.ToLowerInvariant().Contains(searchText.ToLowerInvariant()) == true ||
                              u.PhoneNumber?.Contains(searchText) == true)));

                        if (ghostedMatchesCount > 0)
                        {
                            ResultCountLabelControl.Text = $"🔒 {ghostedMatchesCount} user{(ghostedMatchesCount == 1 ? "" : "s")} hidden by ghost mode";
                            ResultCountLabelControl.TextColor = Color.FromArgb("#FFA500");
                        }
                        else
                        {
                            ResultCountLabelControl.Text = "No matching users found";
                            ResultCountLabelControl.TextColor = Color.FromArgb("#FF6B6B");
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Search cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Search stack trace: {ex.StackTrace}");

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ResultCountLabelControl.Text = "Search error occurred";
                    ResultCountLabelControl.TextColor = Color.FromArgb("#FF6B6B");
                    ResultCountLabelControl.IsVisible = true;
                });
            }
        }

        // Load recent chats
        private async Task LoadRecentChatsAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);

                if (string.IsNullOrEmpty(currentUserPhone))
                    return;

                // Get all users for ghost mode filtering
                var allUsers = await SupabaseService.GetAsync<User>("Users", "");

                var ghostedPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    ghostedPhones = allUsers
                        .Where(u => u.GhostModeMoodShield)
                        .Select(u => (u.PhoneNumber ?? "").Trim())
                        .Where(p => !string.IsNullOrEmpty(p) &&
                                    !string.Equals(p, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ghost mode filter load error: {ex.Message}");
                }

                // Get all conversations from Supabase
                var allConversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or=(ParticipantA.eq.{Uri.EscapeDataString(currentUserPhone)},ParticipantB.eq.{Uri.EscapeDataString(currentUserPhone)})");

                if (!allConversations.Any())
                {
                    AllRecentChats = new ObservableCollection<SearchResult>();
                    return;
                }

                var recentChats = new List<SearchResult>();

                var currentUserUsers = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(currentUserPhone)}&limit=1");
                var currentUser = currentUserUsers.FirstOrDefault();

                foreach (var conv in allConversations)
                {
                    var otherPhone = conv.ParticipantA == currentUserPhone
                        ? conv.ParticipantB
                        : conv.ParticipantA;

                    var cleanOtherForGhost = otherPhone.Contains("·")
                        ? otherPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries)
                               .Skip(1).FirstOrDefault()?.Trim() ?? otherPhone.Trim()
                        : otherPhone.Trim();

                    if (ghostedPhones.Contains(cleanOtherForGhost))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping ghosted user recent chat: {otherPhone}");
                        continue;
                    }

                    // Get messages for this conversation
                    var messages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                        $"ConversationId=eq.{Uri.EscapeDataString(conv.ConversationId)}");

                    var acceptedMessages = messages.Count(m => !m.IsMessageRequest && !m.IsDeclined);
                    var userSentMessages = messages.Count(m => m.SenderPhone == currentUserPhone && !m.IsMessageRequest);

                    bool isRealChat = acceptedMessages > 0 || userSentMessages > 0;

                    if (isRealChat)
                    {
                        var otherUsers = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(otherPhone)}&limit=1");
                        var otherUser = otherUsers.FirstOrDefault();

                        if (otherUser != null && !recentChats.Any(r => r.PhoneNumber == otherPhone))
                        {
                            // Skip users who have HidePhoneNumber enabled
                            if (otherUser.HidePhoneNumber)
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping recent chat from {otherPhone} - user has hidden phone number");
                                continue;
                            }

                            if (currentUser != null)
                            {
                                var matchData = await GetMatchDataAsync(currentUser, otherUser);
                                recentChats.Add(new SearchResult
                                {
                                    User = otherUser,
                                    IsRegistered = true,
                                    MatchPercent = matchData.compatible ? matchData.score : 0,
                                    InterestedIn = matchData.interestedIn
                                });
                            }
                        }
                    }
                }

                recentChats = recentChats
                    .OrderByDescending(r =>
                    {
                        var conv = allConversations.FirstOrDefault(c =>
                            c.ParticipantA == r.PhoneNumber || c.ParticipantB == r.PhoneNumber);
                        return conv?.LastMessageAt ?? DateTime.MinValue;
                    })
                    .Take(30)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AllRecentChats = new ObservableCollection<SearchResult>(recentChats);
                    FilterRecentChats();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load recent chats failed: {ex}");
            }
        }

        private void FilterRecentChats()
        {
            try
            {
                if (AllRecentChats == null || !AllRecentChats.Any())
                {
                    FilteredRecentChats = new ObservableCollection<SearchResult>();
                    OnPropertyChanged(nameof(HasRecentChats));
                    return;
                }

                var searchText = PhoneEntryControl?.Text?.Trim() ?? "";
                var moodFilter = _selectedMoodFilter;
                var locationFilter = _selectedLocationFilter;

                var filtered = AllRecentChats.AsEnumerable();

                // Filter out users who have HidePhoneNumber enabled (privacy)
                filtered = filtered.Where(r => !r.HidePhoneNumber);

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var searchLower = searchText.ToLowerInvariant().Trim();
                    var normalizedSearch = NormalizePhoneNumber(searchText);

                    filtered = filtered.Where(r =>
                        (!string.IsNullOrWhiteSpace(r.Name) &&
                         r.Name.ToLowerInvariant().Contains(searchLower)) ||
                        // Only search by phone number if user hasn't hidden it
                        (!r.HidePhoneNumber &&
                         !string.IsNullOrWhiteSpace(r.PhoneNumber) &&
                         NormalizePhoneNumber(r.PhoneNumber).Contains(normalizedSearch))
                    );
                }

                // Rest of the method remains the same...
                if (!string.IsNullOrEmpty(moodFilter) && moodFilter != "All moods")
                {
                    filtered = filtered.Where(r =>
                        string.Equals(r.Mood?.Trim(), moodFilter, StringComparison.OrdinalIgnoreCase)
                    );
                }

                if (!string.IsNullOrEmpty(locationFilter) && locationFilter != "All locations")
                {
                    var filterLower = locationFilter.ToLowerInvariant().Trim();

                    filtered = filtered.Where(r =>
                    {
                        if (r.User == null) return false;
                        var userLocation = $"{r.User.Country ?? ""}, {r.User.State ?? ""}".Trim(',', ' ').ToLowerInvariant();
                        return userLocation.Contains(filterLower) ||
                               (r.User.Country?.ToLowerInvariant() == filterLower) ||
                               (r.User.State?.ToLowerInvariant() == filterLower);
                    });
                }

                var resultList = filtered
                    .OrderBy(r => r.Name)
                    .ToList();

                FilteredRecentChats = new ObservableCollection<SearchResult>(resultList);
                OnPropertyChanged(nameof(HasRecentChats));

                if (RecentChatsCollectionViewControl != null)
                    RecentChatsCollectionViewControl.ItemsSource = FilteredRecentChats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FilterRecentChats error: {ex}");
            }
        }
        // Picker events
        private async void MoodPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is not Picker picker || picker.SelectedItem is not string selected) return;

                _selectedMoodFilter = (selected == "All moods") ? null : selected.Trim();
                FilterRecentChats();

                if (IsSearchTabActive && PhoneEntryControl != null)
                    await PerformSearchAsync(PhoneEntryControl.Text?.Trim() ?? "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MoodPicker error: {ex}");
            }
        }

        private async void LocationPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is not Picker picker || picker.SelectedItem is not string selected) return;

                _selectedLocationFilter = (selected == "All locations") ? null : selected.Trim();
                FilterRecentChats();

                if (IsSearchTabActive && PhoneEntryControl != null)
                    await PerformSearchAsync(PhoneEntryControl.Text?.Trim() ?? "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LocationPicker error: {ex}");
            }
        }

        // Phone entry text changed
        private async void PhoneEntry_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                _selectedSearchResult = null;
                SelectedUserIsRegistered = false;
                FilterRecentChats();
                await PerformSearchAsync(e.NewTextValue?.Trim() ?? "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PhoneEntry error: {ex}");
            }
        }

        // Helper methods
        private bool IsPhoneNumber(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   text.Any(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' ' || c == '(' || c == ')');
        }

        private void StoreImportedUnregisteredContacts(List<string> unregisteredPhones)
        {
            _importedUnregisteredContacts = unregisteredPhones ?? new List<string>();
        }

        private List<string> GetImportedUnregisteredContacts()
        {
            return _importedUnregisteredContacts ?? new List<string>();
        }

        private string NormalizePhoneNumber(string phone)
        {
            return new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        }

        private string FormatContactName(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return "Unknown Contact";

            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 4)
            {
                var lastFour = digits.Substring(digits.Length - 4);
                return $"Contact (••••{lastFour})";
            }

            return $"Contact ({phoneNumber})";
        }

        private async Task LoadMyOwnMoodAsync()
        {
            try
            {
                var myPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(myPhone)) return;

                var users = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(myPhone)}&limit=1");
                var me = users.FirstOrDefault();
                _myOwnMood = me?.Mood?.Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMyOwnMood failed: {ex.Message}");
            }
        }

        private async Task LoadFriendsList()
        {
            try
            {
                var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);

                var allUsers = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=ne.{Uri.EscapeDataString(currentUserPhone)}");

                var friends = allUsers
                    .Where(u => !string.IsNullOrWhiteSpace(u.PhoneNumber))
                    .Select(u => new SearchResult { User = u, IsRegistered = true })
                    .OrderBy(f => f.Name)
                    .ToList();

                var grouped = friends
                    .GroupBy(f =>
                    {
                        if (string.IsNullOrEmpty(f.Name)) return "#";
                        var firstChar = char.ToUpper(f.Name[0]);
                        return char.IsLetter(firstChar) ? firstChar.ToString() : "#";
                    })
                    .OrderBy(g => g.Key)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    GroupedFriends = new ObservableCollection<IGrouping<string, SearchResult>>(grouped);
                    FriendsCount = friends.Count;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load friends failed: {ex}");
            }
        }

        private async Task LoadUniqueLocationsAsync()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var allUsers = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=ne.{Uri.EscapeDataString(currentUserPhone)}");

                var uniqueLocations = new HashSet<string>();

                foreach (var user in allUsers)
                {
                    var location = string.Empty;
                    if (!string.IsNullOrEmpty(user.Country) && !string.IsNullOrEmpty(user.State))
                    {
                        location = $"{user.Country}, {user.State}";
                    }
                    else if (!string.IsNullOrEmpty(user.Country))
                    {
                        location = user.Country;
                    }
                    else if (!string.IsNullOrEmpty(user.State))
                    {
                        location = user.State;
                    }

                    if (!string.IsNullOrEmpty(location))
                    {
                        uniqueLocations.Add(location);
                    }
                }

                var sortedLocations = uniqueLocations.OrderBy(l => l).ToList();

                if (sortedLocations.Any())
                {
                    Preferences.Set("global_locations", string.Join("|", sortedLocations));
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var locationPicker = this.FindByName<Picker>("LocationPicker");
                    if (locationPicker != null)
                    {
                        locationPicker.Items.Clear();
                        locationPicker.Items.Add("All locations");
                        foreach (var loc in sortedLocations)
                        {
                            locationPicker.Items.Add(loc);
                        }
                        locationPicker.SelectedIndex = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadUniqueLocationsAsync error: {ex.Message}");
                await LoadLocationsFromPreferencesAsync();
            }
        }

        private async Task LoadLocationsFromPreferencesAsync()
        {
            try
            {
                var existingLocations = Preferences.Get("global_locations", string.Empty);
                var locations = string.IsNullOrEmpty(existingLocations)
                    ? new List<string>()
                    : existingLocations.Split('|').ToList();

                locations.Insert(0, "All locations");
                locations = locations.Distinct().ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var locationPicker = this.FindByName<Picker>("LocationPicker");
                    if (locationPicker != null)
                    {
                        string currentSelection = locationPicker.SelectedItem as string ?? "All locations";

                        locationPicker.Items.Clear();
                        foreach (var loc in locations)
                        {
                            locationPicker.Items.Add(loc);
                        }

                        if (locationPicker.Items.Contains(currentSelection))
                        {
                            locationPicker.SelectedIndex = locationPicker.Items.IndexOf(currentSelection);
                        }
                        else
                        {
                            locationPicker.SelectedIndex = 0;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadLocationsFromPreferencesAsync error: {ex.Message}");
            }
        }

        // Event handlers
        private void SuggestionsCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection.Count == 0) return;
                if (e.CurrentSelection[0] is SearchResult result)
                {
                    if (PhoneEntryControl != null)
                    {
                        PhoneEntryControl.Text = string.IsNullOrWhiteSpace(result.User.PhoneNumber)
                            ? result.User.Name
                            : result.User.PhoneNumber;
                    }
                    _selectedSearchResult = result;
                    SelectedUserIsRegistered = result.IsRegistered;

                    if (SuggestionsCollectionViewControl != null)
                        SuggestionsCollectionViewControl.IsVisible = false;
                }
                if (sender is CollectionView cv)
                    cv.SelectedItem = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Suggestions selection error: {ex}");
            }
        }

        private async void SuggestionItem_Tapped(object sender, EventArgs e)
        {
            try
            {
                var result = (sender as TapGestureRecognizer)?.CommandParameter as SearchResult;
                if (result == null) return;

                if (result.IsRegistered)
                {
                    var action = await DisplayActionSheet(
                        $"{result.Name}",
                        "Cancel",
                        null,
                        "Send Message",
                        "View Profile",
                        "Copy Number");

                    switch (action)
                    {
                        case "Send Message":
                            await SendMessageToUser(result);
                            break;
                        case "View Profile":
                            await ViewUserProfile(result);
                            break;
                        case "Copy Number":
                            await Clipboard.SetTextAsync(result.PhoneNumber);
                            await DisplayAlert("Copied", "Phone number copied to clipboard", "OK");
                            break;
                    }
                }
                else
                {
                    var action = await DisplayActionSheet(
                        "Not on Lock",
                        "Cancel",
                        null,
                        "Invite via SMS",
                        "Copy Number");

                    switch (action)
                    {
                        case "Invite via SMS":
                            var message = "Hey! Join me on Lock app - let's chat there!";
                            await Launcher.OpenAsync($"sms:{result.PhoneNumber}?body={Uri.EscapeDataString(message)}");
                            break;
                        case "Copy Number":
                            await Clipboard.SetTextAsync(result.PhoneNumber);
                            await DisplayAlert("Copied", "Phone number copied to clipboard", "OK");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SuggestionItem_Tapped error: {ex}");
            }
        }

        private async void SuggestionMessageButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                SearchResult? result = null;

                if (sender is Border border)
                    result = border.BindingContext as SearchResult;

                if (result == null && sender is VisualElement ve)
                    result = ve.BindingContext as SearchResult;

                if (result == null) return;

                if (!result.IsRegistered)
                {
                    var action = await DisplayActionSheet(
                        "Unregistered Number", "Cancel", null,
                        "Send SMS Invite", "Copy Number");

                    if (action == "Send SMS Invite")
                    {
                        var msg = "Hey! Join me on Lock app - let's chat there!";
                        await Launcher.OpenAsync($"sms:{result.PhoneNumber}?body={Uri.EscapeDataString(msg)}");
                    }
                    else if (action == "Copy Number")
                    {
                        await Clipboard.SetTextAsync(result.PhoneNumber);
                        await DisplayAlert("Copied", "Phone number copied to clipboard", "OK");
                    }
                    return;
                }

                await NavigateToChatAsync(result.User?.PhoneNumber, result.User);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SuggestionMessageButton_Clicked error: {ex}");
                await DisplayAlert("Error", $"Could not open chat: {ex.Message}", "OK");
            }
        }

        private async void SuggestionProfile_Tapped(object? sender, EventArgs e)
        {
            try
            {
                var result = (sender as TapGestureRecognizer)?.CommandParameter as SearchResult
                        ?? (sender as VisualElement)?.BindingContext as SearchResult;
                if (result?.User == null) return;

                if (!result.IsRegistered)
                {
                    await DisplayAlert("Not Registered", "This user is not registered on Lock yet.", "OK");
                    return;
                }

                var identifier = string.IsNullOrWhiteSpace(result.User.PhoneNumber)
                    ? result.User.Name
                    : result.User.PhoneNumber;

                if (string.IsNullOrWhiteSpace(identifier)) return;

                await CloseModalAsync();

                var route = $"//profile?phone={Uri.EscapeDataString(identifier)}&viewOnly=true";
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Profile tap failed: {ex}");
            }
        }

        private async Task SendMessageToUser(SearchResult result)
        {
            try
            {
                var target = result.User.PhoneNumber!;
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty).Trim();

                if (string.IsNullOrEmpty(me) || string.IsNullOrEmpty(target))
                    return;

                var conv = await GetOrCreateConversationAsync(me, target);

                var route = $"chat?conversationId={Uri.EscapeDataString(conv.ConversationId)}&otherPhone={Uri.EscapeDataString(target)}";

                try { await Navigation.PopModalAsync(); } catch { }
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not open chat: {ex.Message}", "OK");
            }
        }

        private async Task ViewUserProfile(SearchResult result)
        {
            try
            {
                var identifier = string.IsNullOrWhiteSpace(result.User.PhoneNumber)
                    ? result.User.Name
                    : result.User.PhoneNumber;

                if (string.IsNullOrWhiteSpace(identifier)) return;

                await CloseModalAsync();

                var route = $"//profile?phone={Uri.EscapeDataString(identifier)}&viewOnly=true";
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"View profile error: {ex}");
            }
        }

        private async Task NavigateToChatAsync(string targetPhone, User targetUser = null)
        {
            if (string.IsNullOrWhiteSpace(targetPhone)) return;

            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty).Trim();
                if (string.IsNullOrEmpty(me)) return;

                var conv = await GetOrCreateConversationAsync(me, targetPhone);

                var chatPage = new ChatPage(conv.ConversationId, targetPhone);

                await Navigation.PopModalAsync(animated: false);
                await Task.Delay(80);

                if (ParentNavigation != null)
                {
                    await ParentNavigation.PushAsync(chatPage, animated: true);
                }
                else
                {
                    await Shell.Current.GoToAsync(
                        $"chat?conversationId={Uri.EscapeDataString(conv.ConversationId)}" +
                        $"&otherPhone={Uri.EscapeDataString(targetPhone)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NavigateToChatAsync error: {ex}");
                await Application.Current.MainPage.DisplayAlert("Error", $"Could not open chat: {ex.Message}", "OK");
            }
        }

        private async void RecentChat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection.FirstOrDefault() is SearchResult selected)
                {
                    await OpenChatWithUser(selected);
                    if (sender is CollectionView cv)
                        cv.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecentChat_SelectionChanged error: {ex}");
            }
        }

        private async void RecentChat_Tapped(object sender, EventArgs e)
        {
            try
            {
                SearchResult? selected = null;

                if (sender is TapGestureRecognizer tap)
                    selected = tap.CommandParameter as SearchResult;

                if (selected == null && sender is VisualElement ve)
                    selected = ve.BindingContext as SearchResult;

                if (selected == null) return;

                await NavigateToChatAsync(selected.User?.PhoneNumber, selected.User);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RecentChat_Tapped error: {ex}");
                await DisplayAlert("Error", "Could not open chat", "OK");
            }
        }

        private async Task OpenChatWithUser(SearchResult user)
        {
            if (user?.User == null || !user.IsRegistered)
            {
                await DisplayAlert("Error", "User not registered on Lock", "OK");
                return;
            }

            await NavigateToChatAsync(user.User.PhoneNumber?.Trim(), user.User);
        }

        private async void CancelButton_Clicked(object sender, EventArgs e)
        {
            await CloseModalAsync();
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await CloseModalAsync();
        }

        private async void CreateButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                if (_selectedSearchResult == null)
                {
                    await DisplayAlert("Cannot Chat", "Please select a user to chat with.", "OK");
                    return;
                }

                var target = _selectedSearchResult.User.PhoneNumber!;

                if (string.IsNullOrWhiteSpace(target))
                {
                    await DisplayAlert("Error", "Please enter a phone number or name.", "OK");
                    return;
                }

                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty).Trim();
                if (string.IsNullOrEmpty(me))
                {
                    await DisplayAlert("Error", "No current user set.", "OK");
                    return;
                }

                var conv = await GetOrCreateConversationAsync(me, target);

                if (_selectedSearchResult.IsRegistered)
                {
                    await Navigation.PopModalAsync();
                    await Task.Delay(100);

                    var chatPage = new ChatPage(conv.ConversationId, target);
                    await Navigation.PushAsync(chatPage);
                }
                else
                {
                    var action = await DisplayActionSheet(
                        "Unregistered User",
                        "Cancel",
                        null,
                        "Send SMS Invite",
                        "Copy Number");

                    switch (action)
                    {
                        case "Send SMS Invite":
                            var message = "Hey! Join me on Lock app - let's chat there!";
                            await Launcher.OpenAsync($"sms:{target}?body={Uri.EscapeDataString(message)}");
                            break;
                        case "Copy Number":
                            await Clipboard.SetTextAsync(target);
                            await DisplayAlert("Copied", "Phone number copied to clipboard", "OK");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not create chat: {ex.Message}", "OK");
            }
        }

        private async void ContactsButton_Clicked(object sender, EventArgs e)
        {
            try
            {
#if ANDROID
                var status = await Permissions.RequestAsync<Permissions.ContactsRead>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permission Denied", "Enable contacts permission in settings.", "OK");
                    return;
                }

                var phone = await Lock.Platforms.Android.ContactPickerService.PickContactPhoneAsync();
                if (string.IsNullOrWhiteSpace(phone))
                {
                    await DisplayAlert("No Number", "No phone number found for that contact.", "OK");
                    return;
                }
                if (PhoneEntryControl != null)
                {
                    PhoneEntryControl.Text = phone;
                    await PerformSearchAsync(phone);
                }

#elif IOS || MACCATALYST
        var status = await Permissions.RequestAsync<Permissions.ContactsRead>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Enable contacts permission in settings.", "OK");
            return;
        }
        await PickContactiOS();

#else
        // Windows / unsupported platform
        await DisplayAlert("Not Supported",
            "Contact picker is only available on Android and iOS devices.\n\n" +
            "Please type the phone number manually in the search box.",
            "OK");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Contacts pick failed: {ex}");
                await DisplayAlert("Error", $"Could not access contacts: {ex.Message}", "OK");
            }
        }

#if ANDROID
private async Task PickContactAndroid()
{
    try
    {
        var status = await Permissions.RequestAsync<Permissions.ContactsRead>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Enable contacts permission in settings.", "OK");
            return;
        }

        // Use the cross-platform contact picker instead of Android-specific
        var selectedContact = await Communication.Contacts.Default.PickContactAsync();
        if (selectedContact == null) return;

        var phone = selectedContact.Phones?.FirstOrDefault();
        if (phone == null || string.IsNullOrWhiteSpace(phone.PhoneNumber))
        {
            await DisplayAlert("No Phone Number", "This contact has no phone number.", "OK");
            return;
        }

        if (PhoneEntryControl != null)
        {
            PhoneEntryControl.Text = phone.PhoneNumber;
            await PerformSearchAsync(phone.PhoneNumber);
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"PickContactAndroid error: {ex}");
        await DisplayAlert("Error", $"Could not pick contact: {ex.Message}", "OK");
    }
}
#endif

#if IOS || MACCATALYST
        private async Task PickContactiOS()
{
    try
    {
        var selectedContact = await Communication.Contacts.Default.PickContactAsync();
        if (selectedContact == null) return;

        var phone = selectedContact.Phones?.FirstOrDefault();
        if (phone == null || string.IsNullOrWhiteSpace(phone.PhoneNumber))
        {
            await DisplayAlert("No Phone Number", "This contact has no phone number.", "OK");
            return;
        }

        if (PhoneEntryControl != null)
        {
            PhoneEntryControl.Text = phone.PhoneNumber;
            await PerformSearchAsync(phone.PhoneNumber);
        }

        if (!string.IsNullOrWhiteSpace(selectedContact.DisplayName))
            await DisplayAlert("Contact Selected", $"Selected: {selectedContact.DisplayName}", "OK");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"PickContactiOS error: {ex}");
        await DisplayAlert("Error", $"Could not pick contact on iOS: {ex.Message}", "OK");
    }
}
#endif

        private async Task PickContactDefault()
        {
            try
            {
                var selectedContact = await Communication.Contacts.Default.PickContactAsync();
                if (selectedContact == null) return;

                var phone = selectedContact.Phones?.FirstOrDefault();
                if (phone == null || string.IsNullOrWhiteSpace(phone.PhoneNumber))
                {
                    await DisplayAlert("No Phone Number", "This contact has no phone number.", "OK");
                    return;
                }

                if (PhoneEntryControl != null)
                {
                    PhoneEntryControl.Text = phone.PhoneNumber;
                    await PerformSearchAsync(phone.PhoneNumber);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PickContactDefault error: {ex}");
                await DisplayAlert("Error", $"Could not pick contact: {ex.Message}", "OK");
            }
        }

        private async void ImportAllContacts_Clicked(object sender, EventArgs e)
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.ContactsRead>();
                if (status != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permission Denied", "Cannot access contacts without permission. Please enable contacts access in settings.", "OK");
                    return;
                }

                await DisplayAlert("Loading", "Loading your contacts... This may take a moment.", "OK");

                var contacts = await Communication.Contacts.Default.GetAllAsync();
                if (contacts == null || !contacts.Any())
                {
                    await DisplayAlert("No Contacts", "No contacts found on your device.", "OK");
                    return;
                }

                // Get all registered users from Supabase
                var allUsers = await SupabaseService.GetAsync<User>("Users", "");
                var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);

                var contactItems = new List<ContactItem>();

                foreach (var contact in contacts)
                {
                    foreach (var phone in contact.Phones)
                    {
                        if (!string.IsNullOrWhiteSpace(phone.PhoneNumber))
                        {
                            var normalizedPhone = NormalizePhoneNumber(phone.PhoneNumber);

                            var registeredUser = allUsers.FirstOrDefault(u =>
                                NormalizePhoneNumber(u.PhoneNumber ?? "") == normalizedPhone &&
                                u.PhoneNumber != currentUserPhone);

                            var contactItem = new ContactItem
                            {
                                DisplayName = string.IsNullOrWhiteSpace(contact.DisplayName)
                                    ? FormatContactName(phone.PhoneNumber)
                                    : contact.DisplayName,
                                PhoneNumber = phone.PhoneNumber,
                                ProfileImage = registeredUser?.ProfileImagePath ?? "https://ui-avatars.com/api/?name=" + Uri.EscapeDataString(contact.DisplayName ?? "Contact") + "&background=333&color=fff",
                                IsRegistered = registeredUser != null,
                                RegisteredUser = registeredUser
                            };

                            contactItems.Add(contactItem);
                            break;
                        }
                    }
                }

                contactItems = contactItems.OrderBy(c => c.DisplayName).ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AllContacts = new ObservableCollection<ContactItem>(contactItems);

                    if (ResultCountLabelControl != null)
                    {
                        var registeredCount = contactItems.Count(c => c.IsRegistered);
                        var unregisteredCount = contactItems.Count(c => !c.IsRegistered);

                        ResultCountLabelControl.Text = $"{contactItems.Count} contacts loaded • {registeredCount} on Lock • {unregisteredCount} to invite";
                        ResultCountLabelControl.TextColor = Color.FromArgb("#4CAF50");
                        ResultCountLabelControl.IsVisible = true;
                    }

                    _suggestions.Clear();
                    if (SuggestionsCollectionViewControl != null)
                        SuggestionsCollectionViewControl.IsVisible = false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import contacts failed: {ex}");
                await DisplayAlert("Error", $"Could not import contacts: {ex.Message}", "OK");
            }
        }

        private async Task<Conversation> GetOrCreateConversationAsync(string userPhone, string otherPhone)
        {
            try
            {
                // Check if conversation already exists
                var existingConvs = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"and(ParticipantA.eq.{Uri.EscapeDataString(userPhone)},ParticipantB.eq.{Uri.EscapeDataString(otherPhone)})," +
                    $"and(ParticipantA.eq.{Uri.EscapeDataString(otherPhone)},ParticipantB.eq.{Uri.EscapeDataString(userPhone)})");

                if (existingConvs.Any())
                {
                    return existingConvs.First();
                }

                // Create new conversation
                var newConversation = new Conversation
                {
                    ConversationId = Guid.NewGuid().ToString(),
                    ParticipantA = userPhone,
                    ParticipantB = otherPhone,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageAt = DateTime.UtcNow,
                    LastMessagePreview = string.Empty
                };

                var inserted = await SupabaseService.InsertAndReturnAsync<Conversation>("Conversations", newConversation);
                return inserted ?? newConversation;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrCreateConversationAsync error: {ex}");
                throw;
            }
        }

        private async void Contact_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection.FirstOrDefault() is ContactItem selected)
                {
                    await HandleContactSelection(selected);

                    if (sender is CollectionView cv)
                        cv.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Contact_SelectionChanged error: {ex}");
            }
        }

        private async void ContactItem_Tapped(object sender, EventArgs e)
        {
            try
            {
                var contact = (sender as TapGestureRecognizer)?.CommandParameter as ContactItem;
                if (contact != null)
                {
                    await HandleContactSelection(contact);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ContactItem_Tapped error: {ex}");
            }
        }

        private async Task HandleContactSelection(ContactItem contact)
        {
            try
            {
                if (contact.IsRegistered && contact.RegisteredUser != null)
                {
                    var me = Preferences.Get(CurrentUserPhoneKey, string.Empty).Trim();
                    var target = contact.RegisteredUser.PhoneNumber;

                    await Lock.Chat.Services.DatabaseService.InitializeAsync();
                    var conv = await ChatRepository.GetOrCreateConversationAsync(me, target);

                    var route = $"chat?conversationId={Uri.EscapeDataString(conv.ConversationId)}&otherPhone={Uri.EscapeDataString(target)}";

                    try { await Navigation.PopModalAsync(); } catch { }
                    await Shell.Current.GoToAsync(route);
                }
                else
                {
                    var action = await DisplayActionSheet(
                        $"Invite {contact.DisplayName}",
                        "Cancel",
                        null,
                        "Send SMS Invite",
                        "Copy Number",
                        "View in Contacts");

                    switch (action)
                    {
                        case "Send SMS Invite":
                            var message = "Hey! Join me on Lock app - let's chat there!";
                            await Launcher.OpenAsync($"sms:{contact.PhoneNumber}?body={Uri.EscapeDataString(message)}");
                            break;

                        case "Copy Number":
                            await Clipboard.SetTextAsync(contact.PhoneNumber);
                            await DisplayAlert("Copied", "Phone number copied to clipboard", "OK");
                            break;

                        case "View in Contacts":
                            await DisplayAlert("Contact", $"{contact.DisplayName}\n{contact.PhoneNumber}", "OK");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleContactSelection error: {ex}");
                await DisplayAlert("Error", $"Could not process: {ex.Message}", "OK");
            }
        }

        private void FriendSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection.Count == 0) return;

                if (e.CurrentSelection[0] is SearchResult selectedFriend)
                {
                    if (PhoneEntryControl != null)
                        PhoneEntryControl.Text = selectedFriend.PhoneNumber;

                    _selectedSearchResult = selectedFriend;
                    SelectedUserIsRegistered = selectedFriend.IsRegistered;

                    if (sender is CollectionView cv)
                        cv.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Friend selection failed: {ex}");
            }
        }

        private void AlphabetTapped(object sender, EventArgs e)
        {
            try
            {
                var label = sender as Label;
                if (label == null) return;

                var letter = label.Text;
                if (string.IsNullOrEmpty(letter)) return;

                var groupedFriends = GroupedFriends;
                if (groupedFriends != null && FriendsCollectionViewControl != null)
                {
                    for (int i = 0; i < groupedFriends.Count; i++)
                    {
                        var group = groupedFriends[i];
                        if (group != null && group.Key == letter)
                        {
                            FriendsCollectionViewControl.ScrollTo(i, -1, ScrollToPosition.Start, false);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alphabet tap failed: {ex}");
            }
        }

        private async void UnregisteredMessageIcon_Tapped(object sender, EventArgs e)
        {
            try
            {
                SearchResult? result = null;

                if (sender is TapGestureRecognizer tap)
                {
                    result = tap.CommandParameter as SearchResult;
                }

                if (result?.User == null && sender is VisualElement ve)
                {
                    result = ve.BindingContext as SearchResult;
                }

                if (result?.User == null || string.IsNullOrWhiteSpace(result.User.PhoneNumber))
                {
                    await DisplayAlert("Error", "Contact not found.", "OK");
                    return;
                }

                var target = result.User.PhoneNumber;

                var action = await DisplayActionSheet(
                    "Invite to Lock",
                    "Cancel",
                    null,
                    "Send SMS Invite",
                    "Copy Number",
                    "Save to Contacts");

                switch (action)
                {
                    case "Send SMS Invite":
                        var message = "Hey! Join me on Lock app - let's chat there!";
                        await Launcher.OpenAsync($"sms:{target}?body={Uri.EscapeDataString(message)}");
                        break;

                    case "Copy Number":
                        await Clipboard.SetTextAsync(target);
                        await DisplayAlert("Copied", "Phone number copied to clipboard", "OK");
                        break;

                    case "Save to Contacts":
                        await DisplayAlert("Info", "Contacts integration coming soon", "OK");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UnregisteredMessageIcon_Tapped error: {ex}");
                await DisplayAlert("Error", $"Could not process: {ex.Message}", "OK");
            }
        }

        private async Task CloseModalAsync()
        {
            try
            {
                await Navigation.PopModalAsync();
            }
            catch { }
        }

        private async void OnPageLoaded(object sender, EventArgs e)
        {
            try
            {
                IsRecentTabActive = true;
                IsSearchTabActive = false;

                if (SuggestionsCollectionViewControl != null)
                    SuggestionsCollectionViewControl.ItemsSource = _suggestions;

                if (RecentChatsCollectionViewControl != null)
                    RecentChatsCollectionViewControl.ItemsSource = FilteredRecentChats;

                if (RecentTabButton != null)
                    RecentTabButton.IsVisible = true;
                if (SearchTabButton != null)
                    SearchTabButton.IsVisible = true;

                if (MoodPickerControl != null && MoodPickerControl.Items.Count > 0)
                    MoodPickerControl.SelectedIndex = 0;

                await LoadUniqueLocationsAsync();
                await LoadMyOwnMoodAsync();
                await LoadFriendsList();
                await LoadRecentChatsAsync();

                GetImportedUnregisteredContacts();

                MessagingCenter.Subscribe<object, string>(this, "LocationListUpdated", async (sender, location) =>
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Location list updated in NewChatPage: {location}");
                        await LoadLocationsFromPreferencesAsync();
                    });
                });

                MessagingCenter.Subscribe<object>(this, "ProfileUpdated", async (sender) =>
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("Profile updated, refreshing locations...");
                        await LoadUniqueLocationsAsync();
                    });
                });

                UpdateTabContent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnPageLoaded error: {ex}");
                await DisplayAlert("Error", "Could not open New Chat", "OK");
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}