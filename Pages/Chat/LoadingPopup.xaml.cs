using CommunityToolkit.Maui.Views;

namespace Lock.Pages.Chat;

public partial class LoadingPopup : Popup
{
    public LoadingPopup(string message)
    {
        InitializeComponent();
        MessageLabel.Text = message;
    }
}