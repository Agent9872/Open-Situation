using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System;

namespace Lock.Pages.Controls
{
    public partial class MessagePopup : Popup
    {
        private readonly Action _onTapAction;
        private System.Timers.Timer _autoCloseTimer;

        public MessagePopup(string senderName, string messagePreview, string avatarPath, Action onTapAction)
        {
            InitializeComponent();

            _onTapAction = onTapAction;

            SenderNameLabel.Text = senderName;
            MessagePreviewLabel.Text = messagePreview;

            if (!string.IsNullOrEmpty(avatarPath) && System.IO.File.Exists(avatarPath))
            {
                AvatarImage.Source = ImageSource.FromFile(avatarPath);
            }
            else
            {
                string initials = GetInitials(senderName);
                AvatarImage.Source = GenerateInitialsAvatar(initials);
            }

            _autoCloseTimer = new System.Timers.Timer(5000);
            _autoCloseTimer.Elapsed += (s, e) =>
            {
                _autoCloseTimer.Stop();
                MainThread.BeginInvokeOnMainThread(() => Close());
            };
            _autoCloseTimer.Start();

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnPopupTapped;
            this.Content.GestureRecognizers.Add(tapGesture);
        }

        private void OnPopupTapped(object sender, EventArgs e)
        {
            _autoCloseTimer?.Stop();
            _onTapAction?.Invoke();
            Close();
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            _autoCloseTimer?.Stop();
            Close();
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Trim().Split(' ');
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length > 0 ? name[0].ToString().ToUpper() : "?";
        }

        private ImageSource GenerateInitialsAvatar(string initials)
        {
            var colors = new[] { "008080", "C05050", "4A6FA5", "E8933C", "9B59B6" };
            var random = new Random(initials.GetHashCode());
            var color = colors[random.Next(colors.Length)];
            return ImageSource.FromUri(new Uri($"https://ui-avatars.com/api/?name={initials}&background={color}&color=fff&size=80"));
        }
    }
}