using Lock.Services.Admin;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Lock.Chat.Services;
using Lock.Models;
using System.Diagnostics;
using Path = System.IO.Path;

namespace Lock.Pages.Admin
{
    public partial class AdminDashboardPage : ContentPage
    {
        private List<User> _allUsers = new();
        private List<Lock.Models.Post> _allPosts = new();
        private List<BlockedUserItem> _blockedUsers = new();
        private string _activeFilter = "All";

        // Filter definitions
        private readonly (string Key, string Label, string Color, string BgColor)[] _filters =
        {
            ("All",        "All Users",      "#00B5B5", "#0D1F1F"),
            ("Verified",   "Verified",       "#F5C518", "#2A2A0A"),
            ("Active",     "Active Today",   "#22C55E", "#0A2A0A"),
            ("New",        "New Today",      "#C084FC", "#2A0A2A"),
            ("Blocked",    "Blocked",        "#FF3B6F", "#2A1520"),
        };

        public AdminDashboardPage()
        {
            InitializeComponent();
            Shell.SetNavBarIsVisible(this, false);
            BuildFilterBar();
            LoadDashboard();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Shell.SetNavBarIsVisible(this, false);
            LoadDashboard();
        }

        // ?? FILTER BAR ??
        private void BuildFilterBar()
        {
            FilterBar.Children.Clear();
            foreach (var (key, label, color, bgColor) in _filters)
            {
                var isActive = key == _activeFilter;
                var chip = new Border
                {
                    BackgroundColor = Color.FromArgb(isActive ? color : bgColor),
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Color.FromArgb(color)),
                    StrokeShape = new RoundRectangle { CornerRadius = 20 },
                    Padding = new Thickness(14, 8)
                };
                chip.Content = new Label
                {
                    Text = label,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = isActive ? Colors.Black : Color.FromArgb(color)
                };
                var filterKey = key;
                chip.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(() => OnFilterTapped(filterKey))
                });
                FilterBar.Children.Add(chip);
            }
        }

        private void OnFilterTapped(string filter)
        {
            _activeFilter = filter;
            BuildFilterBar();
            RenderDashboard();
        }

        private void OnRefreshClicked(object sender, EventArgs e) => LoadDashboard();

        // ?? LOAD DATA ??
        private async void LoadDashboard()
        {
            try
            {
                LoadingOverlay(true);
                _allUsers = await AuthService.GetAllUsersAsync();
                _allPosts = await PostRepository.GetAllAsync() ?? new List<Lock.Models.Post>();
                LastUpdatedLabel.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
                await LoadBlockedUsersData();
                RenderDashboard();
                LoadingOverlay(false);
            }
            catch (Exception ex)
            {
                LoadingOverlay(false);
                await DisplayAlert("Error", $"Failed to load dashboard: {ex.Message}", "OK");
            }
        }

        private async Task LoadBlockedUsersData()
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // Replace this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // var blockedRelations = await db.Table<BlockedUser>()
                //     .Where(b => b.UserPhone == currentUserPhone).ToListAsync();

                // With this Supabase code:
                var blockedRelations = await SupabaseService.GetAsync<BlockedUser>("BlockedUsers",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}");

                // Get all unique blocked phone numbers
                var blockedPhones = blockedRelations.Select(b => b.BlockedPhone).Distinct().ToList();

                if (blockedPhones.Any())
                {
                    // Build a filter for getting all blocked users in one query
                    var phoneFilters = string.Join(",", blockedPhones.Select(p => $"PhoneNumber=eq.{Uri.EscapeDataString(p)}"));
                    var allBlockedUsers = await SupabaseService.GetAsync<Lock.Models.User>("Users", phoneFilters);

                    _blockedUsers = blockedRelations.Select(b =>
                    {
                        var user = allBlockedUsers.FirstOrDefault(u => u.PhoneNumber == b.BlockedPhone);
                        return new BlockedUserItem
                        {
                            Phone = b.BlockedPhone,
                            UserName = user?.Name ?? b.BlockedPhone,
                            ProfileImagePath = user?.ProfileImagePath ?? string.Empty,
                            Initial = user?.Name?.Length > 0 ? user.Name[0].ToString().ToUpper() : "U"
                        };
                    }).ToList();
                }
                else
                {
                    _blockedUsers = new List<BlockedUserItem>();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"LoadBlockedUsersData: {ex}"); }
        }

        // ?? RENDER ??
        private void RenderDashboard()
        {
            DashboardContent.Children.Clear();

            var filteredUsers = _activeFilter switch
            {
                "Verified" => _allUsers.Where(u => u.IsVerified).ToList(),
                "Active" => _allUsers.Where(u => u.LastActive.Date == DateTime.Today).ToList(),
                "New" => _allUsers.Where(u => u.JoinDate.Date == DateTime.Today).ToList(),
                "Blocked" => _allUsers.Where(u => _blockedUsers.Any(b => b.Phone == u.PhoneNumber)).ToList(),
                _ => _allUsers
            };

            // ?? STAT CARDS ??
            AddStatCards(filteredUsers);

            // ?? SECTION: filtered user list or full dashboard ??
            if (_activeFilter == "All")
            {
                AddFullDashboard();
            }
            else
            {
                AddFilteredUserList(filteredUsers);
            }

            // ?? QUICK ACTIONS ??
            AddQuickActions();
        }

        // ?? STAT CARDS ??
        private GraphicsView _donutChart;
        private DonutChartDrawable _donutDrawable;



        // ?? STAT CARDS + CHART ??
        private void AddStatCards(List<User> filteredUsers)
        {
            var total = _allUsers.Count;
            var verified = _allUsers.Count(u => u.IsVerified);
            var activeToday = _allUsers.Count(u => u.LastActive.Date == DateTime.Today);
            var newToday = _allUsers.Count(u => u.JoinDate.Date == DateTime.Today);
            var blocked = _blockedUsers.Count;
            var unverified = total - verified;

            // ?? ROW 1 ??
            var row1 = new Grid
            {
                Padding = new Thickness(16, 20, 16, 0),
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Star }
        },
                ColumnSpacing = 10
            };
            row1.Add(BuildStatCard("TOTAL USERS", total.ToString("N0"), "#00B5B5", "#0D1F1F", "#1A3A3A",
                $"Showing: {total}"), 0, 0);
            row1.Add(BuildStatCard("VERIFIED", verified.ToString("N0"), "#F5C518", "#1A1A0D", "#3A3A1A",
                $"{(total > 0 ? (double)verified / total * 100 : 0):F1}% of total"), 1, 0);
            DashboardContent.Children.Add(row1);

            // ?? ROW 2 ??
            var row2 = new Grid
            {
                Padding = new Thickness(16, 10, 16, 0),
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Star }
        },
                ColumnSpacing = 10
            };
            row2.Add(BuildStatCard("ACTIVE TODAY", activeToday.ToString("N0"), "#22C55E", "#0D1A0D", "#1A3A1A",
                $"{(total > 0 ? (double)activeToday / total * 100 : 0):F1}% of total"), 0, 0);
            row2.Add(BuildStatCard("BLOCKED", blocked.ToString(), "#FF3B6F", "#1A0D14", "#3A1A25",
                $"New today: {newToday}"), 1, 0);
            DashboardContent.Children.Add(row2);

            // ?? DONUT CHART ??
            if (total > 0)
                DashboardContent.Children.Add(BuildDonutChartCard(total, verified, activeToday, newToday, blocked, unverified));
        }

        private View BuildDonutChartCard(int total, int verified, int activeToday, int newToday, int blocked, int unverified)
        {
            // Segments: verified, active-only, new-only, blocked, other
            var segments = new List<(string Label, int Value, string Color)>
    {
        ("Verified",     verified,    "#F5C518"),
        ("Active Today", activeToday, "#22C55E"),
        ("New Today",    newToday,    "#C084FC"),
        ("Blocked",      blocked,     "#FF3B6F"),
        ("Unverified",   Math.Max(0, unverified - activeToday - newToday), "#3A3A4A"),
    }.Where(s => s.Value > 0).ToList();

            var cardStack = new VerticalStackLayout { Spacing = 0 };

            // Card header
            var header = new Grid
            {
                Padding = new Thickness(16, 12),
                BackgroundColor = Color.FromArgb("#12121A"),
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        }
            };
            header.Add(new Label
            {
                Text = "USER BREAKDOWN",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F0EDE8"),
                CharacterSpacing = 1.5,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            header.Add(new Label
            {
                Text = $"{total} total",
                FontSize = 11,
                TextColor = Color.FromArgb("#5A5A6A"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            cardStack.Children.Add(header);
            cardStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });

            // Body: chart left, legend right
            var body = new Grid
            {
                Padding = new Thickness(16, 16),
                ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(160) },
            new ColumnDefinition { Width = GridLength.Star }
        },
                ColumnSpacing = 16
            };

            // ?? SVG DONUT ??
            body.Add(BuildDonutSvg(segments, total), 0, 0);

            // ?? LEGEND ??
            var legend = new VerticalStackLayout
            {
                Spacing = 10,
                VerticalOptions = LayoutOptions.Center
            };

            foreach (var (label, value, color) in segments)
            {
                var pct = (double)value / total * 100;
                var legendRow = new Grid
                {
                    ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
                    ColumnSpacing = 8
                };

                // Color dot
                legendRow.Add(new BoxView
                {
                    WidthRequest = 10,
                    HeightRequest = 10,
                    CornerRadius = 5,
                    Color = Color.FromArgb(color),
                    VerticalOptions = LayoutOptions.Center
                }, 0, 0);

                // Label + bar
                var labelStack = new VerticalStackLayout { Spacing = 3 };
                labelStack.Children.Add(new Label
                {
                    Text = label,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#F0EDE8")
                });

                // Progress bar
                var barBg = new Grid { HeightRequest = 4 };
                barBg.Children.Add(new BoxView
                {
                    HeightRequest = 4,
                    CornerRadius = 2,
                    Color = Color.FromArgb("#1C1C25"),
                    HorizontalOptions = LayoutOptions.Fill
                });
                barBg.Children.Add(new BoxView
                {
                    HeightRequest = 4,
                    CornerRadius = 2,
                    Color = Color.FromArgb(color),
                    HorizontalOptions = LayoutOptions.Start,
                    WidthRequest = (double)value / total * 120  // max bar width ~120
                });
                labelStack.Children.Add(barBg);
                legendRow.Add(labelStack, 1, 0);

                // Value + %
                var valueStack = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.End };
                valueStack.Children.Add(new Label
                {
                    Text = value.ToString(),
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(color),
                    HorizontalOptions = LayoutOptions.End
                });
                valueStack.Children.Add(new Label
                {
                    Text = $"{pct:F0}%",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    HorizontalOptions = LayoutOptions.End
                });
                legendRow.Add(valueStack, 2, 0);

                legend.Children.Add(legendRow);
            }

            body.Add(legend, 1, 0);
            cardStack.Children.Add(body);

            return new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D14"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1C1C25")),
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Margin = new Thickness(16, 14, 16, 0),
                Content = cardStack
            };
        }

        private View BuildDonutSvg(List<(string Label, int Value, string Color)> segments, int total)
        {
            const double cx = 70, cy = 70, r = 55, innerR = 32;
            const double gap = 0.03; // radians gap between segments

            var svgParts = new StringBuilder();
            double startAngle = -Math.PI / 2; // start at top

            // Build arc paths
            foreach (var (label, value, color) in segments)
            {
                double sweep = (double)value / total * (2 * Math.PI) - gap;
                if (sweep <= 0) { startAngle += gap; continue; }

                double endAngle = startAngle + sweep;

                // Outer arc points
                double x1 = cx + r * Math.Cos(startAngle);
                double y1 = cy + r * Math.Sin(startAngle);
                double x2 = cx + r * Math.Cos(endAngle);
                double y2 = cy + r * Math.Sin(endAngle);

                // Inner arc points
                double x3 = cx + innerR * Math.Cos(endAngle);
                double y3 = cy + innerR * Math.Sin(endAngle);
                double x4 = cx + innerR * Math.Cos(startAngle);
                double y4 = cy + innerR * Math.Sin(startAngle);

                int largeArc = sweep > Math.PI ? 1 : 0;

                svgParts.Append($@"<path d='
            M {x1:F2} {y1:F2}
            A {r} {r} 0 {largeArc} 1 {x2:F2} {y2:F2}
            L {x3:F2} {y3:F2}
            A {innerR} {innerR} 0 {largeArc} 0 {x4:F2} {y4:F2}
            Z'
            fill='{color}' opacity='0.9'/>
        ");

                startAngle = endAngle + gap;
            }

            // Center text
            svgParts.Append($@"
        <text x='{cx}' y='{cy - 6}' text-anchor='middle'
              font-size='18' font-weight='bold' fill='#F0EDE8'>{total}</text>
        <text x='{cx}' y='{cy + 12}' text-anchor='middle'
              font-size='9' fill='#5A5A6A' letter-spacing='1'>USERS</text>
    ");

            var svgXml = $@"<svg viewBox='0 0 140 140' xmlns='http://www.w3.org/2000/svg'>{svgParts}</svg>";

            // Render via WebView for reliable SVG support on Android/iOS
            var webView = new WebView
            {
                WidthRequest = 140,
                HeightRequest = 140,
                BackgroundColor = Colors.Transparent,
                Source = new HtmlWebViewSource
                {
                    Html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                <meta name='viewport' content='width=device-width,initial-scale=1'>
                <style>
                    * {{ margin:0; padding:0; box-sizing:border-box; }}
                    html, body {{ background: transparent; width:140px; height:140px; overflow:hidden; }}
                    svg {{ width:140px; height:140px; }}
                </style>
                </head>
                <body>{svgXml}</body>
                </html>"
                }
            };

            return webView;
        }

        private Border BuildStatCard(string title, string value, string accentColor, string bgColor, string borderColor, string subtitle)
        {
            var stack = new VerticalStackLayout { Spacing = 4 };

            var titleRow = new HorizontalStackLayout { Spacing = 6 };
            titleRow.Children.Add(new BoxView
            {
                WidthRequest = 6,
                HeightRequest = 6,
                CornerRadius = 3,
                Color = Color.FromArgb(accentColor),
                VerticalOptions = LayoutOptions.Center
            });
            titleRow.Children.Add(new Label
            {
                Text = title,
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(accentColor),
                CharacterSpacing = 1.2,
                VerticalOptions = LayoutOptions.Center
            });

            stack.Children.Add(titleRow);
            stack.Children.Add(new Label
            {
                Text = value,
                FontSize = 30,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F0EDE8")
            });
            stack.Children.Add(new Label
            {
                Text = subtitle,
                FontSize = 11,
                TextColor = Color.FromArgb("#7A7A8C")
            });

            return new Border
            {
                BackgroundColor = Color.FromArgb(bgColor),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(borderColor)),
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(16, 14),
                Content = stack
            };
        }

        // ?? FILTERED USER LIST ??
        private void AddFilteredUserList(List<User> users)
        {
            var (_, label, color, bgColor) = _filters.First(f => f.Key == _activeFilter);

            DashboardContent.Children.Add(BuildSectionHeader(label, $"{users.Count} users", color));

            if (!users.Any())
            {
                DashboardContent.Children.Add(new Label
                {
                    Text = "No users in this category.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    Margin = new Thickness(16, 8)
                });
                return;
            }

            var container = new VerticalStackLayout
            {
                Spacing = 0,
                Margin = new Thickness(16, 8, 16, 0),
                BackgroundColor = Color.FromArgb("#0D0D14")
            };

            foreach (var user in users.OrderByDescending(u => u.LastActive))
            {
                var badge = _activeFilter switch
                {
                    "Verified" => ("? Verified", "#4CAF50", "#0A2A0A"),
                    "Active" => ("? Active", "#22C55E", "#0A2A0A"),
                    "New" => ("New", "#C084FC", "#2A0A2A"),
                    "Blocked" => ("Blocked", "#FF3B6F", "#2A1520"),
                    _ => ("", "#666666", "#1A1A1A")
                };

                container.Children.Add(BuildUserRow(
                    user.ProfileImagePath,
                    user.Name,
                    user.PhoneNumber,
                    $"Last active: {GetRelativeTime(user.LastActive)}",
                    badge.Item1, badge.Item2, badge.Item3,
                    user.PhoneNumber
                ));
                container.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }

            var cardBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D14"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1C1C25")),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Margin = new Thickness(16, 0),
                Content = container
            };
            DashboardContent.Children.Add(cardBorder);
        }

        // ?? FULL DASHBOARD ??
        private async void AddFullDashboard()
        {
            // Top Active Users
            AddSectionWithUsers(
                "?? TOP ACTIVE USERS", "#F5C518",
                _allUsers.OrderByDescending(u => _allPosts.Count(p => p.AuthorPhone == u.PhoneNumber))
                         .Take(10).ToList(),
                u => $"{_allPosts.Count(p => p.AuthorPhone == u.PhoneNumber)} posts  •  {GetRelativeTime(u.LastActive)}",
                "?", "#F5C518", "#2A2A0A"
            );

            // Verified Users
            AddSectionWithUsers(
                "? VERIFIED USERS", "#F5C518",
                _allUsers.Where(u => u.IsVerified).OrderByDescending(u => u.VerifiedAt).ToList(),
                u => $"{u.PhoneNumber}  •  Verified {(u.VerifiedAt.HasValue ? GetRelativeTime(u.VerifiedAt.Value) : "")}",
                "?", "#F5C518", "#2A2A0A"
            );

            // Active Today
            AddSectionWithUsers(
                "?? ACTIVE TODAY", "#22C55E",
                _allUsers.Where(u => u.LastActive.Date == DateTime.Today)
                         .OrderByDescending(u => u.LastActive).ToList(),
                u => $"{u.PhoneNumber}  •  {GetRelativeTime(u.LastActive)}",
                "?", "#22C55E", "#0A2A0A"
            );

            // New Today
            AddSectionWithUsers(
                "? NEW TODAY", "#C084FC",
                _allUsers.Where(u => u.JoinDate.Date == DateTime.Today)
                         .OrderByDescending(u => u.JoinDate).ToList(),
                u => $"{u.PhoneNumber}  •  Joined {u.JoinDate:hh:mm tt}",
                "New", "#C084FC", "#2A0A2A"
            );

            // Blocked Users
            if (_blockedUsers.Any())
            {
                DashboardContent.Children.Add(BuildSectionHeader("?? BLOCKED USERS", $"{_blockedUsers.Count} users", "#FF3B6F"));
                var blockedContainer = BuildUserCard(Color.FromArgb("#0D0D14"), Color.FromArgb("#1C1C25"));
                foreach (var b in _blockedUsers)
                {
                    blockedContainer.Children.Add(BuildUserRow(
                        b.ProfileImagePath, b.UserName, b.Phone,
                        "Blocked user", "?? Blocked", "#FF3B6F", "#2A1520", b.Phone));
                    blockedContainer.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                WrapAndAdd(blockedContainer);
            }

            // Recent Mood Changes
            try
            {
                var moodChanges = await UserTrackingService.Instance.GetAllMoodChangesAsync(30);
                DashboardContent.Children.Add(BuildSectionHeader("?? RECENT MOOD CHANGES", $"{moodChanges.Count}", "#FB923C"));
                var moodContainer = BuildUserCard(Color.FromArgb("#0D0D14"), Color.FromArgb("#1C1C25"));
                foreach (var c in moodChanges.Take(10))
                {
                    var user = _allUsers.FirstOrDefault(u => u.PhoneNumber == c.UserPhone);
                    moodContainer.Children.Add(BuildUserRow(
                        user?.ProfileImagePath ?? "",
                        user?.Name ?? c.UserPhone,
                        $"'{c.OldMood ?? "None"}' ? '{c.NewMood}'",
                        GetRelativeTime(c.Timestamp),
                        "Mood", "#FB923C", "#2A1A0A", c.UserPhone));
                    moodContainer.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                WrapAndAdd(moodContainer);
            }
            catch (Exception ex) { Debug.WriteLine($"Mood changes: {ex}"); }

            // Recent Profile Changes
            try
            {
                var profileChanges = await UserTrackingService.Instance.GetAllProfileChangesAsync(30);
                DashboardContent.Children.Add(BuildSectionHeader("?? RECENT PROFILE UPDATES", $"{profileChanges.Count}", "#60A5FA"));
                var profileContainer = BuildUserCard(Color.FromArgb("#0D0D14"), Color.FromArgb("#1C1C25"));
                foreach (var c in profileChanges.Take(10))
                {
                    var user = _allUsers.FirstOrDefault(u => u.PhoneNumber == c.UserPhone);
                    profileContainer.Children.Add(BuildUserRow(
                        user?.ProfileImagePath ?? "",
                        user?.Name ?? c.UserPhone,
                        $"Updated {c.FieldName}: {TruncateValue(c.NewValue, 30)}",
                        GetRelativeTime(c.Timestamp),
                        c.FieldName, "#60A5FA", "#0D1520", c.UserPhone));
                    profileContainer.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
                }
                WrapAndAdd(profileContainer);
            }
            catch (Exception ex) { Debug.WriteLine($"Profile changes: {ex}"); }
        }

        private void AddSectionWithUsers(string title, string accentColor, List<User> users,
            Func<User, string> subtitle, string badge, string badgeColor, string badgeBg)
        {
            DashboardContent.Children.Add(BuildSectionHeader(title, $"{users.Count} users", accentColor));

            if (!users.Any())
            {
                DashboardContent.Children.Add(new Label
                {
                    Text = "None",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    Margin = new Thickness(16, 4, 16, 0)
                });
                return;
            }

            var container = BuildUserCard(Color.FromArgb("#0D0D14"), Color.FromArgb("#1C1C25"));
            foreach (var user in users)
            {
                container.Children.Add(BuildUserRow(
                    user.ProfileImagePath,
                    user.Name,
                    user.PhoneNumber,
                    subtitle(user),
                    badge, badgeColor, badgeBg,
                    user.PhoneNumber
                ));
                container.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#1C1C25") });
            }
            WrapAndAdd(container);
        }

        // ?? UI BUILDERS ??
        private View BuildSectionHeader(string title, string subtitle, string color)
        {
            var grid = new Grid
            {
                Margin = new Thickness(16, 24, 16, 8),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            grid.Add(new Label
            {
                Text = title,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(color),
                CharacterSpacing = 1.5,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            grid.Add(new Label
            {
                Text = subtitle,
                FontSize = 11,
                TextColor = Color.FromArgb("#5A5A6A"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            return grid;
        }

        private VerticalStackLayout BuildUserCard(Color bg, Color border)
        {
            return new VerticalStackLayout { Spacing = 0, BackgroundColor = bg };
        }

        private void WrapAndAdd(VerticalStackLayout container)
        {
            DashboardContent.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#0D0D14"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1C1C25")),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Margin = new Thickness(16, 0),
                Content = container
            });
        }

        private View BuildUserRow(string profileImagePath, string name, string phone,
            string detail, string badge, string badgeColor, string badgeBg, string userPhone)
        {
            var row = new Grid
            {
                Padding = new Thickness(14, 11),
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(44) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            // ?? AVATAR ??
            var avatarFrame = new Frame
            {
                WidthRequest = 38,
                HeightRequest = 38,
                CornerRadius = 19,
                Padding = 0,
                IsClippedToBounds = true,
                HasShadow = false,
                BackgroundColor = Color.FromArgb("#16161C"),
                BorderColor = Color.FromArgb("#2A2A38"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var validPath = GetValidProfileImagePath(profileImagePath);
            if (!string.IsNullOrEmpty(validPath))
            {
                avatarFrame.Content = new Image
                {
                    Source = ImageSource.FromFile(validPath),
                    Aspect = Aspect.AspectFill,
                    WidthRequest = 38,
                    HeightRequest = 38
                };
            }
            else
            {
                avatarFrame.Content = new Label
                {
                    Text = name?.Length > 0 ? name[0].ToString().ToUpper() : "?",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00B5B5"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
            }
            row.Add(avatarFrame, 0, 0);

            // ?? TEXT ??
            var textStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textStack.Children.Add(new Label
            {
                Text = name,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F0EDE8"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
            textStack.Children.Add(new Label
            {
                Text = phone,
                FontSize = 11,
                TextColor = Color.FromArgb("#7A7A8C"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
            if (!string.IsNullOrEmpty(detail))
                textStack.Children.Add(new Label
                {
                    Text = detail,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#5A5A6A"),
                    LineBreakMode = LineBreakMode.TailTruncation
                });
            row.Add(textStack, 1, 0);

            // ?? BADGE ??
            if (!string.IsNullOrEmpty(badge))
            {
                row.Add(new Border
                {
                    BackgroundColor = Color.FromArgb(badgeBg),
                    StrokeThickness = 1,
                    Stroke = new SolidColorBrush(Color.FromArgb(badgeColor)),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(8, 4),
                    VerticalOptions = LayoutOptions.Center,
                    Content = new Label
                    {
                        Text = badge,
                        FontSize = 10,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb(badgeColor)
                    }
                }, 2, 0);
            }

            // Tap to navigate
            var phone_captured = userPhone;
            row.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    var detailPage = new UserDetailPage { UserPhone = phone_captured };
                    await Navigation.PushAsync(detailPage);
                })
            });

            return row;
        }

        // ?? QUICK ACTIONS ??
        private void AddQuickActions()
        {
            DashboardContent.Children.Add(new Label
            {
                Text = "QUICK ACTIONS",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5A5A6A"),
                CharacterSpacing = 2,
                Margin = new Thickness(16, 24, 16, 10)
            });

            var grid = new Grid
            {
                Padding = new Thickness(16, 0),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };

            var exportBtn = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                HeightRequest = 52,
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb("#00B5B5"), Offset = 0 },
                        new GradientStop { Color = Color.FromArgb("#008080"), Offset = 1 }
                    }
                },
                Content = new Label
                {
                    Text = "? Export Report",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            exportBtn.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await ExportReport())
            });
            grid.Add(exportBtn, 0, 0);

            var groupBtn = new Border
            {
                BackgroundColor = Color.FromArgb("#16161C"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#2A2A38")),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                HeightRequest = 52,
                Content = new Label
                {
                    Text = "?? Group Activity",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#A78BFA"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            groupBtn.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await OnGroupActivities())
            });
            grid.Add(groupBtn, 1, 0);

            DashboardContent.Children.Add(grid);
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

        private void LoadingOverlay(bool show)
        {
            // Handled inline — no overlay needed since content rebuilds
        }

        private string GetRelativeTime(DateTime timestamp)
        {
            var diff = DateTime.UtcNow - timestamp;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
            return timestamp.ToString("MMM dd, yyyy");
        }

        private string TruncateValue(string value, int max = 30)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            return value.Length > max ? value.Substring(0, max) + "…" : value;
        }

        private async Task ExportReport()
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine("LOCK DATING APP - ADMIN REPORT");
                csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csv.AppendLine("Name,Phone,Age,Gender,Verified,Join Date,Last Active");
                foreach (var u in _allUsers)
                    csv.AppendLine($"\"{u.Name}\",{u.PhoneNumber},{u.GetAge()},{u.Gender},{u.IsVerified},{u.JoinDate:yyyy-MM-dd},{u.LastActive:yyyy-MM-dd}");
                var path = Path.Combine(FileSystem.CacheDirectory, $"Lock_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "Export Report", File = new ShareFile(path) });
            }
            catch (Exception ex) { await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK"); }
        }

        private async Task OnGroupActivities()
        {
            try
            {
                var activities = await UserTrackingService.Instance.GetAllGroupTrackingAsync(100);
                await DisplayAlert("Group Activities", $"Total: {activities.Count}", "OK");
            }
            catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
        }

        // Keep navigation handlers for backward compat
        private async void OnUserTapped(object sender, TappedEventArgs e) { }
        private async void OnExportReportClicked(object sender, EventArgs e) => await ExportReport();
        private async void OnGroupActivitiesClicked(object sender, EventArgs e) => await OnGroupActivities();
        private async void OnViewAllUsersClicked(object sender, EventArgs e) => await Navigation.PopAsync();
        private async void OnViewVerifiedUsersClicked(object sender, EventArgs e) => OnFilterTapped("Verified");
        private async void OnViewActiveUsersClicked(object sender, EventArgs e) => OnFilterTapped("Active");
        private async void OnViewNewUsersClicked(object sender, EventArgs e) => OnFilterTapped("New");
    }

    public class DonutChartDrawable : IDrawable
    {
        public List<(string Label, float Value, Color Color)> Segments { get; set; } = new();
        public string CenterText { get; set; } = "";
        public string CenterSubText { get; set; } = "";

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var cx = dirtyRect.Width / 2f;
            var cy = dirtyRect.Height / 2f;
            var radius = Math.Min(cx, cy) - 16f;
            var innerRadius = radius * 0.58f;

            var total = Segments.Sum(s => s.Value);
            if (total <= 0) return;

            float startAngle = -90f;
            float gapDegrees = 2.5f;

            foreach (var seg in Segments)
            {
                if (seg.Value <= 0) continue;
                float sweep = (seg.Value / total) * 360f - gapDegrees;
                if (sweep < 0) sweep = 0;

                canvas.FillColor = seg.Color;
                canvas.FillArc(cx - radius, cy - radius,
                               radius * 2, radius * 2,
                               startAngle, sweep, true);

                startAngle += (seg.Value / total) * 360f;
            }

            // Punch inner hole
            canvas.FillColor = Color.FromArgb("#0A0A0F");
            canvas.FillCircle(cx, cy, innerRadius);

            // Center text
            canvas.FontColor = Color.FromArgb("#F0EDE8");
            canvas.FontSize = 26;
            canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
            canvas.DrawString(CenterText, cx - 50, cy - 22, 100, 30,
                              HorizontalAlignment.Center, VerticalAlignment.Center);

            canvas.FontColor = Color.FromArgb("#7A7A8C");
            canvas.FontSize = 11;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.DrawString(CenterSubText, cx - 50, cy + 8, 100, 20,
                              HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
    public class BlockedUserItem
    {
        public string Phone { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ProfileImagePath { get; set; } = string.Empty;
        public string Initial { get; set; } = string.Empty;
        public string RankColor { get; set; } = "#E65100";
    }
}