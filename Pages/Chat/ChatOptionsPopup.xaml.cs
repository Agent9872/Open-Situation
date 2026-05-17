using CommunityToolkit.Maui.Views;
using Lock.Chat.Services;
using Lock.Models;
using Lock.Models.Chat;
using Lock.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.IO;
using Lock.Pages.Chat;

namespace Lock.Pages.Chat;

public partial class ChatOptionsPopup : Popup
{
    public ChatOptionsPopup(string contactName, string conversationId, string? profileImagePath = null)
    {
        InitializeComponent();
        var vm = new ChatOptionsViewModel(contactName, conversationId, profileImagePath);
        BindingContext = vm;
    }

    public ChatOptionsPopup(string contactName, string phoneNumber, string conversationId, string? profileImagePath = null)
    {
        InitializeComponent();
        var vm = new ChatOptionsViewModel(contactName, phoneNumber, conversationId, profileImagePath);
        BindingContext = vm;
    }

    private void OnCloseTapped(object sender, EventArgs e)
    {
        Close();
    }

    private async void OnActionTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string action || string.IsNullOrWhiteSpace(action))
            return;

        Debug.WriteLine($"Action tapped: {action}");

        // ===== VIEW CONTACT =====
        if (action == "View contact")
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                string name = vm.ContactName;
                string phone = vm.PhoneNumber;

                if (!string.IsNullOrEmpty(phone))
                {
                    await ShowPhoneNumberDropdownAsync(name, phone);
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Not found",
                        "Could not find phone number for this contact.",
                        "OK"
                    );
                }
            }
            return;
        }

        // ===== SHARE CONTACT =====
        if (action == "Share contact")
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                await ShowContactShareOptionsAsync(vm);
            }
            return;
        }

        // ===== DISAPPEARING MESSAGES =====
        if (action == "Disappearing messages")
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                await ShowDisappearingMessagesOptionsAsync(vm);
            }
            return;
        }

        // ===== BACKGROUND IMAGE =====
        if (action == "Background image")
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                await ShowBackgroundImageOptionsAsync(vm);
            }
            return;
        }

        // ===== BLOCK / UNBLOCK USER =====
        if (action.Contains("Block user") || action.Contains("Unblock user"))
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                try
                {
                    string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    if (string.IsNullOrEmpty(currentUserPhone))
                    {
                        await Application.Current.MainPage.DisplayAlert("Error", "Could not identify current user", "OK");
                        return;
                    }

                    string targetPhone = vm.PhoneNumber;
                    if (string.IsNullOrEmpty(targetPhone))
                    {
                        await Application.Current.MainPage.DisplayAlert("Error", "Could not identify target user", "OK");
                        return;
                    }

                    if (vm.IsBlocked)
                    {
                        await UnblockUserAsync(currentUserPhone, targetPhone, vm);
                    }
                    else
                    {
                        await BlockUserAsync(currentUserPhone, targetPhone, vm);
                    }
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
                    Debug.WriteLine($"Block/Unblock error: {ex}");
                }
            }
            return;
        }

        // ===== MUTE NOTIFICATIONS =====
        if (action == "Mute notifications")
        {
            await ToggleNotificationsAsync();
            return;
        }

        // ===== REPORT USER =====
        if (action == "Report user")
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                bool confirm = await Application.Current.MainPage.DisplayAlert(
                    "Report User",
                    $"Are you sure you want to report {vm.ContactName}?\n\nThis report is anonymous and will be reviewed by our team.",
                    "Report",
                    "Cancel"
                );

                if (confirm)
                {
                    // CLOSE POPUP FIRST and wait fully before navigating
                    await CloseAsync();

                    // Give the popup time to fully dismiss before Shell navigates
                    await Task.Delay(300);

                    var query = new Dictionary<string, object>
                    {
                        ["userPhone"] = vm.PhoneNumber ?? string.Empty,
                        ["userName"] = vm.ContactName ?? string.Empty,
                        ["profileImage"] = vm.ProfileImagePath ?? string.Empty,
                        ["conversationId"] = vm.ConversationId ?? string.Empty
                    };

                    // Navigate on the main thread
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Shell.Current.GoToAsync(nameof(ReportUserPage), query);
                    });
                }
            }
            return;
        }

        // ===== CHAT LOCK =====
        if (action == "Chat lock")
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                Close();
                await Task.Delay(100);

                ChatPage? chatPage = null;

                if (Application.Current?.MainPage?.Navigation?.NavigationStack != null)
                {
                    chatPage = Application.Current.MainPage.Navigation.NavigationStack
                        .OfType<ChatPage>()
                        .LastOrDefault();
                }

                if (chatPage != null)
                {
                    await chatPage.ShowChatLockOptionsFromPopup();
                }
                else
                {
                    string[] options = {
                    "Lock with PIN",
                    "Lock with Pattern",
                    "Remove Lock",
                    "Cancel"
                };

                    string selected = await Application.Current.MainPage.DisplayActionSheet(
                        "Chat Lock",
                        "Cancel",
                        null,
                        options
                    );

                    if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                        return;

                    if (selected == "Lock with PIN")
                    {
                        await SetupPinLockFallback(vm.ConversationId);
                    }
                    else if (selected == "Lock with Pattern")
                    {
                        await SetupPatternLockFallback(vm.ConversationId);
                    }
                    else if (selected == "Remove Lock")
                    {
                        await RemoveLockFallback(vm.ConversationId);
                    }
                }
            }
            return;
        }
    }
    private async void OnReportUserTapped(object sender, EventArgs e)
    {
        try
        {
            if (BindingContext is not ChatOptionsViewModel vm)
            {
                Debug.WriteLine("OnReportUserTapped: ViewModel not found");
                return;
            }

            await CloseAsync();

            var query = new Dictionary<string, object>
            {
                ["userPhone"] = vm.PhoneNumber ?? string.Empty,
                ["userName"] = vm.ContactName ?? string.Empty,
                ["profileImage"] = vm.ProfileImagePath ?? string.Empty,
                ["conversationId"] = vm.ConversationId ?? string.Empty
            };

            await Shell.Current.GoToAsync(nameof(ReportUserPage), query);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnReportUserTapped error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Could not open report page: {ex.Message}", "OK");
        }
    }

    private async void OnShareActionTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string action || string.IsNullOrWhiteSpace(action))
            return;

        Debug.WriteLine($"Share action tapped: {action}");

        if (BindingContext is ChatOptionsViewModel vm)
        {
            try
            {
                switch (action)
                {
                    case "Share with app contact":
                        await ShowContactPickerForSharingAsync(vm);
                        break;

                    case "WhatsApp":
                        await ShareToWhatsAppGeneric(vm.ContactName, vm.PhoneNumber);
                        break;

                    case "Telegram":
                        await ShareToTelegramGeneric(vm.ContactName, vm.PhoneNumber);
                        break;

                    case "Facebook":
                        await ShareToFacebookGeneric(vm.ContactName, vm.PhoneNumber);
                        break;

                    case "Twitter":
                        await ShareToTwitterGeneric(vm.ContactName, vm.PhoneNumber);
                        break;

                    case "Copy link":
                        await CopyProfileLinkToClipboardAsync(vm.ContactName, vm.PhoneNumber);
                        break;

                    case "More":
                        await ShareMoreOptionsAsync(vm.ContactName, vm.PhoneNumber);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnShareActionTapped error: {ex}");
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to share: {ex.Message}", "OK");
            }
        }
    }



    private async Task ShareToWhatsAppGeneric(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile on Lock App\n\nContact: {phoneNumber}";
            string profileLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";
            string fullText = $"{text}\n\n{profileLink}";

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
            Debug.WriteLine($"ShareToWhatsAppGeneric error: {ex}");
        }
    }

    private async Task ShareToTelegramGeneric(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile on Lock App\n\nContact: {phoneNumber}";
            string profileLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";
            string fullText = $"{text}\n\n{profileLink}";

            var telegramUri = $"tg://msg?text={Uri.EscapeDataString(fullText)}";

            bool canOpen = await Launcher.Default.CanOpenAsync(telegramUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(telegramUri);
            }
            else
            {
                var webTelegram = $"https://t.me/share/url?url={Uri.EscapeDataString(profileLink)}&text={Uri.EscapeDataString(text)}";
                await Launcher.Default.OpenAsync(webTelegram);
            }
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToTelegramGeneric error: {ex}");
        }
    }

    private async Task ShareToFacebookGeneric(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"{contactName}\n{phoneNumber}";
            string profileLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";

            var facebookUri = $"fb://facewebmodal/f?href=https://facebook.com/sharer.php?u={Uri.EscapeDataString(profileLink)}&quote={Uri.EscapeDataString(text)}";

            bool canOpen = await Launcher.Default.CanOpenAsync(facebookUri);
            if (canOpen)
            {
                await Launcher.Default.OpenAsync(facebookUri);
            }
            else
            {
                var webFacebook = $"https://www.facebook.com/sharer/sharer.php?u={Uri.EscapeDataString(profileLink)}&quote={Uri.EscapeDataString(text)}";
                await Launcher.Default.OpenAsync(webFacebook);
            }
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToFacebookGeneric error: {ex}");
        }
    }

    private async Task ShareToTwitterGeneric(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile on Lock App\n\nContact: {phoneNumber}";
            string profileLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";
            string fullText = $"{text}\n\n{profileLink}";

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
            Debug.WriteLine($"ShareToTwitterGeneric error: {ex}");
        }
    }

    private async Task CopyProfileLinkToClipboardAsync(string contactName, string phoneNumber)
    {
        try
        {
            string profileLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";
            await Clipboard.Default.SetTextAsync(profileLink);
            await Application.Current.MainPage.DisplayAlert("Copied", "Profile link copied to clipboard", "OK");
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CopyProfileLinkToClipboardAsync error: {ex}");
        }
    }

    private async Task ShareMoreOptionsAsync(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile on Lock App\n\nContact: {phoneNumber}";
            string profileLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";
            string fullText = $"{text}\n\n{profileLink}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = fullText,
                Title = "Share Profile"
            });
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareMoreOptionsAsync error: {ex}");
        }
    }

    #region Share Contact Methods

    private async Task ShowContactShareOptionsAsync(ChatOptionsViewModel vm)
    {
        try
        {
            string[] options = {
            "Share with app contact",
            "Share via social media",
            "Share outside app",
            "Cancel"
        };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                $"Share {vm.ContactName}",
                "Cancel",
                null,
                options
            );

            if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                return;

            switch (selected)
            {
                case "Share with app contact":
                    await ShowContactPickerForSharingAsync(vm);
                    break;
                case "Share via social media":
                    await ShareContactViaSocialMediaAsync(vm.ContactName, vm.PhoneNumber, vm.ProfileImagePath);
                    break;
                case "Share outside app":
                    await ShareContactExternallyAsync(vm.ContactName, vm.PhoneNumber, vm.ProfileImagePath);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowContactShareOptionsAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
        }
    }
    // Change this method from async void to async Task
    private async Task ShowContactPickerForSharingAsync(ChatOptionsViewModel sourceVm)
    {
        try
        {
            var contactPicker = new ContactPickerPopup(
                sourceVm.ContactName,
                sourceVm.PhoneNumber,
                sourceVm.ProfileImagePath ?? "default_profile.png",
                async (targetPhone, targetName, targetProfileImage) =>
                {
                    string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                    string targetConversationId = await GetOrCreateConversationAsync(
                        currentUserPhone,
                        targetPhone,
                        targetName
                    );

                    await SendContactAsMessageAsync(
                        sourceVm,
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

            // Check if conversation already exists - using ParticipantA and ParticipantB
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
                // Store contact name in OtherParticipant for UI
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

    private async Task SendContactAsMessageAsync(ChatOptionsViewModel sourceContact, string targetUserPhone, string targetConversationId, string targetUserName)
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

            // Create a beautiful contact card message
            var contactMessage = new ChatMessage
            {
                ConversationId = targetConversationId,
                SenderPhone = currentUserPhone,
                RecipientPhone = targetUserPhone,
                MessageType = "contact",
                ContactName = sourceContact.ContactName,
                ContactPhone = sourceContact.PhoneNumber,
                ContactProfileImage = sourceContact.ProfileImagePath,
                SentAt = DateTime.UtcNow,
                IsDelivered = true,
                IsRead = false,
                IsLocalOutgoing = true,
                IsEncrypted = false,
                WillDisappear = false,
                IsDisappearingMessage = false,
                DisappearAfterSeconds = 0
            };

            // Save to database
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();
            await db.InsertAsync(contactMessage);

            // Update conversation last message with card emoji
            var conversation = await db.Table<Conversation>()
                .Where(c => c.ConversationId == targetConversationId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                conversation.LastMessagePreview = $"?? Contact card: {sourceContact.ContactName}";
                conversation.LastMessageAt = DateTime.UtcNow;
                conversation.LastMessageType = "contact";
                await db.UpdateAsync(conversation);
            }

            // Notify that messages have been updated
            MessagingCenter.Send(this, "MessagesUpdated");
            MessagingCenter.Send(this, "ConversationsUpdated");

            await Application.Current.MainPage.DisplayAlert(
                "Contact Shared",
                $"? Contact card for {sourceContact.ContactName} has been shared with {targetUserName}.",
                "OK"
            );

            Close("contact_shared");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SendContactAsMessageAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to send contact: {ex.Message}", "OK");
        }
    }


    private async Task ShareContactExternallyAsync(string contactName, string phoneNumber, string? profileImagePath = null)
    {
        try
        {
            string[] options = {
            "Share via social media",
            "Share as text",
            "Share as vCard",
            "Share as link",
            "Copy to clipboard",
            "Cancel"
        };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                $"Share {contactName} externally",
                "Cancel",
                null,
                options
            );

            if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                return;

            switch (selected)
            {
                case "Share via social media":
                    await ShareContactViaSocialMediaAsync(contactName, phoneNumber, profileImagePath);
                    break;

                case "Share as text":
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = $"{contactName}: {phoneNumber}",
                        Title = "Share Contact"
                    });
                    break;

                case "Share as vCard":
                    await ShareAsVCardAsync(contactName, phoneNumber, profileImagePath);
                    break;

                case "Share as link":
                    await ShareAsLinkAsync(contactName, phoneNumber);
                    break;

                case "Copy to clipboard":
                    await CopyToClipboardAsync(contactName, phoneNumber);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareContactExternallyAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to share: {ex.Message}", "OK");
        }
    }

    private async Task ShareAsVCardAsync(string contactName, string phoneNumber, string? profileImagePath = null)
    {
        try
        {
            // Create enhanced vCard with more fields
            string vCard = $@"BEGIN:VCARD
VERSION:3.0
FN:{contactName}
N:{contactName};;;;
TEL;TYPE=CELL:{phoneNumber}
URL;TYPE=PROFILE:lockapp://profile?phone={Uri.EscapeDataString(phoneNumber)}
NOTE:Shared from Lock App
REV:{DateTime.Now:yyyyMMddTHHmmssZ}
END:VCARD";

            string fileName = $"{contactName.Replace(" ", "_")}_{DateTime.Now:yyyyMMddHHmmss}.vcf";
            string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, vCard);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share vCard",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareAsVCardAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to create vCard: {ex.Message}", "OK");
        }
    }

    private async Task ShareAsLinkAsync(string contactName, string phoneNumber)
    {
        try
        {
            string[] linkOptions = {
            "Share as app link",
            "Share as web link",
            "Share both",
            "Generate QR code",
            "Cancel"
        };

            string linkChoice = await Application.Current.MainPage.DisplayActionSheet(
                "Share profile link",
                "Cancel",
                null,
                linkOptions
            );

            if (string.IsNullOrEmpty(linkChoice) || linkChoice == "Cancel")
                return;

            if (linkChoice == "Generate QR code")
            {
                await ShowQRCodeSharingAsync(contactName, phoneNumber);
                return;
            }

            string appLink = $"lockapp://profile?phone={Uri.EscapeDataString(phoneNumber)}&name={Uri.EscapeDataString(contactName)}";
            string webLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";

            string shareText = linkChoice switch
            {
                "Share as app link" => $"?? {contactName}'s profile on Lock App\n?? Open in app: {appLink}",
                "Share as web link" => $"?? {contactName}'s profile on Lock App\n?? Web link: {webLink}",
                "Share both" => $"?? {contactName}'s profile on Lock App\n?? App: {appLink}\n?? Web: {webLink}",
                _ => $"?? {contactName}'s profile: {webLink}"
            };

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = shareText,
                Title = "Share Profile Link"
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareAsLinkAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to share link: {ex.Message}", "OK");
        }
    }

    private async Task CopyToClipboardAsync(string contactName, string phoneNumber)
    {
        try
        {
            string[] copyOptions = {
            "Copy phone number only",
            "Copy name and phone",
            "Copy profile link",
            "Copy vCard format",
            "Cancel"
        };

            string copyChoice = await Application.Current.MainPage.DisplayActionSheet(
                "Copy to clipboard",
                "Cancel",
                null,
                copyOptions
            );

            if (string.IsNullOrEmpty(copyChoice) || copyChoice == "Cancel")
                return;

            string textToCopy = copyChoice switch
            {
                "Copy phone number only" => phoneNumber,
                "Copy name and phone" => $"{contactName}: {phoneNumber}",
                "Copy profile link" => $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}",
                "Copy vCard format" => $@"BEGIN:VCARD
VERSION:3.0
FN:{contactName}
TEL:{phoneNumber}
END:VCARD",
                _ => phoneNumber
            };

            await Clipboard.Default.SetTextAsync(textToCopy);
            await Application.Current.MainPage.DisplayAlert(
                "Copied",
                $"{copyChoice} copied to clipboard",
                "OK"
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CopyToClipboardAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to copy: {ex.Message}", "OK");
        }
    }

    private async Task ShareContactViaSocialMediaAsync(string contactName, string phoneNumber, string? profileImagePath = null)
    {
        try
        {
            string[] socialOptions = {
            "WhatsApp",
            "Telegram",
            "Facebook",
            "Twitter / X",
            "Instagram",
            "Snapchat",
            "TikTok",
            "Messages (SMS)",
            "Email",
            "More...",
            "Cancel"
        };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                "Share via",
                "Cancel",
                null,
                socialOptions
            );

            if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                return;

            string shareText = $"?? {contactName}\n?? {phoneNumber}\n\nShared from Lock App";
            string appLink = $"https://lockapp.com/profile/{Uri.EscapeDataString(phoneNumber)}";
            string fullShareText = $"{shareText}\n\n?? Profile: {appLink}";

            switch (selected)
            {
                case "WhatsApp":
                    await ShareToWhatsApp(contactName, phoneNumber);
                    break;
                case "Telegram":
                    await ShareToTelegram(contactName, phoneNumber);
                    break;
                case "Facebook":
                    await ShareToFacebook(contactName, phoneNumber);
                    break;
                case "Twitter / X":
                    await ShareToTwitter(contactName, phoneNumber);
                    break;
                case "Instagram":
                    await ShareToInstagram(contactName, phoneNumber);
                    break;
                case "Snapchat":
                    await ShareToSnapchat(contactName, phoneNumber);
                    break;
                case "TikTok":
                    await ShareToTikTok(contactName, phoneNumber);
                    break;
                case "Messages (SMS)":
                    await ShareToSMS(contactName, phoneNumber);
                    break;
                case "Email":
                    await ShareToEmail(contactName, phoneNumber);
                    break;
                case "More...":
                    // Fallback to general share
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = fullShareText,
                        Title = "Share Contact"
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareContactViaSocialMediaAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to share: {ex.Message}", "OK");
        }
    }

    private async Task ShareToWhatsApp(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile: {phoneNumber}";

            // Try WhatsApp URI schemes
            var whatsappUri = $"whatsapp://send?text={Uri.EscapeDataString(text)}";
            var whatsappBusinessUri = $"whatsapp://send?phone=&text={Uri.EscapeDataString(text)}";

            try
            {
                bool canOpen = await Launcher.Default.CanOpenAsync(whatsappUri);
                if (canOpen)
                {
                    await Launcher.Default.OpenAsync(whatsappUri);
                }
                else
                {
                    // Try WhatsApp Business
                    canOpen = await Launcher.Default.CanOpenAsync(whatsappBusinessUri);
                    if (canOpen)
                    {
                        await Launcher.Default.OpenAsync(whatsappBusinessUri);
                    }
                    else
                    {
                        // Fallback to web WhatsApp
                        var webWhatsApp = $"https://api.whatsapp.com/send?text={Uri.EscapeDataString(text)}";
                        await Launcher.Default.OpenAsync(webWhatsApp);
                    }
                }
            }
            catch
            {
                // Final fallback
                var webWhatsApp = $"https://web.whatsapp.com/send?text={Uri.EscapeDataString(text)}";
                await Launcher.Default.OpenAsync(webWhatsApp);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToWhatsApp error: {ex}");
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Text = $"{contactName}: {phoneNumber}",
                Title = "Share via WhatsApp"
            });
        }
    }

    private async Task ShareToTelegram(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile: {phoneNumber}";
            var telegramUri = $"tg://msg?text={Uri.EscapeDataString(text)}";

            try
            {
                bool canOpen = await Launcher.Default.CanOpenAsync(telegramUri);
                if (canOpen)
                {
                    await Launcher.Default.OpenAsync(telegramUri);
                }
                else
                {
                    // Fallback to web Telegram
                    var webTelegram = $"https://t.me/share/url?url={Uri.EscapeDataString(phoneNumber)}&text={Uri.EscapeDataString(contactName)}";
                    await Launcher.Default.OpenAsync(webTelegram);
                }
            }
            catch
            {
                // Final fallback
                var webTelegram = $"https://web.telegram.org/#/im";
                await Launcher.Default.OpenAsync(webTelegram);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToTelegram error: {ex}");
        }
    }

    #region Chat Lock Fallback Methods

    private async Task SetupPinLockFallback(string conversationId)
    {
        string pin = await Application.Current.MainPage.DisplayPromptAsync(
            "Set PIN Lock",
            "Enter a 4-6 digit PIN:",
            "Set",
            "Cancel",
            keyboard: Keyboard.Numeric,
            maxLength: 6
        );

        if (string.IsNullOrEmpty(pin)) return;

        if (pin.Length < 4 || pin.Length > 6)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PIN must be 4-6 digits", "OK");
            return;
        }

        string confirmPin = await Application.Current.MainPage.DisplayPromptAsync(
            "Confirm PIN",
            "Enter your PIN again:",
            "Confirm",
            "Cancel",
            keyboard: Keyboard.Numeric,
            maxLength: 6
        );

        if (pin != confirmPin)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "PINs do not match", "OK");
            return;
        }

        bool success = await ChatLockService.SetChatLockAsync(
            conversationId,
            ChatLockService.LockType.Pin,
            pin);

        if (success)
        {
            await Application.Current.MainPage.DisplayAlert("Success", "Chat locked with PIN", "OK");
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to set PIN lock", "OK");
        }
    }

    private async Task SetupPatternLockFallback(string conversationId)
    {
        string pattern = await Application.Current.MainPage.DisplayPromptAsync(
            "Set Pattern Lock",
            "Enter pattern code (1-9 numbers in pattern order):",
            "Set",
            "Cancel",
            keyboard: Keyboard.Numeric,
            maxLength: 9
        );

        if (string.IsNullOrEmpty(pattern)) return;

        if (pattern.Length < 4)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Pattern must have at least 4 points", "OK");
            return;
        }

        string confirmPattern = await Application.Current.MainPage.DisplayPromptAsync(
            "Confirm Pattern",
            "Enter pattern again:",
            "Confirm",
            "Cancel",
            keyboard: Keyboard.Numeric,
            maxLength: 9
        );

        if (pattern != confirmPattern)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Patterns do not match", "OK");
            return;
        }

        bool success = await ChatLockService.SetChatLockAsync(
            conversationId,
            ChatLockService.LockType.Pattern,
            pattern);

        if (success)
        {
            await Application.Current.MainPage.DisplayAlert("Success", "Chat locked with pattern", "OK");
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to set pattern lock", "OK");
        }
    }

    private async Task RemoveLockFallback(string conversationId)
    {
        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Remove Lock",
            "Are you sure you want to remove the lock?",
            "Remove",
            "Cancel"
        );

        if (!confirm) return;

        bool success = await ChatLockService.SetChatLockAsync(conversationId, ChatLockService.LockType.None);

        if (success)
        {
            await Application.Current.MainPage.DisplayAlert("Success", "Lock removed", "OK");
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to remove lock", "OK");
        }
    }

    #endregion

    private async Task ShareToFacebook(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"{contactName}\n{phoneNumber}";
            var facebookUri = $"fb://facewebmodal/f?href=https://facebook.com/sharer.php?u={Uri.EscapeDataString(phoneNumber)}&quote={Uri.EscapeDataString(text)}";

            try
            {
                bool canOpen = await Launcher.Default.CanOpenAsync(facebookUri);
                if (canOpen)
                {
                    await Launcher.Default.OpenAsync(facebookUri);
                }
                else
                {
                    // Fallback to web Facebook
                    var webFacebook = $"https://www.facebook.com/sharer/sharer.php?u={Uri.EscapeDataString(phoneNumber)}&quote={Uri.EscapeDataString(text)}";
                    await Launcher.Default.OpenAsync(webFacebook);
                }
            }
            catch
            {
                // Final fallback
                var webFacebook = $"https://www.facebook.com/sharer.php?u={Uri.EscapeDataString(phoneNumber)}";
                await Launcher.Default.OpenAsync(webFacebook);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToFacebook error: {ex}");
        }
    }

    private async Task ShareToTwitter(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"Check out {contactName}'s profile: {phoneNumber}";
            var twitterUri = $"twitter://post?message={Uri.EscapeDataString(text)}";

            try
            {
                bool canOpen = await Launcher.Default.CanOpenAsync(twitterUri);
                if (canOpen)
                {
                    await Launcher.Default.OpenAsync(twitterUri);
                }
                else
                {
                    // Fallback to web Twitter
                    var webTwitter = $"https://twitter.com/intent/tweet?text={Uri.EscapeDataString(text)}";
                    await Launcher.Default.OpenAsync(webTwitter);
                }
            }
            catch
            {
                // Final fallback
                var webTwitter = $"https://twitter.com/intent/tweet?text={Uri.EscapeDataString(text)}";
                await Launcher.Default.OpenAsync(webTwitter);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToTwitter error: {ex}");
        }
    }

    private async Task ShareToInstagram(string contactName, string phoneNumber)
    {
        try
        {
            // Instagram doesn't support direct text sharing
            string text = $"{contactName}\n{phoneNumber}";

            // Copy to clipboard
            await Clipboard.Default.SetTextAsync(text);

            bool openInstagram = await Application.Current.MainPage.DisplayAlert(
                "Share to Instagram",
                $"Contact information copied to clipboard.\n\nOpen Instagram to paste in story or DM?",
                "Open Instagram",
                "Cancel"
            );

            if (openInstagram)
            {
                try
                {
                    // Try Instagram app
                    bool canOpen = await Launcher.Default.CanOpenAsync("instagram://");
                    if (canOpen)
                    {
                        await Launcher.Default.OpenAsync("instagram://");
                    }
                    else
                    {
                        await Launcher.Default.OpenAsync("https://www.instagram.com");
                    }
                }
                catch
                {
                    await Launcher.Default.OpenAsync("https://www.instagram.com");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToInstagram error: {ex}");
        }
    }

    private async Task ShareToSnapchat(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"{contactName}: {phoneNumber}";
            await Clipboard.Default.SetTextAsync(text);

            bool openSnapchat = await Application.Current.MainPage.DisplayAlert(
                "Share to Snapchat",
                $"Contact information copied to clipboard.\n\nOpen Snapchat to paste?",
                "Open Snapchat",
                "Cancel"
            );

            if (openSnapchat)
            {
                try
                {
                    bool canOpen = await Launcher.Default.CanOpenAsync("snapchat://");
                    if (canOpen)
                    {
                        await Launcher.Default.OpenAsync("snapchat://");
                    }
                    else
                    {
                        await Launcher.Default.OpenAsync("https://www.snapchat.com");
                    }
                }
                catch
                {
                    await Launcher.Default.OpenAsync("https://www.snapchat.com");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToSnapchat error: {ex}");
        }
    }

    private async Task ShareToTikTok(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"{contactName}: {phoneNumber}";
            await Clipboard.Default.SetTextAsync(text);

            bool openTikTok = await Application.Current.MainPage.DisplayAlert(
                "Share to TikTok",
                $"Contact information copied to clipboard.\n\nOpen TikTok to paste in bio or DM?",
                "Open TikTok",
                "Cancel"
            );

            if (openTikTok)
            {
                try
                {
                    bool canOpen = await Launcher.Default.CanOpenAsync("tiktok://");
                    if (canOpen)
                    {
                        await Launcher.Default.OpenAsync("tiktok://");
                    }
                    else
                    {
                        await Launcher.Default.OpenAsync("https://www.tiktok.com");
                    }
                }
                catch
                {
                    await Launcher.Default.OpenAsync("https://www.tiktok.com");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToTikTok error: {ex}");
        }
    }

    private async Task ShareToSMS(string contactName, string phoneNumber)
    {
        try
        {
            string text = $"{contactName}: {phoneNumber}";

            // Try SMS URI
            var smsUri = $"sms:?body={Uri.EscapeDataString(text)}";

            try
            {
                bool canOpen = await Launcher.Default.CanOpenAsync(smsUri);
                if (canOpen)
                {
                    await Launcher.Default.OpenAsync(smsUri);
                }
                else
                {
                    // Fallback to share
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = text,
                        Title = "Share via SMS"
                    });
                }
            }
            catch
            {
                // Final fallback
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = text,
                    Title = "Share via SMS"
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToSMS error: {ex}");
        }
    }

    private async Task ShareToEmail(string contactName, string phoneNumber)
    {
        try
        {
            string subject = $"Contact: {contactName}";
            string body = $@"<h2>{contactName}</h2>
<p><strong>Phone:</strong> {phoneNumber}</p>
<br>
<p>Shared from Lock App</p>";

            var emailUri = $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

            try
            {
                bool canOpen = await Launcher.Default.CanOpenAsync(emailUri);
                if (canOpen)
                {
                    await Launcher.Default.OpenAsync(emailUri);
                }
                else
                {
                    // Fallback to share
                    await Share.Default.RequestAsync(new ShareTextRequest
                    {
                        Text = $"{contactName}: {phoneNumber}",
                        Title = "Share via Email"
                    });
                }
            }
            catch
            {
                // Final fallback
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = $"{contactName}: {phoneNumber}",
                    Title = "Share via Email"
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShareToEmail error: {ex}");
        }
    }

    private async Task ShowQRCodeSharingAsync(string contactName, string phoneNumber)
    {
        try
        {
            // For now, show a message about QR generation
            // You'll need to install ZXing.Net.Maui or similar QR library for actual QR generation
            await Application.Current.MainPage.DisplayAlert(
                "QR Code",
                $"QR code for {contactName}'s profile would be generated here.\n\nProfile data: {phoneNumber}",
                "OK"
            );

            // When you implement QR code generation, you can do:
            // string profileData = $"BEGIN:VCARD\nFN:{contactName}\nTEL:{phoneNumber}\nURL:lockapp://profile?phone={phoneNumber}\nEND:VCARD";
            // var qrCode = GenerateQRCode(profileData);
            // await ShareQRCodeAsync(qrCode, contactName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowQRCodeSharingAsync error: {ex}");
        }
    }
    private string ExtractPhoneNumber(string selection)
    {
        // Format: "Name (1234567890)"
        int start = selection.LastIndexOf('(') + 1;
        int end = selection.LastIndexOf(')');
        if (start > 0 && end > start)
        {
            return selection.Substring(start, end - start);
        }
        return string.Empty;
    }

    #endregion

    #region Existing Methods

    private async Task<bool> ClearChatMessagesAsync(string conversationId)
    {
        try
        {
            Debug.WriteLine($"=== CLEARING CHAT MESSAGES ===");
            Debug.WriteLine($"Conversation ID: {conversationId}");

            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            // Get all messages for this conversation
            var messages = await db.Table<ChatMessage>()
                .Where(m => m.ConversationId == conversationId)
                .ToListAsync();

            Debug.WriteLine($"Found {messages.Count} messages to delete");

            if (!messages.Any())
            {
                Debug.WriteLine("No messages to delete");
                return true;
            }

            // Delete associated media files first
            int filesDeleted = 0;
            foreach (var msg in messages)
            {
                // Delete single media file if present
                if (!string.IsNullOrEmpty(msg.MediaPath) && File.Exists(msg.MediaPath))
                {
                    try
                    {
                        File.Delete(msg.MediaPath);
                        filesDeleted++;
                        Debug.WriteLine($"Deleted media file: {msg.MediaPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete media file: {ex.Message}");
                    }
                }

                // Delete multiple media items if present
                if (msg.MediaItems?.Any() == true)
                {
                    foreach (var media in msg.MediaItems)
                    {
                        if (!string.IsNullOrEmpty(media.Path) && File.Exists(media.Path))
                        {
                            try
                            {
                                File.Delete(media.Path);
                                filesDeleted++;
                                Debug.WriteLine($"Deleted media item: {media.Path}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to delete media item: {ex.Message}");
                            }
                        }
                    }
                }

                // Delete voice messages
                if (msg.IsVoiceMessage && !string.IsNullOrEmpty(msg.MediaPath) && File.Exists(msg.MediaPath))
                {
                    try
                    {
                        File.Delete(msg.MediaPath);
                        filesDeleted++;
                        Debug.WriteLine($"Deleted voice message: {msg.MediaPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete voice message: {ex.Message}");
                    }
                }
            }

            Debug.WriteLine($"Deleted {filesDeleted} media files");

            // Delete all messages from database
            int deletedCount = await db.ExecuteAsync(
                "DELETE FROM ChatMessage WHERE ConversationId = ?",
                conversationId
            );

            Debug.WriteLine($"Deleted {deletedCount} messages from conversation {conversationId}");

            // Update the conversation's last message info
            var conversation = await db.Table<Conversation>()
                .Where(c => c.ConversationId == conversationId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                conversation.LastMessagePreview = string.Empty;
                conversation.LastMessageAt = DateTime.UtcNow;
                await db.UpdateAsync(conversation);
                Debug.WriteLine($"Updated conversation {conversationId} - cleared last message preview");
            }
            else
            {
                Debug.WriteLine($"Conversation {conversationId} not found in database");
            }

            // Notify that messages have been updated
            MessagingCenter.Send(this, "MessagesUpdated");
            MessagingCenter.Send(this, "ConversationsUpdated");

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error type: {ex.GetType().Name}");
            Debug.WriteLine($"Error message: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    private async Task ToggleNotificationsAsync()
    {
        try
        {
            if (BindingContext is ChatOptionsViewModel vm)
            {
                string contactName = vm.ContactName;

                // Get current notification status (you'd need to store this somewhere)
                // For now, we'll assume it's a toggle
                bool currentlyMuted = false; // Replace with actual stored value

                if (!currentlyMuted)
                {
                    bool confirm = await Application.Current.MainPage.DisplayAlert(
                        "Mute Notifications",
                        $"Mute notifications for {contactName}?",
                        "Mute",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        // TODO: Implement mute logic
                        // await ChatRepository.SetNotificationMuteAsync(vm.ConversationId, true);
                        await Application.Current.MainPage.DisplayAlert(
                            "Muted",
                            $"Notifications muted for {contactName}",
                            "OK"
                        );

                        Close("Mute notifications");
                    }
                }
                else
                {
                    bool confirm = await Application.Current.MainPage.DisplayAlert(
                        "Unmute Notifications",
                        $"Unmute notifications for {contactName}?",
                        "Unmute",
                        "Cancel"
                    );

                    if (confirm)
                    {
                        // TODO: Implement unmute logic
                        // await ChatRepository.SetNotificationMuteAsync(vm.ConversationId, false);
                        await Application.Current.MainPage.DisplayAlert(
                            "Unmuted",
                            $"Notifications unmuted for {contactName}",
                            "OK"
                        );

                        Close("Unmute notifications");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ToggleNotificationsAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task ShowBackgroundImageOptionsAsync(ChatOptionsViewModel vm)
    {
        try
        {
            string[] options = {
                "Choose from gallery",
                "Adjust brightness",
                "Use default (none)",
                "Cancel"
            };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                "Chat Background",
                "Cancel",
                null,
                options
            );

            if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                return;

            if (selected == "Use default (none)")
            {
                // Reset to default background
                await SetChatBackgroundAsync(vm, string.Empty);
                return;
            }

            if (selected == "Adjust brightness")
            {
                await ShowBrightnessOptionsAsync(vm);
                return;
            }

            if (selected == "Choose from gallery")
            {
                try
                {
                    var pickOptions = new PickOptions
                    {
                        PickerTitle = "Select background image",
                        FileTypes = FilePickerFileType.Images
                    };

                    var result = await FilePicker.Default.PickAsync(pickOptions);
                    if (result == null) return;

                    // Create a dedicated folder for chat backgrounds
                    string bgFolder = Path.Combine(FileSystem.AppDataDirectory, "chat_backgrounds");
                    if (!Directory.Exists(bgFolder))
                        Directory.CreateDirectory(bgFolder);

                    // Save with conversation-specific name
                    string fileName = $"bg_{vm.ConversationId}_{Guid.NewGuid():N}{Path.GetExtension(result.FileName)}";
                    string targetPath = Path.Combine(bgFolder, fileName);

                    // Copy the selected image
                    using var sourceStream = await result.OpenReadAsync();
                    using var destStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(destStream);

                    // Set as background with default brightness
                    await SetChatBackgroundAsync(vm, targetPath, 0.6);
                }
                catch (Exception ex)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
                    Debug.WriteLine($"Background image pick error: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowBackgroundImageOptionsAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task ShowBrightnessOptionsAsync(ChatOptionsViewModel vm)
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            string key = $"chat_bg_brightness_{currentUserPhone}_{vm.ConversationId}";
            double currentBrightness = Preferences.Get(key, 0.6); // Default 60%

            string currentText = currentBrightness switch
            {
                <= 0.3 => "Subtle (30%)",
                <= 0.5 => "Balanced (50%)",
                <= 0.7 => "Bright (70%)",
                _ => "Very Bright (85%)"
            };

            string[] options = {
                $"Current: {currentText}",
                "Subtle (30%)",
                "Balanced (50%)",
                "Bright (70%)",
                "Very Bright (85%)",
                "Cancel"
            };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                "Background Brightness",
                "Cancel",
                null,
                options
            );

            if (string.IsNullOrEmpty(selected) || selected == "Cancel" || selected.StartsWith("Current:"))
                return;

            double newBrightness = selected switch
            {
                "Subtle (30%)" => 0.3,
                "Balanced (50%)" => 0.5,
                "Bright (70%)" => 0.7,
                "Very Bright (85%)" => 0.85,
                _ => 0.6
            };

            // Save brightness preference
            Preferences.Set(key, newBrightness);

            // Close with brightness update
            Close($"brightness:{newBrightness}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowBrightnessOptionsAsync error: {ex}");
        }
    }

    private async Task SetChatBackgroundAsync(ChatOptionsViewModel vm, string imagePath, double brightness = 0.6)
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            if (string.IsNullOrEmpty(currentUserPhone))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Could not save background setting", "OK");
                return;
            }

            // Save to preferences - USER SPECIFIC (global)
            string key = $"chat_bg_{currentUserPhone}";
            string oldPath = Preferences.Get(key, string.Empty);

            // Check if we need to delete the old image file (if it's different and not the default)
            if (!string.IsNullOrEmpty(oldPath) && oldPath != imagePath && File.Exists(oldPath) && !oldPath.Contains("default"))
            {
                try
                {
                    File.Delete(oldPath);
                    Debug.WriteLine($"Deleted old background image: {oldPath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete old background: {ex}");
                }
            }

            // Save the new background path
            Preferences.Set(key, imagePath);
            Debug.WriteLine($"Saved background path: {imagePath} for user {currentUserPhone}");

            // Save brightness - also user specific
            string brightnessKey = $"chat_bg_brightness_{currentUserPhone}";
            Preferences.Set(brightnessKey, brightness);
            Debug.WriteLine($"Saved brightness: {brightness} for user {currentUserPhone}");

            // Update ViewModel
            vm.BackgroundImagePath = imagePath;

            // Show confirmation
            if (string.IsNullOrEmpty(imagePath))
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Background",
                    "Chat background reset to default for all chats",
                    "OK"
                );
            }
            else
            {
                string fileInfo = File.Exists(imagePath) ? "File exists" : "File not found";
                Debug.WriteLine($"Background image saved: {imagePath} - {fileInfo}");

                await Application.Current.MainPage.DisplayAlert(
                    "Background",
                    "Chat background updated for all chats",
                    "OK"
                );
            }

            // NOTIFY ALL CHAT PAGES THAT BACKGROUND HAS CHANGED
            ChatBackgroundService.NotifyBackgroundChanged(currentUserPhone);

            Close($"background:{imagePath}|brightness:{brightness}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SetChatBackgroundAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert(
                "Error",
                $"Failed to save background: {ex.Message}",
                "OK"
            );
        }
    }

    private async Task ShowDisappearingMessagesOptionsAsync(ChatOptionsViewModel vm)
    {
        try
        {
            string currentSetting = await GetDisappearingMessagesSettingAsync(vm.ConversationId);

            var options = new List<string>
            {
                currentSetting == "Off" ? "? Off" : "Off",
                currentSetting == "5 seconds" ? "? 5 seconds" : "5 seconds (for testing)",
                currentSetting == "5 minutes" ? "? 5 minutes" : "5 minutes",
                currentSetting == "15 minutes" ? "? 15 minutes" : "15 minutes",
                currentSetting == "1 hour" ? "? 1 hour" : "1 hour",
                currentSetting == "24 hours" ? "? 24 hours" : "24 hours",
                currentSetting == "1 week" ? "? 1 week" : "1 week",
                "Cancel"
            };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                "Disappearing Messages",
                "Cancel",
                null,
                options.ToArray()
            );

            if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                return;

            string cleanSelected = selected.Replace("? ", "");

            if (cleanSelected == "5 seconds (for testing)")
                cleanSelected = "5 seconds";

            bool saved = await SaveDisappearingMessagesSettingAsync(vm.ConversationId, cleanSelected);

            if (saved)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Disappearing Messages",
                    $"Messages will disappear after {cleanSelected.ToLower()}",
                    "OK"
                );

                Close($"disappearing:{cleanSelected}");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    "Failed to save disappearing messages setting",
                    "OK"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ShowDisappearingMessagesOptionsAsync error: {ex}");
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed: {ex.Message}", "OK");
        }
    }

    private async Task<string> GetDisappearingMessagesSettingAsync(string conversationId)
    {
        try
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var conversation = await db.Table<Conversation>()
                .Where(c => c.ConversationId == conversationId)
                .FirstOrDefaultAsync();

            if (conversation != null && !string.IsNullOrEmpty(conversation.DisappearingMessagesSetting))
            {
                return conversation.DisappearingMessagesSetting;
            }

            return "Off";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetDisappearingMessagesSettingAsync error: {ex}");
            return "Off";
        }
    }

    private async Task<bool> SaveDisappearingMessagesSettingAsync(string conversationId, string setting)
    {
        try
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var conversation = await db.Table<Conversation>()
                .Where(c => c.ConversationId == conversationId)
                .FirstOrDefaultAsync();

            if (conversation != null)
            {
                conversation.DisappearingMessagesSetting = setting;
                conversation.DisappearingMessagesEnabled = setting != "Off";

                conversation.DisappearingMessagesTimer = setting switch
                {
                    "5 seconds" => 5,
                    "5 minutes" => 300,
                    "15 minutes" => 900,
                    "1 hour" => 3600,
                    "24 hours" => 86400,
                    "1 week" => 604800,
                    _ => 0
                };

                await db.UpdateAsync(conversation);
                Debug.WriteLine($"Saved disappearing messages setting: {setting} for conversation {conversationId}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveDisappearingMessagesSettingAsync error: {ex}");
            return false;
        }
    }

    private async Task BlockUserAsync(string currentUserPhone, string targetPhone, ChatOptionsViewModel vm)
    {
        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Block User",
            $"Are you sure you want to block {vm.ContactName}?\n\n• You won't receive their messages\n• They won't see when you're online\n• You can unblock anytime",
            "Block",
            "Cancel"
        );

        if (!confirm) return;

        bool blocked = await ChatRepository.BlockUserAsync(currentUserPhone, targetPhone);

        if (blocked)
        {
            vm.IsBlocked = true;
            await Application.Current.MainPage.DisplayAlert(
                "Blocked",
                $"{vm.ContactName} has been blocked.",
                "OK"
            );
            Close("blocked");
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to block user", "OK");
        }
    }

    private async Task UnblockUserAsync(string currentUserPhone, string targetPhone, ChatOptionsViewModel vm)
    {
        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Unblock User",
            $"Do you want to unblock {vm.ContactName}?\n\nYou will be able to send and receive messages again.",
            "Unblock",
            "Cancel"
        );

        if (!confirm) return;

        bool unblocked = await ChatRepository.UnblockUserAsync(currentUserPhone, targetPhone);

        if (unblocked)
        {
            vm.IsBlocked = false;
            await Application.Current.MainPage.DisplayAlert(
                "Unblocked",
                $"{vm.ContactName} has been unblocked.",
                "OK"
            );
            Close("unblocked");
        }
        else
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to unblock user", "OK");
        }
    }

    private void OnMediaTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ChatMediaItem media && !string.IsNullOrEmpty(media.Path))
        {
            Close($"media:{media.Path}");
        }
    }
    private async void OnDateFilterTapped(object sender, EventArgs e)
    {
        if (BindingContext is ChatOptionsViewModel vm)
        {
            string[] options = {
            "All Time",
            "Today",
            "This Week",
            "This Month",
            "This Year"
        };

            string selected = await Application.Current.MainPage.DisplayActionSheet(
                "Filter Media by Date",
                "Cancel",
                null,
                options
            );

            if (!string.IsNullOrEmpty(selected) && selected != "Cancel")
            {
                vm.SetDateFilter(selected);
            }
        }
    }
    private void SwitchToMedia(object sender, EventArgs e)
    {
        if (BindingContext is ChatOptionsViewModel vm)
        {
            Debug.WriteLine($"=== SWITCH TO MEDIA ===");
            Debug.WriteLine($"Before - Media: {vm.IsMediaTab}, Info: {vm.IsInfoTab}");

            vm.IsMediaTab = true;
            vm.IsInfoTab = false;

            Debug.WriteLine($"After - Media: {vm.IsMediaTab}, Info: {vm.IsInfoTab}");
        }
    }

    private void SwitchToInfo(object sender, EventArgs e)
    {
        if (BindingContext is ChatOptionsViewModel vm)
        {
            Debug.WriteLine($"=== SWITCH TO INFO ===");
            Debug.WriteLine($"Before - Media: {vm.IsMediaTab}, Info: {vm.IsInfoTab}");

            vm.IsMediaTab = false;
            vm.IsInfoTab = true;

            Debug.WriteLine($"After - Media: {vm.IsMediaTab}, Info: {vm.IsInfoTab}");
        }
    }

    private async Task<string> GetPhoneNumberByNameAsync(string contactName)
    {
        try
        {
            await DatabaseService.InitializeAsync();
            var db = DatabaseService.GetConnection();

            var user = await db.Table<User>()
                .Where(u => u.Name == contactName)
                .FirstOrDefaultAsync();

            return user?.PhoneNumber ?? string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load phone: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task ShowPhoneNumberDropdownAsync(string name, string phone)
    {
        string message = $"{name}\n\nPhone: {phone}\n\nTap OK to copy the number to clipboard.";

        await Application.Current.MainPage.DisplayAlert("Contact Info", message, "OK");

        try
        {
            await Clipboard.Default.SetTextAsync(phone);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard copy failed: {ex.Message}");
        }
    }

    #endregion
}