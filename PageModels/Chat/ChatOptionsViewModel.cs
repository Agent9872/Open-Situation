using Lock.Chat.Services;
using Lock.Models.Chat;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lock.Pages.Chat;

public class ChatOptionsViewModel : INotifyPropertyChanged
{
    private bool _isMediaTab = true;
    private bool _isInfoTab = false;
    private bool _isBlocked = false;
    private bool _isLoading = true;
    private string _selectedDateFilter = "All Time";
    private ObservableCollection<MediaDisplayItem> _filteredMediaItems = new();

    public string ContactName { get; }
    public string PhoneNumber { get; }
    public string ProfileImagePath { get; } = string.Empty;

    public ObservableCollection<MediaDisplayItem> MediaItems { get; } = new();

    // Filtered collection for UI binding
    public ObservableCollection<MediaDisplayItem> FilteredMediaItems
    {
        get => _filteredMediaItems;
        set
        {
            _filteredMediaItems = value;
            OnPropertyChanged();
        }
    }

    public string ConversationId { get; }

    public string SelectedDateFilter
    {
        get => _selectedDateFilter;
        set
        {
            if (_selectedDateFilter != value)
            {
                _selectedDateFilter = value;
                OnPropertyChanged();
                ApplyDateFilter();
            }
        }
    }

    public bool IsMediaTab
    {
        get => _isMediaTab;
        set
        {
            if (_isMediaTab != value)
            {
                _isMediaTab = value;
                OnPropertyChanged();
                if (value && _isInfoTab)
                {
                    IsInfoTab = false;
                }
            }
        }
    }

    public bool IsInfoTab
    {
        get => _isInfoTab;
        set
        {
            if (_isInfoTab != value)
            {
                _isInfoTab = value;
                OnPropertyChanged();
                if (value && _isMediaTab)
                {
                    IsMediaTab = false;
                }
            }
        }
    }

    public bool IsBlocked
    {
        get => _isBlocked;
        set
        {
            if (_isBlocked != value)
            {
                _isBlocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlockButtonText));
                OnPropertyChanged(nameof(BlockButtonIcon));
            }
        }
    }

    public string BlockButtonText => IsBlocked ? "Unblock user" : "Block user";

    public string BlockButtonIcon => IsBlocked ?
        "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z" :
        "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zM4 12c0-4.42 3.58-8 8-8 1.85 0 3.55.63 4.9 1.69L5.69 19.9C4.63 18.55 4 16.85 4 15z";

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    private string _backgroundImagePath = string.Empty;
    public string BackgroundImagePath
    {
        get => _backgroundImagePath;
        set
        {
            if (_backgroundImagePath != value)
            {
                _backgroundImagePath = value;
                OnPropertyChanged();
            }
        }
    }

    public ChatOptionsViewModel(
          string contactName,
          string phoneNumber,
          string conversationId,
          string? profileImagePath = null)
    {
        ContactName = string.IsNullOrWhiteSpace(contactName) ? "Contact" : contactName.Trim();
        PhoneNumber = phoneNumber ?? string.Empty;
        ConversationId = conversationId ?? string.Empty;
        ProfileImagePath = profileImagePath ?? "default_profile.png";

        InitializeAsync().FireAndForget();
    }

    private async Task InitializeAsync()
    {
        IsLoading = true;

        try
        {
            await LoadBlockStatusAsync();
            await LoadBackgroundImageSettingAsync();

            if (!string.IsNullOrEmpty(ConversationId))
            {
                Debug.WriteLine($"Loading media for conversation: {ConversationId}");
                await LoadMediaAsync(ConversationId);
            }
            else
            {
                Debug.WriteLine("Warning: ConversationId is empty, trying PhoneNumber fallback");
                await LoadMediaAsync(PhoneNumber);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBlockStatusAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(PhoneNumber))
                return;

            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            if (string.IsNullOrEmpty(currentUserPhone))
                return;

            IsBlocked = await ChatRepository.IsUserBlockedAsync(currentUserPhone, PhoneNumber);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadBlockStatusAsync error: {ex.Message}");
        }
    }

    private async Task LoadBackgroundImageSettingAsync()
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            if (string.IsNullOrEmpty(currentUserPhone) || string.IsNullOrEmpty(ConversationId))
                return;

            string key = $"chat_bg_{currentUserPhone}_{ConversationId}";
            string savedPath = Preferences.Get(key, string.Empty);

            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                BackgroundImagePath = savedPath;
            }
            else
            {
                BackgroundImagePath = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadBackgroundImageSettingAsync error: {ex}");
            BackgroundImagePath = string.Empty;
        }
    }

    private async Task LoadMediaAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return;

        try
        {
            MediaItems.Clear();

            var messages = await ChatRepository.GetMessagesAsync(ConversationId, 1000);

            Debug.WriteLine($"LoadMediaAsync: Found {messages.Count} total messages");

            foreach (var msg in messages)
            {
                if (msg.MediaItems?.Any() == true)
                {
                    Debug.WriteLine($"Message {msg.Id} has {msg.MediaItems.Count} media items");
                    foreach (var item in msg.MediaItems)
                    {
                        if (item.Type == "image" && !string.IsNullOrEmpty(item.Path))
                        {
                            MediaItems.Add(new MediaDisplayItem
                            {
                                Path = item.Path,
                                Type = item.Type,
                                SentAt = msg.SentAt
                            });
                            Debug.WriteLine($"Added media from MediaItems: {item.Path}");
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(msg.MediaPath) &&
                         (msg.MediaType == "image" || string.IsNullOrEmpty(msg.MediaType)))
                {
                    Debug.WriteLine($"Message {msg.Id} has single media: {msg.MediaPath}");
                    MediaItems.Add(new MediaDisplayItem
                    {
                        Path = msg.MediaPath,
                        Type = "image",
                        SentAt = msg.SentAt
                    });
                }
            }

            Debug.WriteLine($"LoadMediaAsync: Total media items loaded: {MediaItems.Count}");

            // Apply initial filter (All Time)
            ApplyDateFilter();

            OnPropertyChanged(nameof(MediaItems));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Media load failed: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Apply date filter to media items
    private void ApplyDateFilter()
    {
        if (MediaItems == null || MediaItems.Count == 0)
        {
            FilteredMediaItems = new ObservableCollection<MediaDisplayItem>();
            return;
        }

        var now = DateTime.Now;
        var filtered = MediaItems.Where(item => ShouldIncludeItem(item.SentAt, now)).ToList();

        // Sort by date (newest first)
        filtered = filtered.OrderByDescending(item => item.SentAt).ToList();

        FilteredMediaItems = new ObservableCollection<MediaDisplayItem>(filtered);

        Debug.WriteLine($"Date filter '{SelectedDateFilter}': {filtered.Count} of {MediaItems.Count} items");
    }

    // Determine if a media item should be included based on the selected filter
    private bool ShouldIncludeItem(DateTime sentAt, DateTime now)
    {
        return SelectedDateFilter switch
        {
            "Today" => sentAt.Date == now.Date,
            "This Week" => sentAt.Date >= now.AddDays(-7).Date && sentAt.Date <= now.Date,
            "This Month" => sentAt.Year == now.Year && sentAt.Month == now.Month,
            "This Year" => sentAt.Year == now.Year,
            "All Time" => true,
            _ => true
        };
    }

    // Public method to set the date filter
    public void SetDateFilter(string filter)
    {
        SelectedDateFilter = filter;
    }

    public async Task RefreshBlockStatusAsync()
    {
        await LoadBlockStatusAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class TaskExtensions
{
    public static async void FireAndForget(this Task task)
    {
        try { await task; }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"FireAndForget error: {ex}"); }
    }
}