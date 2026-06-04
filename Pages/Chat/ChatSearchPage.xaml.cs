using Lock.Models.Chat;
using Lock.Services.Chat;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Lock.Pages.Chat
{
    public partial class ChatSearchPage : ContentPage
    {
        private string _currentUserPhone;
        private ObservableCollection<SearchResultItem> _searchResults = new();

        public ObservableCollection<SearchResultItem> SearchResults
        {
            get => _searchResults;
            set
            {
                _searchResults = value;
                OnPropertyChanged();
            }
        }

        public ICommand SearchCommand => new Command<string>(async (query) => await PerformSearchAsync(query));

        public ChatSearchPage()
        {
            InitializeComponent();
            BindingContext = this;

            // Hide the default navigation bar (same as ChatPage)
            Shell.SetNavBarIsVisible(this, false);

            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

            Dispatcher.Dispatch(() =>
            {
                SearchBar?.Focus();
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Ensure navigation bar stays hidden when page appears
            Shell.SetNavBarIsVisible(this, false);
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue) || e.NewTextValue.Length < 2)
            {
                SearchResults.Clear();
                if (ResultsCountLabel != null)
                    ResultsCountLabel.IsVisible = false;
                return;
            }

            await PerformSearchAsync(e.NewTextValue);
        }

        private async Task PerformSearchAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return;

                Debug.WriteLine($"Searching for: {query}");

                if (ResultsCountLabel != null)
                {
                    ResultsCountLabel.Text = "Searching...";
                    ResultsCountLabel.IsVisible = true;
                }

                await Task.Delay(100);

                // Get all conversations from Supabase
                var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                    $"or(ParticipantA.eq.{Uri.EscapeDataString(_currentUserPhone)},ParticipantB.eq.{Uri.EscapeDataString(_currentUserPhone)})");

                if (conversations == null || !conversations.Any())
                {
                    if (ResultsCountLabel != null)
                        ResultsCountLabel.Text = "No conversations found";
                    return;
                }

                var allResults = new List<SearchResultItem>();

                foreach (var conv in conversations)
                {
                    // Get messages for this conversation
                    var messages = await SupabaseService.GetAsync<ChatMessage>("ChatMessages",
                        $"ConversationId=eq.{Uri.EscapeDataString(conv.ConversationId)}&order=SentAt.desc&limit=500");

                    var matchingMessages = messages.Where(m =>
                        !string.IsNullOrEmpty(m.Content) &&
                        m.Content.Contains(query, StringComparison.OrdinalIgnoreCase));

                    foreach (var msg in matchingMessages)
                    {
                        var senderPhone = msg.SenderPhone == _currentUserPhone ? _currentUserPhone : msg.SenderPhone;
                        var senderInfo = await GetUserInfoAsync(senderPhone);

                        // Get the other participant for the conversation
                        string otherUserPhone = msg.SenderPhone == _currentUserPhone
                            ? conv.GetOtherParticipant(_currentUserPhone)
                            : msg.SenderPhone;

                        allResults.Add(new SearchResultItem
                        {
                            MessageId = msg.Id,  // Now string to string
                            ConversationId = conv.ConversationId,
                            SenderPhone = senderPhone,
                            SenderName = msg.SenderPhone == _currentUserPhone ? "You" : senderInfo.Name,
                            SenderInitials = GetInitials(msg.SenderPhone == _currentUserPhone ? "You" : senderInfo.Name),
                            SenderProfileImage = senderInfo.ProfileImagePath,
                            HasProfileImage = !string.IsNullOrEmpty(senderInfo.ProfileImagePath) && File.Exists(senderInfo.ProfileImagePath),
                            Content = msg.Content,
                            ContentPreview = HighlightSearchTerm(msg.Content, query),
                            SentAt = msg.SentAt.ToLocalTime(),
                            HasMedia = msg.HasMedia,
                            MediaCount = msg.MediaCount,
                            OtherUserPhone = otherUserPhone
                        });
                    }
                }

                var sortedResults = allResults.OrderByDescending(r => r.SentAt).ToList();

                SearchResults.Clear();
                foreach (var result in sortedResults)
                {
                    SearchResults.Add(result);
                }

                if (ResultsCountLabel != null)
                {
                    ResultsCountLabel.Text = $"{SearchResults.Count} message(s) found";
                    ResultsCountLabel.IsVisible = SearchResults.Any();
                }

                Debug.WriteLine($"Search completed. Found {SearchResults.Count} results");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformSearchAsync error: {ex}");
                if (ResultsCountLabel != null)
                    ResultsCountLabel.Text = "Search failed. Please try again.";
                await DisplayAlert("Error", "Failed to search messages", "OK");
            }
        }

        private async Task<(string Name, string ProfileImagePath)> GetUserInfoAsync(string phoneNumber)
        {
            try
            {
                var users = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phoneNumber)}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    return (user.Name ?? phoneNumber, user.ProfileImagePath);
                }

                return (phoneNumber.Length > 4 ? $"…{phoneNumber[^4..]}" : phoneNumber, null);
            }
            catch
            {
                return (phoneNumber.Length > 4 ? $"…{phoneNumber[^4..]}" : phoneNumber, null);
            }
        }

        private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not SearchResultItem selectedItem)
                return;

            if (sender is CollectionView cv)
                cv.SelectedItem = null;

            try
            {
                var chatPage = new ChatPage(selectedItem.ConversationId, selectedItem.OtherUserPhone);
                await Navigation.PushAsync(chatPage);

                await Task.Delay(500);
                chatPage.ScrollToMessage(selectedItem.MessageId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnResultSelected error: {ex}");
                await DisplayAlert("Error", $"Could not open conversation: {ex.Message}", "OK");
            }
        }

        private string HighlightSearchTerm(string text, string searchTerm)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
                return text;

            int index = text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
                return text.Length > 100 ? text.Substring(0, 100) + "..." : text;

            int start = Math.Max(0, index - 50);
            int length = Math.Min(text.Length - start, 100);
            string snippet = text.Substring(start, length);

            if (start > 0)
                snippet = "..." + snippet;
            if (start + length < text.Length)
                snippet = snippet + "...";

            return snippet;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "You")
                return "U";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && parts[0].Length > 0)
                return parts[0][0].ToString().ToUpper();

            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();

            return "?";
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }

    public class SearchResultItem
    {
        public string MessageId { get; set; } = string.Empty;  // Changed from int to string
        public string ConversationId { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderInitials { get; set; } = string.Empty;
        public string SenderProfileImage { get; set; } = string.Empty;
        public bool HasProfileImage { get; set; }
        public string Content { get; set; } = string.Empty;
        public string ContentPreview { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsVoiceMessage { get; set; }
        public bool HasMedia { get; set; }
        public int MediaCount { get; set; }
        public string OtherUserPhone { get; set; } = string.Empty;

        public string HighlightedContent => ContentPreview ?? Content;
    }
}