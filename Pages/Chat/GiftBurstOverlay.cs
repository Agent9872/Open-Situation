// ============================================================
// NEW FILE: Lock/Pages/Chat/GiftBurstOverlay.cs
// ============================================================
using Lock.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public class GiftBurstOverlay : Grid
    {
        private readonly GiftDefinition _gift;
        private readonly CancellationTokenSource _cts = new();

        private BoxView _backdrop = null!;
        private ContentView _centreEmoji = null!;  // ← was Label
        private Label _nameLabel = null!;
        private Label _descLabel = null!;

        private Color AccentColor => Color.FromArgb(_gift.AnimationColor);

        public GiftBurstOverlay(GiftDefinition gift)
        {
            _gift = gift;
            Grid.SetRowSpan(this, 99);
            ZIndex = 5000;
            BackgroundColor = Colors.Transparent;
            InputTransparent = false;
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
            BuildStaticUI();
        }

        private void BuildStaticUI()
        {
            _backdrop = new BoxView
            {
                Color = Color.FromArgb("#CC000000"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Opacity = 0
            };

            var centreIconPath = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter()
                    .ConvertFromInvariantString(_gift.IconPath)!,
                Fill = new SolidColorBrush(Color.FromArgb(_gift.IconColor)),
                Aspect = Stretch.Uniform,
                WidthRequest = 90,
                HeightRequest = 90,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            _centreEmoji = new ContentView
            {
                Content = centreIconPath,
                WidthRequest = 90,
                HeightRequest = 90,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Scale = 0,
                Opacity = 0
            };

            _nameLabel = new Label
            {
                Text = _gift.Name,
                TextColor = Colors.White,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TranslationY = 80,
                Opacity = 0
            };

            _descLabel = new Label
            {
                Text = _gift.Description,
                TextColor = Color.FromArgb("#A0A0B8"),
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TranslationY = 110,
                Opacity = 0
            };

            Children.Add(_backdrop);
            Children.Add(_centreEmoji);
            Children.Add(_nameLabel);
            Children.Add(_descLabel);
        }

        public async Task RunAndRemoveAsync(Grid parentGrid)
        {
            try { await RunAsync(); }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try { parentGrid.Remove(this); } catch { }
                });
            }
        }

        private async Task RunAsync()
        {
            var token = _cts.Token;

            await _backdrop.FadeTo(1, 180, Easing.CubicOut);

            _centreEmoji.Opacity = 1;
            await Task.WhenAll(
                _centreEmoji.ScaleTo(1.0, 350, Easing.SpringOut),
                _centreEmoji.FadeTo(1, 200, Easing.CubicOut));

            await _centreEmoji.ScaleTo(1.18, 160, Easing.CubicOut);
            await _centreEmoji.ScaleTo(0.95, 120, Easing.CubicIn);
            await _centreEmoji.ScaleTo(1.05, 100, Easing.CubicOut);
            await _centreEmoji.ScaleTo(1.00, 80, Easing.CubicIn);

            await Task.WhenAll(
                _nameLabel.FadeTo(1, 200, Easing.CubicOut),
                _descLabel.FadeTo(1, 200, Easing.CubicOut));

            _ = SpawnParticlesAsync(token);
            _ = SpawnFloatingEmojisAsync(token);

            await Task.Delay(900);

            await Task.WhenAll(
                _backdrop.FadeTo(0, 350, Easing.CubicIn),
                _centreEmoji.FadeTo(0, 300, Easing.CubicIn),
                _nameLabel.FadeTo(0, 250, Easing.CubicIn),
                _descLabel.FadeTo(0, 200, Easing.CubicIn));

            _cts.Cancel();
        }

        private async Task SpawnParticlesAsync(CancellationToken token)
        {
            var rng = new Random();
            for (int i = 0; i < _gift.ParticleCount; i++)
            {
                if (token.IsCancellationRequested) break;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var p = new BoxView
                    {
                        Color = AccentColor,
                        CornerRadius = 4,
                        WidthRequest = rng.Next(6, 14),
                        HeightRequest = rng.Next(6, 14),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Opacity = 0.9
                    };
                    Children.Add(p);
                    double angle = rng.NextDouble() * Math.PI * 2;
                    double dist = 100 + rng.NextDouble() * 200;
                    uint dur = (uint)(600 + rng.Next(0, 400));
                    _ = Task.WhenAll(
                            p.TranslateTo(Math.Cos(angle) * dist, Math.Sin(angle) * dist, dur, Easing.CubicOut),
                            p.FadeTo(0, dur, Easing.CubicIn),
                            p.ScaleTo(0.2, dur, Easing.CubicIn))
                        .ContinueWith(_ => MainThread.BeginInvokeOnMainThread(
                            () => { try { Children.Remove(p); } catch { } }));
                });
                await Task.Delay(rng.Next(10, 40));
            }
        }

        private async Task SpawnFloatingEmojisAsync(CancellationToken token)
        {
            var rng = new Random();
            for (int i = 0; i < 12; i++)
            {
                if (token.IsCancellationRequested) break;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    int size = rng.Next(18, 40);

                    var path = new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = (Geometry)new PathGeometryConverter()
                            .ConvertFromInvariantString(_gift.IconPath)!,
                        Fill = new SolidColorBrush(Color.FromArgb(_gift.IconColor)),
                        Aspect = Stretch.Uniform,
                        WidthRequest = size,
                        HeightRequest = size,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };

                    var f = new ContentView
                    {
                        Content = path,
                        WidthRequest = size,
                        HeightRequest = size,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        TranslationX = rng.Next(-160, 160),
                        TranslationY = rng.Next(-20, 80),
                        Opacity = 0,
                        Scale = 0.4
                    };

                    Children.Add(f);

                    double targetY = f.TranslationY - (180 + rng.NextDouble() * 120);
                    uint dur = (uint)(900 + rng.Next(0, 400));

                    _ = Task.WhenAll(
                            f.FadeTo(0.85, 200, Easing.CubicOut),
                            f.ScaleTo(1.0, 200, Easing.SpringOut))
                        .ContinueWith(async _ =>
                        {
                            await Task.WhenAll(
                                f.TranslateTo(f.TranslationX, targetY, dur, Easing.CubicOut),
                                f.FadeTo(0, (uint)(dur * 0.7), Easing.CubicIn));

                            MainThread.BeginInvokeOnMainThread(
                                () => { try { Children.Remove(f); } catch { } });
                        });
                });
                await Task.Delay(rng.Next(50, 120));
            }
        }
    }
}