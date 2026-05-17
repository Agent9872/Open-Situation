using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Pages.Chat;
using Lock.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PostModel = Lock.Models.Post;

namespace Lock.Pages.Post;

public partial class PostSharePopup : Popup
{
    private readonly PostModel _post;
    private readonly string _currentUserPhone;

    public PostSharePopup(PostModel post, string currentUserPhone)
    {
        InitializeComponent();
        _post = post;
        _currentUserPhone = currentUserPhone;

        var preview = GetPostPreview(post);

        BindingContext = new
        {
            AuthorName = post.AuthorDisplayName ?? "Unknown",
            AuthorProfileImage = post.AuthorProfileImagePath ?? "default_profile.png",
            PostPreview = preview
        };
    }

    private string GetPostPreview(PostModel post)
    {
        if (!string.IsNullOrEmpty(post.Content))
        {
            return post.Content.Length > 100
                ? post.Content.Substring(0, 100) + "..."
                : post.Content;
        }
        else if (post.ImagePathsList?.Any() == true)
        {
            return $"?? {post.ImagePathsList.Length} image(s)";
        }
        else
        {
            return "Shared a post";
        }
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
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to share post", "OK");
        }
    }

    // ===== SHARE WITH APP CONTACT (Copied from ChatOptionsPopup) =====
    private async Task ShowContactPickerForSharingAsync()
    {
        try
        {
            var contactPicker = new ContactPickerPopup(
                _post.AuthorDisplayName ?? "Unknown",
                _post.AuthorPhone ?? string.Empty,
                _post.AuthorProfileImagePath ?? "default_profile.png",
                async (targetPhone, targetName, targetProfileImage) =>
                {
                    string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    string targetConversationId = await GetOrCreateConversationAsync(
                        currentUserPhone,
                        targetPhone,
                        targetName
                    );

                    await SendPostAsMessageAsync(
                        targetPhone,
                        targetConversationId,
                        targetName
                    );
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

    private async Task<string> GetOrCreateConversationAsync(string userPhone, string contactPhone, string contactName)
    {
        try
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Check if conversation already exists
            var existingConversation = await db.Table<Conversation>()
                .Where(c => (c.ParticipantA == userPhone && c.ParticipantB == contactPhone) ||
                           (c.ParticipantA == contactPhone && c.ParticipantB == userPhone))
                .FirstOrDefaultAsync();

            if (existingConversation != null)
                return existingConversation.ConversationId;

            // Create new conversation
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

            await db.InsertAsync(conversation);
            return conversationId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetOrCreateConversationAsync error: {ex}");
            throw;
        }
    }

    private async Task SendPostAsMessageAsync(string targetUserPhone, string targetConversationId, string targetUserName)
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            string postPreview = GetPostPreview(_post);

            var postMessage = new ChatMessage
            {
                ConversationId = targetConversationId,
                SenderPhone = currentUserPhone,
                RecipientPhone = targetUserPhone,
                MessageType = "post",
                Content = !string.IsNullOrEmpty(_post.Content) ? _post.Content : postPreview,
                PostId = _post.Id,
                PostAuthor = _post.AuthorDisplayName ?? "Unknown",
                PostPreview = postPreview,
                PostImageCount = _post.ImagePathsList?.Length ?? 0,
                // Store the original post author's phone for image lookup
                PostAuthorPhone = _post.AuthorPhone ?? string.Empty,
                SentAt = DateTime.UtcNow,
                IsDelivered = true,
                IsRead = false,
                IsLocalOutgoing = true,
                IsEncrypted = false,
                WillDisappear = false,
                IsDisappearingMessage = false,
                DisappearAfterSeconds = 0
            };

            if (_post.ImagePathsList?.Length > 0)
            {
                postMessage.MediaPath = _post.ImagePathsList[0];
                postMessage.MediaType = "image";

                var mediaItems = _post.ImagePathsList.Select(imagePath => new ChatMediaItem
                {
                    Path = imagePath,
                    Type = "image"
                }).ToList();

                postMessage.MediaItems = mediaItems;
                postMessage.MediaItemsJson = System.Text.Json.JsonSerializer.Serialize(mediaItems);
            }

            await ChatRepository.AddMessageAsync(postMessage);

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var conversation = await db.Table<Conversation>()
                .Where(c => c.ConversationId == targetConversationId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                conversation.LastMessagePreview = $"?? Post: {postPreview}";
                conversation.LastMessageAt = DateTime.UtcNow;
                conversation.LastMessageType = "post";
                await db.UpdateAsync(conversation);
            }

            MessagingCenter.Send(this, "MessagesUpdated");
            MessagingCenter.Send(this, "ConversationsUpdated");

            Debug.WriteLine($"Post shared. PostId={_post.Id}, AuthorPhone={_post.AuthorPhone}");

            await Application.Current.MainPage.DisplayAlert(
                "Post Shared",
                $"Post has been shared with {targetUserName}.",
                "OK"
            );

            Close("post_shared");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SendPostAsMessageAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to send post: {ex.Message}", "OK");
        }
    }
    private string GetShareText()
    {
        string shareText = $"Check out this post from {_post.AuthorDisplayName}\n\n";

        if (!string.IsNullOrEmpty(_post.Content))
        {
            shareText += $"\"{_post.Content}\"\n\n";
        }

        if (_post.ImagePathsList?.Length > 0)
        {
            shareText += $"?? {_post.ImagePathsList.Length} image(s)\n\n";
        }

        shareText += $"Shared from Lock App";
        return shareText;
    }

    private string GetPostLink()
    {
        return $"https://lockapp.com/post/{_post.Id}";
    }

    private async Task ShareToWhatsApp()
    {
        try
        {
            string text = GetShareText();
            string link = GetPostLink();
            var fullText = text + "\n" + link;

            var whatsappUri = $"whatsapp://send?text={Uri.EscapeDataString(fullText)}";

            bool canOpen = await Launcher.Default.CanOpenAsync(whatsappUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(whatsappUri);
            }
            else
            {
                var webWhatsApp = $"https://api.whatsapp.com/send?text={Uri.EscapeDataString(fullText)}";
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
            string text = GetShareText();
            string link = GetPostLink();
            var fullText = text + "\n" + link;

            var telegramUri = $"tg://msg?text={Uri.EscapeDataString(fullText)}";

            bool canOpen = await Launcher.Default.CanOpenAsync(telegramUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(telegramUri);
            }
            else
            {
                var webTelegram = $"https://t.me/share/url?url={Uri.EscapeDataString(link)}&text={Uri.EscapeDataString(text)}";
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
            string text = GetShareText();
            string link = GetPostLink();

            var facebookUri = $"fb://facewebmodal/f?href=https://facebook.com/sharer.php?u={Uri.EscapeDataString(link)}&quote={Uri.EscapeDataString(text)}";

            bool canOpen = await Launcher.Default.CanOpenAsync(facebookUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(facebookUri);
            }
            else
            {
                var webFacebook = $"https://www.facebook.com/sharer/sharer.php?u={Uri.EscapeDataString(link)}&quote={Uri.EscapeDataString(text)}";
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
            string text = GetShareText();
            string link = GetPostLink();
            var fullText = text + "\n" + link;

            var twitterUri = $"twitter://post?message={Uri.EscapeDataString(fullText)}";

            bool canOpen = await Launcher.Default.CanOpenAsync(twitterUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(twitterUri);
            }
            else
            {
                var webTwitter = $"https://twitter.com/intent/tweet?text={Uri.EscapeDataString(fullText)}";
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
        string link = GetPostLink();
        await Clipboard.Default.SetTextAsync(link);
        await Application.Current.MainPage.DisplayAlert("Copied", "Post link copied to clipboard", "OK");
        Close();
    }

    private async Task ShareMoreOptionsAsync()
    {
        string text = GetShareText();
        string link = GetPostLink();

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = text + "\n" + link,
            Title = "Share Post"
        });
        Close();
    }
}