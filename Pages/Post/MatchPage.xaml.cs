using Lock.Converter.Post;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Lock.Pages.Post
{
    public partial class MatchPage : ContentPage, INotifyPropertyChanged
    {
        // State variables
        private string _currentSubTab = "TopPicks";
        private List<MatchResult> _allResults = new();
        private double _minScore = 0;
        private string _selectedMood = string.Empty;
        private string _selectedLocation = string.Empty;
        private bool _isComplementaryMode = false;
        private readonly Dictionary<string, string> _activeFilters = new();

        public ObservableCollection<MatchResult> Matches { get; } = new();

        // INotifyPropertyChanged implementation
        public new event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Constructor
        public MatchPage()
        {
            InitializeComponent();
            BindingContext = this;

            // Register converters if not already in resources
            if (!Resources.ContainsKey("ScoreToColorConverter"))
                Resources.Add("ScoreToColorConverter", new ScoreToColorConverter());
            if (!Resources.ContainsKey("GreaterThanZeroConverter"))
                Resources.Add("GreaterThanZeroConverter", new GreaterThanZeroConverter());

            if (MatchCollectionView != null)
                MatchCollectionView.ItemsSource = Matches;

            // Ensure correct initial toggle state
            UpdateModeUI();
        }

        // Toggle Tap Handlers
        private void OnSimilarModeTapped(object sender, TappedEventArgs e)
        {
            if (_isComplementaryMode)
            {
                _isComplementaryMode = false;
                UpdateModeUI();
                _ = LoadMatchesAsync();
            }
        }

        private void OnComplementaryModeTapped(object sender, TappedEventArgs e)
        {
            if (!_isComplementaryMode)
            {
                _isComplementaryMode = true;
                UpdateModeUI();
                _ = LoadMatchesAsync();
            }
        }

        // Update toggle visual state
        private void UpdateModeUI()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_isComplementaryMode)
                    {
                        // Opposites Attract = active
                        if (ComplementaryModeBorder != null)
                        {
                            ComplementaryModeBorder.BackgroundColor = Color.FromArgb("#008080");
                            ComplementaryModeBorder.StrokeThickness = 0;
                        }
                        if (ComplementaryModeIcon != null) ComplementaryModeIcon.TextColor = Color.FromArgb("#008080");
                        if (ComplementaryModeLabel != null) ComplementaryModeLabel.TextColor = Color.FromArgb("#008080");

                        if (SimilarModeBorder != null)
                        {
                            SimilarModeBorder.BackgroundColor = Colors.Transparent;
                            SimilarModeBorder.StrokeThickness = 1;
                            SimilarModeBorder.Stroke = Color.FromArgb("#008080");
                        }
                        if (SimilarModeIcon != null) SimilarModeIcon.TextColor = Color.FromArgb("#777777");
                        if (SimilarModeLabel != null) SimilarModeLabel.TextColor = Color.FromArgb("#777777");
                    }
                    else
                    {
                        // Similar Vibes = active
                        if (SimilarModeBorder != null)
                        {
                            SimilarModeBorder.BackgroundColor = Color.FromArgb("#008080");
                            SimilarModeBorder.StrokeThickness = 0;
                        }
                        if (SimilarModeIcon != null) SimilarModeIcon.TextColor = Color.FromArgb("#008080");
                        if (SimilarModeLabel != null) SimilarModeLabel.TextColor = Color.FromArgb("#008080");

                        if (ComplementaryModeBorder != null)
                        {
                            ComplementaryModeBorder.BackgroundColor = Colors.Transparent;
                            ComplementaryModeBorder.StrokeThickness = 1;
                            ComplementaryModeBorder.Stroke = Color.FromArgb("#3C3C3C");
                        }
                        if (ComplementaryModeIcon != null) ComplementaryModeIcon.TextColor = Color.FromArgb("#777777");
                        if (ComplementaryModeLabel != null) ComplementaryModeLabel.TextColor = Color.FromArgb("#777777");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UpdateModeUI error: {ex}");
                }
            });
        }

        // Lifecycle
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            PopulateLocationPicker();
            await LoadMatchesAsync();
        }

        // Populate location picker with saved locations
        private void PopulateLocationPicker()
        {
            var locationPicker = this.FindByName<Picker>("LocationFilterPicker");
            if (locationPicker == null) return;

            var existingLocations = Preferences.Get("global_locations", string.Empty);
            var locations = string.IsNullOrEmpty(existingLocations)
                ? new List<string>()
                : existingLocations.Split('|').ToList();

            locationPicker.Items.Clear();
            locationPicker.Items.Add("Anywhere");
            foreach (var location in locations)
            {
                if (!string.IsNullOrEmpty(location) && !locationPicker.Items.Contains(location))
                    locationPicker.Items.Add(location);
            }
            locationPicker.SelectedIndex = 0;
        }

        // Load matches
        private async Task LoadMatchesAsync()
        {
            try
            {
                ShowSkeleton(true);

                var phone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(phone))
                {
                    ShowEmpty("Please log in to see matches");
                    return;
                }

                var mode = _isComplementaryMode
                    ? Lock.Services.MatchingMode.Complementary
                    : Lock.Services.MatchingMode.Similar;

                _allResults = await MatchService.GetMatchesAsync(phone, _currentSubTab, mode);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ApplyFilters();
                    ShowSkeleton(false);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MatchPage.LoadMatchesAsync error: {ex}");
                ShowSkeleton(false);
                ShowEmpty("Something went wrong. Try refreshing.");
            }
        }

        // Apply filters
        private void ApplyFilters()
        {
            var filtered = _allResults.AsEnumerable();

            if (_minScore > 0)
                filtered = filtered.Where(r => r.TotalScore >= _minScore);

            if (!string.IsNullOrEmpty(_selectedMood))
                filtered = filtered.Where(r =>
                    string.Equals(r.Mood?.Trim(), _selectedMood.Trim(),
                                  StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(_selectedLocation) && _selectedLocation != "Anywhere")
            {
                // Split the selected location into parts (e.g., "Nigeria, Abia" -> ["Nigeria", "Abia"])
                var selectedLocationParts = _selectedLocation.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim().ToLowerInvariant())
                    .ToList();

                filtered = filtered.Where(r =>
                {
                    if (string.IsNullOrEmpty(r.Location)) return false;

                    // Split the result location into parts
                    var resultLocationParts = r.Location.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim().ToLowerInvariant())
                        .ToList();

                    // Check if ANY part of selected location matches ANY part of result location
                    return selectedLocationParts.Any(selectedPart =>
                        resultLocationParts.Any(resultPart =>
                            selectedPart == resultPart));
                });
            }

            Matches.Clear();
            foreach (var m in filtered)
                Matches.Add(m);

            bool hasResults = Matches.Any();
            if (MatchCollectionView != null) MatchCollectionView.IsVisible = hasResults;
            if (EmptyContainer != null) EmptyContainer.IsVisible = !hasResults;
            if (!hasResults && EmptySubLabel != null)
                EmptySubLabel.Text = BuildEmptyMessage();
        }

        private string BuildEmptyMessage()
        {
            var filters = new List<string>();
            if (!string.IsNullOrEmpty(_selectedMood))
                filters.Add($"mood \"{_selectedMood}\"");
            if (!string.IsNullOrEmpty(_selectedLocation) && _selectedLocation != "Anywhere")
                filters.Add($"location \"{_selectedLocation}\"");
            if (_minScore > 0)
                filters.Add($"score above {_minScore:F0}%");

            if (filters.Any())
                return $"No matches with {string.Join(" and ", filters)}";

            return "Complete your profile to get better matches";
        }

        // Sub-tab click
        private async void OnSubTabClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;

            var frames = new[] { TopPicksFrame, MoodMatchFrame, NearMeFrame, VibesFrame };
            var buttons = new[] { TopPicksTab, MoodMatchTab, NearMeTab, VibesTab };

            foreach (var f in frames)
            {
                if (f == null) continue;
                f.BackgroundColor = Color.FromArgb("#1E1E1E");
                f.BorderColor = Color.FromArgb("#2E2E2E");
                f.HasShadow = false;
            }
            foreach (var b in buttons)
            {
                if (b == null) continue;
                b.TextColor = Color.FromArgb("#AAAAAA");
                b.FontAttributes = FontAttributes.None;
            }

            btn.TextColor = Colors.White;
            btn.FontAttributes = FontAttributes.Bold;

            Frame? activeFrame = btn switch
            {
                var b when b == TopPicksTab => TopPicksFrame,
                var b when b == MoodMatchTab => MoodMatchFrame,
                var b when b == NearMeTab => NearMeFrame,
                var b when b == VibesTab => VibesFrame,
                _ => null
            };

            if (activeFrame != null)
            {
                activeFrame.BackgroundColor = Colors.Transparent;
                activeFrame.BorderColor = Color.FromArgb("#008080");
                activeFrame.HasShadow = false;
            }

            _currentSubTab = btn.Text?.Replace(" ", "") ?? "TopPicks";
            await LoadMatchesAsync();
        }

        // Score slider
        private void OnMinScoreChanged(object sender, ValueChangedEventArgs e)
        {
            _minScore = Math.Round(e.NewValue / 5.0) * 5;

            if (_minScore > 0)
            {
                if (MinScoreLabel != null) MinScoreLabel.Text = $"{_minScore:F0}%+ match";
                if (ClearScoreButton != null) ClearScoreButton.IsVisible = true;
                UpsertChip("Score", $"{_minScore:F0}%+");
            }
            else
            {
                if (MinScoreLabel != null) MinScoreLabel.Text = "Any match";
                if (ClearScoreButton != null) ClearScoreButton.IsVisible = false;
                RemoveChip("Score");
            }

            ApplyFilters();
        }

        private void OnClearScoreFilter(object sender, TappedEventArgs e)
        {
            if (MinScoreSlider != null) MinScoreSlider.Value = 0;
            _minScore = 0;
            if (MinScoreLabel != null) MinScoreLabel.Text = "Any match";
            if (ClearScoreButton != null) ClearScoreButton.IsVisible = false;
            RemoveChip("Score");
            ApplyFilters();
        }

        // Mood picker
        private void OnMoodFilterSelected(object sender, EventArgs e)
        {
            if (MoodFilterPicker == null || MoodFilterPicker.SelectedIndex < 0) return;
            var sel = MoodFilterPicker.Items[MoodFilterPicker.SelectedIndex];

            if (sel == "All moods" || string.IsNullOrEmpty(sel))
            {
                _selectedMood = string.Empty;
                RemoveChip("Mood");
            }
            else
            {
                _selectedMood = sel;
                UpsertChip("Mood", sel);
            }

            ApplyFilters();
        }

        // Location picker
        private void OnLocationFilterSelected(object sender, EventArgs e)
        {
            if (LocationFilterPicker == null || LocationFilterPicker.SelectedIndex < 0) return;
            var sel = LocationFilterPicker.Items[LocationFilterPicker.SelectedIndex];

            if (sel == "Anywhere" || string.IsNullOrEmpty(sel))
            {
                _selectedLocation = string.Empty;
                RemoveChip("Location");
            }
            else
            {
                _selectedLocation = sel;
                UpsertChip("Location", sel);
            }

            ApplyFilters();
        }

        // Filter chips
        private void UpsertChip(string filterType, string displayValue)
        {
            _activeFilters[filterType] = displayValue;
            RebuildChips();
        }

        private void RemoveChip(string filterType)
        {
            _activeFilters.Remove(filterType);
            RebuildChips();
        }

        private void RebuildChips()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ActiveFiltersLayout == null || ActiveFiltersScrollView == null) return;

                ActiveFiltersLayout.Children.Clear();
                ActiveFiltersScrollView.IsVisible = _activeFilters.Any();

                foreach (var kv in _activeFilters)
                {
                    var capturedType = kv.Key;

                    var chipBorder = new Border
                    {
                        BackgroundColor = Color.FromArgb("#2A2A2A"),
                        StrokeThickness = 2,
                        Stroke = Color.FromArgb("#008080"),
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Padding = new Thickness(12, 6, 8, 6),
                        VerticalOptions = LayoutOptions.Center,
                        HeightRequest = 32
                    };

                    var grid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        VerticalOptions = LayoutOptions.Center
                    };

                    var label = new Label
                    {
                        Text = kv.Value,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#F0F0F0"),
                        VerticalOptions = LayoutOptions.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    Grid.SetColumn(label, 0);
                    grid.Children.Add(label);

                    var closeFrame = new Frame
                    {
                        Content = new Label
                        {
                            Text = "X",
                            FontSize = 11,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        },
                        BackgroundColor = Color.FromArgb("#FF3B6F"),
                        CornerRadius = 10,
                        HasShadow = false,
                        Padding = 0,
                        WidthRequest = 20,
                        HeightRequest = 20,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        IsClippedToBounds = true
                    };

                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (_, _) => ClearFilterByType(capturedType);
                    closeFrame.GestureRecognizers.Add(tap);

                    Grid.SetColumn(closeFrame, 1);
                    grid.Children.Add(closeFrame);

                    chipBorder.Content = grid;
                    chipBorder.Margin = new Thickness(0, 0, 8, 0);
                    ActiveFiltersLayout.Children.Add(chipBorder);
                }
            });
        }

        private void ClearFilterByType(string filterType)
        {
            switch (filterType)
            {
                case "Score":
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (MinScoreSlider != null) MinScoreSlider.Value = 0;
                        _minScore = 0;
                        if (MinScoreLabel != null) MinScoreLabel.Text = "Any match";
                        if (ClearScoreButton != null) ClearScoreButton.IsVisible = false;
                    });
                    break;

                case "Mood":
                    _selectedMood = string.Empty;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (MoodFilterPicker != null) MoodFilterPicker.SelectedIndex = 0;
                    });
                    break;

                case "Location":
                    _selectedLocation = string.Empty;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (LocationFilterPicker != null) LocationFilterPicker.SelectedIndex = 0;
                    });
                    break;
            }

            RemoveChip(filterType);
            ApplyFilters();
        }

        // Card tap
        private async void OnMatchCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not MatchResult match) return;
            try
            {
                await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                {
                    ["phone"] = match.PhoneNumber,
                    ["viewOnly"] = "true"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MatchPage navigate error: {ex}");
            }
        }

        private async void OnCompleteProfileTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var phone = Preferences.Get("current_user_phone", string.Empty);
                await Shell.Current.GoToAsync("///profile", new Dictionary<string, object>
                {
                    ["phone"] = phone,
                    ["viewOnly"] = "false"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MatchPage complete profile error: {ex}");
            }
        }

        private async void OnRefreshTapped(object sender, TappedEventArgs e)
            => await LoadMatchesAsync();

        // Skeleton / empty helpers
        private void ShowSkeleton(bool show)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (SkeletonView != null) SkeletonView.IsVisible = show;
                if (show)
                {
                    if (MatchCollectionView != null) MatchCollectionView.IsVisible = false;
                    if (EmptyContainer != null) EmptyContainer.IsVisible = false;
                }
            });
        }

        private void ShowEmpty(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (SkeletonView != null) SkeletonView.IsVisible = false;
                if (MatchCollectionView != null) MatchCollectionView.IsVisible = false;
                if (EmptyContainer != null) EmptyContainer.IsVisible = true;
                if (EmptySubLabel != null) EmptySubLabel.Text = message;
            });
        }
    }

    // Converters
    public class ScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double score)
            {
                if (score >= 0.7) return Color.FromArgb("#FF3B6F");
                if (score >= 0.4) return Color.FromArgb("#E8933C");
                if (score >= 0.2) return Color.FromArgb("#4A90D9");
                return Color.FromArgb("#2A2A2A");
            }
            return Color.FromArgb("#2A2A2A");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}