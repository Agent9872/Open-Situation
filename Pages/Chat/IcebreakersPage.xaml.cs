
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace Lock.Pages.Chat
{
    public partial class IcebreakersPage : ContentPage
    {
        private List
<IcebreakerCategoryModel> _allCategories;
        private bool _isToastVisible = false;
        public IcebreakersPage()
        {
            InitializeComponent();
            LoadCategories();
        }
        private void LoadCategories()
        {
            _allCategories = GetIcebreakerCategories();
            RenderCategories(_allCategories);
        }
        private void RenderCategories(List
    <IcebreakerCategoryModel> categories)
        {
            CategoriesLayout.Children.Clear();
            foreach (var category in categories)
            {
                // Create category container
                var categoryLayout = new VerticalStackLayout
                {
                    Spacing = 8,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                // Category Header
                var headerBorder = new Border
                {
                    BackgroundColor = Color.FromArgb("#1F1F1F"),
                    StrokeThickness = 0,
                    Padding = new Thickness(16, 12),
                    Margin = new Thickness(0, 0, 0, 0)
                };
                headerBorder.StrokeShape = new RoundRectangle { CornerRadius = 12 };
                var headerGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                };
                // Category Emoji
                var emojiLabel = new Label
                {
                    Text = category.Icon,
                    FontSize = 24,
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalOptions = LayoutOptions.Center
                };
                Grid.SetColumn(emojiLabel, 0);
                headerGrid.Children.Add(emojiLabel);
                // Category Name and Count
                var nameStack = new VerticalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center
                };
                nameStack.Children.Add(new Label
                {
                    Text = category.Name,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White
                });
                nameStack.Children.Add(new Label
                {
                    Text = $"{category.Icebreakers.Count} icebreakers",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#888888")
                });
                Grid.SetColumn(nameStack, 1);
                headerGrid.Children.Add(nameStack);
                // Expand/Collapse Indicator - Using simple text
                var expandLabel = new Label
                {
                    Text = category.IsExpanded ? "?" : "?",
                    TextColor = Color.FromArgb("#C05050"),
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center
                };
                var expandTap = new TapGestureRecognizer();
                expandTap.Tapped += (s, e) => ToggleCategoryExpanded(category);
                expandLabel.GestureRecognizers.Add(expandTap);
                Grid.SetColumn(expandLabel, 2);
                headerGrid.Children.Add(expandLabel);
                headerBorder.Content = headerGrid;
                categoryLayout.Children.Add(headerBorder);
                // Icebreakers Container (visible when expanded)
                var icebreakersContainer = new StackLayout
                {
                    IsVisible = category.IsExpanded,
                    Spacing = 4,
                    Margin = new Thickness(16, 4, 16, 8)
                };
                foreach (var icebreaker in category.Icebreakers)
                {
                    var icebreakerBorder = new Border
                    {
                        BackgroundColor = Color.FromArgb("#2A2A2A"),
                        Padding = new Thickness(14, 10),
                        Margin = new Thickness(0, 2),
                        StrokeThickness = 0
                    };
                    icebreakerBorder.StrokeShape = new RoundRectangle { CornerRadius = 8 };
                    var icebreakerGrid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto }
                        }
                    };
                    var icebreakerLabel = new Label
                    {
                        Text = icebreaker,
                        TextColor = Color.FromArgb("#F0F0F0"),
                        FontSize = 13,
                        LineBreakMode = LineBreakMode.WordWrap
                    };
                    Grid.SetColumn(icebreakerLabel, 0);
                    icebreakerGrid.Children.Add(icebreakerLabel);
                    // Copy Icon - Using simple text
                    var copyLabel = new Label
                    {
                        Text = "??",
                        TextColor = Color.FromArgb("#C05050"),
                        FontSize = 16,
                        Margin = new Thickness(8, 0, 0, 0),
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Center
                    };
                    var copyTap = new TapGestureRecognizer();
                    copyTap.Tapped += async (s, e) => await CopyIcebreaker(icebreaker);
                    copyLabel.GestureRecognizers.Add(copyTap);
                    Grid.SetColumn(copyLabel, 1);
                    icebreakerGrid.Children.Add(copyLabel);
                    icebreakerBorder.Content = icebreakerGrid;
                    // Also make the whole border tappable
                    var borderTap = new TapGestureRecognizer();
                    borderTap.Tapped += async (s, e) => await CopyIcebreaker(icebreaker);
                    icebreakerBorder.GestureRecognizers.Add(borderTap);
                    icebreakersContainer.Children.Add(icebreakerBorder);
                }
                categoryLayout.Children.Add(icebreakersContainer);
                // Separator (only when collapsed)
                if (!category.IsExpanded)
                {
                    categoryLayout.Children.Add(new BoxView
                    {
                        HeightRequest = 1,
                        Color = Color.FromArgb("#333333"),
                        Margin = new Thickness(16, 4, 16, 0)
                    });
                }
                CategoriesLayout.Children.Add(categoryLayout);
            }
        }
        private void ToggleCategoryExpanded(IcebreakerCategoryModel category)
        {
            category.IsExpanded = !category.IsExpanded;
            // Refresh the entire view
            var searchText = SearchEntry.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                RenderCategories(_allCategories);
            }
            else
            {
                FilterCategories(searchText);
            }
        }
        private async Task CopyIcebreaker(string icebreaker)
        {
            await Clipboard.Default.SetTextAsync(icebreaker);
            await ShowToast("? Copied to clipboard!");
        }
        private List
        <IcebreakerCategoryModel> GetIcebreakerCategories()
        {
            return new List
            <IcebreakerCategoryModel>
                {
                new IcebreakerCategoryModel
                {
                    Name = "Serious relationship",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                <string>
                    {
                        "What does your ideal long-term relationship look like?",
                        "What are the most important values you look for in a partner?",
                        "How do you envision your life in 5 years?",
                        "What's your love language?",
                        "What's the most important lesson from past relationships?",
                        "How do you handle conflict in a relationship?",
                        "What does 'building a life together' mean to you?",
                        "How important is family to you?",
                        "What qualities make a relationship last?",
                        "Where do you see yourself settling down?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Long-term potential",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                    <string>
                        {
                        "What are your long-term goals?",
                        "How do you balance independence with partnership?",
                        "What's your approach to growing together?",
                        "What are you working to improve about yourself?",
                        "How do you handle life transitions with a partner?",
                        "What role does communication play for you?",
                        "How do you keep the spark alive long-term?",
                        "What's your idea of quality time?",
                        "How do you support your partner's dreams?",
                        "What's your view on personal growth in relationships?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Just vibes / casual fun",
                    Icon = "?",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                        <string>
                            {
                        "What's your idea of a perfect casual weekend?",
                        "Any fun plans coming up?",
                        "What's the best spontaneous adventure you've had?",
                        "What would be a fun activity to do together?",
                        "What's a hidden talent you have?",
                        "What's your go-to karaoke song?",
                        "What made you laugh recently?",
                        "What's the best concert you've been to?",
                        "If you could teleport anywhere, where?",
                        "What's a fun fact about you?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Something casual",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                            <string>
                                {
                        "How do you define 'casual dating'?",
                        "What's your ideal balance of space vs time together?",
                        "What does a perfect low-key date look like?",
                        "How do you like to spend your free time?",
                        "What are you currently binging?",
                        "Coffee or drinks for a first meet-up?",
                        "What's your favorite local spot?",
                        "Spontaneous plans or scheduled ones?",
                        "What's a new hobby you've tried?",
                        "What's your ideal Sunday?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Hook-up / FWB",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                <string>
                                    {
                        "What's your vibe with casual connections?",
                        "How do you like to establish boundaries?",
                        "What immediately catches your attention?",
                        "How do you keep things fun and drama-free?",
                        "What's your idea of good chemistry?",
                        "How do you communicate expectations?",
                        "Keeping it casual vs catching feelings?",
                        "How do you check in about boundaries?",
                        "Ideal frequency of hanging out?",
                        "How do you keep things respectful?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "ENM / Open to non-monogamy",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                    <string>
                                        {
                        "How do you practice ethical non-monogamy?",
                        "What's your experience with ENM?",
                        "How do you communicate about other partners?",
                        "What does compersion mean to you?",
                        "How do you navigate boundaries in ENM?",
                        "What's your preferred dynamic?",
                        "How do you handle jealousy?",
                        "What's your approach to disclosure?",
                        "How do you balance multiple connections?",
                        "What ENM resources have influenced you?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Deep talks and connection",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                        <string>
                                            {
                        "What have you been thinking about lately?",
                        "What's a belief you hold that most disagree with?",
                        "What experience changed you as a person?",
                        "What's the most vulnerable you've been?",
                        "What does emotional intimacy mean to you?",
                        "What fear are you working through?",
                        "What have you never told anyone on a first date?",
                        "What does 'soul connection' mean to you?",
                        "What's a moment that felt truly magical?",
                        "What's your philosophy on love?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Let's see where it goes",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                            <string>
                                                {
                        "Go with the flow or intentional dating?",
                        "What attracted you to my profile?",
                        "What's a green flag that things are heading right?",
                        "How do you navigate early dating?",
                        "What's your ideal first date?",
                        "How do you know when there's real potential?",
                        "What are you open to exploring?",
                        "Optimism vs realism when dating?",
                        "What's the best date you've been on?",
                        "How do you progress from chatting to meeting?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Networking / collabs / friends first",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                                <string>
                                                    {
                        "What creative/professional projects are you into?",
                        "Always looking to expand my network - what do you do?",
                        "Would love to collaborate! What skills do you bring?",
                        "What project are you passionate about right now?",
                        "How do you build meaningful connections in your field?",
                        "What are you learning or developing skills in?",
                        "Virtual coffee or in-person meetups?",
                        "What's the best career advice you've received?",
                        "How do you balance friendship with professional relationships?",
                        "What goal are you working toward professionally?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "OS (open situationship)",
                    Icon = "??",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                                    <string>
                                                        {
                        "How do you define an open situationship?",
                        "What's your ideal dynamic when things are undefined?",
                        "How do you communicate about evolving feelings?",
                        "What boundaries help you feel comfortable?",
                        "How do you navigate when one person catches feelings?",
                        "How do you check in about where things stand?",
                        "How do you keep things transparent?",
                        "What's your approach to seeing other people?",
                        "When should a situationship become something more?",
                        "What's the best part about keeping things undefined?"
                    }
                },
                new IcebreakerCategoryModel
                {
                    Name = "Chalance (all-in effort)",
                    Icon = "?",
                    IsExpanded = false,
                    Icebreakers = new ObservableCollection
                                                        <string>
                                                            {
                        "What does 'all-in effort' mean to you?",
                        "How do you show someone they're a priority?",
                        "What's going above and beyond for someone?",
                        "How do you like to be pursued?",
                        "What's the most romantic gesture you've made?",
                        "How do you maintain 'all-in' energy over time?",
                        "What makes you want to give full effort?",
                        "How do you handle unreciprocated effort?",
                        "What's your love language when you're really into someone?",
                        "How do you balance all-in without moving too fast?"
                    }
                }
            };
        }
        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            await FilterCategories(e.NewTextValue);
        }
        private async Task FilterCategories(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                RenderCategories(_allCategories);
                return;
            }
            searchText = searchText.ToLower();
            var filteredCategories = _allCategories
                .Select(c => new IcebreakerCategoryModel
                {
                    Name = c.Name,
                    Icon = c.Icon,
                    IsExpanded = true,
                    Icebreakers = new ObservableCollection
                                                            <string>(
                        c.Icebreakers.Where(i => i.ToLower().Contains(searchText)).ToList()
                    )
                })
                .Where(c => c.Icebreakers.Any())
                .ToList();
            RenderCategories(filteredCategories);
        }
        private async void OnRandomIcebreakerClicked(object sender, EventArgs e)
        {
            var allIcebreakers = _allCategories.SelectMany(c => c.Icebreakers).ToList();
            if (allIcebreakers.Any())
            {
                var random = new Random();
                var randomIcebreaker = allIcebreakers[random.Next(allIcebreakers.Count)];
                await Clipboard.Default.SetTextAsync(randomIcebreaker);
                await ShowToast($"? Random icebreaker copied!");
            }
        }
        private async void OnFavoritesClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Favorites", "Favorites feature coming soon!", "OK");
        }
        private async Task ShowToast(string message)
        {
            if (_isToastVisible) return;
            _isToastVisible = true;
            ToastLabel.Text = message;
            ToastMessage.IsVisible = true;
            await ToastMessage.FadeTo(1, 200);
            await Task.Delay(1500);
            await ToastMessage.FadeTo(0, 200);
            ToastMessage.IsVisible = false;
            _isToastVisible = false;
        }
        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
        protected override bool OnBackButtonPressed()
        {
            MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopModalAsync());
            return true;
        }
    }
    public class IcebreakerCategoryModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        public string Name { get; set; }
        public string Icon { get; set; }
        public ObservableCollection
                                                                <string> Icebreakers
        { get; set; }
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}