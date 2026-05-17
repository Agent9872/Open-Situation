using Lock.Models;
using Lock.Pages.Profile;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lock.Pages.Post
{
    public partial class StatusViewerPage : ContentPage
    {
        // Updated tuple to include Moods list
        private List<(string UserPhone, List<string> ImagePaths, string UserName, List<string> Moods)> _usersWithStatus;
        private int _currentUserIndex;
        private int _currentImageIndex;
        private bool _isPlaying = true;
        private bool _isExiting = false;
        private double _currentProgress = 0;
        private const int ImageDurationMs = 5000;
        private const int TickMs = 40; 
        private CancellationTokenSource _animationCts;
        private List<ProgressBar> _progressBars = new List<ProgressBar>();
        private string _currentUserPhone;
        private bool _isOwner;
        private FullScreenMediaPage _currentMediaPage;

        public StatusViewerPage(
            List<(string UserPhone, List<string> ImagePaths, string UserName, List<string> Moods)> usersWithStatus,
            int startUserIndex = 0,
            int startImageIndex = 0)
        {
            try
            {
                InitializeComponent();

                System.Diagnostics.Debug.WriteLine($"StatusViewerPage constructor called with {usersWithStatus.Count} users");

                _usersWithStatus = usersWithStatus;
                _currentUserIndex = startUserIndex;
                _currentImageIndex = startImageIndex;
                _currentUserPhone = Preferences.Get("current_user_phone", string.Empty) ?? string.Empty;

                UpdateUI();
                LoadCurrentUserStatus();

                System.Diagnostics.Debug.WriteLine("StatusViewerPage constructor completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in StatusViewerPage constructor: {ex}");
            }
        }

        private void UpdateUI()
        {
            try
            {
                var currentUser = _usersWithStatus[_currentUserIndex];
                _isOwner = string.Equals(currentUser.UserPhone, _currentUserPhone, StringComparison.OrdinalIgnoreCase);

                UserNameLabel.Text = currentUser.UserName;

                var moodDot = this.FindByName<Label>("MoodDot");

                bool hasMood = _currentImageIndex < currentUser.Moods.Count
                               && !string.IsNullOrEmpty(currentUser.Moods[_currentImageIndex]);

                if (hasMood)
                {
                    MoodLabel.Text = currentUser.Moods[_currentImageIndex];
                    MoodLabel.IsVisible = true;
                    if (moodDot != null) moodDot.IsVisible = true;
                }
                else
                {
                    MoodLabel.IsVisible = false;
                    if (moodDot != null) moodDot.IsVisible = false;
                }

                PrevUserButton.IsVisible = _currentUserIndex > 0;
                NextUserButton.IsVisible = _currentUserIndex < _usersWithStatus.Count - 1;
                EditButton.IsVisible = _isOwner;
                DeleteButton.IsVisible = _isOwner;
                DownloadButton.IsVisible = true;
                CancelButton.IsVisible = true;

                ProgressBarsContainer.Children.Clear();
                ProgressBarsContainer.ColumnDefinitions.Clear();
                _progressBars.Clear();

                int count = currentUser.ImagePaths.Count;
                for (int i = 0; i < count; i++)
                    ProgressBarsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

                for (int i = 0; i < count; i++)
                {
                    double progress = i < _currentImageIndex ? 1.0
                                    : i == _currentImageIndex ? _currentProgress
                                    : 0.0;

                    var progressBar = new ProgressBar
                    {
                        Progress = progress,
                        HeightRequest = 3,
                        ProgressColor = Color.FromArgb("#008080"),
                        BackgroundColor = Colors.White.WithAlpha(0.3f),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(0)
                    };

                    Grid.SetColumn(progressBar, i);
                    _progressBars.Add(progressBar);
                    ProgressBarsContainer.Children.Add(progressBar);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateUI: {ex}");
            }
        }

        private async void LoadCurrentUserStatus()
        {
            try
            {
                var currentUser = _usersWithStatus[_currentUserIndex];

                _currentMediaPage = new FullScreenMediaPage(currentUser.ImagePaths, _currentImageIndex, true);

                if (_currentMediaPage.Content is View mediaContent)
                {
                    HideCounterInView(mediaContent);
                    MediaFrame.Content = mediaContent;
                }

                // Always show controls immediately — never hide them
                ShowControls();

                StartAnimation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading status: {ex}");
            }
        }
        // ?? Recursively finds and hides any label showing "x/y" counter format ??
        private void HideCounterInView(View view)
        {
            try
            {
                if (view is Label label)
                {
                    var text = label.Text ?? string.Empty;
                    // Hide if it looks like a counter e.g. "1/4", "2/3"
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+/\d+$"))
                    {
                        label.IsVisible = false;
                        return;
                    }
                }

                if (view is Layout layout)
                {
                    foreach (var child in layout.Children)
                    {
                        if (child is View childView)
                            HideCounterInView(childView);
                    }
                }

                if (view is ContentView contentView && contentView.Content != null)
                    HideCounterInView(contentView.Content);

                if (view is Border border && border.Content != null)
                    HideCounterInView(border.Content);

                if (view is Frame frame && frame.Content != null)
                    HideCounterInView(frame.Content);

                if (view is ScrollView scrollView && scrollView.Content != null)
                    HideCounterInView(scrollView.Content);

                if (view is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is View childView)
                            HideCounterInView(childView);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HideCounterInView error: {ex}");
            }
        }
        private void StartAnimation()
        {
            _animationCts?.Cancel();
            _animationCts = new CancellationTokenSource();
            var token = _animationCts.Token;

            _currentProgress = 0;
            _isPlaying = true;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested && !_isExiting &&
                           _currentUserIndex < _usersWithStatus.Count &&
                           _currentImageIndex < _usersWithStatus[_currentUserIndex].ImagePaths.Count)
                    {
                        // Animate current progress
                        while (_currentProgress < 1.0 && !token.IsCancellationRequested && !_isExiting)
                        {
                            while (!_isPlaying && !token.IsCancellationRequested && !_isExiting)
                            {
                                await Task.Delay(100, token);
                            }

                            _currentProgress += (double)TickMs / ImageDurationMs;  // 8 seconds / 40ms ticks = 200 steps, 1/200 = 0.005
                            if (_currentProgress > 1.0) _currentProgress = 1.0;

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    if (_currentImageIndex < _progressBars.Count && !_isExiting)
                                        _progressBars[_currentImageIndex].Progress = _currentProgress;
                                }
                                catch { }
                            });

                            await Task.Delay(TickMs, token);
                        }

                        if (_currentProgress >= 1.0)
                        {
                            if (_currentImageIndex < _usersWithStatus[_currentUserIndex].ImagePaths.Count - 1)
                            {
                                // Next image in same user
                                _currentImageIndex++;
                                _currentProgress = 0;

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    try
                                    {
                                        LoadCurrentUserStatus();
                                        UpdateUI(); // This updates mood for new image
                                    }
                                    catch { }
                                });
                            }
                            else if (_currentUserIndex < _usersWithStatus.Count - 1)
                            {
                                // Next user
                                _currentUserIndex++;
                                _currentImageIndex = 0;
                                _currentProgress = 0;

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    try
                                    {
                                        LoadCurrentUserStatus();
                                        UpdateUI(); // This updates mood for new user's first image
                                    }
                                    catch { }
                                });
                            }
                            else
                            {
                                // End of all statuses
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    try
                                    {
                                        await Navigation.PopModalAsync();
                                    }
                                    catch { }
                                });
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Animation error: {ex}");
                }
            }, token);
        }

        private void ShowControls()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (BottomControls != null)
                    BottomControls.IsVisible = true;
                if (ActionControls != null)
                    ActionControls.IsVisible = true;
            });
        }


        private void OnLeftTapped(object sender, EventArgs e)
        {
            ShowControls();

            if (_currentImageIndex > 0)
            {
                // Previous image in same user
                _currentImageIndex--;
                _currentProgress = 0;
                _animationCts?.Cancel();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadCurrentUserStatus();
                    UpdateUI(); // Updates mood
                });
            }
            else if (_currentUserIndex > 0)
            {
                // Previous user, go to their last image
                _currentUserIndex--;
                _currentImageIndex = _usersWithStatus[_currentUserIndex].ImagePaths.Count - 1;
                _currentProgress = 0;
                _animationCts?.Cancel();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadCurrentUserStatus();
                    UpdateUI(); // Updates mood
                });
            }
        }

        private void OnCenterTapped(object sender, EventArgs e)
        {
            ShowControls();
            _isPlaying = !_isPlaying;
        }

        private void OnRightTapped(object sender, EventArgs e)
        {
            ShowControls();

            if (_currentImageIndex < _usersWithStatus[_currentUserIndex].ImagePaths.Count - 1)
            {
                // Next image in same user
                _currentImageIndex++;
                _currentProgress = 0;
                _animationCts?.Cancel();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadCurrentUserStatus();
                    UpdateUI(); // Updates mood
                });
            }
            else if (_currentUserIndex < _usersWithStatus.Count - 1)
            {
                // Next user, go to their first image
                _currentUserIndex++;
                _currentImageIndex = 0;
                _currentProgress = 0;
                _animationCts?.Cancel();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadCurrentUserStatus();
                    UpdateUI(); // Updates mood
                });
            }
        }

        private async void OnPrevUserTapped(object sender, EventArgs e)
        {
            if (_currentUserIndex > 0)
            {
                _currentUserIndex--;
                _currentImageIndex = 0;
                _currentProgress = 0;
                _animationCts?.Cancel();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadCurrentUserStatus();
                    UpdateUI(); // Updates mood
                });
                if (BottomControls != null)
                    BottomControls.IsVisible = false;
                if (ActionControls != null)
                    ActionControls.IsVisible = false;
            }
        }

        private async void OnNextUserTapped(object sender, EventArgs e)
        {
            if (_currentUserIndex < _usersWithStatus.Count - 1)
            {
                _currentUserIndex++;
                _currentImageIndex = 0;
                _currentProgress = 0;
                _animationCts?.Cancel();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadCurrentUserStatus();
                    UpdateUI(); // Updates mood
                });
                if (BottomControls != null)
                    BottomControls.IsVisible = false;
                if (ActionControls != null)
                    ActionControls.IsVisible = false;
            }
        }

        private async void OnCancelTapped(object sender, EventArgs e)
        {
            _isExiting = true;
            _animationCts?.Cancel();
            await Navigation.PopModalAsync();
        }

        private async void OnDownloadTapped(object sender, EventArgs e)
        {
            ShowControls();

            try
            {
                var currentUser = _usersWithStatus[_currentUserIndex];
                var currentImagePath = currentUser.ImagePaths[_currentImageIndex];

                if (string.IsNullOrEmpty(currentImagePath) || !File.Exists(currentImagePath))
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
                string fileName = $"status_{timestamp}.jpg";
                string destPath = Path.Combine(destinationFolder, fileName);

                File.Copy(currentImagePath, destPath, true);

                await DisplayAlert("Success", $"Image saved to:\n{destinationFolder}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
            }
        }

        private async void OnEditTapped(object sender, EventArgs e)
        {
            try
            {
                var currentUser = _usersWithStatus[_currentUserIndex];
                var currentImagePath = currentUser.ImagePaths[_currentImageIndex];

                var choice = await DisplayActionSheet("Edit Status", "Cancel", null,
                    "Change Image", "Change Mood", "Delete");

                if (choice == "Change Image")
                {
                    var result = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = "Select new image",
                        FileTypes = FilePickerFileType.Images
                    });

                    if (result != null)
                    {
                        var destFileName = $"status_{Guid.NewGuid():N}{System.IO.Path.GetExtension(result.FileName)}";
                        var savedPath = await SavePickedFileAsync(result, destFileName);

                        if (!string.IsNullOrEmpty(savedPath))
                        {
                            await Lock.Chat.Services.DatabaseService.InitializeAsync();
                            var db = Lock.Chat.Services.DatabaseService.GetConnection();

                            var statusPost = await db.Table<Lock.Models.Post>()
                                .Where(p => p.StatusImagePath == currentImagePath && p.AuthorPhone == currentUser.UserPhone)
                                .FirstOrDefaultAsync();

                            if (statusPost != null)
                            {
                                try { if (File.Exists(currentImagePath)) File.Delete(currentImagePath); } catch { }

                                statusPost.StatusImagePath = savedPath;
                                await db.UpdateAsync(statusPost);

                                currentUser.ImagePaths[_currentImageIndex] = savedPath;
                                // Keep the same mood

                                _animationCts?.Cancel();
                                LoadCurrentUserStatus();
                                UpdateUI();

                                await DisplayAlert("Success", "Image updated successfully", "OK");
                            }
                        }
                    }
                }
                else if (choice == "Change Mood")
                {
                    await Lock.Chat.Services.DatabaseService.InitializeAsync();
                    var db = Lock.Chat.Services.DatabaseService.GetConnection();

                    var statusPost = await db.Table<Lock.Models.Post>()
                        .Where(p => p.StatusImagePath == currentImagePath && p.AuthorPhone == currentUser.UserPhone)
                        .FirstOrDefaultAsync();

                    if (statusPost != null)
                    {
                        var moodChoice = await DisplayActionSheet("Change mood", "Cancel", null,
                            "Happy", "Sad", "Excited", "Angry", "Neutral", "Custom");

                        string mood = string.Empty;
                        if (!string.IsNullOrEmpty(moodChoice) && moodChoice != "Cancel")
                        {
                            if (moodChoice == "Custom")
                            {
                                mood = await DisplayPromptAsync("Custom Mood", "Enter mood:",
                                    initialValue: statusPost.Mood) ?? statusPost.Mood;
                            }
                            else
                            {
                                mood = moodChoice;
                            }

                            if (!string.IsNullOrEmpty(mood))
                            {
                                statusPost.Mood = mood;
                                await db.UpdateAsync(statusPost);

                                // Update the mood in our list
                                currentUser.Moods[_currentImageIndex] = mood;

                                // Update UI to show new mood
                                UpdateUI();

                                await DisplayAlert("Success", "Mood updated successfully", "OK");
                            }
                        }
                    }
                }
                else if (choice == "Delete")
                {
                    OnDeleteTapped(sender, e);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnEditTapped: {ex}");
                await DisplayAlert("Error", "Failed to edit status", "OK");
            }
        }

        private async Task<string?> SavePickedFileAsync(FileResult result, string destFileName)
        {
            if (result == null) return null;

            try
            {
                var folder = FileSystem.AppDataDirectory;
                var destPath = System.IO.Path.Combine(folder, destFileName);

                using var sourceStream = await result.OpenReadAsync();
                using var destStream = File.Open(destPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destStream);
                return destPath;
            }
            catch
            {
                return null;
            }
        }

        private async void OnDeleteTapped(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert("Delete Image", "Delete this status image?", "Yes", "No");
                if (!confirm) return;

                var currentUser = _usersWithStatus[_currentUserIndex];
                var currentImagePath = currentUser.ImagePaths[_currentImageIndex];

                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                var db = Lock.Chat.Services.DatabaseService.GetConnection();

                var statusPost = await db.Table<Lock.Models.Post>()
                    .Where(p => p.StatusImagePath == currentImagePath && p.AuthorPhone == currentUser.UserPhone)
                    .FirstOrDefaultAsync();

                if (statusPost != null)
                {
                    await db.DeleteAsync(statusPost);
                    try { if (File.Exists(currentImagePath)) File.Delete(currentImagePath); } catch { }

                    // Remove image and its mood
                    currentUser.ImagePaths.RemoveAt(_currentImageIndex);
                    if (_currentImageIndex < currentUser.Moods.Count)
                        currentUser.Moods.RemoveAt(_currentImageIndex);

                    if (currentUser.ImagePaths.Count == 0)
                    {
                        // Remove user if no images left
                        _usersWithStatus.RemoveAt(_currentUserIndex);

                        if (_usersWithStatus.Count == 0)
                        {
                            await Navigation.PopModalAsync();
                            return;
                        }

                        if (_currentUserIndex >= _usersWithStatus.Count)
                            _currentUserIndex = _usersWithStatus.Count - 1;

                        _currentImageIndex = 0;
                    }
                    else
                    {
                        if (_currentImageIndex >= currentUser.ImagePaths.Count)
                            _currentImageIndex = currentUser.ImagePaths.Count - 1;
                    }

                    _currentProgress = 0;
                    _animationCts?.Cancel();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LoadCurrentUserStatus();
                        UpdateUI(); // Updates mood for the new current image
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnDeleteTapped: {ex}");
                await DisplayAlert("Error", "Failed to delete image", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _animationCts?.Cancel();
        }
    }
}