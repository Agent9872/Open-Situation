using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Lock.Pages.Chat
{
    public partial class GroupDetailsPage : ContentPage
    {
        private Group _group;
        private string _currentUserPhone;
        private bool _isMember;

        public string JoinButtonText => _isMember ? "Joined ?" : "Join Group";

        public GroupDetailsPage(Group group, string currentUserPhone)
        {
            InitializeComponent();
            BindingContext = this;
            _group = group;
            _currentUserPhone = currentUserPhone;

            _ = CheckMembershipAsync();
        }

        private async Task CheckMembershipAsync()
        {
            try
            {
                var member = await GroupRepository.GetMemberAsync(_group.Id, _currentUserPhone);
                _isMember = member != null;
                OnPropertyChanged(nameof(JoinButtonText));

                if (JoinButton != null)
                {
                    JoinButton.Text = JoinButtonText;
                    JoinButton.BackgroundColor = _isMember ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#008080");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckMembershipAsync error: {ex}");
            }
        }

        private async void OnJoinButtonClicked(object sender, EventArgs e)
        {
            try
            {
                if (_isMember)
                {
                    var chatPage = new GroupChatPage();
                    chatPage.GroupId = _group.Id;
                    await Navigation.PushAsync(chatPage);
                    return;
                }

                bool confirm = await DisplayAlert("Join Group", $"Join '{_group.Name}'?", "Join", "Cancel");
                if (!confirm) return;

                if (JoinButton != null)
                {
                    JoinButton.IsEnabled = false;
                    JoinButton.Text = "Joining...";
                }

                var (success, message) = await GroupRepository.JoinGroupAsync(_group.Id, _currentUserPhone);

                if (success)
                {
                    _isMember = true;
                    OnPropertyChanged(nameof(JoinButtonText));

                    if (JoinButton != null)
                    {
                        JoinButton.Text = "Joined ?";
                        JoinButton.BackgroundColor = Color.FromArgb("#2A2A2A");
                        JoinButton.IsEnabled = true;
                    }

                    await DisplayAlert("Success", $"You joined '{_group.Name}'", "OK");

                    var chatPage = new GroupChatPage();
                    chatPage.GroupId = _group.Id;
                    await Navigation.PushAsync(chatPage);
                }
                else
                {
                    if (JoinButton != null)
                    {
                        JoinButton.Text = "Join Group";
                        JoinButton.IsEnabled = true;
                    }
                    await DisplayAlert("Cannot Join", message, "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnJoinButtonClicked error: {ex}");
                if (JoinButton != null)
                {
                    JoinButton.Text = "Join Group";
                    JoinButton.IsEnabled = true;
                }
                await DisplayAlert("Error", "Could not join group", "OK");
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}