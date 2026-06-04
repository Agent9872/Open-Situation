using CommunityToolkit.Maui.Views;
using Lock.Models;
using Lock.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lock.Chat.Services;

namespace Lock.Pages.Chat;

public partial class ContactPickerPopup : Popup, INotifyPropertyChanged
{
    private readonly string _sourceContactName;
    private readonly string _sourceContactPhone;
    private readonly string _sourceContactProfileImage;
    private readonly Func<string, string, string, Task> _onContactSelected;

    public string SourceContactName => _sourceContactName;
    public string SourceContactPhone => _sourceContactPhone;
    public string SourceContactProfileImage => _sourceContactProfileImage;

    public ObservableCollection<ContactPickerItem> AllContacts { get; set; } = new();
    public ObservableCollection<ContactPickerItem> DisplayedContacts { get; set; } = new();

    public ContactPickerPopup(
        string sourceContactName,
        string sourceContactPhone,
        string sourceContactProfileImage,
        Func<string, string, string, Task> onContactSelected)
    {
        InitializeComponent();

        _sourceContactName = sourceContactName;
        _sourceContactPhone = sourceContactPhone;
        _sourceContactProfileImage = sourceContactProfileImage ?? "default_profile.png";
        _onContactSelected = onContactSelected;

        BindingContext = this;
        LoadContacts();
    }

    private async void LoadContacts()
    {
        try
        {
            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);

            // Remove this SQLite code:
            // await DatabaseService.InitializeAsync();
            // var db = DatabaseService.GetConnection();
            // var users = await db.Table<User>()
            //     .Where(u => u.PhoneNumber != currentUserPhone &&
            //                u.PhoneNumber != _sourceContactPhone)
            //     .ToListAsync();

            // Replace with Supabase code:
            var users = await SupabaseService.GetAsync<User>("Users",
                $"PhoneNumber=neq.{Uri.EscapeDataString(currentUserPhone)}&PhoneNumber=neq.{Uri.EscapeDataString(_sourceContactPhone)}");

            AllContacts.Clear();
            foreach (var user in users)
            {
                AllContacts.Add(new ContactPickerItem
                {
                    PhoneNumber = user.PhoneNumber,
                    Name = user.Name,
                    ProfileImagePath = !string.IsNullOrEmpty(user.ProfileImagePath) && File.Exists(user.ProfileImagePath)
                        ? user.ProfileImagePath
                        : "default_profile.png"
                });
            }

            // Show all contacts
            foreach (var contact in AllContacts)
            {
                DisplayedContacts.Add(contact);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadContacts error: {ex}");
        }
    }

    private void SearchEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        DisplayedContacts.Clear();

        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            // Show all contacts
            foreach (var contact in AllContacts)
            {
                DisplayedContacts.Add(contact);
            }
        }
        else
        {
            // Filter contacts
            var filtered = AllContacts.Where(c =>
                c.Name.Contains(e.NewTextValue, StringComparison.OrdinalIgnoreCase) ||
                c.PhoneNumber.Contains(e.NewTextValue)
            ).ToList();

            foreach (var contact in filtered)
            {
                DisplayedContacts.Add(contact);
            }
        }
    }

    private async void OnContactTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ContactPickerItem contact)
        {
            await ShowShareConfirmationAsync(contact);
        }
    }

    private async void OnShareIconTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ContactPickerItem contact)
        {
            await ShowShareConfirmationAsync(contact);
        }
    }

    private async Task ShowShareConfirmationAsync(ContactPickerItem contact)
    {
        // Create a custom confirmation with contact preview
        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Share Contact",
            $"Share {_sourceContactName}'s contact card with {contact.Name}?",
            "Share",
            "Cancel"
        );

        if (confirm)
        {
            await _onContactSelected(contact.PhoneNumber, contact.Name, contact.ProfileImagePath);
            Close(contact);
        }
    }

    private async void OnShareAllTapped(object sender, EventArgs e)
    {
        if (!DisplayedContacts.Any())
        {
            await Application.Current.MainPage.DisplayAlert("No Contacts", "No contacts to share with", "OK");
            return;
        }

        bool confirmAll = await Application.Current.MainPage.DisplayAlert(
            "Share with All",
            $"Share {_sourceContactName}'s contact card with all {DisplayedContacts.Count} contacts?",
            "Share All",
            "Cancel"
        );

        if (confirmAll)
        {
            int successCount = 0;
            foreach (var contact in DisplayedContacts)
            {
                try
                {
                    await _onContactSelected(contact.PhoneNumber, contact.Name, contact.ProfileImagePath);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to share with {contact.Name}: {ex}");
                }
            }

            await Application.Current.MainPage.DisplayAlert(
                "Complete",
                $"Contact card shared with {successCount} out of {DisplayedContacts.Count} contacts",
                "OK"
            );

            Close();
        }
    }

    private void OnCloseTapped(object sender, EventArgs e) => Close();
    private void OnCancelTapped(object sender, EventArgs e) => Close();

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ContactPickerItem
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProfileImagePath { get; set; } = "default_profile.png";
}