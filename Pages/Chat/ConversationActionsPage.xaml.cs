using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Lock.Models.Chat;
using Lock.Services;
using Lock.Chat.Services;

namespace Lock.Pages.Chat
{
    public partial class ConversationActionsPage : ContentPage
    {
        private readonly Conversation _conversation;
        private readonly Func<Task>? _onChanged;
        private bool _isClosing;

        public ConversationActionsPage(Conversation conversation, Func<Task>? onChanged = null)
        {
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

            // safe set of named controls
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

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            await CloseModalAsync();
        }

        private async void ActionButton_Clicked(object? sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            string action = string.Empty;

            if (sender is TapGestureRecognizer tg)
            {
                action = tg.CommandParameter as string ?? tg.BindingContext as string ?? string.Empty;
            }
            else if (sender is VisualElement ve)
            {
                action = ve.BindingContext as string ?? string.Empty;
            }
            else if (sender is Button btn)
            {
                action = btn.CommandParameter as string ?? btn.Text ?? string.Empty;
            }

            if (string.IsNullOrEmpty(action))
            {
                try { await CloseModalAsync(); } catch { }
                _isClosing = false;
                return;
            }

            try
            {
                switch (action)
                {
                    case "Archive chat":
                        _conversation.IsArchived = !_conversation.IsArchived;
                        await SafeUpdateConversationAsync(_conversation);
                        await DisplayAlert(_conversation.IsArchived ? "Archived" : "Unarchived",
                            _conversation.IsArchived ? "Conversation archived." : "Conversation unarchived.", "OK");
                        break;

                    case var s when s == "Mute notifications" || s == "Unmute notifications":
                        _conversation.IsMuted = !_conversation.IsMuted;
                        await SafeUpdateConversationAsync(_conversation);
                        await DisplayAlert(_conversation.IsMuted ? "Muted" : "Unmuted",
                            _conversation.IsMuted ? "Notifications muted." : "Notifications unmuted.", "OK");
                        break;

                    case var s2 when s2 == "Pin chat" || s2 == "Unpin chat":
                        _conversation.IsPinned = !_conversation.IsPinned;
                        await SafeUpdateConversationAsync(_conversation);
                        await DisplayAlert(_conversation.IsPinned ? "Pinned" : "Unpinned",
                            _conversation.IsPinned ? "Conversation pinned." : "Conversation unpinned.", "OK");
                        break;

                    case "Mark as unread":
                        var currentUserPhone = Microsoft.Maui.Storage.Preferences.Get("current_user_phone", string.Empty);
                        if (!string.IsNullOrEmpty(currentUserPhone))
                        {
                            await SupabaseService.UpdateAsync("ChatMessages",
                                $"ConversationId=eq.{Uri.EscapeDataString(_conversation.ConversationId)}&RecipientPhone=eq.{Uri.EscapeDataString(currentUserPhone)}",
                                new { IsRead = false });
                            await DisplayAlert("Marked", "Conversation marked as unread.", "OK");
                        }
                        else
                        {
                            await DisplayAlert("Error", "Could not mark as unread.", "OK");
                        }
                        break;

                    case var s3 when s3 == "Add to favorites" || s3 == "Remove from favorites":
                        _conversation.IsStarred = !_conversation.IsStarred;
                        await SafeUpdateConversationAsync(_conversation);
                        await DisplayAlert(_conversation.IsStarred ? "Added to favorites" : "Removed from favorites",
                            _conversation.IsStarred ? "Added to favorites." : "Removed from favorites.", "OK");
                        break;

                    case "Add to list":
                        var listName = await DisplayPromptAsync("Add to list", "Enter list name:");
                        if (!string.IsNullOrWhiteSpace(listName))
                        {
                            var map = LoadConversationLists();
                            map[_conversation.ConversationId] = listName.Trim();
                            SaveConversationLists(map);
                            await DisplayAlert("Added", $"Conversation added to '{listName}'", "OK");
                        }
                        break;

                    case "Block":
                        var currentUser = Microsoft.Maui.Storage.Preferences.Get("current_user_phone", string.Empty);
                        var otherPhone = _conversation.ParticipantA == currentUser ? _conversation.ParticipantB : _conversation.ParticipantA;
                        var confirm = await DisplayAlert("Block", $"Block {otherPhone}? You can unblock later.", "Block", "Cancel");
                        if (confirm)
                        {
                            var success = await ChatRepository.BlockUserAsync(currentUser, otherPhone);
                            if (success)
                            {
                                _conversation.IsMuted = true;
                                await SafeUpdateConversationAsync(_conversation);
                                await DisplayAlert("Blocked", "Contact blocked.", "OK");
                            }
                            else
                            {
                                await DisplayAlert("Error", "Could not block user.", "OK");
                            }
                        }
                        break;

                    case "Delete chat":
                        var del = await DisplayAlert("Delete", "Delete this conversation and all messages? This cannot be undone.", "Delete", "Cancel");
                        if (del)
                        {
                            try
                            {
                                // Delete all messages in conversation
                                await SupabaseService.DeleteAsync("ChatMessages", $"ConversationId=eq.{Uri.EscapeDataString(_conversation.ConversationId)}");
                                // Delete the conversation
                                await SupabaseService.DeleteAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(_conversation.ConversationId)}");

                                // Remove from lists if present
                                var map = LoadConversationLists();
                                if (map.Remove(_conversation.ConversationId))
                                    SaveConversationLists(map);

                                await DisplayAlert("Deleted", "Conversation deleted.", "OK");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Delete conversation error: " + ex);
                                await DisplayAlert("Error", "Could not delete conversation: " + ex.Message, "OK");
                            }
                        }
                        break;

                    default:
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

        // Helper methods for conversation lists
        private Dictionary<string, string> LoadConversationLists()
        {
            try
            {
                var me = Microsoft.Maui.Storage.Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(me)) return new Dictionary<string, string>();
                var json = Microsoft.Maui.Storage.Preferences.Get($"conversation_lists_{me}", string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch { return new Dictionary<string, string>(); }
        }

        private void SaveConversationLists(Dictionary<string, string> map)
        {
            try
            {
                var me = Microsoft.Maui.Storage.Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(me)) return;
                Microsoft.Maui.Storage.Preferences.Set($"conversation_lists_{me}", System.Text.Json.JsonSerializer.Serialize(map));
            }
            catch { }
        }

        // local safe update helper using Supabase
        private static async Task SafeUpdateConversationAsync(Conversation conv)
        {
            try
            {
                await SupabaseService.UpdateAsync("Conversations", $"ConversationId=eq.{Uri.EscapeDataString(conv.ConversationId)}", conv);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SafeUpdateConversationAsync error: {ex}");
                try
                {
                    await SupabaseService.InsertAsync("Conversations", conv);
                }
                catch (Exception insertEx)
                {
                    Debug.WriteLine($"SafeUpdateConversationAsync insert fallback error: {insertEx}");
                }
            }
        }

        // Safely close the modal dialog
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