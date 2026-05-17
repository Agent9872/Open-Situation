using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Helpers;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Pages.Chat.Popups;
using Lock.Pages.Post;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class ConversationsPage : ContentPage
    {
        private const string CurrentUserPhoneKey = "current_user_phone";
        private const string ConversationListsKey = "conversation_lists";
        private const string MessageRequestsKey = "message_requests";

        private HashSet<string> _unlockedConversations = new HashSet<string>();

        private enum ConversationFilter
        {
            All, Pinned, Starred, Unread, Archived, MessageRequest, Groups, Lists
        }

        private ConversationFilter _filter = ConversationFilter.All;
        private string _activeListName = string.Empty;

        public class ConversationItem
        {
            public Conversation Conversation { get; set; } = default!;
            public string OtherPhone { get; set; } = string.Empty;
            public string OtherName { get; set; } = string.Empty;
            public string OtherProfileImage { get; set; } = string.Empty;
            public string Mood { get; set; } = string.Empty;
            public DateTime MoodLastUpdated { get; set; }
            public string MoodLastUpdatedRelative { get; set; } = string.Empty;
            public string ListName { get; set; } = string.Empty;
            public bool IsArchived { get; set; }
            public bool IsMessageRequest { get; set; }
            public int UnreadCount { get; set; }
            public bool HasUnread => UnreadCount > 0;
            public string UnreadDisplay => UnreadCount > 99 ? "99+" : UnreadCount.ToString();
            public int TotalPostsCount { get; set; }
            public bool HasPosts => TotalPostsCount > 0;
            public string TotalPostsDisplay => TotalPostsCount > 99 ? "99+" : TotalPostsCount.ToString();
            public int UnreadPostsCount { get; set; }
            public bool HasUnreadPosts => UnreadPostsCount > 0;
            public string UnreadPostsDisplay => UnreadPostsCount > 99 ? "99+" : UnreadPostsCount.ToString();
            public bool IsOnline { get; set; }
            public bool IsGroupChat { get; set; }
            public int MatchPercent { get; set; }
        }

        private ObservableCollection<ConversationItem> _items = new();
        private readonly List<ConversationItem> _allItems = new();

        private string? _searchQuery;
        private Conversation? _overlayConversation;
        private bool _overlayBusy;
        private CollectionView? ConversationsCv => this.FindByName<CollectionView>("ConversationsCollectionView");

        private readonly record struct TabCounts(int All, int Pinned, int Starred, int Unread, int MessageRequest);

        private bool _isNavigating = false;
        public bool HasArchivedConversations { get; set; }

        private DateTime _lastFullLoad = DateTime.MinValue;
        private Dictionary<string, int> _cachedTotalPostsCounts = new();
        private Dictionary<string, int> _cachedUnreadPostsCounts = new();
        private bool _isLoadingConversations = false;

        private DateTime _lastNavigationOut = DateTime.MinValue;
        private bool _skipNextFullLoad = false;
        private string _lastActiveTabKey = string.Empty;

        // ?? XAML TAB HANDLERS ??????????????????????????????????????????
        private void OnAllTabClicked(object s, EventArgs e) => FilterTabClicked("All");
        private void OnPinnedTabClicked(object s, EventArgs e) => FilterTabClicked("Pinned");
        private void OnStarredTabClicked(object s, EventArgs e) => FilterTabClicked("Starred");
        private void OnUnreadTabClicked(object s, EventArgs e) => FilterTabClicked("Unread");
        private void OnRequestTabClicked(object s, EventArgs e) => FilterTabClicked("MessageRequest");
        private void OnGroupsTabClicked(object s, EventArgs e) => FilterTabClicked("Groups");

        // ?? FIX 1: ExploreGroupsButton is a Border in XAML, not a Button ??
        private bool _isExploreButtonVisible = false;
        public bool IsExploreButtonVisible
        {
            get => _isExploreButtonVisible;
            set
            {
                _isExploreButtonVisible = value;
                OnPropertyChanged(nameof(IsExploreButtonVisible));
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Correct type: Border, not Button
                    var exploreBtn = this.FindByName<Border>("ExploreGroupsButton");
                    if (exploreBtn != null)
                        exploreBtn.IsVisible = value;
                });
            }
        }

        private void SaveCurrentTabState()
        {
            if (_filter == ConversationFilter.Lists && !string.IsNullOrEmpty(_activeListName))
                _lastActiveTabKey = "List:" + _activeListName;
            else
                _lastActiveTabKey = _filter.ToString();
        }

        public ConversationsPage()
        {
            EnsureInitializeComponent();
            Shell.SetNavBarIsVisible(this, false);
            this.Appearing += async (s, e) =>
            {
                if (!await IsUserLoggedIn())
                    await Shell.Current.GoToAsync("///LoginPage");
            };
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            var cv = ConversationsCv;
            if (cv != null && cv.ItemsSource == null)
                cv.ItemsSource = _items;
        }

        private void EnsureInitializeComponent()
        {
            var mi = this.GetType().GetMethod("InitializeComponent",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (mi != null) { mi.Invoke(this, null); return; }
            Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(this, this.GetType());
        }

        private async void NewChatFab_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var newChatPage = new NewChatPage();
                newChatPage.ParentNavigation = this.Navigation;
                await Navigation.PushModalAsync(newChatPage, animated: true);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not open New Chat: " + ex.Message, "OK");
            }
        }

        private async Task UpdateBottomNavChatBadge()
        {
            try
            {
                var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    SetBottomNavChatBadgeVisibility(false);
                    return;
                }

                int conversationsWithUnread = _allItems.Count(i =>
                    !i.IsArchived && !i.IsMessageRequest && i.UnreadCount > 0);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var chatBadge = this.FindByName<Border>("ChatBadge");
                    var chatBadgeLabel = this.FindByName<Label>("ChatBadgeLabel");
                    if (chatBadge != null && chatBadgeLabel != null)
                    {
                        chatBadge.IsVisible = conversationsWithUnread > 0;
                        chatBadgeLabel.Text = conversationsWithUnread > 99 ? "99+" : conversationsWithUnread.ToString();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating bottom nav chat badge: {ex}");
                SetBottomNavChatBadgeVisibility(false);
            }
        }

        private void SetBottomNavChatBadgeVisibility(bool isVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var chatBadge = this.FindByName<Border>("ChatBadge");
                if (chatBadge != null) chatBadge.IsVisible = isVisible;
            });
        }

        private async void ConversationMenuButton_Clicked(object? sender, EventArgs e)
        {
            try
            {
                ConversationItem? item = null;

                if (sender is Button btn)
                {
                    if (btn.CommandParameter is ConversationItem paramItem)
                        item = paramItem;
                    else if (btn.BindingContext is ConversationItem bindingItem)
                        item = bindingItem;
                }

                if (item == null && sender is TapGestureRecognizer tapRecognizer)
                {
                    if (tapRecognizer.CommandParameter is ConversationItem tapItem)
                        item = tapItem;
                }

                if (item == null)
                {
                    await DisplayAlert("Error", "Could not open menu for this conversation", "OK");
                    return;
                }

                _overlayConversation = item.Conversation;

                var preview = string.IsNullOrWhiteSpace(item.Conversation.LastMessagePreview)
                    ? "—" : item.Conversation.LastMessagePreview;

                var previewLabel = this.FindByName<Label>("OverlayPreviewLabel");
                if (previewLabel != null) previewLabel.Text = preview;

                UpdateOverlayActionTexts(item);

                var overlay = this.FindByName<Grid>("ActionsOverlay");
                if (overlay != null) overlay.IsVisible = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening actions: {ex}");
                await DisplayAlert("Error", "Could not open conversation menu: " + ex.Message, "OK");
            }
        }

        private void UpdateOverlayActionTexts(ConversationItem item)
        {
            try
            {
                var overlay = this.FindByName<Grid>("ActionsOverlay");
                if (overlay == null) return;

                foreach (var child in overlay.Children)
                {
                    if (child is Border border && border.Content is ScrollView scrollView)
                    {
                        var scrollContent = scrollView.Content as VerticalStackLayout;
                        if (scrollContent == null) continue;

                        foreach (var layoutChild in scrollContent.Children)
                        {
                            if (layoutChild is not Grid actionGrid || actionGrid.Children.Count <= 1) continue;

                            var label = actionGrid.Children.OfType<Label>().FirstOrDefault();
                            var gesture = actionGrid.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
                            if (label == null || gesture == null) continue;

                            switch (label.Text)
                            {
                                case string s when s.Contains("Archive") || s.Contains("Unarchive"):
                                    label.Text = item.IsArchived ? "Unarchive chat" : "Archive chat";
                                    gesture.CommandParameter = item.IsArchived ? "Unarchive chat" : "Archive chat";
                                    break;
                                case string s when s.Contains("Mute") || s.Contains("Unmute"):
                                    label.Text = item.Conversation.IsMuted ? "Unmute notifications" : "Mute notifications";
                                    gesture.CommandParameter = item.Conversation.IsMuted ? "Unmute notifications" : "Mute notifications";
                                    break;
                                case string s when s.Contains("Pin") || s.Contains("Unpin"):
                                    label.Text = item.Conversation.IsPinned ? "Unpin chat" : "Pin chat";
                                    gesture.CommandParameter = item.Conversation.IsPinned ? "Unpin chat" : "Pin chat";
                                    break;
                                case string s when s.Contains("favorites") || s.Contains("Remove from favorites"):
                                    label.Text = item.Conversation.IsStarred ? "Remove from favorites" : "Add to favorites";
                                    gesture.CommandParameter = item.Conversation.IsStarred ? "Remove from favorites" : "Add to favorites";
                                    break;
                                case string s when s.Contains("Block") || s.Contains("Unblock"):
                                    _ = UpdateBlockActionTextAsync(actionGrid, label, gesture);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating overlay action texts: {ex}");
            }
        }

        private async Task UpdateBlockActionTextAsync(Grid actionGrid, Label label, TapGestureRecognizer gesture)
        {
            try
            {
                bool iBlockedThem = await HaveIBlockedThisUserAsync();
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    label.Text = iBlockedThem ? "Unblock" : "Block";
                    gesture.CommandParameter = iBlockedThem ? "Unblock" : "Block";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating block action text: {ex}");
            }
        }

        private async Task<bool> HaveIBlockedThisUserAsync()
        {
            if (_overlayConversation == null) return false;
            var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
            if (string.IsNullOrEmpty(me)) return false;
            string otherPhone = _overlayConversation.ParticipantA == me
                ? _overlayConversation.ParticipantB
                : _overlayConversation.ParticipantA;
            return await ChatRepository.IsUserBlockedAsync(me, otherPhone);
        }

        private async void OpenArchiveButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var page = new ArchivePage();
                await Navigation.PushModalAsync(page);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Could not open Archive: " + ex.Message, "OK");
            }
        }

        private HashSet<string> LoadMessageRequests()
        {
            try
            {
                var json = Preferences.Get(MessageRequestsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return new HashSet<string>();
                return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
            }
            catch { return new HashSet<string>(); }
        }

        private async Task LoadConversationsAsync(bool forceFullLoad = false)
        {
            if (_isLoadingConversations) return;
            _isLoadingConversations = true;

            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(me))
                {
                    _allItems.Clear();
                    _items.Clear();
                    try { await UpdateArchiveBadge(0); } catch { }
                    BuildTabs(new Dictionary<string, string>(), new TabCounts(0, 0, 0, 0, 0));
                    return;
                }

                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();

                bool needsFullLoad = forceFullLoad || (DateTime.UtcNow - _lastFullLoad).TotalSeconds > 30;

                var ghostedPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var allUsers = await db.Table<Lock.Models.User>().ToListAsync();
                    ghostedPhones = allUsers
                        .Where(u => u.GhostModeMoodShield)
                        .Select(u => (u.PhoneNumber ?? "").Trim())
                        .Where(p => !string.IsNullOrEmpty(p) && !string.Equals(p, me, StringComparison.OrdinalIgnoreCase))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex) { Debug.WriteLine($"Ghost mode filter load error: {ex.Message}"); }

                var convs = await ChatRepository.GetConversationsForUserAsync(me);

                Dictionary<string, int> totalPostsCounts;
                Dictionary<string, int> unreadPostsCounts;

                if (needsFullLoad)
                {
                    var notificationPreferences = new Dictionary<string, bool>();
                    bool anyNotificationsEnabled = notificationPreferences.Any(kv => kv.Value);
                    var allPosts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();

                    var regularPosts = allPosts.Where(p =>
                        (!string.IsNullOrEmpty(p.Content) && p.Content.Length > 3) ||
                        (p.ImagePathsList != null && p.ImagePathsList.Any() && string.IsNullOrEmpty(p.StatusImagePath)) ||
                        (!string.IsNullOrEmpty(p.Content) && p.ImagePathsList != null && p.ImagePathsList.Any())
                    ).ToList();

                    totalPostsCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var post in regularPosts)
                    {
                        if (string.IsNullOrEmpty(post.AuthorPhone)) continue;
                        var phone = post.AuthorPhone.Trim();
                        if (phone.Contains("·"))
                        {
                            var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                            phone = parts.Length > 1 ? parts[1].Trim() : phone;
                        }
                        if (totalPostsCounts.ContainsKey(phone)) totalPostsCounts[phone]++;
                        else totalPostsCounts[phone] = 1;
                    }

                    unreadPostsCounts = await PostRepository.GetUnreadPostsCountAsync(me, notificationPreferences);

                    if (!anyNotificationsEnabled)
                    {
                        totalPostsCounts = totalPostsCounts.Keys.ToDictionary(k => k, k => 0);
                        unreadPostsCounts = unreadPostsCounts.Keys.ToDictionary(k => k, k => 0);
                    }

                    _cachedTotalPostsCounts = totalPostsCounts;
                    _cachedUnreadPostsCounts = unreadPostsCounts;
                    _lastFullLoad = DateTime.UtcNow;
                }
                else
                {
                    totalPostsCounts = _cachedTotalPostsCounts;
                    unreadPostsCounts = _cachedUnreadPostsCounts;
                }

                var messageRequests = await ChatRepository.GetMessageRequestsAsync(me);
                var pendingRequests = await ChatRepository.GetPendingMessageRequestsAsync(me);

                var requestGroups = messageRequests
                    .GroupBy(m => m.ConversationId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var pendingRequestDict = pendingRequests
                    .ToDictionary(r => r.ConversationId, r => r);

                convs = convs.OrderByDescending(c => c.IsPinned)
                             .ThenByDescending(c => c.LastMessageAt)
                             .ToList();

                var listsMap = LoadConversationLists();
                var allItemsList = new List<ConversationItem>();

                var allUserPhones = convs
                    .Select(c => c.ParticipantA == me ? c.ParticipantB : c.ParticipantA)
                    .Where(p => p != me && !string.IsNullOrEmpty(p))
                    .Where(p =>
                    {
                        var clean = p.Contains("·")
                            ? p.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Trim() ?? p.Trim()
                            : p.Trim();
                        return !ghostedPhones.Contains(clean);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var req in pendingRequests)
                {
                    var senderPhone = (req.SenderPhone ?? "").Trim();
                    if (!ghostedPhones.Contains(senderPhone) && senderPhone != me && !allUserPhones.Contains(senderPhone))
                        allUserPhones.Add(senderPhone);
                }

                var userMoodMap = new Dictionary<string, (string Mood, DateTime MoodLastUpdated)>(StringComparer.OrdinalIgnoreCase);
                foreach (var phone in allUserPhones)
                {
                    try
                    {
                        var user = await db.Table<Lock.Models.User>()
                            .Where(u => u.PhoneNumber == phone).FirstOrDefaultAsync();

                        if (user != null)
                        {
                            userMoodMap[phone] = (user.Mood ?? string.Empty,
                                user.MoodLastUpdated != DateTime.MinValue ? user.MoodLastUpdated : DateTime.UtcNow);
                        }
                        else
                        {
                            var post = await db.Table<Lock.Models.Post>()
                                .Where(p => p.AuthorPhone == phone)
                                .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync();
                            userMoodMap[phone] = post != null
                                ? (post.Mood ?? string.Empty, post.MoodLastUpdated != DateTime.MinValue ? post.MoodLastUpdated : DateTime.UtcNow)
                                : (string.Empty, DateTime.UtcNow);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading user data for {phone}: {ex.Message}");
                        userMoodMap[phone] = (string.Empty, DateTime.UtcNow);
                    }
                }

                foreach (var c in convs)
                {
                    var other = c.ParticipantA == me ? c.ParticipantB : c.ParticipantA;
                    var cleanOtherForGhost = other.Contains("·")
                        ? other.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Trim() ?? other.Trim()
                        : other.Trim();

                    if (ghostedPhones.Contains(cleanOtherForGhost)) continue;
                    if (other == me) continue;

                    var displayName = other;
                    var avatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(displayName)}&background=2F3337&color=E6E6E6&size=128";

                    Lock.Models.User? user = null;
                    try
                    {
                        user = await db.Table<Lock.Models.User>().Where(u => u.PhoneNumber == other).FirstOrDefaultAsync();
                        if (user != null)
                        {
                            displayName = string.IsNullOrEmpty(user.Name) ? other : user.Name;
                            if (!string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                                avatarUrl = user.ProfileImagePath;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error loading user {other}: {ex.Message}"); }

                    int unreadCount = 0;
                    try
                    {
                        unreadCount = await db.Table<ChatMessage>()
                            .Where(m => m.ConversationId == c.ConversationId &&
                                       m.RecipientPhone == me &&
                                       m.IsRead == false &&
                                       m.IsMessageRequest == false &&
                                       !m.IsDeclined)
                            .CountAsync();
                    }
                    catch (Exception ex) { Debug.WriteLine($"Error getting unread count: {ex.Message}"); }

                    bool isMessageRequest = requestGroups.ContainsKey(c.ConversationId) || pendingRequestDict.ContainsKey(c.ConversationId);

                    bool isOnline = false;
                    if (user != null)
                        isOnline = (DateTime.UtcNow - user.LastActive).TotalMinutes < 5;

                    string cleanOther = other.Contains("·")
                        ? other.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Trim() ?? other.Trim()
                        : other.Trim();

                    string mood = string.Empty;
                    DateTime moodLastUpdated = DateTime.UtcNow;
                    string moodLastUpdatedRelative = string.Empty;

                    if (userMoodMap.TryGetValue(cleanOther, out var moodData))
                    {
                        mood = moodData.Mood;
                        moodLastUpdated = moodData.MoodLastUpdated;
                        moodLastUpdatedRelative = GetRelativeTimeString(moodLastUpdated);
                    }

                    int totalPostsCount = totalPostsCounts.TryGetValue(cleanOther, out int totalCount) ? totalCount : 0;
                    int unreadPostsCount = unreadPostsCounts.TryGetValue(cleanOther, out int unreadPostValue) ? unreadPostValue : 0;

                    int matchPercent = 0;
                    if (user != null)
                    {
                        try
                        {
                            var currentUser = await db.Table<Lock.Models.User>().Where(u => u.PhoneNumber == me).FirstOrDefaultAsync();
                            if (currentUser != null)
                                matchPercent = await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, user);
                        }
                        catch (Exception ex) { Debug.WriteLine($"Error calculating match percentage: {ex.Message}"); }
                    }

                    string previewText = c.LastMessagePreview;
                    ChatMessage? lastMsgForPrefix = null;
                    try
                    {
                        lastMsgForPrefix = await db.Table<ChatMessage>()
                            .Where(m => m.ConversationId == c.ConversationId && !m.IsDeclined)
                            .OrderByDescending(m => m.SentAt).FirstOrDefaultAsync();
                    }
                    catch { }

                    if (previewText?.Contains("Encrypted") == true || previewText?.Contains("??") == true)
                    {
                        try
                        {
                            var lastMsg = await db.Table<ChatMessage>()
                                .Where(m => m.ConversationId == c.ConversationId && !m.IsDeclined)
                                .OrderByDescending(m => m.SentAt).FirstOrDefaultAsync();

                            if (lastMsg != null)
                            {
                                if (lastMsg.MessageType == "post")
                                    previewText = string.IsNullOrEmpty(lastMsg.PostPreview) ? "Shared a post" : "Post: " + lastMsg.PostPreview;
                                else if (lastMsg.MessageType == "contact")
                                    previewText = string.IsNullOrEmpty(lastMsg.ContactName) ? "Shared contact" : "Contact: " + lastMsg.ContactName;
                                else if (lastMsg.IsVoiceMessage)
                                    previewText = "Voice message";
                                else if (!string.IsNullOrEmpty(lastMsg.MediaPath) && lastMsg.MediaType == "image")
                                    previewText = lastMsg.MediaItems != null && lastMsg.MediaItems.Count > 1 ? $"{lastMsg.MediaItems.Count} images" : "Image";
                                else if (!string.IsNullOrWhiteSpace(lastMsg.Content))
                                    previewText = lastMsg.IsEncrypted && !string.IsNullOrEmpty(lastMsg.EncryptionIV)
                                        ? "New message"
                                        : (lastMsg.Content.Length > 120 ? lastMsg.Content.Substring(0, 120) + "…" : lastMsg.Content);
                                else
                                    previewText = "New message";
                            }
                        }
                        catch { previewText = "Message"; }
                    }

                    if (lastMsgForPrefix != null && lastMsgForPrefix.SenderPhone == me && !isMessageRequest)
                        previewText = "You: " + previewText;

                    var item = new ConversationItem
                    {
                        Conversation = c,
                        OtherPhone = other,
                        OtherName = displayName,
                        OtherProfileImage = avatarUrl,
                        UnreadCount = unreadCount,
                        TotalPostsCount = totalPostsCount,
                        UnreadPostsCount = unreadPostsCount,
                        ListName = listsMap.TryGetValue(c.ConversationId, out var ln) ? ln : string.Empty,
                        IsArchived = c.IsArchived,
                        IsMessageRequest = isMessageRequest,
                        IsOnline = isOnline,
                        Mood = mood,
                        MoodLastUpdated = moodLastUpdated,
                        MoodLastUpdatedRelative = moodLastUpdatedRelative,
                        IsGroupChat = false,
                        MatchPercent = matchPercent
                    };
                    item.Conversation.LastMessagePreview = previewText;
                    allItemsList.Add(item);
                }

                // Process pending message requests
                foreach (var request in pendingRequests)
                {
                    var cleanSenderForGhost = (request.SenderPhone ?? "").Trim();
                    if (ghostedPhones.Contains(cleanSenderForGhost)) continue;

                    bool exists = allItemsList.Any(i => i.Conversation.ConversationId == request.ConversationId);
                    if (!exists)
                    {
                        var firstMsg = messageRequests
                            .Where(m => m.ConversationId == request.ConversationId)
                            .OrderBy(m => m.SentAt).FirstOrDefault();

                        if (firstMsg != null)
                        {
                            var senderPhone = firstMsg.SenderPhone;
                            var senderName = senderPhone;
                            var avatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(senderPhone)}&background=2F3337&color=E6E6E6&size=128";

                            try
                            {
                                var user = await db.Table<Lock.Models.User>().Where(u => u.PhoneNumber == senderPhone).FirstOrDefaultAsync();
                                if (user != null)
                                {
                                    senderName = string.IsNullOrEmpty(user.Name) ? senderPhone : user.Name;
                                    if (!string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                                        avatarUrl = user.ProfileImagePath;
                                }
                            }
                            catch { }

                            string cleanSender = senderPhone.Contains("·")
                                ? senderPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Trim() ?? senderPhone.Trim()
                                : senderPhone.Trim();

                            string mood = string.Empty;
                            DateTime moodLastUpdated = DateTime.UtcNow;
                            string moodLastUpdatedRelative = string.Empty;
                            if (userMoodMap.TryGetValue(cleanSender, out var moodData))
                            {
                                mood = moodData.Mood;
                                moodLastUpdated = moodData.MoodLastUpdated;
                                moodLastUpdatedRelative = GetRelativeTimeString(moodLastUpdated);
                            }

                            string requestPreview = request.MessagePreview ?? "Message request";
                            if (firstMsg.MessageType == "post")
                                requestPreview = string.IsNullOrEmpty(firstMsg.PostPreview) ? "Shared a post" : "Post: " + firstMsg.PostPreview;
                            else if (firstMsg.IsVoiceMessage)
                                requestPreview = "Voice message";
                            else if (!string.IsNullOrWhiteSpace(firstMsg.Content))
                                requestPreview = firstMsg.Content.Length > 120 ? firstMsg.Content.Substring(0, 120) + "…" : firstMsg.Content;

                            var tempConv = new Conversation
                            {
                                ConversationId = request.ConversationId,
                                ParticipantA = request.SenderPhone,
                                ParticipantB = request.RecipientPhone,
                                LastMessageAt = request.RequestedAt,
                                LastMessagePreview = requestPreview,
                                CreatedAt = request.RequestedAt,
                            };

                            allItemsList.Add(new ConversationItem
                            {
                                Conversation = tempConv,
                                OtherPhone = senderPhone,
                                OtherName = $"?? {senderName}",
                                OtherProfileImage = avatarUrl,
                                UnreadCount = 1,
                                IsMessageRequest = true,
                                IsOnline = false,
                                Mood = mood,
                                MoodLastUpdated = moodLastUpdated,
                                MoodLastUpdatedRelative = moodLastUpdatedRelative,
                                IsGroupChat = false,
                            });
                        }
                    }
                }

                // Load Groups
                try
                {
                    await GroupDatabaseService.InitializeAsync();
                    var groupDb = GroupDatabaseService.GetConnection();

                    var memberships = await groupDb.Table<GroupMember>().Where(m => m.UserPhone == me).ToListAsync();

                    if (memberships.Any())
                    {
                        var groupIds = memberships.Select(m => m.GroupId).Distinct().ToList();

                        foreach (var groupId in groupIds)
                        {
                            var group = await groupDb.Table<Group>().Where(g => g.Id == groupId).FirstOrDefaultAsync();
                            if (group == null || !group.IsActive) continue;

                            var memberInfo = memberships.First(m => m.GroupId == groupId);

                            int unreadCount = 0;
                            try
                            {
                                unreadCount = await groupDb.Table<GroupMessage>()
                                    .Where(m => m.GroupId == groupId && m.SenderPhone != me &&
                                               m.SentAt > memberInfo.LastReadAt && !m.IsDeleted)
                                    .CountAsync();
                            }
                            catch { }

                            string lastMessagePreview = "No messages yet";
                            DateTime lastMessageAt = group.CreatedAt;
                            try
                            {
                                var lastMsg = await groupDb.Table<GroupMessage>()
                                    .Where(m => m.GroupId == groupId && !m.IsDeleted)
                                    .OrderByDescending(m => m.SentAt).FirstOrDefaultAsync();

                                if (lastMsg != null)
                                {
                                    string messageText = await GetDecryptedGroupMessageContent(lastMsg, groupId);
                                    if (string.IsNullOrEmpty(messageText))
                                    {
                                        messageText = lastMsg.MessageType switch
                                        {
                                            GroupMessageType.Image => "Photo",
                                            GroupMessageType.Poll => "Poll",
                                            GroupMessageType.Voice => "Voice message",
                                            GroupMessageType.Event => "Event",
                                            _ => "New message"
                                        };
                                    }
                                    if (messageText.Length > 50) messageText = messageText.Substring(0, 47) + "...";
                                    lastMessagePreview = $"{lastMsg.DisplaySenderName}: {messageText}";
                                    lastMessageAt = lastMsg.SentAt;
                                }
                            }
                            catch { }

                            var groupConversation = new Conversation
                            {
                                ConversationId = $"group_{groupId}",
                                ParticipantA = me,
                                ParticipantB = group.Name,
                                LastMessageAt = lastMessageAt,
                                LastMessagePreview = lastMessagePreview,
                                CreatedAt = group.CreatedAt,
                            };

                            allItemsList.Add(new ConversationItem
                            {
                                Conversation = groupConversation,
                                OtherPhone = groupId,
                                OtherName = group.Name,
                                OtherProfileImage = string.IsNullOrEmpty(group.CoverImagePath)
                                    ? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(group.Name)}&background=008080&color=FFFFFF&size=128"
                                    : group.CoverImagePath,
                                UnreadCount = unreadCount,
                                IsGroupChat = true,
                            });
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error loading groups: {ex.Message}"); }

                // Sort
                allItemsList = allItemsList
                    .OrderByDescending(i => i.IsMessageRequest)
                    .ThenByDescending(i => i.Conversation.IsPinned)
                    .ThenByDescending(i => i.Conversation.LastMessageAt)
                    .ToList();

                _allItems.Clear();
                foreach (var item in allItemsList) _allItems.Add(item);

                var counts = new TabCounts(
                    _allItems.Count(i => !i.IsArchived && !i.IsMessageRequest),
                    _allItems.Count(i => i.Conversation.IsPinned && !i.IsArchived && !i.IsMessageRequest),
                    _allItems.Count(i => i.Conversation.IsStarred && !i.IsArchived && !i.IsMessageRequest),
                    _allItems.Count(i => !i.IsArchived && !i.IsMessageRequest && i.UnreadCount > 0),
                    _allItems.Count(i => i.IsMessageRequest && !i.IsArchived)
                );

                try
                {
                    var archivedCount = _allItems.Count(i => i.IsArchived);
                    await UpdateArchiveBadge(archivedCount);
                    HasArchivedConversations = archivedCount > 0;
                    OnPropertyChanged(nameof(HasArchivedConversations));
                }
                catch { }

                BuildTabs(listsMap, counts);
                ApplySearchFilter();
                await UpdateBottomNavChatBadge();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadConversationsAsync error: {ex}");
                await DisplayAlert("Error", "Failed to load conversations: " + ex.Message, "OK");
            }
            finally
            {
                _isLoadingConversations = false;
            }
        }

        private async Task<string> GetDecryptedGroupMessageContent(GroupMessage message, string groupId)
        {
            try
            {
                if (!message.IsEncrypted) return message.Content;
                if (string.IsNullOrEmpty(message.EncryptedContent)) return message.Content;
                return DecryptGroupMessage(message.EncryptedContent, groupId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decryption error: {ex.Message}");
                return string.Empty;
            }
        }

        private async void OnRowTapped(object sender, TappedEventArgs e)
        {
            if (_isNavigating) return;
            _isNavigating = true;

            try
            {
                ConversationItem? item = null;
                if (e.Parameter is ConversationItem paramItem) item = paramItem;
                else if (sender is View view && view.BindingContext is ConversationItem bindingItem) item = bindingItem;
                if (item == null) return;

                if (item.IsMessageRequest)
                {
                    await HandleMessageRequestTap(item);
                }
                else if (item.OtherPhone?.StartsWith("group_") == true || item.Conversation.ConversationId?.StartsWith("group_") == true)
                {
                    var groupId = item.OtherPhone;
                    if (groupId?.StartsWith("group_") == true) groupId = groupId.Substring(6);
                    var groupChatPage = new GroupChatPage();
                    groupChatPage.GroupId = groupId;
                    await Navigation.PushAsync(groupChatPage, false);
                }
                else
                {
                    if (!_unlockedConversations.Contains(item.Conversation.ConversationId))
                    {
                        bool isLocked = await IsConversationLockedAsync(item.Conversation.ConversationId);
                        if (isLocked)
                        {
                            bool unlocked = await ShowUnlockScreenForConversationAsync(item.Conversation.ConversationId);
                            if (!unlocked) { _isNavigating = false; return; }
                            _unlockedConversations.Add(item.Conversation.ConversationId);
                        }
                    }

                    var currentUserPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                    if (!string.IsNullOrEmpty(currentUserPhone))
                        await PostRepository.MarkPostsAsSeenAsync(currentUserPhone, item.OtherPhone);

                    var chatPage = new ChatPage(item.Conversation.ConversationId, item.OtherPhone);
                    await Navigation.PushAsync(chatPage, false);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Row tap error: {ex}"); }
            finally
            {
                await Task.Delay(200);
                _isNavigating = false;
            }
        }

        private string DecryptGroupMessage(string cipherText, string groupId)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);
                using var aes = System.Security.Cryptography.Aes.Create();
                using var sha = System.Security.Cryptography.SHA256.Create();
                aes.Key = sha.ComputeHash(Encoding.UTF8.GetBytes(groupId + "_lock_group_key"));
                var iv = new byte[aes.BlockSize / 8];
                var encrypted = new byte[fullCipher.Length - iv.Length];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                Array.Copy(fullCipher, iv.Length, encrypted, 0, encrypted.Length);
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length));
            }
            catch { return string.Empty; }
        }

        private async void OnPostCountBadgeTapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not ConversationItem item || item == null) return;

                string userPhone = item.OtherPhone;
                if (userPhone.Contains("·"))
                {
                    var parts = userPhone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                    userPhone = parts.Length > 1 ? parts[1].Trim() : userPhone;
                }
                userPhone = userPhone.Trim();
                if (string.IsNullOrEmpty(userPhone)) return;

                var filterInfo = new PostFilterInfo { UserPhone = userPhone, UserName = item.OtherName, ScrollToLatest = true };
                MessagingCenter.Send(this, "FilterUserPosts", filterInfo);
                await Shell.Current.GoToAsync("///post");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to user posts: {ex}");
                await DisplayAlert("Error", "Could not navigate to user posts", "OK");
            }
        }

        public class PostFilterInfo
        {
            public string UserPhone { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public bool ScrollToLatest { get; set; }
        }

        private async Task<bool> IsConversationLockedAsync(string conversationId)
        {
            try { return await ChatLockService.IsChatLockedAsync(conversationId); }
            catch { return false; }
        }

        private async Task<bool> ShowUnlockScreenForConversationAsync(string conversationId)
        {
            try
            {
                var lockType = await ChatLockService.GetLockTypeAsync(conversationId);
                if (lockType == ChatLockService.LockType.None) return true;
                var tcs = new TaskCompletionSource<bool>();
                var lockPopup = new PinEntryPopup(conversationId, lockType, (success) => tcs.TrySetResult(success));
                await Application.Current.MainPage.ShowPopupAsync(lockPopup);
                return await tcs.Task;
            }
            catch { return false; }
        }

        private async Task HandleMessageRequestTap(ConversationItem item)
        {
            try
            {
                string action = await DisplayActionSheet("Message Request", "Cancel", null, "Accept", "Decline", "View Message");
                switch (action)
                {
                    case "Accept":
                        await ChatRepository.AcceptMessageRequestAsync(item.Conversation.ConversationId);
                        await LoadConversationsAsync();
                        await DisplayAlert("Accepted", "Message request accepted.", "OK");
                        break;
                    case "Decline":
                        if (await DisplayAlert("Decline Request", "Decline this message request?", "Decline", "Cancel"))
                        {
                            await ChatRepository.DeclineMessageRequestAsync(item.Conversation.ConversationId);
                            await LoadConversationsAsync();
                        }
                        break;
                    case "View Message":
                        await Navigation.PushAsync(new ChatPage(item.Conversation.ConversationId, item.OtherPhone));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HandleMessageRequestTap error: {ex}");
                await DisplayAlert("Error", "Could not process message request", "OK");
            }
        }

        private string GetRelativeTimeString(DateTime dateTime)
        {
            try
            {
                var ts = DateTime.UtcNow - dateTime;
                if (ts.TotalSeconds < 60) return "just now";
                if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}m ago";
                if (ts.TotalHours < 24) return $"{(int)ts.TotalHours}h ago";
                if (ts.TotalDays < 7) return $"{(int)ts.TotalDays}d ago";
                if (ts.TotalDays < 30) return $"{(int)(ts.TotalDays / 7)}w ago";
                if (ts.TotalDays < 365) return $"{(int)(ts.TotalDays / 30)}mo ago";
                return $"{(int)(ts.TotalDays / 365)}y ago";
            }
            catch { return "unknown"; }
        }

        private async void SettingsMenuButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var firstItem = _items.FirstOrDefault();
                if (firstItem != null)
                {
                    if (firstItem.OtherPhone?.StartsWith("group_") == true)
                    {
                        var groupId = firstItem.OtherPhone.Substring(6);
                        var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                        await Navigation.PushModalAsync(new GroupSettingsPage(groupId, currentUserPhone));
                    }
                    else
                    {
                        await Navigation.PushModalAsync(new ConversationSettingsPage(
                            firstItem.Conversation.ConversationId, firstItem.OtherName, "Unknown"));
                    }
                    return;
                }
                await DisplayAlert("No Conversations", "You don't have any conversations or groups yet.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening settings: {ex}");
                await DisplayAlert("Error", "Could not open settings", "OK");
            }
        }

        // ?? FIX 2: BuildTabs — no longer touches old TabsContainer, only XAML names ??
        private void BuildTabs(Dictionary<string, string> listsMap, TabCounts counts)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Update the XAML button texts with counts
                    AllTabBtn.Text = counts.All > 0 ? $"All  {counts.All}" : "All";
                    PinnedTabBtn.Text = counts.Pinned > 0 ? $"Pinned  {counts.Pinned}" : "Pinned";
                    StarredTabBtn.Text = counts.Starred > 0 ? $"Starred  {counts.Starred}" : "Starred";
                    UnreadTabBtn.Text = counts.Unread > 0 ? $"Unread  {counts.Unread}" : "Unread";
                    RequestTabBtn.Text = counts.MessageRequest > 0 ? $"Requests  {counts.MessageRequest}" : "Requests";

                    var groupsCount = _allItems.Count(i => i.IsGroupChat && !i.IsArchived);
                    GroupsTabBtn.Text = groupsCount > 0 ? $"Groups  {groupsCount}" : "Groups";

                    // Rebuild dynamic list tabs
                    var dynContainer = this.FindByName<HorizontalStackLayout>("DynamicTabsContainer");
                    if (dynContainer == null) return;
                    dynContainer.Children.Clear();

                    var distinctLists = listsMap.Values
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(s => s)
                        .ToList();

                    foreach (var listName in distinctLists)
                    {
                        var listCount = _allItems.Count(i =>
                            string.Equals(i.ListName?.Trim(), listName, StringComparison.OrdinalIgnoreCase)
                            && !i.IsArchived && !i.IsMessageRequest);

                        var btn = new Button
                        {
                            Text = listCount > 0 ? $"{listName}  {listCount}" : listName,
                            BackgroundColor = Colors.Transparent,
                            TextColor = Color.FromArgb("#A0A0A0"),
                            FontSize = 12,
                            Padding = new Thickness(14, 6),
                            ClassId = "List:" + listName
                        };
                        btn.Clicked += (s, ev) => FilterTabClicked("List:" + listName);

                        dynContainer.Children.Add(new Border
                        {
                            BindingContext = "List:" + listName,
                            Content = btn,
                            BackgroundColor = Colors.Transparent,
                            StrokeThickness = 1.5,
                            Stroke = Colors.Transparent,
                            StrokeShape = new RoundRectangle { CornerRadius = 14 },
                            Padding = new Thickness(0)
                        });
                    }

                    UpdateTabVisuals();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BuildTabs error: {ex}");
                }
            });
        }

        // ?? FIX 3: UpdateTabVisuals — directly references XAML named controls, no FindByName needed ??
        private void UpdateTabVisuals()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var teal = Color.FromArgb("#00B5B5");
                    var grey = Color.FromArgb("#A0A0A0");
                    var stroke = Color.FromArgb("#008080");
                    var none = Colors.Transparent;

                    void Set(Border b, Button btn, bool active)
                    {
                        b.Stroke = active ? stroke : none;
                        btn.TextColor = active ? teal : grey;
                        btn.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
                    }

                    Set(AllTabBorder, AllTabBtn, _filter == ConversationFilter.All);
                    Set(PinnedTabBorder, PinnedTabBtn, _filter == ConversationFilter.Pinned);
                    Set(StarredTabBorder, StarredTabBtn, _filter == ConversationFilter.Starred);
                    Set(UnreadTabBorder, UnreadTabBtn, _filter == ConversationFilter.Unread);
                    Set(RequestTabBorder, RequestTabBtn, _filter == ConversationFilter.MessageRequest);
                    Set(GroupsTabBorder, GroupsTabBtn, _filter == ConversationFilter.Groups);

                    // Dynamic list tabs
                    var dynContainer = this.FindByName<HorizontalStackLayout>("DynamicTabsContainer");
                    if (dynContainer == null) return;
                    foreach (var child in dynContainer.Children)
                    {
                        if (child is not Border db) continue;
                        if (db.Content is not Button dbtn) continue;
                        var key = db.BindingContext as string ?? string.Empty;
                        bool isActive = _filter == ConversationFilter.Lists &&
                                        key.StartsWith("List:", StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals(key.Substring(5), _activeListName, StringComparison.OrdinalIgnoreCase);
                        Set(db, dbtn, isActive);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UpdateTabVisuals error: {ex}");
                }
            });
        }

        // ?? FIX 4: FilterTabClicked — inline filter + CollectionView detach/reattach ??
        private void FilterTabClicked(string param)
        {
            if (string.IsNullOrEmpty(param)) return;

            if (param.StartsWith("List:", StringComparison.OrdinalIgnoreCase))
            {
                _filter = ConversationFilter.Lists;
                _activeListName = param.Substring("List:".Length);
                // FIX: set via field directly to avoid the FindByName<Button> crash in the setter
                _isExploreButtonVisible = false;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var exploreBtn = this.FindByName<Border>("ExploreGroupsButton");
                    if (exploreBtn != null) exploreBtn.IsVisible = false;
                });
            }
            else
            {
                _activeListName = string.Empty;
                bool showExplore = param == "Groups";

                _filter = param switch
                {
                    "Pinned" => ConversationFilter.Pinned,
                    "Starred" => ConversationFilter.Starred,
                    "Unread" => ConversationFilter.Unread,
                    "Archive" => ConversationFilter.Archived,
                    "MessageRequest" => ConversationFilter.MessageRequest,
                    "Groups" => ConversationFilter.Groups,
                    _ => ConversationFilter.All,
                };

                // FIX: update ExploreGroupsButton as Border, not Button
                _isExploreButtonVisible = showExplore;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var exploreBtn = this.FindByName<Border>("ExploreGroupsButton");
                    if (exploreBtn != null) exploreBtn.IsVisible = showExplore;
                });
            }

            SaveCurrentTabState();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    UpdateTabVisuals();
                    _items.Clear();

                    IEnumerable<ConversationItem> filtered = _allItems;
                    filtered = _filter switch
                    {
                        ConversationFilter.Pinned => filtered.Where(i => i.Conversation.IsPinned && !i.IsArchived && !i.IsMessageRequest),
                        ConversationFilter.Starred => filtered.Where(i => i.Conversation.IsStarred && !i.IsArchived && !i.IsMessageRequest),
                        ConversationFilter.Unread => filtered.Where(i => i.UnreadCount > 0 && !i.IsArchived && !i.IsMessageRequest),
                        ConversationFilter.Archived => filtered.Where(i => i.IsArchived),
                        ConversationFilter.MessageRequest => filtered.Where(i => i.IsMessageRequest && !i.IsArchived),
                        ConversationFilter.Groups => filtered.Where(i => i.IsGroupChat && !i.IsArchived),
                        ConversationFilter.Lists => string.IsNullOrWhiteSpace(_activeListName)
                            ? filtered.Where(i => !string.IsNullOrWhiteSpace(i.ListName) && !i.IsArchived && !i.IsMessageRequest)
                            : filtered.Where(i => string.Equals(i.ListName?.Trim(), _activeListName.Trim(), StringComparison.OrdinalIgnoreCase) && !i.IsArchived && !i.IsMessageRequest),
                        _ => filtered.Where(i => !i.IsArchived && !i.IsMessageRequest),
                    };

                    if (!string.IsNullOrWhiteSpace(_searchQuery))
                    {
                        var q = _searchQuery.Trim();
                        filtered = filtered.Where(it =>
                            (!string.IsNullOrEmpty(it.OtherName) && it.OtherName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrEmpty(it.OtherPhone) && it.OtherPhone.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrEmpty(it.ListName) && it.ListName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    foreach (var it in filtered) _items.Add(it);

                    // CRITICAL: detach + reattach forces CollectionView to re-render immediately
                    var cv = ConversationsCv;
                    if (cv != null) { cv.ItemsSource = null; cv.ItemsSource = _items; }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FilterTabClicked inner error: {ex}");
                }
            });
        }

        private void ApplySearchFilter()
        {
            // Delegate to FilterTabClicked to keep logic in one place
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _items.Clear();

                    IEnumerable<ConversationItem> filtered = _allItems;
                    filtered = _filter switch
                    {
                        ConversationFilter.Pinned => filtered.Where(i => i.Conversation.IsPinned && !i.IsArchived && !i.IsMessageRequest),
                        ConversationFilter.Starred => filtered.Where(i => i.Conversation.IsStarred && !i.IsArchived && !i.IsMessageRequest),
                        ConversationFilter.Unread => filtered.Where(i => i.UnreadCount > 0 && !i.IsArchived && !i.IsMessageRequest),
                        ConversationFilter.Archived => filtered.Where(i => i.IsArchived),
                        ConversationFilter.MessageRequest => filtered.Where(i => i.IsMessageRequest && !i.IsArchived),
                        ConversationFilter.Groups => filtered.Where(i => i.IsGroupChat && !i.IsArchived),
                        ConversationFilter.Lists => string.IsNullOrWhiteSpace(_activeListName)
                            ? filtered.Where(i => !string.IsNullOrWhiteSpace(i.ListName) && !i.IsArchived && !i.IsMessageRequest)
                            : filtered.Where(i => string.Equals(i.ListName?.Trim(), _activeListName.Trim(), StringComparison.OrdinalIgnoreCase) && !i.IsArchived && !i.IsMessageRequest),
                        _ => filtered.Where(i => !i.IsArchived && !i.IsMessageRequest),
                    };

                    if (!string.IsNullOrWhiteSpace(_searchQuery))
                    {
                        var q = _searchQuery.Trim();
                        filtered = filtered.Where(it =>
                            (!string.IsNullOrEmpty(it.OtherName) && it.OtherName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrEmpty(it.OtherPhone) && it.OtherPhone.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (!string.IsNullOrEmpty(it.ListName) && it.ListName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
                    }

                    foreach (var it in filtered) _items.Add(it);

                    var cv = ConversationsCv;
                    if (cv != null) { cv.ItemsSource = null; cv.ItemsSource = _items; }
                }
                catch (Exception ex) { Debug.WriteLine($"ApplySearchFilter error: {ex}"); }
            });
        }

        private void SearchBar_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _searchQuery = e.NewTextValue;
            ApplySearchFilter();
        }

        private void SearchBar_SearchButtonPressed(object? sender, EventArgs e)
        {
            if (sender is SearchBar sb)
            {
                _searchQuery = sb.Text;
                ApplySearchFilter();
                sb.Unfocus();
            }
        }

        private void OverlayBackground_Tapped(object sender, EventArgs e)
        {
            try
            {
                var overlay = this.FindByName<Grid>("ActionsOverlay");
                if (overlay != null) overlay.IsVisible = false;
                _overlayConversation = null;
                _overlayBusy = false;
            }
            catch { }
        }

        private Dictionary<string, string> LoadConversationLists()
        {
            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(me)) return new Dictionary<string, string>();
                var json = Preferences.Get($"conversation_lists_{me}", string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch { return new Dictionary<string, string>(); }
        }

        private void SaveConversationLists(Dictionary<string, string> map)
        {
            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(me)) return;
                Preferences.Set($"conversation_lists_{me}", JsonSerializer.Serialize(map));
            }
            catch { }
        }

        private async void OverlayAction_Tapped(object sender, EventArgs e)
        {
            if (_overlayBusy) return;
            _overlayBusy = true;

            try
            {
                string action = string.Empty;
                if (sender is TapGestureRecognizer tgSender)
                    action = tgSender.CommandParameter as string ?? string.Empty;
                else if (sender is View view)
                {
                    var tg = view.GestureRecognizers?.OfType<TapGestureRecognizer>().FirstOrDefault();
                    action = tg?.CommandParameter as string ?? string.Empty;
                }

                if (string.IsNullOrEmpty(action)) { OverlayBackground_Tapped(this, EventArgs.Empty); return; }
                if (_overlayConversation == null)
                {
                    await DisplayAlert("Error", "No conversation selected.", "OK");
                    OverlayBackground_Tapped(this, EventArgs.Empty);
                    return;
                }

                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();

                switch (action)
                {
                    case "Pin chat":
                    case "Unpin chat":
                        _overlayConversation.IsPinned = !_overlayConversation.IsPinned;
                        await SafeUpdateConversationAsync(db, _overlayConversation);
                        await LoadConversationsAsync();
                        await DisplayAlert(_overlayConversation.IsPinned ? "Pinned" : "Unpinned",
                            _overlayConversation.IsPinned ? "Conversation pinned." : "Conversation unpinned.", "OK");
                        break;

                    case "Add to favorites":
                    case "Remove from favorites":
                        _overlayConversation.IsStarred = !_overlayConversation.IsStarred;
                        await SafeUpdateConversationAsync(db, _overlayConversation);
                        await LoadConversationsAsync();
                        await DisplayAlert(_overlayConversation.IsStarred ? "Added to favorites" : "Removed from favorites",
                            _overlayConversation.IsStarred ? "Added to favorites." : "Removed from favorites.", "OK");
                        break;

                    case "Mute notifications":
                    case "Unmute notifications":
                        _overlayConversation.IsMuted = !_overlayConversation.IsMuted;
                        await SafeUpdateConversationAsync(db, _overlayConversation);
                        await LoadConversationsAsync();
                        await DisplayAlert(_overlayConversation.IsMuted ? "Muted" : "Unmuted",
                            _overlayConversation.IsMuted ? "Notifications muted." : "Notifications unmuted.", "OK");
                        break;

                    case "Archive chat":
                    case "Unarchive chat":
                        _overlayConversation.IsArchived = !_overlayConversation.IsArchived;
                        await SafeUpdateConversationAsync(db, _overlayConversation);
                        await DisplayAlert(_overlayConversation.IsArchived ? "Archived" : "Unarchived",
                            _overlayConversation.IsArchived ? "Conversation archived." : "Conversation unarchived.", "OK");
                        await LoadConversationsAsync();
                        if (_filter != ConversationFilter.Archived && _overlayConversation.IsArchived)
                        {
                            _filter = ConversationFilter.Archived;
                            UpdateTabVisuals();
                            ApplySearchFilter();
                        }
                        break;

                    case "Mark as unread":
                        var me2 = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                        if (!string.IsNullOrEmpty(me2))
                        {
                            try { await db.ExecuteAsync("UPDATE ChatMessage SET IsRead = 0 WHERE ConversationId = ? AND RecipientPhone = ?", _overlayConversation.ConversationId, me2); }
                            catch
                            {
                                var msgs = await db.Table<ChatMessage>().Where(m => m.ConversationId == _overlayConversation.ConversationId && m.RecipientPhone == me2).ToListAsync();
                                foreach (var msg in msgs) { msg.IsRead = false; await db.UpdateAsync(msg); }
                            }
                            await DisplayAlert("Marked", "Conversation marked as unread.", "OK");
                            await LoadConversationsAsync();
                        }
                        break;

                    case "Add to list":
                        var listName = await DisplayPromptAsync("Add to list", "Enter list name:");
                        if (!string.IsNullOrWhiteSpace(listName))
                        {
                            var map = LoadConversationLists();
                            map[_overlayConversation.ConversationId] = listName.Trim();
                            SaveConversationLists(map);
                            await DisplayAlert("Added", $"Conversation added to '{listName}'", "OK");
                            await LoadConversationsAsync();
                        }
                        break;

                    case "Block":
                    case "Unblock":
                        var meBlock = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                        if (!string.IsNullOrEmpty(meBlock))
                        {
                            string otherPhone = _overlayConversation.ParticipantA == meBlock
                                ? _overlayConversation.ParticipantB : _overlayConversation.ParticipantA;
                            bool isBlocking = action == "Block";
                            bool confirmed = await DisplayAlert(
                                isBlocking ? "Block User" : "Unblock User",
                                isBlocking ? $"Block {otherPhone}?" : $"Unblock {otherPhone}?",
                                isBlocking ? "Block" : "Unblock", "Cancel");
                            if (confirmed)
                            {
                                bool result = isBlocking
                                    ? await ChatRepository.BlockUserAsync(meBlock, otherPhone)
                                    : await ChatRepository.UnblockUserAsync(meBlock, otherPhone);
                                await DisplayAlert(result ? (isBlocking ? "Blocked" : "Unblocked") : "Error",
                                    result ? "Done." : "Failed. Try again.", "OK");
                                await LoadConversationsAsync();
                            }
                        }
                        break;

                    case "Delete chat":
                        if (await DisplayAlert("Delete", "Delete this conversation? This cannot be undone.", "Delete", "Cancel"))
                        {
                            var msgs2 = await db.Table<ChatMessage>().Where(m => m.ConversationId == _overlayConversation.ConversationId).ToListAsync();
                            foreach (var msg in msgs2) await db.DeleteAsync(msg);
                            await db.DeleteAsync(_overlayConversation);
                            var map2 = LoadConversationLists();
                            if (map2.Remove(_overlayConversation.ConversationId)) SaveConversationLists(map2);
                            await DisplayAlert("Deleted", "Conversation deleted.", "OK");
                            await LoadConversationsAsync();
                        }
                        break;
                }
            }
            catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
            finally
            {
                try { OverlayBackground_Tapped(this, EventArgs.Empty); } catch { }
                _overlayBusy = false;
            }
        }

        private async Task UpdateArchiveBadge(int unreadCount)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var badgeBorder = this.FindByName<Border>("ArchiveBadgeBorder");
                    var badgeLabel = this.FindByName<Label>("ArchiveBadgeLabel");
                    if (badgeBorder == null || badgeLabel == null) return;
                    badgeBorder.IsVisible = unreadCount > 0;
                    if (unreadCount > 0) badgeLabel.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
                });
            }
            catch { }
        }

        private async void OnHomeTapped(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("//post"); }
            catch { await DisplayAlert("Error", "Could not navigate to home", "OK"); }
        }

        private void OnChatsTapped(object sender, EventArgs e)
        {
            try { ConversationsCv?.ScrollTo(0); }
            catch (Exception ex) { Debug.WriteLine($"Scroll error: {ex}"); }
        }

        private async void OnExploreGroupsClicked(object sender, EventArgs e)
        {
            try
            {
                await LoadConversationsAsync();
                await Navigation.PushAsync(new ExploreGroupsPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnExploreGroupsClicked error: {ex}");
                await DisplayAlert("Error", "Could not open explore groups", "OK");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                if (!await IsUserLoggedIn())
                {
                    await Shell.Current.GoToAsync("///LoginPage");
                    return;
                }

                string tabToRestore = _lastActiveTabKey;
                bool cameBackFromPage = _skipNextFullLoad && (DateTime.UtcNow - _lastNavigationOut).TotalSeconds < 10;

                if (cameBackFromPage && _allItems.Count > 0)
                    await UpdateUnreadCountsOnlyAsync();
                else
                    await LoadConversationsAsync();

                _skipNextFullLoad = false;

                if (!string.IsNullOrEmpty(tabToRestore))
                {
                    await Task.Delay(80);
                    MainThread.BeginInvokeOnMainThread(() => FilterTabClicked(tabToRestore));
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() => { UpdateTabVisuals(); ApplySearchFilter(); });
                }

                await UpdateBottomNavChatBadge();
                await UpdateNotificationBadgeCount();
                await CheckModerationStatusAsync();

            }
            catch (Exception ex) { Debug.WriteLine($"OnAppearing error: {ex}"); }
        }

        private async Task CheckModerationStatusAsync()
        {
            try
            {
                var phone = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(phone)) return;

                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();
                var user = await db.Table<Lock.Models.User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();

                if (user == null) return;

                // Auto-lift expired temp bans
                if (user.IsBanned && user.BanType == "temporary" && user.BanExpiresAt.HasValue
                    && DateTime.UtcNow >= user.BanExpiresAt.Value)
                {
                    await UserService.CheckAndLiftExpiredBanAsync(phone);
                    return;
                }

                // Show unacknowledged warning
                if (user.HasWarning && !user.WarningAcknowledged)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert(
                            "Warning from Moderation Team",
                            $"{user.WarningMessage}\n\nIssued: {user.WarnedAt:MMM dd, yyyy}\n\nPlease review our community guidelines to avoid further action.",
                            "I Understand");
                        await UserService.AcknowledgeWarningAsync(phone);
                    });
                    return;
                }

                // Show moderation note if not yet seen (resolved/dismissed/under review)
                if (!string.IsNullOrEmpty(user.ModerationNote)
                    && user.ModerationStatus != "warned"      // warnings handled above
                    && user.ModerationStatus != "perm_banned" // handled at login
                    && user.ModerationStatus != "temp_banned" // handled at login
                    && user.ModerationUpdatedAt.HasValue
                    && (DateTime.UtcNow - user.ModerationUpdatedAt.Value).TotalHours < 72)
                {
                    string title = user.ModerationStatus switch
                    {
                        "resolved" => "Report Update",
                        "dismissed" => "Report Update",
                        _ => "Account Notice"
                    };

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert(title, user.ModerationNote, "OK");
                        // Clear note so it doesn't show again
                        user.ModerationNote = string.Empty;
                        user.ModerationUpdatedAt = null;
                        await db.UpdateAsync(user);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckModerationStatusAsync error: {ex.Message}");
            }
        }

        private async Task UpdateUnreadCountsOnlyAsync()
        {
            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(me)) return;

                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();

                foreach (var item in _allItems)
                {
                    try
                    {
                        var newUnreadCount = await db.Table<ChatMessage>()
                            .Where(m => m.ConversationId == item.Conversation.ConversationId &&
                                       m.RecipientPhone == me && m.IsRead == false &&
                                       m.IsMessageRequest == false && !m.IsDeclined)
                            .CountAsync();
                        item.UnreadCount = newUnreadCount;
                    }
                    catch { }
                }
                ApplySearchFilter();
            }
            catch (Exception ex) { Debug.WriteLine($"UpdateUnreadCountsOnlyAsync error: {ex}"); }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            SaveCurrentTabState();
            _lastNavigationOut = DateTime.UtcNow;
            _skipNextFullLoad = true;

            try
            {
                MessagingCenter.Unsubscribe<object, NotificationItem>(this, "NewUnreadNotification");
                MessagingCenter.Unsubscribe<object>(this, "NotificationRead");
                MessagingCenter.Unsubscribe<object>(this, "AllNotificationsRead");
                MessagingCenter.Unsubscribe<object>(this, "MessagesUpdated");
                MessagingCenter.Unsubscribe<object>(this, "ConversationsUpdated");
                MessagingCenter.Unsubscribe<object>(this, "PostsUpdated");
                MessagingCenter.Unsubscribe<object>(this, "NewPostCreated");
                MessagingCenter.Unsubscribe<object>(this, "NotificationPreferencesChanged");
                MessagingCenter.Unsubscribe<object>(this, "MoodUpdated");
                MessagingCenter.Unsubscribe<object>(this, "MoodSaved");
            }
            catch (Exception ex) { Debug.WriteLine($"OnDisappearing error: {ex}"); }
        }

        private async Task<bool> IsUserLoggedIn()
        {
            var savedPhone = Preferences.Get(CurrentUserPhoneKey, string.Empty)?.Trim();
            if (string.IsNullOrEmpty(savedPhone)) return false;
            try
            {
                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();
                var user = await db.Table<Lock.Models.User>().Where(u => u.PhoneNumber == savedPhone).FirstOrDefaultAsync();
                return user != null;
            }
            catch { return false; }
        }

        private async void OnProfileTapped(object sender, EventArgs e)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone))
                {
                    await Shell.Current.GoToAsync("///LoginPage");
                    return;
                }
                if (!await SafeNavigateToProfileAsync(currentUserPhone))
                    await DisplayAlert("Error", "Could not navigate to profile", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Profile navigation error: {ex}");
                await DisplayAlert("Error", "Could not navigate to profile", "OK");
            }
        }

        private async void OnMatchTapped(object sender, EventArgs e)
        {
            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Delay(100);
                await Shell.Current.GoToAsync("//match");
            }
            catch { await DisplayAlert("Error", "Could not navigate to matches", "OK"); }
        }

        private async Task UpdateNotificationBadgeCount()
        {
            try
            {
                var json = Preferences.Get("notifications_v2", string.Empty);
                int unreadCount = 0;
                if (!string.IsNullOrEmpty(json))
                {
                    var notifications = JsonSerializer.Deserialize<List<NotificationItem>>(json);
                    if (notifications != null) unreadCount = notifications.Count(n => !n.IsRead);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var notificationBadge = this.FindByName<Border>("NotificationBadge");
                    var notificationBadgeLabel = this.FindByName<Label>("NotificationBadgeLabel");
                    if (notificationBadge != null && notificationBadgeLabel != null)
                    {
                        notificationBadge.IsVisible = unreadCount > 0;
                        notificationBadgeLabel.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
                    }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error updating notification badge: {ex.Message}"); }
        }

        private void SetNotificationBadgeVisibility(bool isVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var notificationBadge = this.FindByName<Border>("NotificationBadge");
                if (notificationBadge != null) notificationBadge.IsVisible = isVisible;
            });
        }

        private async void OnNotificationsTapped(object sender, EventArgs e)
        {
            try { await Navigation.PushAsync(new Lock.Pages.Post.NotificationPage()); }
            catch (Exception ex)
            {
                Debug.WriteLine($"Notifications navigation error: {ex}");
                await DisplayAlert("Error", "Could not navigate to notifications", "OK");
            }
        }

        private async Task<bool> SafeNavigateToProfileAsync(string phone)
        {
            try { await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(phone)}&viewOnly=false"); return true; }
            catch
            {
                try { await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(phone)}"); return true; }
                catch { return false; }
            }
        }

        private void PreventClose(object sender, EventArgs e) { }

        private void CloseOverlayButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var overlay = this.FindByName<Grid>("ActionsOverlay");
                if (overlay != null) overlay.IsVisible = false;
                _overlayConversation = null;
                _overlayBusy = false;
            }
            catch (Exception ex) { Debug.WriteLine($"Error closing overlay: {ex}"); }
        }

        private async Task DebugCheckGroups()
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var groupDb = GroupDatabaseService.GetConnection();
                var allGroups = await groupDb.Table<Group>().ToListAsync();
                Debug.WriteLine($"=== TOTAL GROUPS IN DATABASE: {allGroups.Count} ===");
                var me = Preferences.Get("current_user_phone", string.Empty);
                var memberships = await groupDb.Table<GroupMember>().Where(m => m.UserPhone == me).ToListAsync();
                Debug.WriteLine($"=== MY GROUP MEMBERSHIPS: {memberships.Count} ===");
            }
            catch (Exception ex) { Debug.WriteLine($"Debug error: {ex.Message}"); }
        }

        private static async Task SafeUpdateConversationAsync(SQLiteAsyncConnection db, Conversation conv)
        {
            try { await db.UpdateAsync(conv); }
            catch { try { await db.InsertAsync(conv); } catch { } }
        }

        // Kept for any remaining references — routes to FilterTabClicked
        private void TabButton_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn && !string.IsNullOrEmpty(btn.ClassId))
                FilterTabClicked(btn.ClassId);
        }

        private async void MessageRequestAction_Tapped(object sender, EventArgs e)
        {
            try
            {
                if ((sender as TapGestureRecognizer)?.CommandParameter is ConversationItem item)
                    await HandleMessageRequestTap(item);
            }
            catch (Exception ex) { Debug.WriteLine($"MessageRequestAction_Tapped error: {ex}"); }
        }

        private async void FilterTab_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            FilterTabClicked(btn.CommandParameter as string ?? btn.Text ?? string.Empty);
            await Task.CompletedTask;
        }

        private async Task RefreshMatchPercentagesAsync()
        {
            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(me)) return;
                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();
                var currentUser = await db.Table<Lock.Models.User>().Where(u => u.PhoneNumber == me).FirstOrDefaultAsync();
                if (currentUser == null) return;
                foreach (var item in _allItems)
                {
                    if (item.IsGroupChat || item.IsMessageRequest || item.IsArchived) continue;
                    try
                    {
                        var otherUser = await db.Table<Lock.Models.User>().Where(u => u.PhoneNumber == item.OtherPhone).FirstOrDefaultAsync();
                        if (otherUser != null)
                            item.MatchPercent = await CompatibilityService.CalculateCompatibilityScoreAsync(currentUser, otherUser);
                    }
                    catch { }
                }
                ApplySearchFilter();
            }
            catch (Exception ex) { Debug.WriteLine($"RefreshMatchPercentagesAsync error: {ex}"); }
        }
    }
}