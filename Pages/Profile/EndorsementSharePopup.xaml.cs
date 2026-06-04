using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Pages.Chat;
using Lock.Services;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;
using System.Linq;

namespace Lock.Pages.Profile;

public partial class EndorsementSharePopup : Popup
{
    private readonly string _shareText;
    private readonly string _friendPhone;
    private readonly string _friendName;
    private readonly string _requestorName;
    private readonly string _requestorProfileImage;

    public EndorsementSharePopup(string shareText, string friendPhone, string friendName, string requestorName, string requestorProfileImage)
    {
        InitializeComponent();
        _shareText = shareText;
        _friendPhone = friendPhone;
        _friendName = friendName;
        _requestorName = requestorName;
        _requestorProfileImage = requestorProfileImage;

        // Create preview text (first 100 chars)
        string previewText = shareText.Length > 100 ? shareText.Substring(0, 100) + "..." : shareText;

        BindingContext = new
        {
            RequestorName = requestorName,
            RequestorProfileImage = !string.IsNullOrEmpty(requestorProfileImage) && File.Exists(requestorProfileImage)
                ? ImageSource.FromFile(requestorProfileImage)
                : "default_profile.png",
            PreviewText = previewText
        };
    }

    private void OnCloseTapped(object sender, EventArgs e)
    {
        Close();
    }

    private async void OnShareActionTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string action)
            return;

        try
        {
            switch (action)
            {
                case "Share with contact":
                    await ShowContactPickerForSharingAsync();
                    break;

                case "WhatsApp":
                    await ShareToWhatsApp();
                    break;

                case "Telegram":
                    await ShareToTelegram();
                    break;

                case "Facebook":
                    await ShareToFacebook();
                    break;

                case "Twitter":
                    await ShareToTwitter();
                    break;

                case "Copy link":
                    await CopyLinkToClipboardAsync();
                    break;

                case "More":
                    await ShareMoreOptionsAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Share error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to share endorsement request", "OK");
        }
    }

    // ===== SHARE WITH APP CONTACT =====
    private async Task ShowContactPickerForSharingAsync()
    {
        try
        {
            // Create a contact picker for sharing endorsement
            var contactPicker = new ContactPickerPopup(
                _requestorName,
                _friendPhone,
                _requestorProfileImage,
                async (targetPhone, targetName, targetProfileImage) =>
                {
                    await SendEndorsementRequestAsMessageAsync(targetPhone, targetName);
                }
            );

            await Application.Current.MainPage.ShowPopupAsync(contactPicker);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowContactPickerForSharingAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task SendEndorsementRequestAsMessageAsync(string targetUserPhone, string targetUserName)
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

            // Get or create conversation
            string conversationId = await GetOrCreateConversationAsync(currentUserPhone, targetUserPhone, targetUserName);

            // Create message - DON'T set Id, let Supabase auto-generate it or use int
            var message = new ChatMessage
            {
                // Remove the Id assignment - let Supabase handle it
                // If your ChatMessage uses int Id, you cannot assign a string GUID
                ConversationId = conversationId,
                SenderPhone = currentUserPhone,
                RecipientPhone = targetUserPhone,
                MessageType = "text",
                Content = _shareText,
                SentAt = DateTime.UtcNow,
                IsDelivered = true,
                IsRead = false,
                IsLocalOutgoing = true
            };

            // Insert message to Supabase
            await SupabaseService.InsertAsync("ChatMessages", message);

            // Update conversation
            var conversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}&limit=1");

            var conversation = conversations.FirstOrDefault();

            if (conversation != null)
            {
                conversation.LastMessagePreview = _shareText.Length > 50 ? _shareText.Substring(0, 50) + "..." : _shareText;
                conversation.LastMessageAt = DateTime.UtcNow;
                await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conversationId)}", conversation);
            }

            MessagingCenter.Send(this, "MessagesUpdated");
            MessagingCenter.Send(this, "ConversationsUpdated");

            await Application.Current.MainPage.DisplayAlert(
                "Request Sent",
                $"Endorsement request sent to {targetUserName}.",
                "OK"
            );

            Close("request_sent");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SendEndorsementRequestAsMessageAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to send: {ex.Message}", "OK");
        }
    }

    private async Task<string> GetOrCreateConversationAsync(string userPhone, string contactPhone, string contactName)
    {
        try
        {
            // Check for existing conversation in Supabase
            var existingConversations = await SupabaseService.GetAsync<Conversation>("Conversations",
                $"(ParticipantA=eq.{Uri.EscapeDataString(userPhone)}&ParticipantB=eq.{Uri.EscapeDataString(contactPhone)})" +
                $"or(ParticipantA=eq.{Uri.EscapeDataString(contactPhone)}&ParticipantB=eq.{Uri.EscapeDataString(userPhone)})&limit=1");

            var existingConversation = existingConversations.FirstOrDefault();

            if (existingConversation != null)
                return existingConversation.ConversationId;

            string conversationId = Guid.NewGuid().ToString();
            var conversation = new Conversation
            {
                ConversationId = conversationId,
                ParticipantA = userPhone,
                ParticipantB = contactPhone,
                LastMessageAt = DateTime.UtcNow,
                LastMessagePreview = "",
                CreatedAt = DateTime.UtcNow
            };

            await SupabaseService.InsertAsync("Conversations", conversation);
            return conversationId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetOrCreateConversationAsync error: {ex}");
            throw;
        }
    }

    // ===== SOCIAL MEDIA SHARING METHODS =====
    private async Task ShareToWhatsApp()
    {
        try
        {
            var whatsappUri = $"whatsapp://send?text={Uri.EscapeDataString(_shareText)}";
            bool canOpen = await Launcher.Default.CanOpenAsync(whatsappUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(whatsappUri);
            }
            else
            {
                var webWhatsApp = $"https://api.whatsapp.com/send?text={Uri.EscapeDataString(_shareText)}";
                await Launcher.Default.OpenAsync(webWhatsApp);
            }
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToWhatsApp error: {ex}");
            await ShareMoreOptionsAsync();
        }
    }

    private async Task ShareToTelegram()
    {
        try
        {
            var telegramUri = $"tg://msg?text={Uri.EscapeDataString(_shareText)}";
            bool canOpen = await Launcher.Default.CanOpenAsync(telegramUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(telegramUri);
            }
            else
            {
                var webTelegram = $"https://t.me/share/url?url=&text={Uri.EscapeDataString(_shareText)}";
                await Launcher.Default.OpenAsync(webTelegram);
            }
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToTelegram error: {ex}");
            Close();
        }
    }

    private async Task ShareToFacebook()
    {
        try
        {
            var facebookUri = $"fb://facewebmodal/f?href=https://facebook.com/sharer.php?u=&quote={Uri.EscapeDataString(_shareText)}";
            bool canOpen = await Launcher.Default.CanOpenAsync(facebookUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(facebookUri);
            }
            else
            {
                var webFacebook = $"https://www.facebook.com/sharer/sharer.php?u=&quote={Uri.EscapeDataString(_shareText)}";
                await Launcher.Default.OpenAsync(webFacebook);
            }
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToFacebook error: {ex}");
            Close();
        }
    }

    private async Task ShareToTwitter()
    {
        try
        {
            var twitterUri = $"twitter://post?message={Uri.EscapeDataString(_shareText)}";
            bool canOpen = await Launcher.Default.CanOpenAsync(twitterUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(twitterUri);
            }
            else
            {
                var webTwitter = $"https://twitter.com/intent/tweet?text={Uri.EscapeDataString(_shareText)}";
                await Launcher.Default.OpenAsync(webTwitter);
            }
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToTwitter error: {ex}");
            Close();
        }
    }

    private async Task CopyLinkToClipboardAsync()
    {
        await Clipboard.Default.SetTextAsync(_shareText);
        await Application.Current.MainPage.DisplayAlert("Copied", "Endorsement request link copied to clipboard", "OK");
        Close();
    }

    private async Task ShareMoreOptionsAsync()
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = _shareText,
            Title = "Share Endorsement Request"
        });
        Close();
    }
}