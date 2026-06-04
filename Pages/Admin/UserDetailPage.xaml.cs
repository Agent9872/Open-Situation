using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Lock.Services.Admin;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace Lock.Pages.Admin
{
    [QueryProperty(nameof(UserPhone), "phone")]
    public partial class UserDetailPage : ContentPage
    {
        private string _userPhone = string.Empty;
        private User _user;
        private List<Lock.Models.Post> _userPosts;
        private List<UserMoodTracking> _moodHistory;
        private List<UserProfileTracking> _profileChanges;
        private List<UserLoginTracking> _loginHistory;

        public string UserPhone
        {
            get => _userPhone;
            set
            {
                _userPhone = value;
                if (!string.IsNullOrEmpty(_userPhone))
                    LoadUserData();
            }
        }

        public UserDetailPage()
        {
            InitializeComponent();
            Shell.SetNavBarIsVisible(this, false);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetNavBarIsVisible(this, false);
        }

        private async void LoadUserData()
        {
            try
            {
                await ShowLoading(true);

                // Replace this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // _user = await db.Table<User>().Where(u => u.PhoneNumber == _userPhone).FirstOrDefaultAsync();

                // With this Supabase code:
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(_userPhone)}&limit=1");
                _user = users.FirstOrDefault();

                if (_user == null)
                {
                    await DisplayAlert("Error", "User not found", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                var allPosts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                _userPosts = allPosts.Where(p => p.AuthorPhone == _userPhone && string.IsNullOrEmpty(p.StatusImagePath)).ToList();

                await LoadTrackingData();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    BuildPage();
                });

                await ShowLoading(false);
            }
            catch (Exception ex)
            {
                await ShowLoading(false);
                Debug.WriteLine($"LoadUserData error: {ex}");
                await DisplayAlert("Error", $"Failed to load: {ex.Message}", "OK");
            }
        }

        private async Task LoadTrackingData()
        {
            try
            {
                _moodHistory = await UserTrackingService.Instance.GetMoodHistoryAsync(_userPhone, 30);
                _profileChanges = await UserTrackingService.Instance.GetProfileChangeHistoryAsync(_userPhone, 50);
                _loginHistory = await UserTrackingService.Instance.GetUserLoginHistoryAsync(_userPhone, 20);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadTrackingData error: {ex}");
                _moodHistory = new List<UserMoodTracking>();
                _profileChanges = new List<UserProfileTracking>();
                _loginHistory = new List<UserLoginTracking>();
            }
        }

        // ??????????????????????????????????????????
        // MAIN BUILD
        // ??????????????????????????????????????????
        private void BuildPage()
        {
            PageContent.Children.Clear();

            BuildHeroCard();
            BuildStatPills();
            BuildAdminStatusBar();
            BuildSection("BASIC INFORMATION", BuildBasicInfo());
            BuildSection("LOCATION & BIO", BuildLocationBio());
            BuildSection("PHYSICAL ATTRIBUTES", BuildPhysicalAttributes());
            BuildSection("LIFESTYLE", BuildLifestyle());
            BuildSection("FAMILY & KIDS", BuildFamilyKids());
            BuildSection("INTERESTS & HOBBIES", BuildInterests());
            BuildSection("RECENT POSTS", BuildRecentPosts());
            BuildSection("MOOD HISTORY", BuildMoodHistory());
            BuildSection("PROFILE CHANGE HISTORY", BuildProfileChanges());
            BuildSection("LOGIN HISTORY", BuildLoginHistory());
            BuildSection("ADMIN ACTIONS", BuildAdminActions());
        }

        // ?? HERO CARD ??
        private void BuildHeroCard()
        {
            var age = _user.GetAge();
            var isOnline = _user.LastActive > DateTime.UtcNow.AddMinutes(-15);
            var lastSeenText = isOnline ? "Online now" : $"Last seen {GetRelativeTime(_user.LastActive)}";

            // ?? Avatar ??
            var avatarFrame = new Frame
            {
                WidthRequest = 80,
                HeightRequest = 80,
                CornerRadius = 40,
                Padding = 0,
                IsClippedToBounds = true,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#12121A"),
                BorderColor = Color.FromArgb("#00B5B5")
            };

            var validPath = GetValidProfileImagePath(_user.ProfileImagePath);
            if (!string.IsNullOrEmpty(validPath))
            {
                avatarFrame.Content = new Image
                {
                    Source = ImageSource.FromFile(validPath),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = 80,
                    HeightRequest = 80
                };
            }
            else
            {
                avatarFrame.Content = new Label
                {
                    Text = _user.Name?.Length > 0 ? _user.Name[0].ToString().ToUpper() : "?",
                    FontSize = 32,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00B5B5"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
            }

            // ?? Online dot overlay ??
            var avatarGrid = new Grid { WidthRequest = 80, HeightRequest = 80 };
            avatarGrid.Children.Add(avatarFrame);
            avatarGrid.Children.Add(new Ellipse
            {
                WidthRequest = 16,
                HeightRequest = 16,
                Fill = new SolidColorBrush(Color.FromArgb(isOnline ? "#22C55E" : "#555555")),
                Stroke = new SolidColorBrush(Color.FromArgb("#0D0D14")),
                StrokeThickness = 2.5f,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End
            });

            // ?? Right side info ??
            var infoStack = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };

            // Name + verified badge row
            var nameRow = new HorizontalStackLayout { Spacing = 8 };
            nameRow.Children.Add(new Label
            {
                Text = _user.Name ?? "Unknown",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F0EDE8"),
                VerticalOptions = LayoutOptions.Center
            });
            if (_user.IsVerified)
            {
                nameRow.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#1A1A0D"),
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Color.FromArgb("#F5C518")),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(6, 3),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = "? Verified",
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#F5C518")
                    }
                });
            }
            infoStack.Children.Add(nameRow);

            // Phone
            infoStack.Children.Add(new Label
            {
                Text = _user.PhoneNumber,
                FontSize = 13,
                TextColor = Color.FromArgb("#7A7A8C")
            });

            // Online status row
            var onlineRow = new HorizontalStackLayout { Spacing = 5 };
            onlineRow.Children.Add(new Ellipse
            {
                WidthRequest = 7,
                HeightRequest = 7,
                Fill = new SolidColorBrush(Color.FromArgb(isOnline ? "#22C55E" : "#555555")),
                VerticalOptions = LayoutOptions.Center
            });
            onlineRow.Children.Add(new Label
            {
                Text = lastSeenText,
                FontSize = 11,
                TextColor = Color.FromArgb(isOnline ? "#22C55E" : "#555555"),
                VerticalOptions = LayoutOptions.Center
            });
            infoStack.Children.Add(onlineRow);

            // Age / gender / join date
            infoStack.Children.Add(new Label
            {
                Text = $"Joined {_user.JoinDate:MMM dd, yyyy}  •  Age {age}  •  {_user.Gender ?? "—"}",
                FontSize = 11,
                TextColor = Color.FromArgb("#5A5A6A")
            });

            // Location
            if (!string.IsNullOrEmpty(_user.Country))
            {
                infoStack.Children.Add(new Label
                {
                    Text = $"?? {_user.Country}{(string.IsNullOrEmpty(_user.State) ? "" : ", " + _user.State)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#5A5A6A")
                });
            }

            // ?? Hero layout ??
            var heroRow = new Grid
            {
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Star }
        },
                ColumnSpacing = 16
            };
            heroRow.Add(avatarGrid, 0, 0);
            heroRow.Add(infoStack, 1, 0);

            // ?? Profile photo strip (main photo only — User model has one ProfileImagePath) ??
            var cardStack = new VerticalStackLayout { Spacing = 12 };
            cardStack.Children.Add(heroRow);

            // If we have a valid profile image, show a larger preview strip
            if (!string.IsNullOrEmpty(validPath))
            {
                cardStack.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#1C1C25")
                });

                var photoSection = new VerticalStackLayout { Spacing = 8 };
                photoSection.Children.Add(new Label
                {
                    Text = "PROFILE PHOTO",
                    FontSize = 9,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    CharacterSpacing = 1.5
                });

                // Larger photo preview
                var photoFrame = new Frame
                {
                    WidthRequest = 80,
                    HeightRequest = 80,
                    CornerRadius = 12,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#16161C"),
                    BorderColor = Color.FromArgb("#2A2A38"),
                    Content = new Image
                    {
                        Source = ImageSource.FromFile(validPath),
                        Aspect = Aspect.AspectFill,
                        WidthRequest = 80,
                        HeightRequest = 80
                    }
                };

                var photoRow = new HorizontalStackLayout { Spacing = 8 };
                photoRow.Children.Add(photoFrame);
                photoSection.Children.Add(photoRow);
                cardStack.Children.Add(photoSection);
            }

            // ?? Wrap in card ??
            var heroBorder = new Border
            {
                Margin = new Thickness(16, 20, 16, 0),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1A3A3A")),
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(20, 18),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = Color.FromArgb("#0D1F1F"), Offset = 0 },
                new GradientStop { Color = Color.FromArgb("#0D0D14"), Offset = 1 }
            }
                },
                Content = cardStack
            };

            PageContent.Children.Add(heroBorder);
        }

        private Frame BuildPhotoThumb(string path, int size)
        {
            return new Frame
            {
                WidthRequest = size,
                HeightRequest = size,
                CornerRadius = 10,
                Padding = 0,
                IsClippedToBounds = true,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#16161C"),
                BorderColor = Color.FromArgb("#2A2A38"),
                Content = new Image
                {
                    Source = ImageSource.FromFile(path),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = size,
                    HeightRequest = size
                }
            };
        }

        // ?? STAT PILLS ??
        private void BuildStatPills()
        {
            var totalLoves = _userPosts.Sum(p => p.LoveCount);
            var totalSparks = _userPosts.Sum(p => p.SparkCount);
            var totalLogins = _loginHistory.Count;

            var grid = new Grid
            {
                Margin = new Thickness(16, 12, 16, 0),
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Star }
        },
                ColumnSpacing = 8
            };

            grid.Add(BuildPill(
                "M160-200v-80h80v-560h480v560h80v80H160Zm260-80h80v-80h-80v80Zm0-160h80v-80h-80v80Zm0-160h80v-80h-80v80Z",
                _userPosts.Count.ToString(), "Posts", "#22C55E", "#0D1A0D", "#1A3A1A"), 0, 0);

            grid.Add(BuildPill(
                "M480-473q-25-25-41-46.5t-25-42T401-607q-2-17-2-33 0-66 47-113t113-47q66 0 113 47t47 113q0 57-26 97.5T624-473L480-329 336-473q-25-25-41-46.5t-25-42T257-607q-2-17-2-33 0-66 47-113t113-47q66 0 113 47t47 113q0 57-26 97.5T624-473Z",
                totalLoves.ToString(), "Loves", "#FF3B6F", "#1A0D12", "#3A1A22"), 1, 0);

            grid.Add(BuildPill(
                "M420-80q-17 0-28.5-11.5T380-120v-240L214-568q-15-20-4.5-43T240-634h220l-60-286q-4-20 8.5-35t32.5-15q11 0 20.5 5.5T477-950l243 370q9 14 9 30t-9 30L480-150q-8 15-19.5 22.5T420-120z",
                totalSparks.ToString(), "Sparks", "#FB923C", "#1A150D", "#3A2A1A"), 2, 0);

            grid.Add(BuildPill(
                "M480-120v-80h280v-560H480v-80h280q33 0 56.5 23.5T840-760v560q0 33-23.5 56.5T760-120H480Zm-80-160-55-58 102-102H120v-80h327L345-622l55-58 200 200-200 200Z",
                totalLogins.ToString(), "Logins", "#00B5B5", "#0D1F1F", "#1A3A3A"), 3, 0);

            PageContent.Children.Add(grid);
        }

        private Border BuildPill(string pathData, string value, string label,
            string accent, string bg, string border)
        {
            return new Border
            {
                BackgroundColor = Color.FromArgb(bg),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(border)),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(8, 12),
                Content = new VerticalStackLayout
                {
                    Spacing = 3,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
            {
                new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)
                        new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                        .ConvertFromInvariantString(pathData),
                    Fill = new SolidColorBrush(Color.FromArgb(accent)),
                    HeightRequest = 18,
                    WidthRequest = 18,
                    Aspect = Stretch.Uniform,
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = value,
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = label,
                    FontSize = 9,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    HorizontalOptions = LayoutOptions.Center
                }
            }
                }
            };
        }
        private Border BuildEmojiPill(string icon, string value, string label,
            string accent, string bg, string border)
        {
            return new Border
            {
                BackgroundColor = Color.FromArgb(bg),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(border)),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(8, 12),
                Content = new VerticalStackLayout
                {
                    Spacing = 3,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
            {
                new Label { Text = icon, FontSize = 16, HorizontalOptions = LayoutOptions.Center },
                new Label
                {
                    Text = value,
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    HorizontalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = label,
                    FontSize = 9,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    HorizontalOptions = LayoutOptions.Center
                }
            }
                }
            };
        }
        // ?? ADMIN STATUS BAR ??
        private void BuildAdminStatusBar()
        {
            var totalDaysSinceJoin = (DateTime.UtcNow - _user.JoinDate).Days;
            var avgPostsPerDay = totalDaysSinceJoin > 0 ? (double)_userPosts.Count / totalDaysSinceJoin : 0;
            var engagementScore = (_userPosts.Sum(p => p.LoveCount) + _userPosts.Sum(p => p.SparkCount));
            var verStatus = _user.IsVerified ? "Verified" :
                            _user.VerificationStatus == "pending" ? "Pending" :
                            _user.VerificationStatus == "rejected" ? "Rejected" : "Not Verified";
            var verColor = _user.IsVerified ? "#4CAF50" :
                           _user.VerificationStatus == "pending" ? "#FF9800" :
                           _user.VerificationStatus == "rejected" ? "#F44336" : "#666666";

            var grid = new Grid
            {
                Margin = new Thickness(16, 10, 16, 0),
                BackgroundColor = Color.FromArgb("#12121A"),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(1) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(1) },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            grid.Add(BuildAdminStat("Verification", verStatus, verColor), 0, 0);
            grid.Add(new BoxView { BackgroundColor = Color.FromArgb("#1C1C25"), WidthRequest = 1 }, 1, 0);
            grid.Add(BuildAdminStat("Avg Posts/Day", $"{avgPostsPerDay:F1}", "#00B5B5"), 2, 0);
            grid.Add(new BoxView { BackgroundColor = Color.FromArgb("#1C1C25"), WidthRequest = 1 }, 3, 0);
            grid.Add(BuildAdminStat("Engagement", engagementScore.ToString(), "#C084FC"), 4, 0);

            PageContent.Children.Add(new Border
            {
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1C1C25")),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Margin = new Thickness(16, 10, 16, 0),
                Content = grid
            });

            // Days member bar
            var memberBar = new Border
            {
                BackgroundColor = Color.FromArgb("#12121A"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1C1C25")),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Margin = new Thickness(16, 8, 16, 0),
                Padding = new Thickness(16, 12)
            };

            var memberStack = new VerticalStackLayout { Spacing = 6 };
            memberStack.Children.Add(new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new Label
                    {
                        Text = "Member Duration",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#7A7A8C"),
                        VerticalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = $"{totalDaysSinceJoin} days",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#00B5B5"),
                        HorizontalOptions = LayoutOptions.End
                    }
                }
            });

            // Progress bar (capped at 1 year = 365 days)
            var progress = Math.Min((float)totalDaysSinceJoin / 365f, 1f);
            var barBg = new Border
            {
                BackgroundColor = Color.FromArgb("#1C1C25"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                HeightRequest = 6
            };
            var barFill = new Border
            {
                BackgroundColor = Color.FromArgb("#00B5B5"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                HeightRequest = 6,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = progress  // Will be set after layout
            };

            // Simple progress using Grid
            var progressGrid = new Grid { HeightRequest = 6 };
            progressGrid.Children.Add(new BoxView
            {
                BackgroundColor = Color.FromArgb("#1C1C25"),
                CornerRadius = 3,
                HeightRequest = 6,
                HorizontalOptions = LayoutOptions.Fill
            });
            progressGrid.Children.Add(new BoxView
            {
                BackgroundColor = Color.FromArgb("#00B5B5"),
                CornerRadius = 3,
                HeightRequest = 6,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = progress * 300  // approximate
            });

            memberStack.Children.Add(progressGrid);
            memberStack.Children.Add(new Label
            {
                Text = $"Joined {_user.JoinDate:MMMM dd, yyyy}  •  Last active {GetRelativeTime(_user.LastActive)}",
                FontSize = 10,
                TextColor = Color.FromArgb("#3A3A4A")
            });

            memberBar.Content = memberStack;
            PageContent.Children.Add(memberBar);
        }

        private VerticalStackLayout BuildAdminStat(string label, string value, string valueColor)
        {
            return new VerticalStackLayout
            {
                Spacing = 3,
                HorizontalOptions = LayoutOptions.Center,
                Padding = new Thickness(8, 12),
                Children =
                {
                    new Label
                    {
                        Text = value,
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb(valueColor),
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = label,
                        FontSize = 9,
                        TextColor = Color.FromArgb("#5A5A6A"),
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            };
        }

        // ?? SECTION WRAPPER ??
        private void BuildSection(string title, View content)
        {
            PageContent.Children.Add(new Label
            {
                Text = title,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5A5A6A"),
                CharacterSpacing = 2,
                Margin = new Thickness(16, 24, 16, 10)
            });

            PageContent.Children.Add(new Border
            {
                Margin = new Thickness(16, 0),
                BackgroundColor = Color.FromArgb("#16161C"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#2A2A38")),
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(0),
                Content = content
            });
        }

        // ?? ROW BUILDER ??
        private View BuildInfoRow(string label, string value, string valueColor = "#F0EDE8", bool isLast = false)
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            var grid = new Grid
            {
                Padding = new Thickness(16, 13),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };
            grid.Add(new Label
            {
                Text = label,
                FontSize = 12,
                TextColor = Color.FromArgb("#7A7A8C"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            grid.Add(new Label
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(valueColor),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            }, 1, 0);
            stack.Children.Add(grid);
            if (!isLast)
                stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(16, 0) });
            return stack;
        }

        private View BuildMultiLineRow(string label, string value, bool isLast = false)
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            var inner = new VerticalStackLayout { Padding = new Thickness(16, 13), Spacing = 5 };
            inner.Children.Add(new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#7A7A8C") });
            inner.Children.Add(new Label
            {
                Text = string.IsNullOrEmpty(value) ? "—" : value,
                FontSize = 13,
                TextColor = Color.FromArgb("#F0EDE8"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            stack.Children.Add(inner);
            if (!isLast)
                stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25"), Margin = new Thickness(16, 0) });
            return stack;
        }

        // ?? SECTION CONTENTS ??
        private View BuildBasicInfo()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(BuildInfoRow("Full Name", _user.Name));
            stack.Children.Add(BuildInfoRow("Phone", _user.PhoneNumber));
            stack.Children.Add(BuildInfoRow("Age", $"{_user.GetAge()} years old"));
            stack.Children.Add(BuildInfoRow("Gender", _user.Gender));
            stack.Children.Add(BuildInfoRow("Looking For", _user.Mood, "#FF3B6F"));
            stack.Children.Add(BuildInfoRow("Date of Birth", _user.DateOfBirth.ToString("MMMM dd, yyyy")));
            stack.Children.Add(BuildInfoRow("Join Date", _user.JoinDate.ToString("MMM dd, yyyy hh:mm tt")));
            stack.Children.Add(BuildInfoRow("Last Active", GetRelativeTime(_user.LastActive)));
            stack.Children.Add(BuildInfoRow("Verification", _user.IsVerified ? "Verified" : (_user.VerificationStatus ?? "Not Verified"),
                _user.IsVerified ? "#4CAF50" : "#FF9800", isLast: true));
            return stack;
        }

        private View BuildLocationBio()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(BuildInfoRow("Country", _user.Country));
            stack.Children.Add(BuildInfoRow("State", _user.State));
            stack.Children.Add(BuildMultiLineRow("Bio", _user.Bio, isLast: true));
            return stack;
        }

        private View BuildPhysicalAttributes()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            string heightText = "—";
            if (_user.HeightCm.HasValue && _user.HeightCm.Value > 0)
            {
                int feet = (int)(_user.HeightCm.Value / 30.48);
                int inches = (int)((_user.HeightCm.Value % 30.48) / 2.54);
                heightText = $"{feet}'{inches}\" ({_user.HeightCm.Value}cm)";
            }
            stack.Children.Add(BuildInfoRow("Height", heightText));
            stack.Children.Add(BuildInfoRow("Body Type", _user.BodyType));
            stack.Children.Add(BuildInfoRow("Ethnicity", _user.Ethnicity));
            stack.Children.Add(BuildInfoRow("Tribe", _user.Tribe));
            stack.Children.Add(BuildInfoRow("Personality", _user.PersonalityType, isLast: true));
            return stack;
        }

        private View BuildLifestyle()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(BuildInfoRow("Drinks", _user.Drinks));
            stack.Children.Add(BuildInfoRow("Smokes", _user.Smokes ? "Yes" : "No"));
            stack.Children.Add(BuildInfoRow("Has Pets", _user.HasPets ? "Yes" : "No"));
            stack.Children.Add(BuildInfoRow("Religion", _user.Religion));
            stack.Children.Add(BuildInfoRow("Politics", _user.PoliticalViews, isLast: true));
            return stack;
        }

        private View BuildFamilyKids()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(BuildInfoRow("Kids Preference", _user.KidsPreference));
            stack.Children.Add(BuildInfoRow("Has Children", _user.HasChildren, isLast: true));
            return stack;
        }

        private View BuildInterests()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(BuildMultiLineRow("Interests", _user.Interests));
            stack.Children.Add(BuildInfoRow("Music", _user.MusicGenres));
            stack.Children.Add(BuildInfoRow("Movies", _user.FavoriteMovies));
            stack.Children.Add(BuildInfoRow("Books", _user.FavoriteBooks));
            stack.Children.Add(BuildInfoRow("Occupation", _user.Occupation));
            stack.Children.Add(BuildInfoRow("Education", _user.Education, isLast: true));
            return stack;
        }

        // ?? RECENT POSTS ??
        private View BuildRecentPosts()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };

            if (!_userPosts.Any())
            {
                stack.Children.Add(new Label
                {
                    Text = "No posts yet.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    Padding = new Thickness(16, 14)
                });
                return stack;
            }

            foreach (var post in _userPosts.OrderByDescending(p => p.CreatedAt).Take(5))
            {
                var postRow = new VerticalStackLayout
                {
                    Padding = new Thickness(16, 12),
                    Spacing = 8
                };

                postRow.Children.Add(new Label
                {
                    Text = post.Content?.Length > 120
                        ? post.Content.Substring(0, 120) + "…"
                        : post.Content ?? "",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    LineBreakMode = LineBreakMode.WordWrap
                });

                // Stats row using Path icons
                var statsRow = new HorizontalStackLayout { Spacing = 16 };

                // Loves stat
                var loveRow = new HorizontalStackLayout { Spacing = 5 };
                loveRow.Children.Add(new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)
                        new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                        .ConvertFromInvariantString("M480-473q-25-25-41-46.5t-25-42T401-607q-2-17-2-33 0-66 47-113t113-47q66 0 113 47t47 113q0 57-26 97.5T624-473L480-329 336-473q-25-25-41-46.5t-25-42T257-607q-2-17-2-33 0-66 47-113t113-47q66 0 113 47t47 113q0 57-26 97.5T624-473Z"),
                    Fill = new SolidColorBrush(Color.FromArgb("#FF3B6F")),
                    HeightRequest = 14,
                    WidthRequest = 14,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                });
                loveRow.Children.Add(new Label
                {
                    Text = post.LoveCount.ToString(),
                    FontSize = 11,
                    TextColor = Color.FromArgb("#FF3B6F"),
                    VerticalOptions = LayoutOptions.Center
                });
                statsRow.Children.Add(loveRow);

                // Sparks stat
                var sparkRow = new HorizontalStackLayout { Spacing = 5 };
                sparkRow.Children.Add(new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)
                        new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                        .ConvertFromInvariantString("M420-80q-17 0-28.5-11.5T380-120v-240L214-568q-15-20-4.5-43T240-634h220l-60-286q-4-20 8.5-35t32.5-15q11 0 20.5 5.5T477-950l243 370q9 14 9 30t-9 30L480-150q-8 15-19.5 22.5T420-120z"),
                    Fill = new SolidColorBrush(Color.FromArgb("#FB923C")),
                    HeightRequest = 14,
                    WidthRequest = 14,
                    Aspect = Stretch.Uniform,
                    VerticalOptions = LayoutOptions.Center
                });
                sparkRow.Children.Add(new Label
                {
                    Text = post.SparkCount.ToString(),
                    FontSize = 11,
                    TextColor = Color.FromArgb("#FB923C"),
                    VerticalOptions = LayoutOptions.Center
                });
                statsRow.Children.Add(sparkRow);

                // Date
                statsRow.Children.Add(new Label
                {
                    Text = post.CreatedAt.ToString("MMM dd, yyyy"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A"),
                    VerticalOptions = LayoutOptions.Center
                });

                postRow.Children.Add(statsRow);
                stack.Children.Add(postRow);
                stack.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#1C1C25"),
                    Margin = new Thickness(16, 0)
                });
            }

            if (_userPosts.Count > 5)
            {
                stack.Children.Add(new Label
                {
                    Text = $"+ {_userPosts.Count - 5} more posts",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#00B5B5"),
                    Padding = new Thickness(16, 10),
                    HorizontalOptions = LayoutOptions.Center
                });
            }

            return stack;
        }
        // ?? MOOD HISTORY ??
        private View BuildMoodHistory()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };

            if (!_moodHistory.Any())
            {
                stack.Children.Add(new Label
                {
                    Text = "No mood changes recorded.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    Padding = new Thickness(16, 14)
                });
                return stack;
            }

            foreach (var m in _moodHistory)
            {
                var row = new Grid
                {
                    Padding = new Thickness(16, 12),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(36) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    ColumnSpacing = 12
                };

                var dot = new Frame
                {
                    WidthRequest = 34,
                    HeightRequest = 34,
                    CornerRadius = 17,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#1A150D"),
                    BorderColor = Color.FromArgb("#3A2A1A"),
                    Content = new Label
                    {
                        Text = "??",
                        FontSize = 15,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                };

                var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
                textStack.Children.Add(new Label
                {
                    Text = $"'{m.OldMood ?? "None"}' ? '{m.NewMood}'",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#FB923C")
                });
                textStack.Children.Add(new Label
                {
                    Text = m.Timestamp.ToString("MMM dd, yyyy hh:mm tt"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A")
                });

                row.Add(dot, 0, 0);
                row.Add(textStack, 1, 0);
                row.Add(new Label
                {
                    Text = m.Source ?? "",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    VerticalOptions = LayoutOptions.Center
                }, 2, 0);

                stack.Children.Add(row);
                stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }

            return stack;
        }

        // ?? PROFILE CHANGES ??
        private View BuildProfileChanges()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };

            if (!_profileChanges.Any())
            {
                stack.Children.Add(new Label
                {
                    Text = "No profile changes recorded.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    Padding = new Thickness(16, 14)
                });
                return stack;
            }

            foreach (var c in _profileChanges)
            {
                var row = new Grid
                {
                    Padding = new Thickness(16, 12),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(36) },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    ColumnSpacing = 12
                };

                var iconFrame = new Frame
                {
                    WidthRequest = 34,
                    HeightRequest = 34,
                    CornerRadius = 17,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#0D1520"),
                    BorderColor = Color.FromArgb("#1A2A3A"),
                    Content = new Label
                    {
                        Text = GetFieldIcon(c.FieldName),
                        FontSize = 14,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center
                    }
                };

                var fieldBadge = new Border
                {
                    BackgroundColor = Color.FromArgb("#0D1520"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    Padding = new Thickness(6, 2),
                    HorizontalOptions = LayoutOptions.Start,
                    Content = new Label
                    {
                        Text = c.FieldName,
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#60A5FA")
                    }
                };

                var textStack = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
                textStack.Children.Add(fieldBadge);
                textStack.Children.Add(new Label
                {
                    Text = $"'{TruncateValue(c.OldValue)}' ? '{TruncateValue(c.NewValue)}'",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#7A7A8C"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 2
                });
                textStack.Children.Add(new Label
                {
                    Text = c.Timestamp.ToString("MMM dd, yyyy hh:mm tt"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A")
                });

                row.Add(iconFrame, 0, 0);
                row.Add(textStack, 1, 0);
                stack.Children.Add(row);
                stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }

            return stack;
        }

        // ?? LOGIN HISTORY ??
        private View BuildLoginHistory()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };

            if (!_loginHistory.Any())
            {
                stack.Children.Add(new Label
                {
                    Text = "No login history recorded.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    Padding = new Thickness(16, 14)
                });
                return stack;
            }

            foreach (var l in _loginHistory)
            {
                var duration = l.LogoutTime.HasValue
                    ? GetSessionDuration(l.LoginTime, l.LogoutTime.Value)
                    : "Active";
                var isActive = !l.LogoutTime.HasValue;

                var row = new Grid
                {
                    Padding = new Thickness(16, 12),
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(44) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
                    ColumnSpacing = 12
                };

                var iconFrame = new Frame
                {
                    WidthRequest = 36,
                    HeightRequest = 36,
                    CornerRadius = 18,
                    Padding = 0,
                    IsClippedToBounds = true,
                    HasShadow = false,
                    BackgroundColor = Color.FromArgb("#0D1A0D"),
                    BorderColor = Color.FromArgb("#1A3A1A"),
                    Content = new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = (Microsoft.Maui.Controls.Shapes.Geometry)
                            new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                            .ConvertFromInvariantString("M480-120v-80h280v-560H480v-80h280q33 0 56.5 23.5T840-760v560q0 33-23.5 56.5T760-120H480Zm-80-160-55-58 102-102H120v-80h327L345-622l55-58 200 200-200 200Z"),
                        Fill = new SolidColorBrush(Color.FromArgb("#22C55E")),
                        HeightRequest = 16,
                        WidthRequest = 16,
                        Aspect = Stretch.Uniform,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                };

                var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
                textStack.Children.Add(new Label
                {
                    Text = $"Device: {TruncateValue(l.DeviceId, 22)}",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8"),
                    LineBreakMode = LineBreakMode.TailTruncation
                });
                textStack.Children.Add(new Label
                {
                    Text = l.LoginTime.ToString("MMM dd, yyyy hh:mm tt"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3A3A4A")
                });

                row.Add(iconFrame, 0, 0);
                row.Add(textStack, 1, 0);
                row.Add(new Border
                {
                    BackgroundColor = Color.FromArgb(isActive ? "#0A2A0A" : "#1A1A1A"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(8, 4),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = duration,
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb(isActive ? "#22C55E" : "#7A7A8C")
                    }
                }, 2, 0);

                stack.Children.Add(row);
                stack.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = Color.FromArgb("#1C1C25")
                });
            }

            return stack;
        }
        // ?? ADMIN ACTIONS ??
        private View BuildAdminActions()
        {
            var stack = new VerticalStackLayout { Spacing = 0 };

            stack.Children.Add(BuildActionRow(
                "M480-320 280-520l56-58 104 104v-326h80v326l104-104 56 58-200 200ZM240-160q-33 0-56.5-23.5T160-240v-120h80v120h480v-120h80v120q0 33-23.5 56.5T720-160H240Z",
                "#00B5B5",
                "Export User Data",
                "Download full user report as CSV",
                "#00B5B5",
                async () =>
                {
                    var csv = new StringBuilder();
                    csv.AppendLine($"User Report: {_user.Name}");
                    csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    csv.AppendLine("");
                    csv.AppendLine($"Name,{_user.Name}");
                    csv.AppendLine($"Phone,{_user.PhoneNumber}");
                    csv.AppendLine($"Age,{_user.GetAge()}");
                    csv.AppendLine($"Gender,{_user.Gender}");
                    csv.AppendLine($"Joined,{_user.JoinDate:yyyy-MM-dd}");
                    csv.AppendLine($"Verified,{_user.IsVerified}");
                    csv.AppendLine($"Posts,{_userPosts.Count}");
                    csv.AppendLine($"Total Loves,{_userPosts.Sum(p => p.LoveCount)}");
                    csv.AppendLine($"Total Sparks,{_userPosts.Sum(p => p.SparkCount)}");
                    csv.AppendLine($"Logins,{_loginHistory.Count}");
                    csv.AppendLine($"Profile Changes,{_profileChanges.Count}");
                    csv.AppendLine($"Mood Changes,{_moodHistory.Count}");

                    var fileName = $"User_{_user.PhoneNumber}_{DateTime.Now:yyyyMMdd}.csv";
                    var filePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
                    System.IO.File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Export User Data",
                        File = new ShareFile(filePath)
                    });
                }));

            stack.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#1C1C25"),
                Margin = new Thickness(16, 0)
            });

            stack.Children.Add(BuildActionRow(
                "M160-200v-80h80v-560h480v560h80v80H160Zm260-80h80v-80h-80v80Zm0-160h80v-80h-80v80Zm0-160h80v-80h-80v80Z",
                "#22C55E",
                "View All Posts",
                $"{_userPosts.Count} posts by this user",
                "#22C55E",
                async () =>
                {
                    if (!_userPosts.Any())
                        await DisplayAlert("Posts", "This user has no posts.", "OK");
                    else
                        await DisplayAlert("Posts",
                            string.Join("\n\n", _userPosts.Take(5)
                                .Select(p => $"• {p.Content?.Substring(0, Math.Min(80, p.Content?.Length ?? 0))}...")),
                            "OK");
                }));

            stack.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#1C1C25"),
                Margin = new Thickness(16, 0)
            });

            stack.Children.Add(BuildActionRow(
                "M280-280h160v-200H280v200Zm240 0h160v-560H520v560ZM160-120v-720h640v720H160Zm80-80h480v-560H240v560Zm0 0v-560 560Z",
                "#C084FC",
                "Account Summary",
                "View engagement & activity stats",
                "#C084FC",
                async () =>
                {
                    var totalDays = (DateTime.UtcNow - _user.JoinDate).Days;
                    var summary =
                        $"Member for {totalDays} days\n" +
                        $"Posts: {_userPosts.Count}\n" +
                        $"Total Loves received: {_userPosts.Sum(p => p.LoveCount)}\n" +
                        $"Total Sparks received: {_userPosts.Sum(p => p.SparkCount)}\n" +
                        $"Profile changes: {_profileChanges.Count}\n" +
                        $"Mood changes: {_moodHistory.Count}\n" +
                        $"Login sessions: {_loginHistory.Count}";
                    await DisplayAlert("Account Summary", summary, "OK");
                }));

            return stack;
        }

        private View BuildActionRow(string pathData, string iconColor,
            string title, string subtitle, string titleColor, Func<Task> action)
        {
            var row = new Grid
            {
                Padding = new Thickness(16, 14),
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(44) },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        },
                ColumnSpacing = 12
            };

            var iconFrame = new Frame
            {
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                IsClippedToBounds = true,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#16161C"),
                BorderColor = Color.FromArgb("#2A2A38"),
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)
                        new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                        .ConvertFromInvariantString(pathData),
                    Fill = new SolidColorBrush(Color.FromArgb(iconColor)),
                    HeightRequest = 16,
                    WidthRequest = 16,
                    Aspect = Stretch.Uniform,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Children.Add(new Label
            {
                Text = title,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(titleColor)
            });
            textStack.Children.Add(new Label
            {
                Text = subtitle,
                FontSize = 11,
                TextColor = Color.FromArgb("#7A7A8C")
            });

            var chevron = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Microsoft.Maui.Controls.Shapes.Geometry)
                    new Microsoft.Maui.Controls.Shapes.PathGeometryConverter()
                    .ConvertFromInvariantString("M504-480 320-664l56-56 240 240-240 240-56-56 184-184Z"),
                Fill = new SolidColorBrush(Color.FromArgb("#3A3A4A")),
                HeightRequest = 16,
                WidthRequest = 16,
                Aspect = Stretch.Uniform,
                VerticalOptions = LayoutOptions.Center
            };

            row.Add(iconFrame, 0, 0);
            row.Add(textStack, 1, 0);
            row.Add(chevron, 2, 0);

            row.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await action())
            });

            return row;
        }
        private View BuildActionRow(string icon, string title, string subtitle, string iconColor, Func<Task> action)
        {
            var row = new Grid
            {
                Padding = new Thickness(16, 14),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12
            };

            row.Add(new Label
            {
                Text = icon,
                FontSize = 22,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Children.Add(new Label
            {
                Text = title,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(iconColor)
            });
            textStack.Children.Add(new Label
            {
                Text = subtitle,
                FontSize = 11,
                TextColor = Color.FromArgb("#7A7A8C")
            });
            row.Add(textStack, 1, 0);

            row.Add(new Label
            {
                Text = "›",
                FontSize = 20,
                TextColor = Color.FromArgb("#3A3A4A"),
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            row.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await action())
            });

            return row;
        }

        // ?? HELPERS ??
        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        // ?? HELPERS ??
        private string GetValidProfileImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (File.Exists(path)) return path;
            var fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
            {
                var local = Path.Combine(FileSystem.AppDataDirectory, fileName);
                if (File.Exists(local)) return local;
                var cache = Path.Combine(FileSystem.CacheDirectory, fileName);
                if (File.Exists(cache)) return cache;
            }
            return string.Empty;
        }

        private string GetRelativeTime(DateTime timestamp)
        {
            var diff = DateTime.UtcNow - timestamp;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return timestamp.ToString("MMM dd, yyyy");
        }

        private string GetFieldIcon(string fieldName)
        {
            return fieldName?.ToLower() switch
            {
                var s when s != null && s.Contains("mood") => "??",
                var s when s != null && s.Contains("bio") => "??",
                var s when s != null && (s.Contains("photo") || s.Contains("image")) => "??",
                var s when s != null && s.Contains("interest") => "?",
                var s when s != null && s.Contains("location") => "??",
                var s when s != null && s.Contains("height") => "??",
                var s when s != null && s.Contains("body") => "??",
                var s when s != null && s.Contains("drink") => "??",
                var s when s != null && s.Contains("smoke") => "??",
                _ => "??"
            };
        }

        private string TruncateValue(string value, int max = 30)
        {
            if (string.IsNullOrEmpty(value)) return "nothing";
            return value.Length > max ? value.Substring(0, max) + "…" : value;
        }

        private string GetSessionDuration(DateTime login, DateTime logout)
        {
            var d = logout - login;
            if (d.TotalMinutes < 1) return "<1m";
            if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m";
            if (d.TotalHours < 24) return $"{(int)d.TotalHours}h {d.Minutes}m";
            return $"{(int)d.TotalDays}d";
        }

        private async Task ShowLoading(bool isLoading)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                LoadingIndicator.IsVisible = isLoading;
                LoadingIndicator.IsRunning = isLoading;
            });
        }
    }
}