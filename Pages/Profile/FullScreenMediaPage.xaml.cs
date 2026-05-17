using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace Lock.Pages.Profile
{
    public partial class FullScreenMediaPage : ContentPage
    {
        private List<string> _imagePaths;
        private int _currentIndex;

        public FullScreenMediaPage(List<string> imagePaths, int startIndex = 0, bool hideControls = false)
        {
            InitializeComponent();

            _imagePaths = imagePaths ?? new List<string>();
            _currentIndex = Math.Clamp(startIndex, 0, _imagePaths.Count - 1);

            // Show/hide navigation arrows based on number of images
            LeftArrow.IsVisible = _imagePaths.Count > 1 && !hideControls;
            RightArrow.IsVisible = _imagePaths.Count > 1 && !hideControls;

            // Hide download and close buttons if hideControls is true
            if (hideControls)
            {
                DownloadButton.IsVisible = false;
                CloseButton.IsVisible = false;
            }

            LoadCurrentImage();
        }

        private void LoadCurrentImage()
        {
            if (_imagePaths.Count == 0) return;

            var path = _imagePaths[_currentIndex];
            try
            {
                if (File.Exists(path))
                    MediaImage.Source = ImageSource.FromFile(path);
                else if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
                    MediaImage.Source = ImageSource.FromUri(new Uri(path));
            }
            catch
            {
                MediaImage.Source = null;
            }

            CounterLabel.Text = $"{_currentIndex + 1}/{_imagePaths.Count}";
        }

        private async void OnCloseTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnDownloadTapped(object sender, EventArgs e)
        {
            await DownloadCurrentImage();
        }

        private void OnLeftArrowTapped(object sender, EventArgs e)
        {
            _currentIndex = (_currentIndex - 1 + _imagePaths.Count) % _imagePaths.Count;
            LoadCurrentImage();
        }

        private void OnRightArrowTapped(object sender, EventArgs e)
        {
            _currentIndex = (_currentIndex + 1) % _imagePaths.Count;
            LoadCurrentImage();
        }

        private async Task DownloadCurrentImage()
        {
            try
            {
                var imagePath = _imagePaths[_currentIndex];

                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    await DisplayAlert("Error", "Image file not found", "OK");
                    return;
                }

                var saveOption = await DisplayActionSheet(
                    "Save Image To",
                    "Cancel",
                    null,
                    "Pictures Folder",
                    "Downloads Folder"
                );

                if (saveOption == "Cancel" || saveOption == null)
                    return;

                string destinationFolder = "";
                string appFolderName = "MyApp";

                switch (saveOption)
                {
                    case "Pictures Folder":
                        destinationFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            appFolderName);
                        break;

                    case "Downloads Folder":
                        destinationFolder = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Downloads",
                            appFolderName);
                        break;
                }

                if (!Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"media_{timestamp}.jpg";
                string destPath = Path.Combine(destinationFolder, fileName);

                File.Copy(imagePath, destPath, true);

                await DisplayAlert("Success", $"Image saved to:\n{destinationFolder}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
            }
        }
    }
}