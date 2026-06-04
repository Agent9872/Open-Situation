// ============================================================
// NEW FILE: Lock/Pages/Chat/GiftPickerPopup.xaml.cs
// ============================================================
using CommunityToolkit.Maui.Views;
using Lock.Models;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class GiftPickerPopup : Popup
    {
        public event EventHandler<GiftDefinition>? GiftSelected;

        public GiftPickerPopup()
        {
            InitializeComponent();
        }

        private async void GiftItem_Tapped(object sender, TappedEventArgs e)
        {
            try
            {
                if (e.Parameter is not string giftId) return;
                var gift = GiftDefinition.FindById(giftId);
                if (gift == null) return;

                if (sender is Border border)
                {
                    await border.ScaleTo(0.85, 80, Easing.CubicIn);
                    await border.ScaleTo(1.10, 120, Easing.CubicOut);
                    await border.ScaleTo(1.00, 80, Easing.CubicIn);
                }

                await Task.Delay(60);
                GiftSelected?.Invoke(this, gift);
                Close(gift);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GiftPickerPopup.GiftItem_Tapped error: {ex}");
            }
        }

        private void CloseButton_Tapped(object sender, TappedEventArgs e)
            => Close(null);
    }
}