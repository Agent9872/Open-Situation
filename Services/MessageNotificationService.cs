using CommunityToolkit.Maui.Views;
using Lock.Pages.Controls;  // ← Must match your folder structure
using Lock.Models.Chat;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Lock.Services
{
    public interface IMessageNotificationService
    {
        Task ShowNewMessagePopupAsync(ChatMessage message, string senderName, string avatarPath, Action onTap);
        void DismissCurrentPopup();
    }

    public class MessageNotificationService : IMessageNotificationService
    {
        private MessagePopup _currentPopup;

        public async Task ShowNewMessagePopupAsync(ChatMessage message, string senderName, string avatarPath, Action onTap)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                DismissCurrentPopup();

                string preview = GetMessagePreview(message);
                _currentPopup = new MessagePopup(senderName, preview, avatarPath, onTap);

                var currentPage = GetCurrentPage();
                if (currentPage != null)
                {
                    currentPage.ShowPopup(_currentPopup);
                }
            });
        }

        public void DismissCurrentPopup()
        {
            if (_currentPopup != null)
            {
                _currentPopup.Close();
                _currentPopup = null;
            }
        }

        private string GetMessagePreview(ChatMessage message)
        {
            if (message.IsVoiceMessage) return "🎤 Voice message";
            if (message.IsImage) return "📷 Photo";
            if (message.MessageType == "post") return "📝 Shared a post";
            if (!string.IsNullOrEmpty(message.Content))
            {
                var content = message.Content.Replace("\n", " ").Trim();
                return content.Length > 60 ? content.Substring(0, 60) + "..." : content;
            }
            return "New message";
        }

        private Page GetCurrentPage()
        {
            var mainPage = Application.Current?.MainPage;
            if (mainPage is NavigationPage navigationPage)
                return navigationPage.CurrentPage;
            if (mainPage is Shell shell)
                return shell.CurrentPage;
            return mainPage;
        }
    }
}