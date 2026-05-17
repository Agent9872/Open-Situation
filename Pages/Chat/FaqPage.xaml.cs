using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Lock.Pages.Chat
{
    public partial class FaqPage : ContentPage
    {
        private string _currentSection = "General";

        public FaqPage()
        {
            InitializeComponent();

            // Remove the default navigation bar completely
            NavigationPage.SetHasNavigationBar(this, false);
            NavigationPage.SetHasBackButton(this, false);

            // Hide the Shell navigation bar if using Shell
            Shell.SetNavBarIsVisible(this, false);

            // Clear the default title
            this.Title = null;

            // Set background color to match the dark theme
            this.BackgroundColor = Color.FromArgb("#0E0E12");

            SearchEntry.TextChanged += OnSearchTextChanged;
            UpdateTabStyles();
            ShowSection("General");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Ensure navigation bar is hidden when page appears
            Shell.SetNavBarIsVisible(this, false);
            NavigationPage.SetHasNavigationBar(this, false);
        }

        private void OnGeneralTabTapped(object sender, EventArgs e)
        {
            _currentSection = "General";
            UpdateTabStyles();
            ShowSection("General");
        }

        private void OnChatsTabTapped(object sender, EventArgs e)
        {
            _currentSection = "Chats";
            UpdateTabStyles();
            ShowSection("Chats");
        }

        private void OnGroupsTabTapped(object sender, EventArgs e)
        {
            _currentSection = "Groups";
            UpdateTabStyles();
            ShowSection("Groups");
        }

        private void OnLiveTabTapped(object sender, EventArgs e)
        {
            _currentSection = "Live";
            UpdateTabStyles();
            ShowSection("Live");
        }

        private void UpdateTabStyles()
        {
            // Reset all tabs to inactive state
            var tabs = new[] { GeneralTab, ChatsTab, GroupsTab, LiveTab };
            foreach (var tab in tabs)
            {
                if (tab != null)
                {
                    tab.Background = null; // Clear any gradient
                    tab.BackgroundColor = Color.FromArgb("#16161C");
                    tab.Stroke = Color.FromArgb("#2A2A38");
                    tab.StrokeThickness = 1;
                    if (tab.Content is Label label)
                    {
                        label.TextColor = Color.FromArgb("#5A5A6A");
                        label.FontAttributes = FontAttributes.None;
                    }
                }
            }

            // Highlight active tab
            Border activeTab = _currentSection switch
            {
                "General" => GeneralTab,
                "Chats" => ChatsTab,
                "Groups" => GroupsTab,
                "Live" => LiveTab,
                _ => GeneralTab
            };

            if (activeTab != null)
            {
                // Apply gradient background for active tab
                activeTab.Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb("#FF3B6F"), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb("#CC2F59"), Offset = 1 }
                    }
                };
                activeTab.BackgroundColor = null;
                activeTab.StrokeThickness = 0;
                if (activeTab.Content is Label activeLabel)
                {
                    activeLabel.TextColor = Colors.White;
                    activeLabel.FontAttributes = FontAttributes.Bold;
                }
            }
        }

        private void ShowSection(string section)
        {
            GeneralSection.IsVisible = section == "General";
            ChatsSection.IsVisible = section == "Chats";
            GroupsSection.IsVisible = section == "Groups";
            LiveSection.IsVisible = section == "Live";
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string query = e.NewTextValue?.Trim()?.ToLower() ?? string.Empty;

                if (string.IsNullOrEmpty(query))
                {
                    // Show all sections normally
                    GeneralSection.IsVisible = _currentSection == "General";
                    ChatsSection.IsVisible = _currentSection == "Chats";
                    GroupsSection.IsVisible = _currentSection == "Groups";
                    LiveSection.IsVisible = _currentSection == "Live";
                    return;
                }

                // Hide all sections first
                GeneralSection.IsVisible = false;
                ChatsSection.IsVisible = false;
                GroupsSection.IsVisible = false;
                LiveSection.IsVisible = false;

                // Search through all FAQ items
                bool found = false;

                // Search General section
                if (SearchInSection(GeneralSection, query))
                {
                    GeneralSection.IsVisible = true;
                    found = true;
                }

                // Search Chats section
                if (SearchInSection(ChatsSection, query))
                {
                    ChatsSection.IsVisible = true;
                    found = true;
                }

                // Search Groups section
                if (SearchInSection(GroupsSection, query))
                {
                    GroupsSection.IsVisible = true;
                    found = true;
                }

                // Search Live section
                if (SearchInSection(LiveSection, query))
                {
                    LiveSection.IsVisible = true;
                    found = true;
                }

                if (!found)
                {
                    // Show a "no results" message
                    await DisplayAlert("No Results", $"No FAQs found matching '{query}'", "OK");
                    // Restore current section
                    ShowSection(_currentSection);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Search error: {ex}");
            }
        }

        private bool SearchInSection(VerticalStackLayout section, string query)
        {
            if (section == null || !section.Children.Any()) return false;

            bool found = false;

            foreach (var child in section.Children)
            {
                if (child is VerticalStackLayout faqItem)
                {
                    bool itemMatches = false;

                    foreach (var innerChild in faqItem.Children)
                    {
                        if (innerChild is Label label)
                        {
                            string text = label.Text?.ToLower() ?? string.Empty;
                            if (text.Contains(query))
                            {
                                itemMatches = true;
                                break;
                            }
                        }
                    }

                    faqItem.IsVisible = itemMatches;
                    if (itemMatches) found = true;
                }
            }

            return found;
        }

        private async void OnCloseTapped(object sender, EventArgs e)
        {
            await ClosePage();
        }

        private async Task ClosePage()
        {
            try
            {
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error closing FAQ page: {ex.Message}");
                await Navigation.PopAsync();
            }
        }

        protected override bool OnBackButtonPressed()
        {
            MainThread.BeginInvokeOnMainThread(async () => await ClosePage());
            return true;
        }
    }
}