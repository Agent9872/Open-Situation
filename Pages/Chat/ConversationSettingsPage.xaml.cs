using Lock.Chat.Services;
using Lock.Models.Chat;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage; // Required for MediaPicker
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class ConversationSettingsPage : ContentPage
    {
        private readonly string _matchId;
        private readonly string _matchName;
        private readonly string _currentUserPhone;
        private Conversation? _conversation;

        // 1. Added backing field for the image
        private string _matchProfileImage = "default_user.png";

        public ConversationSettingsPage(string matchId = null, string matchName = null, string matchType = null)
        {
            _matchId = matchId ?? string.Empty;
            _matchName = matchName ?? "Unknown Match";
            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

            InitializeComponent();
            BindingContext = this;
            _ = LoadConversationAsync();
        }

        // 2. Public properties for Data Binding
        public string MatchId => _matchId;
        public string MatchName => _matchName;

        public string MatchProfileImage
        {
            get => _matchProfileImage;
            set
            {
                if (_matchProfileImage != value)
                {
                    _matchProfileImage = value;
                    OnPropertyChanged(nameof(MatchProfileImage));
                }
            }
        }

        public string CreatedAt => _conversation != null
            ? _conversation.CreatedAt.ToString("MMM dd, yyyy")
            : DateTime.UtcNow.ToString("MMM dd, yyyy");

        private async Task LoadConversationAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_matchId))
                {
                    _conversation = new Conversation
                    {
                        ConversationId = Guid.NewGuid().ToString(),
                        ParticipantA = _currentUserPhone,
                        ParticipantB = _matchName,
                        CreatedAt = DateTime.UtcNow,
                        LastMessageAt = DateTime.UtcNow
                    };

                    await ChatRepository.SaveConversationSilentlyAsync(_conversation);
                }
                else
                {
                    _conversation = await ChatRepository.GetConversationAsync(_matchId);

                    if (_conversation == null)
                    {
                        _conversation = new Conversation
                        {
                            ConversationId = _matchId,
                            ParticipantA = _currentUserPhone,
                            ParticipantB = _matchName,
                            CreatedAt = DateTime.UtcNow,
                            LastMessageAt = DateTime.UtcNow
                        };

                        await ChatRepository.SaveConversationSilentlyAsync(_conversation);
                    }
                }

                // 3. Load existing image from model if available
                // MatchProfileImage = _conversation.ProfileImageUrl ?? "default_user.png";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(CreatedAt));
                    OnPropertyChanged(nameof(MatchProfileImage));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading conversation: {ex}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", "Could not load conversation settings", "OK"));
            }
        }

        // 4. NEW: Handler to change the profile image
        private async void OnChangeImageTapped(object sender, EventArgs e)
        {
            try
            {
                // Opens the native photo picker
                var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select Profile Image"
                });

                if (result != null)
                {
                    // Update UI immediately
                    MatchProfileImage = result.FullPath;

                    // Update the conversation object so it saves on ClosePage()
                    if (_conversation != null)
                    {
                        // Ensure your Conversation model has this property:
                        // _conversation.ProfileImageUrl = result.FullPath; 
                    }
                }
            }
            catch (PermissionException)
            {
                await DisplayAlert("Permissions Denied", "Please allow photo access in settings.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error picking image: {ex}");
            }
        }

        private async void OnSearchTapped(object sender, EventArgs e)
        {
            try
            {
                string searchTerm = await DisplayPromptAsync("Search", "Enter text to search for:", "Search", "Cancel");

                if (!string.IsNullOrWhiteSpace(searchTerm) && _conversation != null)
                {
                    await DisplayAlert("Search", $"Searching for: {searchTerm}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in search: {ex}");
            }
        }

        private async void OnReadAllTapped(object sender, EventArgs e)
        {
            try
            {
                if (_conversation != null)
                {
                    await ChatRepository.MarkMessagesReadAsync(_conversation.ConversationId, _currentUserPhone);
                    await DisplayAlert("Success", "All messages marked as read", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error marking messages as read: {ex}");
                await DisplayAlert("Error", "Could not mark messages as read", "OK");
            }
        }

        private async void OnCreateGroupTapped(object sender, EventArgs e)
        {
            try
            {
                await ClosePage();           // Close the settings modal first
                await Task.Delay(100);       // Small delay for smooth UX

                var createGroupPage = new CreateGroupPage();

                // Use Shell navigation if available (recommended in MAUI)
                if (Shell.Current?.Navigation != null)
                    await Shell.Current.Navigation.PushAsync(createGroupPage);
                else
                    await Navigation.PushAsync(createGroupPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to Create Group page: {ex}");
                await DisplayAlert("Error", "Could not open group creation page", "OK");
            }
        }

        private async Task NavigateAfterCloseAsync(ContentPage page)
        {
            await ClosePage();
            await Task.Delay(80);

            if (Shell.Current?.Navigation != null)
                await Shell.Current.Navigation.PushAsync(page);
            else
                await Navigation.PushAsync(page);
        }

        // ========== ADD THIS METHOD ==========
        private async void OnFaqTapped(object sender, EventArgs e)
        {
            try
            {
                // Close the current modal (ConversationSettingsPage)
                await Navigation.PopModalAsync();

                // Create and navigate to FAQ page (non-modal, regular page)
                var faqPage = new FaqPage();

                // Push as regular page (not modal) - same pattern as CreateGroupPage
                if (Shell.Current?.Navigation != null)
                    await Shell.Current.Navigation.PushAsync(faqPage);
                else
                    await Navigation.PushAsync(faqPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to FAQ page: {ex}");
                await DisplayAlert("Error", "Could not open FAQ page", "OK");
            }
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await ClosePage();
        }

        private async void OnCloseTapped(object sender, EventArgs e)
        {
            await ClosePage();
        }

        private async Task ClosePage()
        {
            try
            {
                if (_conversation != null && !string.IsNullOrEmpty(_conversation.ConversationId))
                {
                    await ChatRepository.SaveConversationSilentlyAsync(_conversation);
                }
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error closing page: {ex.Message}");
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
            }
        }

        protected override bool OnBackButtonPressed()
        {
            MainThread.BeginInvokeOnMainThread(async () => await ClosePage());
            return true;
        }
    }
}