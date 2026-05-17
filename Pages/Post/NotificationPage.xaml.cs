using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Lock.Pages.Post
{
    public partial class NotificationPage : ContentPage, INotifyPropertyChanged
    {
        private const string NotificationsPrefKey = "notifications_v2";
        private const string AutoMarkDaysPrefKey = "auto_mark_days_enabled";
        private const string LastAutoMarkDatePrefKey = "last_auto_mark_date";

        private ObservableCollection<NotificationItem> _allNotifications = new();
        private ObservableCollection<NotificationItem> _filteredNotifications = new();
        private string _currentFilter = "All";
        private bool _isRefreshing;
        private HashSet<NotificationItem> _selectedItems = new();
        private bool _autoMarkEnabled = false;
        private bool _isNavigating = false;

        // Command fields
        private ICommand _backCommand;
        private ICommand _markAllReadCommand;
        private ICommand _showMenuCommand;
        private ICommand _selectAllCommand;
        private ICommand _selectPriorityCommand;
        private ICommand _selectUnreadCommand;
        private ICommand _selectMentionsCommand;
        private ICommand _refreshCommand;
        private ICommand _notificationTappedCommand;
        private ICommand _menuCommand;
        private ICommand _toggleSelectAllCommand;
        private ICommand _markSelectedReadCommand;
        private ICommand _deleteSelectedCommand;
        private ICommand _openSettingsCommand;
        private ICommand _toggleAutoMarkCommand;

        // Command properties
        public ICommand BackCommand => _backCommand;
        public ICommand MarkAllReadCommand => _markAllReadCommand;
        public ICommand ShowMenuCommand => _showMenuCommand;
        public ICommand SelectAllCommand => _selectAllCommand;
        public ICommand SelectPriorityCommand => _selectPriorityCommand;
        public ICommand SelectUnreadCommand => _selectUnreadCommand;
        public ICommand SelectMentionsCommand => _selectMentionsCommand;
        public ICommand RefreshCommand => _refreshCommand;
        public ICommand NotificationTappedCommand => _notificationTappedCommand;
        public ICommand MenuCommand => _menuCommand;
        public ICommand ToggleSelectAllCommand => _toggleSelectAllCommand;
        public ICommand MarkSelectedReadCommand => _markSelectedReadCommand;
        public ICommand DeleteSelectedCommand => _deleteSelectedCommand;
        public ICommand OpenSettingsCommand => _openSettingsCommand;
        public ICommand ToggleAutoMarkCommand => _toggleAutoMarkCommand;

        // Properties
        public ObservableCollection<NotificationItem> FilteredNotifications
        {
            get => _filteredNotifications;
            set
            {
                _filteredNotifications = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public int UnreadCount => _allNotifications?.Count(n => !n.IsRead) ?? 0;
        public int PriorityCount => _allNotifications?.Count(n => n.IsPriority) ?? 0;
        public bool HasUnread => UnreadCount > 0;
        public bool HasPriority => PriorityCount > 0;
        public bool HasItems => FilteredNotifications?.Any() == true;
        public bool IsEmpty => !HasItems;
        public bool HasSelectedItems => _selectedItems.Count > 0;
        public bool IsAllSelectedForBatch => HasSelectedItems && _selectedItems.Count == FilteredNotifications?.Count;

        public bool AutoMarkEnabled
        {
            get => _autoMarkEnabled;
            set
            {
                _autoMarkEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isRefreshingValue;
        public bool IsRefreshing
        {
            get => _isRefreshingValue;
            set
            {
                _isRefreshingValue = value;
                OnPropertyChanged();
            }
        }

        private string _emptyMessage = "No notifications yet";
        public string EmptyMessage
        {
            get => _emptyMessage;
            set
            {
                _emptyMessage = value;
                OnPropertyChanged();
            }
        }

        // Filter states
        private bool _isAllSelected = true;
        private bool _isPrioritySelected;
        private bool _isUnreadSelected;
        private bool _isMentionsSelected;

        public bool IsAllSelected
        {
            get => _isAllSelected;
            set { _isAllSelected = value; OnPropertyChanged(); }
        }

        public bool IsPrioritySelected
        {
            get => _isPrioritySelected;
            set { _isPrioritySelected = value; OnPropertyChanged(); }
        }

        public bool IsUnreadSelected
        {
            get => _isUnreadSelected;
            set { _isUnreadSelected = value; OnPropertyChanged(); }
        }

        public bool IsMentionsSelected
        {
            get => _isMentionsSelected;
            set { _isMentionsSelected = value; OnPropertyChanged(); }
        }

        public NotificationPage()
        {
            InitializeComponent();
            InitializeCommands();
            LoadAutoMarkSetting();
            LoadSavedNotifications();
            FilterNotifications();
            BindingContext = this;
        }

        private void InitializeCommands()
        {
            _backCommand = new Command(async () => await Navigation.PopAsync());
            _markAllReadCommand = new Command(MarkAllAsRead);
            _showMenuCommand = new Command(ShowMainMenu);
            _selectAllCommand = new Command(() => SetFilter("All"));
            _selectPriorityCommand = new Command(() => SetFilter("Priority"));
            _selectUnreadCommand = new Command(() => SetFilter("Unread"));
            _selectMentionsCommand = new Command(() => SetFilter("Mentions"));
            _refreshCommand = new Command(async () => await RefreshNotifications());
            _notificationTappedCommand = new Command<NotificationItem>(OnNotificationTapped);
            _menuCommand = new Command<NotificationItem>(OnNotificationMenuClicked);
            _toggleSelectAllCommand = new Command(ToggleSelectAll);
            _markSelectedReadCommand = new Command(MarkSelectedAsRead);
            _deleteSelectedCommand = new Command(DeleteSelected);
            _openSettingsCommand = new Command(OpenSettings);
            _toggleAutoMarkCommand = new Command(ToggleAutoMark);
        }

        private void LoadAutoMarkSetting()
        {
            AutoMarkEnabled = Preferences.Get(AutoMarkDaysPrefKey, false);
            if (AutoMarkEnabled)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(500);
                    ApplyAutoMarkRead();
                });
            }
        }

        private async void ToggleAutoMark()
        {
            AutoMarkEnabled = !AutoMarkEnabled;
            Preferences.Set(AutoMarkDaysPrefKey, AutoMarkEnabled);

            if (AutoMarkEnabled)
            {
                Preferences.Set(LastAutoMarkDatePrefKey, DateTime.MinValue);
                await DisplayAlert("Auto-mark Enabled",
                    "? Notifications older than 7 days will be automatically marked as read.\n\nThis will run once per day.",
                    "OK");
                ApplyAutoMarkRead();
            }
            else
            {
                await DisplayAlert("Auto-mark Disabled",
                    "? Auto-marking read for old notifications has been disabled.\n\nOld notifications will remain unread until you manually mark them.",
                    "OK");
            }
        }

        private async void ApplyAutoMarkRead()
        {
            try
            {
                if (!AutoMarkEnabled)
                {
                    Debug.WriteLine("Auto-mark is disabled, skipping");
                    return;
                }

                var lastRun = Preferences.Get(LastAutoMarkDatePrefKey, DateTime.MinValue);
                var today = DateTime.UtcNow.Date;

                Debug.WriteLine($"Last auto-mark run: {lastRun}, Today: {today}");

                // Only run once per day
                if (lastRun.Date == today)
                {
                    Debug.WriteLine("Auto-mark already ran today, skipping");
                    return;
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-7);
                var oldUnreadNotifications = _allNotifications.Where(n => !n.IsRead && n.Timestamp < cutoffDate).ToList();

                Debug.WriteLine($"Found {oldUnreadNotifications.Count} old unread notifications");

                if (oldUnreadNotifications.Any())
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var item in oldUnreadNotifications)
                        {
                            item.IsRead = true;
                            Debug.WriteLine($"Auto-marked as read: {item.Preview} from {item.Timestamp}");
                        }

                        SaveNotifications();
                        FilterNotifications();
                        OnPropertyChanged(nameof(UnreadCount));
                        OnPropertyChanged(nameof(HasUnread));
                    });

                    // Show notification
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("Auto-mark Complete",
                            $"?? {oldUnreadNotifications.Count} notification(s) older than 7 days were automatically marked as read.",
                            "OK");
                    });
                }

                // Update last run date
                Preferences.Set(LastAutoMarkDatePrefKey, today);
                Debug.WriteLine($"Updated last auto-mark run to {today}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in auto-mark: {ex.Message}");
            }
        }

        private void SetFilter(string filter)
        {
            IsAllSelected = filter == "All";
            IsPrioritySelected = filter == "Priority";
            IsUnreadSelected = filter == "Unread";
            IsMentionsSelected = filter == "Mentions";
            _currentFilter = filter;
            ClearSelection();
            FilterNotifications();
        }

        private void FilterNotifications()
        {
            IEnumerable<NotificationItem> filtered;

            switch (_currentFilter)
            {
                case "Priority":
                    filtered = _allNotifications.Where(n => n.IsPriority);
                    EmptyMessage = "No priority notifications";
                    break;
                case "Unread":
                    filtered = _allNotifications.Where(n => !n.IsRead);
                    EmptyMessage = "No unread notifications";
                    break;
                case "Mentions":
                    filtered = _allNotifications.Where(n => n.Action?.Contains("mention", StringComparison.OrdinalIgnoreCase) == true ||
                                                            n.Action?.Contains("tag", StringComparison.OrdinalIgnoreCase) == true);
                    EmptyMessage = "No mentions";
                    break;
                default:
                    filtered = _allNotifications;
                    EmptyMessage = "No notifications yet";
                    break;
            }

            FilteredNotifications = new ObservableCollection<NotificationItem>(
                filtered.OrderByDescending(n => n.Timestamp)
            );
        }

        private void ClearSelection()
        {
            _selectedItems.Clear();
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(IsAllSelectedForBatch));
        }

        private void ToggleSelectAll()
        {
            if (IsAllSelectedForBatch)
            {
                _selectedItems.Clear();
            }
            else
            {
                _selectedItems = new HashSet<NotificationItem>(FilteredNotifications);
            }
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(IsAllSelectedForBatch));
        }

        private async void MarkSelectedAsRead()
        {
            int count = _selectedItems.Count;
            foreach (var item in _selectedItems)
            {
                item.IsRead = true;
            }
            SaveNotifications();
            FilterNotifications();
            ClearSelection();

            await DisplayAlert("Success", $"{count} notifications marked as read", "OK");
        }

        private async void DeleteSelected()
        {
            int count = _selectedItems.Count;
            var confirm = await DisplayAlert("Delete Notifications",
                $"Are you sure you want to delete {count} notifications?",
                "Delete", "Cancel");

            if (confirm)
            {
                foreach (var item in _selectedItems.ToList())
                {
                    _allNotifications.Remove(item);
                }
                SaveNotifications();
                FilterNotifications();
                ClearSelection();
            }
        }

        private async void OpenSettings()
        {
            await DisplayAlert("Notification Settings",
                "Configure notification preferences, sounds, and delivery options",
                "OK");
        }

        private async void ShowMainMenu()
        {
            var autoMarkStatus = AutoMarkEnabled ? "? Disable auto-mark (7 days)" : "? Enable auto-mark (7 days)";

            var action = await DisplayActionSheet("Notification Settings", "Cancel", null,
                "Clear all notifications",
                autoMarkStatus,
                "Notification sounds",
                "About notifications");

            if (action == "Clear all notifications")
            {
                var confirm = await DisplayAlert("Clear All", "Delete all notifications?", "Yes", "No");
                if (confirm)
                {
                    _allNotifications.Clear();
                    SaveNotifications();
                    FilterNotifications();
                    await DisplayAlert("Success", "All notifications cleared", "OK");
                }
            }
            else if (action == "? Enable auto-mark (7 days)" || action == "Enable auto-mark (7 days)")
            {
                ToggleAutoMark();
            }
            else if (action == "? Disable auto-mark (7 days)" || action == "Disable auto-mark (7 days)")
            {
                ToggleAutoMark();
            }
            else if (action == "Notification sounds")
            {
                await DisplayAlert("Notification Sounds", "Sound settings will be available in the next update", "OK");
            }
            else if (action == "About notifications")
            {
                await DisplayAlert("About Notifications",
                    "Notifications help you stay updated with activity from people you follow.\n\n" +
                    "• Priority notifications are highlighted in pink\n" +
                    "• Unread notifications have a teal ring around the avatar\n" +
                    "• Notifications older than 7 days can be auto-marked as read",
                    "OK");
            }
        }

        private async Task RefreshNotifications()
        {
            IsRefreshing = true;
            await Task.Delay(1000);
            LoadSavedNotifications();

            if (AutoMarkEnabled)
            {
                ApplyAutoMarkRead();
            }

            FilterNotifications();
            IsRefreshing = false;
        }

        private void OnNotificationTapped(NotificationItem item)
        {
            if (item == null) return;
            MarkAsRead(item);
            NavigateToContent(item);
        }

        private async void NavigateToContent(NotificationItem item)
        {
            if (item == null || _isNavigating) return;

            try
            {
                _isNavigating = true;

                if (item.PostId.HasValue)
                {
                    var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

                    // Create CommentsPage with correct constructor parameters
                    var commentsPage = new CommentsPage(item.PostId.Value, currentUserPhone);

                    // Use PushAsync instead of Shell navigation for more reliable navigation
                    await Navigation.PushAsync(commentsPage);
                }
                else if (!string.IsNullOrWhiteSpace(item.ActorPhone))
                {
                    await Shell.Current.GoToAsync($"profilepage?phone={Uri.EscapeDataString(item.ActorPhone)}&viewOnly=true");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Navigation Error",
                        "Could not open this notification. Please try again.",
                        "OK");
                });
            }
            finally
            {
                _isNavigating = false;
            }
        }
        private void MarkAsRead(NotificationItem item)
        {
            if (item?.IsRead == true) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (item != null)
                {
                    item.IsRead = true;
                    var index = _allNotifications.IndexOf(item);
                    if (index >= 0) _allNotifications[index] = item;
                    SaveNotifications();
                    FilterNotifications();
                    MessagingCenter.Send(this, "NotificationRead", item);
                }
            });
        }

        private bool IsCurrentUser(string actorPhone)
        {
            var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            return !string.IsNullOrEmpty(currentUserPhone) &&
                   string.Equals(actorPhone?.Trim(), currentUserPhone.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void MarkAsUnread(NotificationItem item)
        {
            if (item?.IsRead == false) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (item != null)
                {
                    item.IsRead = false;
                    var index = _allNotifications.IndexOf(item);
                    if (index >= 0) _allNotifications[index] = item;
                    SaveNotifications();
                    FilterNotifications();
                }
            });
        }

        private void MarkAllAsRead()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                bool anyChanged = false;
                foreach (var item in _allNotifications.Where(n => !n.IsRead))
                {
                    item.IsRead = true;
                    anyChanged = true;
                }
                if (anyChanged)
                {
                    SaveNotifications();
                    FilterNotifications();
                }
            });
        }

        private async void OnNotificationMenuClicked(NotificationItem item)
        {
            if (item == null) return;

            var markOption = item.IsRead ? "Mark as unread" : "Mark as read";
            var priorityOption = item.IsPriority ? "Remove priority" : "Set priority";

            var action = await DisplayActionSheet(null, "Cancel", null,
                markOption,
                priorityOption,
                "Delete notification",
                "Mute this user");

            switch (action)
            {
                case "Mark as read":
                    MarkAsRead(item);
                    break;
                case "Mark as unread":
                    MarkAsUnread(item);
                    break;
                case "Set priority":
                    TogglePriority(item, true);
                    break;
                case "Remove priority":
                    TogglePriority(item, false);
                    break;
                case "Delete notification":
                    RemoveNotification(item);
                    break;
                case "Mute this user":
                    await MuteUser(item.ActorPhone, item.DisplayActorText);
                    break;
            }
        }

        private async Task MuteUser(string phone, string name)
        {
            var confirm = await DisplayAlert("Mute User",
                $"Stop notifications from {name}?",
                "Mute", "Cancel");

            if (confirm)
            {
                RemoveAllFromActor(phone);
                await DisplayAlert("Muted", $"You won't receive notifications from {name}", "OK");
            }
        }

        private void TogglePriority(NotificationItem item, bool setPriority)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                item.IsPriority = setPriority;
                item.PriorityLevel = setPriority ? 1 : 0;

                var index = _allNotifications.IndexOf(item);
                if (index >= 0) _allNotifications[index] = item;

                SaveNotifications();
                FilterNotifications();
            });
        }

        private void RemoveNotification(NotificationItem item)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _allNotifications.Remove(item);
                SaveNotifications();
                FilterNotifications();
            });
        }

        private void RemoveAllFromActor(string actorPhone)
        {
            if (string.IsNullOrWhiteSpace(actorPhone)) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var toRemove = _allNotifications.Where(n =>
                    string.Equals(n.ActorPhone?.Trim(), actorPhone.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var item in toRemove) _allNotifications.Remove(item);
                SaveNotifications();
                FilterNotifications();
            });
        }

        private void LoadSavedNotifications()
        {
            try
            {
                var json = Preferences.Get(NotificationsPrefKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonSerializer.Deserialize<List<NotificationItem>>(json);
                    if (list?.Any() == true)
                    {
                        _allNotifications = new ObservableCollection<NotificationItem>(
                            ConsolidateNotificationGroups(list)
                        );
                        OnPropertyChanged(nameof(UnreadCount));
                        OnPropertyChanged(nameof(PriorityCount));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load error: {ex}");
            }
            _allNotifications = new ObservableCollection<NotificationItem>();
        }

        private List<NotificationItem> ConsolidateNotificationGroups(List<NotificationItem> notifications)
        {
            var groups = new Dictionary<string, NotificationItem>();

            foreach (var notif in notifications)
            {
                string groupId = notif.PostId.HasValue ?
                    $"post_{notif.PostId.Value}_{notif.Action}" :
                    $"unique_{notif.Timestamp.Ticks}";

                if (groups.TryGetValue(groupId, out var existing))
                {
                    if (!existing.ActorPhones.Contains(notif.ActorPhone))
                    {
                        existing.ActorNames.Add(notif.Actor);
                        existing.ActorPhones.Add(notif.ActorPhone);
                        existing.ActorCount = existing.ActorNames.Count;
                        existing.Timestamp = DateTime.UtcNow;
                    }
                }
                else
                {
                    notif.GroupId = groupId;
                    notif.ActorNames = new List<string> { notif.Actor };
                    notif.ActorPhones = new List<string> { notif.ActorPhone };
                    notif.ActorCount = 1;
                    groups[groupId] = notif;
                }
            }

            return groups.Values.OrderByDescending(n => n.Timestamp).ToList();
        }

        private void SaveNotifications()
        {
            try
            {
                var json = JsonSerializer.Serialize(_allNotifications.ToList());
                Preferences.Set(NotificationsPrefKey, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save error: {ex}");
            }
        }

        private async void AddAndSaveNotification(NotificationItem newItem)
        {
            if (newItem == null) return;

            var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            if (string.Equals(newItem.ActorPhone, currentUserPhone, StringComparison.OrdinalIgnoreCase))
                return;

            newItem.IsRead = false;
            newItem.SetActionIconFromAction();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _allNotifications.Insert(0, newItem);

                const int Max = 200;
                while (_allNotifications.Count > Max)
                    _allNotifications.RemoveAt(_allNotifications.Count - 1);

                SaveNotifications();
                FilterNotifications();
                OnPropertyChanged(nameof(UnreadCount));
                OnPropertyChanged(nameof(PriorityCount));
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Reload auto-mark setting
            AutoMarkEnabled = Preferences.Get(AutoMarkDaysPrefKey, false);

            MessagingCenter.Subscribe<object, NotificationItem>(this, "NewNotificationStructured", (s, n) => AddAndSaveNotification(n));
            MessagingCenter.Subscribe<object, string>(this, "NewNotification", (s, m) => AddAndSaveNotification(NotificationItem.FromMessage(m)));
            MessagingCenter.Subscribe<object, int>(this, "NotificationStoreChanged_RemoveComment", (s, id) => RemoveInMemoryByCommentId(id));
            MessagingCenter.Subscribe<object, string>(this, "NotificationStoreChanged_RemoveReaction", (s, payload) => RemoveInMemoryByReaction(payload));

            OnPropertyChanged(nameof(UnreadCount));
            OnPropertyChanged(nameof(PriorityCount));

            if (AutoMarkEnabled)
            {
                ApplyAutoMarkRead();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<object, NotificationItem>(this, "NewNotificationStructured");
            MessagingCenter.Unsubscribe<object, string>(this, "NewNotification");
            MessagingCenter.Unsubscribe<object, int>(this, "NotificationStoreChanged_RemoveComment");
            MessagingCenter.Unsubscribe<object, string>(this, "NotificationStoreChanged_RemoveReaction");
        }

        private void RemoveInMemoryByCommentId(int commentId)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var toRemove = _allNotifications.Where(n => n.CommentId == commentId).ToList();
                foreach (var r in toRemove) _allNotifications.Remove(r);
                SaveNotifications();
                FilterNotifications();
            });
        }

        private void RemoveInMemoryByReaction(string payload)
        {
            try
            {
                var parts = (payload ?? string.Empty).Split('|');
                if (parts.Length >= 1 && int.TryParse(parts[0], out var postId))
                {
                    var actorPhone = parts.Length > 1 ? parts[1] : string.Empty;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var toRemove = _allNotifications.Where(n =>
                            n.PostId == postId &&
                            !string.IsNullOrWhiteSpace(actorPhone) &&
                            string.Equals(n.ActorPhone?.Trim(), actorPhone.Trim(), StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                        foreach (var r in toRemove) _allNotifications.Remove(r);
                        SaveNotifications();
                        FilterNotifications();
                    });
                }
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // NotificationItem Class
    public class NotificationItem : INotifyPropertyChanged
    {
        public string Actor { get; set; } = string.Empty;
        public string ActorPhone { get; set; } = string.Empty;
        public string ActorProfileImagePath { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string TargetPhone { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public int? PostId { get; set; }
        public int? CommentId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Grouping properties
        public string GroupId { get; set; } = string.Empty;
        public List<string> ActorNames { get; set; } = new List<string>();
        public List<string> ActorPhones { get; set; } = new List<string>();
        public List<string> ActorProfileImages { get; set; } = new List<string>();
        public int ActorCount { get; set; } = 1;

        // Priority and read status
        public bool IsPriority { get; set; }
        public int PriorityLevel { get; set; }
        public bool IsRead { get; set; }

        // UI Enhancement properties
        private string _actionIcon = "??";
        public string ActionIcon
        {
            get => _actionIcon;
            set
            {
                _actionIcon = value;
                OnPropertyChanged();
            }
        }

        public bool HasReaction { get; set; }
        public string ReactionImage { get; set; } = string.Empty;
        public string ReactionType { get; set; } = string.Empty;
        public string CommentContent { get; set; } = string.Empty;

        // Post Image Properties
        private string _postImagePath = string.Empty;
        private List<string> _postImagePathsList = new();

        public string PostImagePath
        {
            get => _postImagePath;
            set
            {
                _postImagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPostImage));
                OnPropertyChanged(nameof(FirstImagePath));
            }
        }

        public List<string> PostImagePathsList
        {
            get => _postImagePathsList;
            set
            {
                _postImagePathsList = value ?? new List<string>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPostImage));
                OnPropertyChanged(nameof(FirstImagePath));
            }
        }

        public bool HasPostImage => PostImagePathsList?.Any() == true || (!string.IsNullOrEmpty(PostImagePath) && File.Exists(PostImagePath));
        public string FirstImagePath => HasPostImage ? (PostImagePathsList?.FirstOrDefault() ?? PostImagePath) : null;
        public bool HasComment => !string.IsNullOrEmpty(CommentContent);

        public string DisplayActorText
        {
            get
            {
                if (ActorCount <= 1 || ActorNames.Count <= 1)
                    return ActorNames.Count > 0 ? ActorNames[0] : Actor;
                else if (ActorCount == 2)
                    return $"{ActorNames[0]} and {ActorNames[1]}";
                else
                    return $"{ActorNames[0]} and {ActorCount - 1} others";
            }
        }

        public string DisplayTime => GetPrettyTime();

        private string GetPrettyTime()
        {
            try
            {
                var now = DateTime.UtcNow;
                var span = now - Timestamp;

                if (span.TotalSeconds < 60) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d";
                return Timestamp.ToLocalTime().ToString("MMM d");
            }
            catch { return Timestamp.ToLocalTime().ToString("g"); }
        }

        public void SetActionIconFromAction()
        {
            if (string.IsNullOrEmpty(Action))
            {
                ActionIcon = "??";
                return;
            }

            var actionLower = Action.ToLowerInvariant();

            if (actionLower.Contains("comment"))
                ActionIcon = "??";
            else if (actionLower.Contains("react") || actionLower.Contains("like"))
                ActionIcon = "??";
            else if (actionLower.Contains("mention") || actionLower.Contains("tag"))
                ActionIcon = "@";
            else if (actionLower.Contains("share"))
                ActionIcon = "??";
            else if (actionLower.Contains("follow"))
                ActionIcon = "??";
            else
                ActionIcon = "??";
        }

        public void SetReactionImageFromType()
        {
            if (string.IsNullOrEmpty(ReactionType))
            {
                HasReaction = false;
                ReactionImage = string.Empty;
                return;
            }

            var reactionLower = ReactionType.ToLowerInvariant();
            HasReaction = true;

            switch (reactionLower)
            {
                case "like":
                    ReactionImage = "??";
                    break;
                case "love":
                    ReactionImage = "??";
                    break;
                case "laugh":
                case "haha":
                    ReactionImage = "??";
                    break;
                case "wow":
                case "surprised":
                    ReactionImage = "??";
                    break;
                case "sad":
                    ReactionImage = "??";
                    break;
                case "angry":
                    ReactionImage = "??";
                    break;
                default:
                    ReactionImage = "??";
                    break;
            }
        }

        public void SetPostImage(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                PostImagePath = imagePath;
                if (!PostImagePathsList.Contains(imagePath))
                {
                    PostImagePathsList.Add(imagePath);
                }
            }
        }

        public void SetPostImages(List<string> imagePaths)
        {
            if (imagePaths?.Any() == true)
            {
                PostImagePathsList = imagePaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)).ToList();
                PostImagePath = PostImagePathsList.FirstOrDefault() ?? string.Empty;
            }
        }

        public static NotificationItem FromPost(Lock.Models.Post post, string action, string actorName, string actorPhone, string actorProfileImagePath = null)
        {
            if (post == null) return null;

            var item = new NotificationItem
            {
                Timestamp = DateTime.UtcNow,
                PostId = post.Id,
                Actor = actorName,
                ActorPhone = actorPhone,
                ActorProfileImagePath = actorProfileImagePath ?? string.Empty,
                Action = action,
                Preview = post.Content?.Length > 100 ? post.Content.Substring(0, 100) + "..." : post.Content ?? "",
            };

            if (post.ImagePathsList?.Any() == true)
            {
                item.SetPostImages(post.ImagePathsList?.ToList());
            }

            item.SetActionIconFromAction();
            return item;
        }

        public static NotificationItem FromMessage(string message, List<string> postImagePaths = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return new NotificationItem { Actor = "Someone", Action = "did something", Preview = "" };

            try
            {
                var item = new NotificationItem { Timestamp = DateTime.UtcNow };

                if (postImagePaths?.Any() == true)
                {
                    item.SetPostImages(postImagePaths);
                }

                if (message.Contains("created a post", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = message.Split(new[] { "created a post" }, StringSplitOptions.RemoveEmptyEntries);
                    item.Actor = parts.Length > 0 ? parts[0].Trim() : "Someone";
                    item.Action = "posted";
                    var previewStart = message.IndexOf(':');
                    if (previewStart >= 0 && previewStart + 1 < message.Length)
                    {
                        item.Preview = message.Substring(previewStart + 1).Trim().Trim('"').Trim();
                    }
                }
                else if (message.Contains("reacted", StringComparison.OrdinalIgnoreCase))
                {
                    var words = message.Split(' ');
                    item.Actor = words.Length > 0 ? words[0] : "Someone";
                    item.Action = "reacted";

                    if (message.Contains("with", StringComparison.OrdinalIgnoreCase))
                    {
                        var withIdx = message.IndexOf("with", StringComparison.OrdinalIgnoreCase);
                        if (withIdx >= 0)
                        {
                            var reactionPart = message.Substring(withIdx + 4).Trim();
                            var reactionWords = reactionPart.Split(' ');
                            if (reactionWords.Length > 0)
                            {
                                item.ReactionType = reactionWords[0].TrimEnd('.', '!', '?');
                                item.HasReaction = true;
                                item.SetReactionImageFromType();
                            }
                        }
                    }

                    var toIdx = message.IndexOf("to ");
                    if (toIdx >= 0 && toIdx + 3 < message.Length)
                        item.Preview = message.Substring(toIdx + 3).Trim();
                }
                else if (message.Contains("commented", StringComparison.OrdinalIgnoreCase))
                {
                    var onIdx = message.IndexOf(" on ");
                    if (onIdx > 0)
                    {
                        item.Actor = message.Substring(0, onIdx).Trim();
                        item.Action = "commented";
                        var colon = message.IndexOf(':');
                        if (colon > 0 && colon + 1 < message.Length)
                        {
                            item.Preview = message.Substring(colon + 1).Trim().Trim('"');
                            item.CommentContent = item.Preview;
                        }
                    }
                    else
                    {
                        item.Preview = message;
                        item.CommentContent = message;
                        item.Actor = "Someone";
                        item.Action = "commented";
                    }
                }
                else if (message.Contains("mentioned", StringComparison.OrdinalIgnoreCase) ||
                         message.Contains("tagged", StringComparison.OrdinalIgnoreCase))
                {
                    item.Actor = message.Split(' ')[0];
                    item.Action = "mentioned you";
                    item.IsPriority = true;
                    item.PriorityLevel = 3;
                    item.Preview = message;
                }
                else if (message.Contains("followed", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = message.Split(new[] { "followed" }, StringSplitOptions.RemoveEmptyEntries);
                    item.Actor = parts.Length > 0 ? parts[0].Trim() : "Someone";
                    item.Action = "followed you";
                    item.Preview = $"{item.Actor} started following you";
                }
                else
                {
                    item.Preview = message;
                    item.Actor = "Notification";
                    item.Action = "";
                }

                item.SetActionIconFromAction();
                return item;
            }
            catch
            {
                return new NotificationItem
                {
                    Preview = message,
                    Actor = "Notification",
                    Action = ""
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}