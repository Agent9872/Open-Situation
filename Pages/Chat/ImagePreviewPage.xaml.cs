using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class ImagePreviewPage : ContentPage
    {
        private List<string> _imagePaths;
        private string _groupId;
        private string _currentUserPhone;
        private int? _replyToMessageId;
        private List<string> _captions = new();
        private int _currentIndex = 0;
        private List<ImageSource> _imageSources = new();

        public ImagePreviewPage(List<string> imagePaths, string groupId, string currentUserPhone, int? replyToMessageId = null)
        {
            InitializeComponent();
            _imagePaths = imagePaths;
            _groupId = groupId;
            _currentUserPhone = currentUserPhone;
            _replyToMessageId = replyToMessageId;
            _captions = new List<string>(new string[imagePaths.Count]);

            LoadImages();
            SetupGestures();
        }

        private void LoadImages()
        {
            foreach (var path in _imagePaths)
            {
                _imageSources.Add(ImageSource.FromFile(path));
            }

            UpdateImageDisplay();

            if (_imagePaths.Count > 1)
            {
                ImageCounterBadge.IsVisible = true;
                NavigationDots.IsVisible = true;
                UpdateNavigationDots();
                UpdateButtonText();
            }
        }

        private void UpdateImageDisplay()
        {
            PreviewImage.Source = _imageSources[_currentIndex];
            ImageCounterLabel.Text = $"{_currentIndex + 1}/{_imagePaths.Count}";
            CaptionEditor.Text = _captions[_currentIndex] ?? string.Empty;
        }

        private void UpdateNavigationDots()
        {
            NavigationDots.Children.Clear();

            for (int i = 0; i < _imagePaths.Count; i++)
            {
                var dot = new BoxView
                {
                    WidthRequest = 8,
                    HeightRequest = 8,
                    CornerRadius = 4,
                    BackgroundColor = i == _currentIndex ? Color.FromArgb("#008080") : Color.FromArgb("#444444"),
                    Margin = new Thickness(4, 0)
                };
                NavigationDots.Children.Add(dot);
            }
        }

        private void UpdateButtonText()
        {
            if (_imagePaths.Count > 1)
            {
                var remaining = _imagePaths.Count - (_currentIndex + 1);
                if (remaining > 0)
                    SendButtonText.Text = $"Next ({remaining})";
                else
                    SendButtonText.Text = "Send All";
            }
            else
            {
                SendButtonText.Text = "Send";
            }
        }

        private void SetupGestures()
        {
            var leftSwipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
            leftSwipe.Swiped += OnSwipeLeft;
            PreviewImage.GestureRecognizers.Add(leftSwipe);

            var rightSwipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
            rightSwipe.Swiped += OnSwipeRight;
            PreviewImage.GestureRecognizers.Add(rightSwipe);
        }

        private void OnSwipeLeft(object sender, SwipedEventArgs e)
        {
            if (_currentIndex < _imagePaths.Count - 1)
            {
                _captions[_currentIndex] = CaptionEditor.Text ?? string.Empty;
                _currentIndex++;
                UpdateImageDisplay();
                UpdateNavigationDots();
                UpdateButtonText();
            }
        }

        private void OnSwipeRight(object sender, SwipedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _captions[_currentIndex] = CaptionEditor.Text ?? string.Empty;
                _currentIndex--;
                UpdateImageDisplay();
                UpdateNavigationDots();
                UpdateButtonText();
            }
        }

        private async Task SendImagesAsync(bool withCaptions)
        {
            try
            {
                // Save current caption
                _captions[_currentIndex] = CaptionEditor.Text ?? string.Empty;

                string messageContent;

                if (withCaptions && _captions.Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    if (_imagePaths.Count == 1)
                    {
                        messageContent = _captions[0] ?? string.Empty;
                    }
                    else
                    {
                        var captionList = new List<string>();
                        for (int i = 0; i < _imagePaths.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(_captions[i]))
                            {
                                captionList.Add($"?? {_captions[i]}");
                            }
                        }
                        messageContent = captionList.Any()
                            ? string.Join("\n", captionList)
                            : $"?? {_imagePaths.Count} photos";
                    }
                }
                else
                {
                    messageContent = _imagePaths.Count == 1
                        ? "?? Photo"
                        : $"?? {_imagePaths.Count} photos";
                }

                // Fix: Convert int? to string? (or pass null)
                var msg = await GroupRepository.SendMessageAsync(
                    _groupId,
                    _currentUserPhone,
                    messageContent,
                    GroupMessageType.Image,
                    mediaPaths: _imagePaths,
                    replyToMessageId: _replyToMessageId?.ToString());  // Convert to string? using ToString()

                msg.IsOutgoing = true;
                msg.ShowAvatar = false;

                await Navigation.PopModalAsync();
                MessagingCenter.Send(this, "ImagesSent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Send images error: {ex}");
                await DisplayAlert("Error", "Could not send images: " + ex.Message, "OK");
            }
        }

        private async void OnSendTapped(object sender, EventArgs e)
        {
            if (_imagePaths.Count > 1 && _currentIndex < _imagePaths.Count - 1)
            {
                // Navigate to next image instead of sending
                _captions[_currentIndex] = CaptionEditor.Text ?? string.Empty;
                _currentIndex++;
                UpdateImageDisplay();
                UpdateNavigationDots();
                UpdateButtonText();
            }
            else
            {
                await SendImagesAsync(true);
            }
        }

        private async void OnSendWithoutCaptionTapped(object sender, EventArgs e)
        {
            await SendImagesAsync(false);
        }

        private async void OnCloseTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}