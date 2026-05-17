using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using ChatDatabaseService = Lock.Chat.Services.DatabaseService;

namespace Lock.Pages.Chat
{
    public partial class ArchivePage : ContentPage
    {
        private const string CurrentUserPhoneKey = "current_user_phone";

        private class ArchiveItem
        {
            public Conversation Conversation { get; set; } = default!;
            public string OtherPhone { get; set; } = string.Empty;
            public string OtherName { get; set; } = string.Empty;
            public string OtherProfileImage { get; set; } = string.Empty;

            // Unread count properties
            public int UnreadCount { get; set; }
            public bool HasUnread => UnreadCount > 0;
            public string UnreadDisplay => UnreadCount > 99 ? "99+" : UnreadCount.ToString();
        }

        private readonly ObservableCollection<ArchiveItem> _items = new();

        public ArchivePage()
        {
            EnsureInitializeComponent();
            var cv = this.FindByName<CollectionView>("ArchiveCollectionView");
            if (cv != null)
                cv.ItemsSource = _items;
        }

        private void EnsureInitializeComponent()
        {
            var mi = this.GetType().GetMethod("InitializeComponent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (mi != null)
            {
                mi.Invoke(this, null);
                return;
            }

            Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(this, this.GetType());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadArchivedAsync();
        }

        private async Task LoadArchivedAsync()
        {
            try
            {
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                if (string.IsNullOrEmpty(me))
                {
                    _items.Clear();
                    var emptyGridClear = this.FindByName<Grid>("EmptyStateGrid");
                    if (emptyGridClear != null) emptyGridClear.IsVisible = true;
                    return;
                }

                await ChatDatabaseService.InitializeAsync();
                var db = ChatDatabaseService.GetConnection();

                // ?? Load ghosted phones (users with Ghost Mode + Mood Shield ON) ??
                var ghostedPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var allUsers = await db.Table<User>().ToListAsync();
                    ghostedPhones = allUsers
                        .Where(u => u.GhostModeMoodShield)
                        .Select(u => (u.PhoneNumber ?? "").Trim())
                        .Where(p => !string.IsNullOrEmpty(p) &&
                                    !string.Equals(p, me, StringComparison.OrdinalIgnoreCase))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    System.Diagnostics.Debug.WriteLine($"Archive ghost filter: {ghostedPhones.Count} ghosted users will be hidden from archive");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Archive ghost filter load error: {ex.Message}");
                }

                var convs = await db.Table<Conversation>()
                    .Where(c => c.IsArchived == true && (c.ParticipantA == me || c.ParticipantB == me))
                    .ToListAsync();

                _items.Clear();

                foreach (var c in convs.OrderByDescending(c => c.LastMessageAt))
                {
                    var other = c.ParticipantA == me ? c.ParticipantB : c.ParticipantA;

                    // ?? Skip ghosted users ????????????????????????????????????????
                    var cleanOtherForGhost = other.Contains("·")
                        ? other.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries)
                               .Skip(1).FirstOrDefault()?.Trim() ?? other.Trim()
                        : other.Trim();

                    if (ghostedPhones.Contains(cleanOtherForGhost))
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping ghosted user archived conversation: {other}");
                        continue;
                    }
                    // ?? END ghost skip ????????????????????????????????????????????

                    var displayName = other;
                    var avatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(displayName)}&background=2F3337&color=E6E6E6&size=128";

                    try
                    {
                        var user = await db.Table<User>().Where(u => u.PhoneNumber == other).FirstOrDefaultAsync();
                        if (user != null)
                        {
                            displayName = string.IsNullOrEmpty(user.Name) ? other : user.Name;
                            if (!string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath))
                                avatarUrl = user.ProfileImagePath;
                        }
                    }
                    catch { }

                    var item = new ArchiveItem
                    {
                        Conversation = c,
                        OtherPhone = other,
                        OtherName = displayName,
                        OtherProfileImage = avatarUrl,
                        UnreadCount = 0
                    };

                    // load unread count for this conversation (recipient = current user)
                    try
                    {
                        item.UnreadCount = await db.Table<ChatMessage>()
                            .Where(m => m.ConversationId == c.ConversationId && m.RecipientPhone == me && m.IsRead == false)
                            .CountAsync();
                    }
                    catch
                    {
                        item.UnreadCount = 0;
                    }

                    _items.Add(item);
                }

                // show/hide empty state grid
                var emptyGrid = this.FindByName<Grid>("EmptyStateGrid");
                if (emptyGrid != null)
                    emptyGrid.IsVisible = _items.Count == 0;

                // Also show a message if there were ghosted archived conversations
                if (_items.Count == 0 && convs.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"All archived conversations were from ghosted users: {convs.Count} total, {convs.Count - _items.Count} filtered out");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Failed to load archive: " + ex.Message, "OK");
            }
        }

        // Keep button-based handler (in case a Button is used elsewhere)
        private async void UnarchiveButton_Clicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Conversation conv)
            {
                // Check if the other user is ghosted before unarchiving
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                var other = conv.ParticipantA == me ? conv.ParticipantB : conv.ParticipantA;

                await ChatDatabaseService.InitializeAsync();
                var db = ChatDatabaseService.GetConnection();

                // Check if user is ghosted
                var otherUser = await db.Table<User>().Where(u => u.PhoneNumber == other).FirstOrDefaultAsync();
                if (otherUser?.GhostModeMoodShield == true)
                {
                    await DisplayAlert("Cannot Unarchive",
                        "This user has Ghost Mode enabled. You cannot unarchive or message them.",
                        "OK");
                    return;
                }

                try
                {
                    conv.IsArchived = false;
                    try { await db.UpdateAsync(conv); } catch { try { await db.InsertAsync(conv); } catch { } }

                    await LoadArchivedAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "Could not unarchive: " + ex.Message, "OK");
                }
            }
        }

        // Handle SwipeItem.Invoked (signature expected by XAML: Invoked="UnarchiveButton_Invoked")
        private async void UnarchiveButton_Invoked(object? sender, EventArgs e)
        {
            // SwipeItem is the sender for Invoked; retrieve CommandParameter
            if (sender is SwipeItem swipe && swipe.CommandParameter is Conversation conv)
            {
                // Check if the other user is ghosted before unarchiving
                var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
                var other = conv.ParticipantA == me ? conv.ParticipantB : conv.ParticipantA;

                await ChatDatabaseService.InitializeAsync();
                var db = ChatDatabaseService.GetConnection();

                // Check if user is ghosted
                var otherUser = await db.Table<User>().Where(u => u.PhoneNumber == other).FirstOrDefaultAsync();
                if (otherUser?.GhostModeMoodShield == true)
                {
                    await DisplayAlert("Cannot Unarchive",
                        "This user has Ghost Mode enabled. You cannot unarchive or message them.",
                        "OK");
                    return;
                }

                try
                {
                    conv.IsArchived = false;
                    try { await db.UpdateAsync(conv); } catch { try { await db.InsertAsync(conv); } catch { } }

                    await LoadArchivedAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "Could not unarchive: " + ex.Message, "OK");
                }
            }
        }

        private async void ArchiveCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0) return;
            var selected = e.CurrentSelection[0] as ArchiveItem;
            var cv = this.FindByName<CollectionView>("ArchiveCollectionView");

            // defensive: clear selection early to avoid re-entrancy
            try
            {
                if (cv != null)
                    cv.SelectedItem = null;
            }
            catch { }

            if (selected == null)
                return;

            if (selected.Conversation == null || string.IsNullOrWhiteSpace(selected.OtherPhone))
            {
                await DisplayAlert("Error", "Cannot open conversation: missing data.", "OK");
                return;
            }

            // Check if the user is ghosted before opening chat
            var me = Preferences.Get(CurrentUserPhoneKey, string.Empty);
            var other = selected.OtherPhone;

            await ChatDatabaseService.InitializeAsync();
            var db = ChatDatabaseService.GetConnection();

            var otherUser = await db.Table<User>().Where(u => u.PhoneNumber == other).FirstOrDefaultAsync();
            if (otherUser?.GhostModeMoodShield == true)
            {
                await DisplayAlert("Cannot Open Chat",
                    "This user has Ghost Mode enabled. You cannot message them.",
                    "OK");

                // Re-enable the collection
                if (cv != null) cv.IsEnabled = true;
                return;
            }

            // disable the collection while we navigate to avoid double-taps / races
            if (cv != null) cv.IsEnabled = false;

            var route = $"chat?conversationId={Uri.EscapeDataString(selected.Conversation.ConversationId)}&otherPhone={Uri.EscapeDataString(selected.OtherPhone)}";

            try
            {
                // Navigate first (same approach used by ConversationsPage)
                try
                {
                    await Shell.Current.GoToAsync(route);
                }
                catch (Exception navEx)
                {
                    await DisplayAlert("Navigation error", "Could not open chat: " + navEx.Message, "OK");
                    return;
                }

                // After successful navigation, close the archive modal if it was presented modally.
                // PopModalAsync is awaited here so the modal is removed cleanly; failure is swallowed.
                try { await Navigation.PopModalAsync(); } catch { }
            }
            finally
            {
                // always re-enable the collection
                try
                {
                    if (cv != null)
                        cv.IsEnabled = true;
                }
                catch { }
            }
        }

        private async void BackButton_Clicked(object sender, EventArgs e)
        {
            try { await Navigation.PopModalAsync(); } catch { }
        }
    }
}