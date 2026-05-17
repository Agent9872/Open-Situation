using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Lock.Models.Chat;
using SQLite;
using ChatDatabaseService = Lock.Chat.Services.DatabaseService;

namespace Lock.Pages.Chat
{
    public partial class ConversationActionsPage : ContentPage
    {
        private readonly Conversation _conversation;
        private readonly Func<Task>? _onChanged;
        private bool _isClosing;

        public ConversationActionsPage(Conversation conversation, Func<Task>? onChanged = null)
        {
            // Use a robust initializer that works whether XAML was compiled/generated or not.
            EnsureInitializeComponent();

            _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            _onChanged = onChanged;

            // show first 4 words of the last message (compact preview)
            var previewSrc = string.IsNullOrEmpty(_conversation.LastMessagePreview) ? "—" : _conversation.LastMessagePreview;
            string preview;
            if (string.IsNullOrWhiteSpace(previewSrc))
            {
                preview = "—";
            }
            else
            {
                var words = previewSrc.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length <= 4)
                    preview = string.Join(" ", words);
                else
                    preview = string.Join(" ", words.Take(4)) + "…";
            }

            // safe set of named controls (use FindByName to avoid relying on generated fields)
            var previewLabel = this.FindByName<Label>("PreviewLabel");
            if (previewLabel != null)
                previewLabel.Text = preview;

            // build actions with dynamic labels
            var actions = new List<string>
            {
                "Archive chat",
                _conversation.IsMuted ? "Unmute notifications" : "Mute notifications",
                _conversation.IsPinned ? "Unpin chat" : "Pin chat",
                "Mark as unread",
                _conversation.IsStarred ? "Remove from favorites" : "Add to favorites",
                    _conversation.IsArchived ? "Unarchive chat" : "Archive chat",

                "Add to list",
                "Block",
                "Delete chat"
            };

            var actionsCv = this.FindByName<CollectionView>("ActionsCollectionView");
            if (actionsCv != null)
                actionsCv.ItemsSource = actions;
        }

        // EnsureInitializeComponent fallback used across pages so code works whether XAML was compiled
        // into generated InitializeComponent or requires runtime load.
        private void EnsureInitializeComponent()
        {
            var mi = this.GetType().GetMethod("InitializeComponent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (mi != null)
            {
                mi.Invoke(this, null);
                return;
            }

            // Fallback: load the XAML at runtime if the generated method is not present
            Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(this, this.GetType());
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await CloseModalAsync();
        }


        private async void ActionButton_Clicked(object? sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            string action = string.Empty;

            // TapGestureRecognizer sender (when using Grid + TapGestureRecognizer)
            if (sender is TapGestureRecognizer tg)
            {
                action = tg.CommandParameter as string ?? tg.BindingContext as string ?? string.Empty;
            }
            // If called from a Grid or other VisualElement (Tapped routed), use its BindingContext
            else if (sender is VisualElement ve)
            {
                action = ve.BindingContext as string ?? string.Empty;
            }
            // If button was used (older template), fall back to Button.CommandParameter/Text
            else if (sender is Button btn)
            {
                action = btn.CommandParameter as string ?? btn.Text ?? string.Empty;
            }

            if (string.IsNullOrEmpty(action))
            {
                // nothing to do, close the modal
                try { await CloseModalAsync(); } catch { }
                _isClosing = false;
                return;
            }

            try
            {
                // use explicit chat DB service to avoid ambiguous reference
                await ChatDatabaseService.InitializeAsync();
                var db = ChatDatabaseService.GetConnection();

                switch (action)
                {
                    case "Archive chat":
                        await DisplayAlert("Archive", "Conversation archived (UI only).", "OK");
                        break;

                    case var s when s == "Mute notifications" || s == "Unmute notifications":
                        _conversation.IsMuted = !_conversation.IsMuted;
                        await SafeUpdateConversationAsync(db, _conversation);
                        break;

                    case var s2 when s2 == "Pin chat" || s2 == "Unpin chat":
                        _conversation.IsPinned = !_conversation.IsPinned;
                        await SafeUpdateConversationAsync(db, _conversation);
                        break;

                    case "Mark as unread":
                        await DisplayAlert("Marked", "Conversation marked as unread (UI only).", "OK");
                        break;

                    case var s3 when s3 == "Add to favorites" || s3 == "Remove from favorites":
                        _conversation.IsStarred = !_conversation.IsStarred;
                        await SafeUpdateConversationAsync(db, _conversation);
                        break;

                    case "Add to list":
                        var listName = await DisplayPromptAsync("Add to list", "Enter list name:");
                        if (!string.IsNullOrWhiteSpace(listName))
                            await DisplayAlert("Added", $"Conversation added to '{listName}' (UI only).", "OK");
                        break;

                    case "Block":
                        var confirm = await DisplayAlert("Block", "Block this contact? You can unblock later.", "Block", "Cancel");
                        if (confirm)
                        {
                            _conversation.IsMuted = true;
                            await SafeUpdateConversationAsync(db, _conversation);
                            await DisplayAlert("Blocked", "Contact blocked (muted).", "OK");
                        }
                        break;

                    case "Delete chat":
                        var del = await DisplayAlert("Delete", "Delete this conversation and messages?", "Delete", "Cancel");
                        if (del)
                        {
                            try
                            {
                                await db.DeleteAsync(_conversation);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Delete conversation error: " + ex);
                                await DisplayAlert("Error", "Could not delete conversation: " + ex.Message, "OK");
                            }
                        }
                        break;

                    default:
                        // no-op for unknown actions
                        break;
                }

                // notify caller to refresh if provided
                if (_onChanged != null)
                {
                    try { await _onChanged.Invoke(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ConversationActionsPage action error: " + ex);
                await DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                try { await CloseModalAsync(); } catch { }
                _isClosing = false;
            }
        }

        // local safe update helper
        private static async Task SafeUpdateConversationAsync(SQLiteAsyncConnection db, Conversation conv)
        {
            try
            {
                await db.UpdateAsync(conv);
            }
            catch
            {
                try { await db.InsertAsync(conv); } catch { }
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

                if (nav.ModalStack != null && nav.ModalStack.Count > 0 && nav.ModalStack[^1] == this)
                {
                    await nav.PopModalAsync();
                    return;
                }

                if (nav.NavigationStack != null && nav.NavigationStack.Count > 1)
                {
                    await nav.PopAsync();
                    return;
                }

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
