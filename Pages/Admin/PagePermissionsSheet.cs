// ════════════════════════════════════════════════════
// FILE — FULL REPLACEMENT
// Path: Lock/Pages/Admin/PagePermissionsSheet.cs
// ════════════════════════════════════════════════════

using Lock.Services.Admin;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Lock.Pages.Admin
{
    // ─────────────────────────────────────────────────────────────────────────
    // PagePermissionToggleVm
    // ─────────────────────────────────────────────────────────────────────────
    public class PagePermissionToggleVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;   // SVG path data
        public string GroupName { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#00C9C9";

        private bool _isAllowed = true;
        public bool IsAllowed
        {
            get => _isAllowed;
            set { _isAllowed = value; OnPropChanged(); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PagePermissionsSheet
    // ─────────────────────────────────────────────────────────────────────────
    public class PagePermissionsSheet : ContentPage
    {
        private readonly List<PagePermissionToggleVm> _toggles;
        private readonly string _userName;
        public event Action<List<PagePermissionToggleVm>>? OnSaved;

        // ── Material SVG paths used for per-page icons ──────────────────────
        // These are the path data strings used throughout the app already.
        // Each icon is rendered via Microsoft.Maui.Controls.Shapes.Path.

        // check_circle (allowed state)
        private const string IC_CHECK_CIRCLE =
            "M438-240 296-382l58-58 84 84 168-168 58 58-226 226ZM480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Z";

        // block (denied state)
        private const string IC_BLOCK =
            "M480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80ZM280-440h400v-80H280v80Z";

        // Accent colours per group
        private static readonly Dictionary<string, (string Accent, string Bg, string Border)> _groupColors = new()
        {
            { "Core",   ("#00C9C9", "#041212", "#082020") },
            { "Social", ("#A78BFA", "#0D0814", "#1A1028") },
            { "Admin",  ("#F87171", "#140404", "#241010") },
        };

        public PagePermissionsSheet(string userName, List<PagePermissionToggleVm> toggles)
        {
            _toggles = toggles;
            _userName = userName;
            BackgroundColor = Color.FromArgb("#08080F");
            Shell.SetNavBarIsVisible(this, false);

            var root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Auto },
                }
            };

            root.Add(BuildHeader(), 0, 0);
            root.Add(BuildBody(), 0, 1);
            root.Add(BuildFooter(), 0, 2);

            Content = root;
        }

        // ══════════════════════════════════════════════════════
        // HEADER  — compact, clean, no wild gradients
        // ══════════════════════════════════════════════════════
        private View BuildHeader()
        {
            var wrapper = new Grid
            {
                BackgroundColor = Color.FromArgb("#0C0C1A"),
                Padding = new Thickness(20, 52, 20, 16),
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = 1 },
                }
            };

            // ── Row 0: nav row ──────────────────────────────
            var navRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Margin = new Thickness(0, 0, 0, 18)
            };

            // Close button — minimal circle
            var closeBtn = BuildIconButton(
                "M256-200l-56-56 224-224-224-224 56-56 224 224 224-224 56 56-224 224 224 224-56 56-224-224-224 224Z",
                "#4A4A6A", "#0E0E1C", "#1C1C30", 36);
            closeBtn.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await Navigation.PopModalAsync())
            });

            // Title block
            var titleBlock = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            titleBlock.Children.Add(new Label
            {
                Text = "PAGE ACCESS",
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#00C9C9"),
                CharacterSpacing = 3.5,
                HorizontalOptions = LayoutOptions.Center
            });
            titleBlock.Children.Add(new Label
            {
                Text = _userName,
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E8EEFF"),
                HorizontalOptions = LayoutOptions.Center
            });

            // Save button — teal pill
            var saveBtn = new Border
            {
                Padding = new Thickness(18, 10),
                BackgroundColor = Color.FromArgb("#00C9C9"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = "SAVE",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#060F0F"),
                    CharacterSpacing = 1.5,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            saveBtn.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    OnSaved?.Invoke(_toggles);
                    await Navigation.PopModalAsync();
                })
            });

            navRow.Add(closeBtn, 0, 0);
            navRow.Add(titleBlock, 1, 0);
            navRow.Add(saveBtn, 2, 0);
            wrapper.Add(navRow, 0, 0);

            // ── Row 1: stats strip ──────────────────────────
            var allowed = _toggles.Count(t => t.IsAllowed);
            var denied = _toggles.Count(t => !t.IsAllowed);
            var total = _toggles.Count;

            var statsRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(1) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(1) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                Margin = new Thickness(0, 0, 0, 2)
            };

            statsRow.Add(BuildStatCell($"{total}", "TOTAL", "#7A7A9A"), 0, 0);
            statsRow.Add(new BoxView { BackgroundColor = Color.FromArgb("#1A1A2C"), WidthRequest = 1 }, 1, 0);
            statsRow.Add(BuildStatCell($"{allowed}", "ALLOWED", "#22C55E"), 2, 0);
            statsRow.Add(new BoxView { BackgroundColor = Color.FromArgb("#1A1A2C"), WidthRequest = 1 }, 3, 0);
            statsRow.Add(BuildStatCell($"{denied}", "BLOCKED", "#FF3B6F"), 4, 0);

            var statsContainer = new Border
            {
                BackgroundColor = Color.FromArgb("#0E0E1C"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#1A1A2C")),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Margin = new Thickness(0, 0, 0, 0),
                Content = statsRow
            };
            wrapper.Add(statsContainer, 0, 1);

            // ── Row 2: divider ──────────────────────────────
            wrapper.Add(new BoxView
            {
                BackgroundColor = Color.FromArgb("#1A1A2C"),
                HeightRequest = 1
            }, 0, 2);

            return wrapper;
        }

        private View BuildStatCell(string value, string label, string color)
        {
            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                Padding = new Thickness(0, 12)
            };
            stack.Children.Add(new Label
            {
                Text = value,
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(color),
                HorizontalOptions = LayoutOptions.Center
            });
            stack.Children.Add(new Label
            {
                Text = label,
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(color),
                CharacterSpacing = 1.8,
                Opacity = 0.65,
                HorizontalOptions = LayoutOptions.Center
            });
            return stack;
        }

        // ── Small icon-only circle button ──────────────────────────────────
        private Border BuildIconButton(
            string pathData,
            string iconColor, string bgColor, string borderColor,
            double size = 38)
        {
            var btn = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                BackgroundColor = Color.FromArgb(bgColor),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(borderColor)),
                StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Geometry)new PathGeometryConverter().ConvertFromString(pathData),
                    Fill = new SolidColorBrush(Color.FromArgb(iconColor)),
                    WidthRequest = size * 0.42,
                    HeightRequest = size * 0.42,
                    Aspect = Stretch.Uniform,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            return btn;
        }

        // ══════════════════════════════════════════════════════
        // BODY
        // ══════════════════════════════════════════════════════
        private View BuildBody()
        {
            var body = new VerticalStackLayout
            {
                Spacing = 0,
                Padding = new Thickness(0, 8, 0, 28)
            };

            body.Children.Add(BuildPresetsBar());
            body.Children.Add(new BoxView { HeightRequest = 4, Color = Colors.Transparent });

            var groups = _toggles.GroupBy(t => t.GroupName);
            foreach (var group in groups)
            {
                var colors = _groupColors.TryGetValue(group.Key, out var c)
                    ? c : ("#00C9C9", "#041212", "#082020");
                body.Children.Add(BuildGroupSection(group.Key, group.ToList(), colors));
            }

            return new ScrollView
            {
                Content = body,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                VerticalScrollBarVisibility = ScrollBarVisibility.Never
            };
        }

        // ── Group section ──────────────────────────────────────────────────
        private View BuildGroupSection(
            string groupName,
            List<PagePermissionToggleVm> toggles,
            (string Accent, string Bg, string Border) colors)
        {
            var outer = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(0, 12, 0, 0) };

            // Group label row
            var groupRow = new Grid
            {
                Padding = new Thickness(20, 0, 20, 8),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(3) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                ColumnSpacing = 10
            };

            groupRow.Add(new BoxView
            {
                Color = Color.FromArgb(colors.Accent),
                WidthRequest = 3,
                HeightRequest = 16,
                CornerRadius = 2,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            groupRow.Add(new Label
            {
                Text = groupName.ToUpper(),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(colors.Accent),
                CharacterSpacing = 2.5,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            groupRow.Add(new Border
            {
                BackgroundColor = Color.FromArgb(colors.Bg),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(colors.Border)),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(8, 3),
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = $"{toggles.Count} pages",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(colors.Accent)
                }
            }, 2, 0);

            outer.Children.Add(groupRow);

            // Card
            var cardStack = new VerticalStackLayout { Spacing = 0 };
            for (int i = 0; i < toggles.Count; i++)
            {
                if (i > 0)
                    cardStack.Children.Add(new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#111120"),
                        Margin = new Thickness(72, 0, 16, 0)
                    });

                cardStack.Children.Add(BuildToggleRow(toggles[i], colors.Accent));
            }

            outer.Children.Add(new Border
            {
                Margin = new Thickness(16, 0),
                BackgroundColor = Color.FromArgb("#0B0B18"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(colors.Border)),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Content = cardStack
            });

            return outer;
        }

        // ── Single toggle row ──────────────────────────────────────────────
        private View BuildToggleRow(PagePermissionToggleVm toggle, string groupAccent)
        {
            // ── Icon circle ──────────────────────────────────────────────
            var iconPath = new Microsoft.Maui.Controls.Shapes.Path
            {
                WidthRequest = 20,
                HeightRequest = 20,
                Aspect = Stretch.Uniform,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var iconCircle = new Border
            {
                WidthRequest = 44,
                HeightRequest = 44,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = iconPath
            };

            // ── Text ─────────────────────────────────────────────────────
            var nameLabel = new Label
            {
                Text = toggle.DisplayName,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            var descLabel = new Label
            {
                Text = toggle.Description,
                FontSize = 11,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            var textStack = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center
            };
            textStack.Children.Add(nameLabel);
            textStack.Children.Add(descLabel);

            // ── Custom toggle track ───────────────────────────────────────
            // We draw our own toggle so it never clips.
            // Track background
            var track = new Border
            {
                WidthRequest = 48,
                HeightRequest = 26,
                StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = 13 },
                HorizontalOptions = LayoutOptions.Center
            };

            // Thumb — uses a Path icon inside a circle
            var thumbIconPath = new Microsoft.Maui.Controls.Shapes.Path
            {
                WidthRequest = 11,
                HeightRequest = 11,
                Aspect = Stretch.Uniform,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            var thumb = new Border
            {
                WidthRequest = 20,
                HeightRequest = 20,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = thumbIconPath,
                VerticalOptions = LayoutOptions.Center
            };

            // Track inner layout (thumb slides left/right via margin)
            var trackInner = new Grid
            {
                WidthRequest = 48,
                HeightRequest = 26,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            track.Content = trackInner;
            trackInner.Children.Add(thumb);

            // Status pill label
            var pillLabel = new Label
            {
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 0.8,
                HorizontalOptions = LayoutOptions.Center
            };
            var pill = new Border
            {
                Padding = new Thickness(8, 3),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Content = pillLabel,
                HorizontalOptions = LayoutOptions.Center
            };

            var rightStack = new VerticalStackLayout
            {
                Spacing = 6,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End,
                MinimumWidthRequest = 68
            };
            rightStack.Children.Add(pill);
            rightStack.Children.Add(track);

            // ── Refresh visuals ───────────────────────────────────────────
            void Refresh()
            {
                bool ok = toggle.IsAllowed;

                // Icon circle
                iconCircle.BackgroundColor = ok
                    ? Color.FromArgb("#051A0A") : Color.FromArgb("#1A0505");
                iconCircle.Stroke = new SolidColorBrush(ok
                    ? Color.FromArgb("#0A3012") : Color.FromArgb("#301010"));

                // Page icon (SVG path data from the toggle vm)
                if (!string.IsNullOrEmpty(toggle.Icon))
                {
                    try
                    {
                        iconPath.Data = (Geometry)new PathGeometryConverter()
                                            .ConvertFromString(toggle.Icon);
                    }
                    catch { /* fallback: leave blank */ }
                }
                iconPath.Fill = new SolidColorBrush(ok
                    ? Color.FromArgb("#22C55E") : Color.FromArgb("#FF3B6F"));

                // Name / desc colours
                nameLabel.TextColor = ok
                    ? Color.FromArgb("#E8EEFF") : Color.FromArgb("#4A4A5A");
                descLabel.TextColor = ok
                    ? Color.FromArgb("#6B7280") : Color.FromArgb("#2A2A35");

                // Pill
                pill.BackgroundColor = ok
                    ? Color.FromArgb("#051A08") : Color.FromArgb("#1A0505");
                pill.Stroke = new SolidColorBrush(ok
                    ? Color.FromArgb("#22C55E") : Color.FromArgb("#FF3B6F"));
                pillLabel.TextColor = ok
                    ? Color.FromArgb("#22C55E") : Color.FromArgb("#FF3B6F");
                pillLabel.Text = ok ? "ALLOWED" : "BLOCKED";

                // Track
                track.BackgroundColor = ok
                    ? Color.FromArgb("#052A10") : Color.FromArgb("#2A0508");
                track.Stroke = new SolidColorBrush(ok
                    ? Color.FromArgb("#22C55E") : Color.FromArgb("#FF3B6F"));

                // Thumb position: right = allowed, left = blocked
                thumb.Margin = ok
                    ? new Thickness(24, 3, 3, 3)
                    : new Thickness(3, 3, 24, 3);
                thumb.BackgroundColor = ok
                    ? Color.FromArgb("#22C55E") : Color.FromArgb("#FF3B6F");

                // Thumb icon: check or block
                try
                {
                    thumbIconPath.Data = (Geometry)new PathGeometryConverter()
                        .ConvertFromString(ok ? IC_CHECK_CIRCLE : IC_BLOCK);
                }
                catch { }
                thumbIconPath.Fill = new SolidColorBrush(ok
                    ? Color.FromArgb("#030D05") : Color.FromArgb("#0D0303"));
            }

            Refresh();

            // ── Tap whole row ─────────────────────────────────────────────
            var rowWrapper = new Border
            {
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Padding = new Thickness(16, 14)
            };

            var rowGrid = new Grid
            {
                ColumnSpacing = 14,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(46) },
                    new ColumnDefinition { Width = GridLength.Star   },
                    new ColumnDefinition { Width = GridLength.Auto   },
                }
            };
            rowGrid.Add(iconCircle, 0, 0);
            rowGrid.Add(textStack, 1, 0);
            rowGrid.Add(rightStack, 2, 0);
            rowWrapper.Content = rowGrid;

            rowWrapper.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    toggle.IsAllowed = !toggle.IsAllowed;
                    Refresh();
                })
            });

            // Also tap the track itself
            trackInner.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    toggle.IsAllowed = !toggle.IsAllowed;
                    Refresh();
                })
            });

            return rowWrapper;
        }

        // ══════════════════════════════════════════════════════
        // PRESETS BAR
        // ══════════════════════════════════════════════════════
        private View BuildPresetsBar()
        {
            // SVG paths for preset chip icons
            const string IC_ALLOW_ALL =
                "M438-240 296-382l58-58 84 84 168-168 58 58-226 226ZM480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Z";
            const string IC_CORE_ONLY =
                "M480-80q-139-35-229.5-159.5T160-516v-244l320-120 320 120v244q0 85-29 163t-80.5 139T480-80Zm0-84q97-30 162-118.5T718-480H480v-316l-240 90v206q0 7 .5 14t1.5 14h238v308Z";
            const string IC_BLOCK_ADMIN =
                "M480-80q-139-35-229.5-159.5T160-516v-244l320-120 320 120v244q0 85-29 163t-80.5 139T480-80Zm-40-320v-240h-80v240h80Zm80 160v-80h-80v80h80Z";
            const string IC_BLOCK_ALL =
                "M480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80ZM280-440h400v-80H280v80Z";

            var container = new Border
            {
                Margin = new Thickness(16, 10, 16, 4),
                BackgroundColor = Color.FromArgb("#0C0C1A"),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb("#181828")),
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(14, 12)
            };

            var inner = new VerticalStackLayout { Spacing = 10 };
            inner.Children.Add(new Label
            {
                Text = "QUICK PRESETS",
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3A3A5C"),
                CharacterSpacing = 2.2
            });

            var chips = new HorizontalStackLayout { Spacing = 8 };

            chips.Children.Add(BuildPresetChip(IC_ALLOW_ALL, "Allow All", "#22C55E", "#052A0A", "#0A3A10",
                () => { foreach (var t in _toggles) t.IsAllowed = true; }));
            chips.Children.Add(BuildPresetChip(IC_CORE_ONLY, "Core Only", "#00C9C9", "#041212", "#082020",
                () => { foreach (var t in _toggles) t.IsAllowed = t.GroupName == "Core"; }));
            chips.Children.Add(BuildPresetChip(IC_BLOCK_ADMIN, "Block Admin", "#F87171", "#140404", "#241010",
                () => { foreach (var t in _toggles) t.IsAllowed = t.GroupName != "Admin"; }));
            chips.Children.Add(BuildPresetChip(IC_BLOCK_ALL, "Block All", "#FB923C", "#140900", "#241500",
                () => { foreach (var t in _toggles) t.IsAllowed = false; }));

            inner.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = chips,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Never
            });

            container.Content = inner;
            return container;
        }

        private Border BuildPresetChip(
            string iconPath,
            string label,
            string color, string bgColor, string borderColor,
            Action action)
        {
            var iconShape = new Microsoft.Maui.Controls.Shapes.Path
            {
                WidthRequest = 14,
                HeightRequest = 14,
                Aspect = Stretch.Uniform,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            try
            {
                iconShape.Data = (Geometry)new PathGeometryConverter().ConvertFromString(iconPath);
            }
            catch { }
            iconShape.Fill = new SolidColorBrush(Color.FromArgb(color));

            var row = new HorizontalStackLayout
            {
                Spacing = 6,
                VerticalOptions = LayoutOptions.Center
            };
            row.Children.Add(iconShape);
            row.Children.Add(new Label
            {
                Text = label,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(color),
                VerticalOptions = LayoutOptions.Center
            });

            var chip = new Border
            {
                Padding = new Thickness(12, 9),
                BackgroundColor = Color.FromArgb(bgColor),
                StrokeThickness = 1,
                Stroke = new SolidColorBrush(Color.FromArgb(borderColor)),
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                Content = row
            };
            chip.GestureRecognizers.Add(
                new TapGestureRecognizer { Command = new Command(action) });
            return chip;
        }

        // ══════════════════════════════════════════════════════
        // FOOTER
        // ══════════════════════════════════════════════════════
        private View BuildFooter()
        {
            const string IC_SAVE =
                "M480-320 280-520l56-58 104 104v-326h80v326l104-104 56 58-200 200ZM240-160q-33 0-56.5-23.5T160-240v-120h80v120h480v-120h80v120q0 33-23.5 56.5T720-160H240Z";

            var divider = new BoxView
            {
                BackgroundColor = Color.FromArgb("#1A1A2C"),
                HeightRequest = 1
            };

            var saveIconShape = new Microsoft.Maui.Controls.Shapes.Path
            {
                WidthRequest = 18,
                HeightRequest = 18,
                Aspect = Stretch.Uniform,
                VerticalOptions = LayoutOptions.Center
            };
            try
            {
                saveIconShape.Data = (Geometry)new PathGeometryConverter().ConvertFromString(IC_SAVE);
            }
            catch { }
            saveIconShape.Fill = new SolidColorBrush(Color.FromArgb("#060F0F"));

            var saveBtn = new Border
            {
                HeightRequest = 54,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                BackgroundColor = Color.FromArgb("#00C9C9"),
                Content = new HorizontalStackLayout
                {
                    Spacing = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        saveIconShape,
                        new Label
                        {
                            Text             = "SAVE PERMISSIONS",
                            FontSize         = 13,
                            FontAttributes   = FontAttributes.Bold,
                            TextColor        = Color.FromArgb("#060F0F"),
                            CharacterSpacing = 1.2,
                            VerticalOptions  = LayoutOptions.Center
                        }
                    }
                }
            };
            saveBtn.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    OnSaved?.Invoke(_toggles);
                    await Navigation.PopModalAsync();
                })
            });

            var wrapper = new VerticalStackLayout
            {
                Spacing = 0,
                BackgroundColor = Color.FromArgb("#0C0C1A"),
                Padding = new Thickness(20, 14, 20, 40)
            };
            wrapper.Children.Add(divider);
            wrapper.Children.Add(new BoxView { HeightRequest = 14, Color = Colors.Transparent });
            wrapper.Children.Add(saveBtn);

            return wrapper;
        }
    }
}