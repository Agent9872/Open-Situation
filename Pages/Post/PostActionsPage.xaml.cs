using CommunityToolkit.Maui.Views;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Post
{
    public partial class PostActionsPage : ContentPage
    {
        private readonly Lock.Models.Post _post;
        private readonly Action<Lock.Models.Post> _onEdit;
        private readonly Action<Lock.Models.Post> _onDelete;
        private readonly string _currentUserPhone;
        private readonly bool _isCurrentUserPost;
        private bool _isClosing;

        public PostActionsPage(Lock.Models.Post post, Action<Lock.Models.Post> onEdit, Action<Lock.Models.Post> onDelete)
        {
            InitializeComponent();

            _post = post ?? throw new ArgumentNullException(nameof(post));
            _onEdit = onEdit;
            _onDelete = onDelete;

            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty)?.Trim() ?? string.Empty;

            _isCurrentUserPost = string.Equals(
                NormalizePhone(_post.AuthorPhone),
                NormalizePhone(_currentUserPhone),
                StringComparison.OrdinalIgnoreCase);

            Debug.WriteLine($"PostActionsPage — author: '{NormalizePhone(_post.AuthorPhone)}', me: '{NormalizePhone(_currentUserPhone)}', isOwn: {_isCurrentUserPost}");

            string preview = string.IsNullOrWhiteSpace(_post.Content)
                ? (_post.ImagePathsList?.Any() == true ? "?? Post with image" : "Post")
                : (_post.Content.Length > 60 ? _post.Content.Substring(0, 60) + "…" : _post.Content);

            PreviewLabel.Text = preview;

            ApplyVisibilityBasedOnOwnership();
            UpdateMuteLabel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

#if ANDROID
            try
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                activity?.Window?.SetBackgroundDrawable(
                    new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.Transparent));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Modal background fix error: {ex.Message}");
            }
#endif
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

#if ANDROID
            try
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                activity?.Window?.SetBackgroundDrawableResource(Android.Resource.Color.Black);
            }
            catch { }
#endif
        }

        // ?? Helpers ???????????????????????????????????????????????????????????

        private string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            phone = phone.Trim();
            if (phone.Contains("·"))
            {
                var parts = phone.Split(new[] { '·' }, StringSplitOptions.RemoveEmptyEntries);
                phone = parts.Length > 1 ? parts[1].Trim() : phone;
            }
            return phone.Trim();
        }

        private void ApplyVisibilityBasedOnOwnership()
        {
            if (_isCurrentUserPost)
            {
                // Own post: show Edit + Delete, hide social actions
                EditPostRow.IsVisible = true;
                EditDivider.IsVisible = true;
                DeletePostRow.IsVisible = true;
                DeleteDivider.IsVisible = true;

                HidePostRow.IsVisible = false;
                HidePostDivider.IsVisible = false;
                MuteUserRow.IsVisible = false;
                MuteUserDivider.IsVisible = false;
                BlockUserRow.IsVisible = false;
                BlockUserDivider.IsVisible = false;
                FollowUserRow.IsVisible = false;
                FollowUserDivider.IsVisible = false;
                NotInterestedRow.IsVisible = false;
            }
            else
            {
                // Other's post: hide Edit + Delete, show social actions
                EditPostRow.IsVisible = false;
                EditDivider.IsVisible = false;
                DeletePostRow.IsVisible = false;
                DeleteDivider.IsVisible = false;

                HidePostRow.IsVisible = true;
                HidePostDivider.IsVisible = true;
                MuteUserRow.IsVisible = true;
                MuteUserDivider.IsVisible = true;
                BlockUserRow.IsVisible = true;
                BlockUserDivider.IsVisible = true;
                FollowUserRow.IsVisible = true;
                FollowUserDivider.IsVisible = true;
                NotInterestedRow.IsVisible = true;
            }
        }

        private void UpdateMuteLabel()
        {
            try
            {
                if (_isCurrentUserPost) return;
                bool isMuted = MuteUserService.IsUserMuted(
                    NormalizePhone(_post.AuthorPhone), _currentUserPhone);
                if (MuteUserLabel != null)
                    MuteUserLabel.Text = isMuted
                        ? "Unmute user (show their posts again)"
                        : "Mute user (stop seeing their posts)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateMuteLabel error: {ex}");
            }
        }

        // ?? Dismiss ???????????????????????????????????????????????????????????

        private async Task PopAsync()
        {
            try
            {
                var nav = Navigation;
                if (nav == null) return;
                if (nav.ModalStack?.Count > 0 && nav.ModalStack[^1] == this)
                    await nav.PopModalAsync(animated: false);
                else if (nav.NavigationStack?.Count > 1)
                    await nav.PopAsync(animated: false);
                else
                    await nav.PopModalAsync(animated: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PostActionsPage] PopAsync error: {ex.Message}");
            }
        }

        private async void OnBackgroundTapped(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;
            await PopAsync();
        }

        private void OnCardTapped(object sender, EventArgs e) { }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;
            await PopAsync();
        }

        // ?? Own post actions ??????????????????????????????????????????????????

        private async void OnEditTapped(object sender, EventArgs e)
        {
            if (_isClosing || !_isCurrentUserPost) return;
            _isClosing = true;
            try
            {
                await PopAsync();
                await Task.Delay(150);
                _onEdit?.Invoke(_post);
            }
            catch (Exception ex) { Debug.WriteLine($"OnEditTapped: {ex}"); }
        }

        private async void OnDeleteTapped(object sender, EventArgs e)
        {
            if (_isClosing || !_isCurrentUserPost) return;
            _isClosing = true;
            try
            {
                await PopAsync();
                await Task.Delay(150);
                _onDelete?.Invoke(_post);
            }
            catch (Exception ex) { Debug.WriteLine($"OnDeleteTapped: {ex}"); }
        }

        // ?? Other user actions ????????????????????????????????????????????????

        private async void OnHidePostTapped(object sender, EventArgs e)
        {
            if (_isClosing || _isCurrentUserPost) return;

            try
            {
                var confirm = await DisplayAlert(
                    "Hide Post",
                    "Hide this post from your feed?\n\nYou won't see this post again.",
                    "Hide", "Cancel");

                if (!confirm) return;

                _isClosing = true;
                bool success = await HidePostService.HidePostAsync(_post.Id, _currentUserPhone);

                if (success)
                {
                    await PopAsync();
                    await Task.Delay(150);
                    MessagingCenter.Send(this, "PostHidden", _post.Id);
                    await Application.Current.MainPage.DisplayAlert("Post Hidden",
                        "This post has been hidden from your feed.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Could not hide post. Please try again.", "OK");
                    _isClosing = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnHidePostTapped: {ex}");
                _isClosing = false;
            }
        }

        private async void OnMuteUserTapped(object sender, EventArgs e)
        {
            if (_isClosing || _isCurrentUserPost) return;

            try
            {
                string authorPhone = NormalizePhone(_post.AuthorPhone);
                bool isMuted = MuteUserService.IsUserMuted(authorPhone, _currentUserPhone);

                if (isMuted)
                {
                    // Unmute
                    var confirm = await DisplayAlert(
                        "Unmute User",
                        $"Start seeing posts from {_post.AuthorDisplayName} again?",
                        "Unmute", "Cancel");

                    if (!confirm) return;

                    _isClosing = true;
                    await MuteUserService.UnmuteUserAsync(authorPhone, _currentUserPhone);
                    await PopAsync();
                    MessagingCenter.Send(this, "UserUnmuted", authorPhone);
                    await Application.Current.MainPage.DisplayAlert(
                        "Unmuted", $"You'll see posts from {_post.AuthorDisplayName} again.", "OK");
                }
                else
                {
                    // Mute
                    var confirm = await DisplayAlert(
                        "Mute User",
                        $"Stop seeing posts from {_post.AuthorDisplayName}?\n\nYou can unmute them anytime.",
                        "Mute", "Cancel");

                    if (!confirm) return;

                    _isClosing = true;
                    await MuteUserService.MuteUserAsync(authorPhone, _currentUserPhone);
                    await PopAsync();

                    // Tell the feed to refresh and hide this user's posts
                    MessagingCenter.Send(this, "UserMuted", authorPhone);

                    await Application.Current.MainPage.DisplayAlert(
                        "User Muted",
                        $"You won't see posts from {_post.AuthorDisplayName} anymore.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnMuteUserTapped: {ex}");
                _isClosing = false;
            }
        }

        private async void OnBlockUserTapped(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;
            try
            {
                await PopAsync();
                var confirm = await Application.Current.MainPage.DisplayAlert(
                    "Block User",
                    $"Block {_post.AuthorDisplayName}?\n\n• You won't see their posts\n• They won't be able to message you\n• You can unblock anytime",
                    "Block", "Cancel");

                if (confirm)
                    await Application.Current.MainPage.DisplayAlert("Blocked", "User has been blocked", "OK");
            }
            catch (Exception ex) { Debug.WriteLine($"OnBlockUserTapped: {ex}"); }
        }


        private async void OnCopyLinkTapped(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            try
            {
                // Build a deep-link style string the app can handle
                // Format: lockapp://post/{postId}
                string link = $"lockapp://post/{_post.Id}";

                await Clipboard.SetTextAsync(link);
                await PopAsync();
                await Application.Current.MainPage.DisplayAlert(
                    "Link Copied",
                    "Post link copied to clipboard.\n\n" + link,
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnCopyLinkTapped: {ex}");
                await DisplayAlert("Error", "Could not copy link: " + ex.Message, "OK");
                _isClosing = false;
            }
        }

        private async void OnSharePostTapped(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;
            try
            {
                await PopAsync();
                await Task.Delay(150);
                string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                var sharePopup = new PostSharePopup(_post, currentUserPhone);
                var result = await Application.Current.MainPage.ShowPopupAsync(sharePopup);
                if (result?.ToString() == "post_shared")
                    await Application.Current.MainPage.DisplayAlert("Success", "Post shared successfully", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSharePostTapped: {ex}");
                await Application.Current.MainPage.DisplayAlert("Error", "Could not open share menu", "OK");
            }
        }

        private async void OnFollowUserTapped(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;
            try
            {
                await PopAsync();
                await Application.Current.MainPage.DisplayAlert("Info", "Follow/Unfollow feature coming soon!", "OK");
            }
            catch (Exception ex) { Debug.WriteLine($"OnFollowUserTapped: {ex}"); }
        }

        private async void OnNotInterestedTapped(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;
            try
            {
                await PopAsync();
                await Application.Current.MainPage.DisplayAlert("Info", "Not interested feature coming soon!", "OK");
            }
            catch (Exception ex) { Debug.WriteLine($"OnNotInterestedTapped: {ex}"); }
        }
    }
}