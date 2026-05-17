using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Lock.Models.Chat;
using Lock.Chat.Services;

namespace Lock.Pages.Chat
{
    public partial class MessageActionsPage : ContentPage
    {
        private readonly ChatMessage _message;
        private bool _isClosing;

        public MessageActionsPage(ChatMessage message)
        {
            InitializeComponent();
            _message = message ?? throw new ArgumentNullException(nameof(message));

            // short preview (truncate)
            var preview = string.IsNullOrEmpty(_message.Content) ? "—" : _message.Content;
            if (preview.Length > 200) preview = preview.Substring(0, 200) + "…";
            MessagePreviewLabel.Text = preview;

            var actions = new List<string>
            {
                "Copy text",
                "Forward",
                "Delete",
                "Report",
            };

            ActionsCollectionView.ItemsSource = actions;
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await CloseModalAsync();
        }

        private async void ActionButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            if (_isClosing) return;

            var action = (btn.CommandParameter as string) ?? btn.Text ?? string.Empty;

            try
            {
                switch (action)
                {
                    case "Copy text":
                        if (!string.IsNullOrEmpty(_message.Content))
                        {
                            await Clipboard.SetTextAsync(_message.Content);
                            await DisplayAlert("Copied", "Message copied to clipboard.", "OK");
                        }
                        break;

                    case "Forward":
                        await DisplayAlert("Forward", "Forward flow not implemented yet.", "OK");
                        break;

                    case "Delete":
                        var confirm = await DisplayAlert("Delete", "Delete this message?", "Delete", "Cancel");
                        if (confirm)
                        {
                            try
                            {
                                await ChatRepository.DeleteMessageAsync(_message);
                                await DisplayAlert("Deleted", "Message deleted.", "OK");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("DeleteMessageAsync error: " + ex);
                                await DisplayAlert("Error", "Could not delete message: " + ex.Message, "OK");
                            }
                        }
                        break;

                    case "Report":
                        await DisplayAlert("Report", "Thank you — the message has been reported.", "OK");
                        break;

                    default:
                        await DisplayAlert("Action", $"Selected: {action}", "OK");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ActionButton_Clicked error: " + ex);
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                await CloseModalAsync();
            }
        }

        // Safely close the modal dialog (handles re-entrancy)
        private async Task CloseModalAsync()
        {
            if (_isClosing) return;
            _isClosing = true;

            try
            {
                var nav = Navigation;
                if (nav == null)
                {
                    Debug.WriteLine("CloseModalAsync: Navigation is null");
                    return;
                }

                // prefer modal pop since we present as modal
                if (nav.ModalStack != null && nav.ModalStack.Count > 0 && nav.ModalStack[^1] == this)
                {
                    await nav.PopModalAsync();
                    return;
                }

                // fallback to PopAsync if the page was pushed
                if (nav.NavigationStack != null && nav.NavigationStack.Count > 1)
                {
                    await nav.PopAsync();
                    return;
                }

                // last-resort modal pop
                try
                {
                    await nav.PopModalAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("CloseModalAsync final PopModalAsync failed: " + ex);
                }
            }
            finally
            {
                _isClosing = false;
            }
        }
    }
}