using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Post
{
    public partial class StatusPrivacyContactsPage : ContentPage
    {
        public enum PrivacyType
        {
            Allowed,  // Users who can see my status
            Blocked   // Users who cannot see my status
        }

        private readonly PrivacyType _privacyType;
        private List<ContactItem> _allContacts = new();
        private List<ContactItem> _filteredContacts = new();

        public class ContactItem
        {
            public string Phone { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ProfileImage { get; set; } = string.Empty;
            public bool IsSelected { get; set; }

            public string Initials => string.IsNullOrEmpty(Name) ? "?" :
                new string(Name.Where(char.IsLetter).Take(2).ToArray()).ToUpper();

            public bool HasProfileImage => !string.IsNullOrEmpty(ProfileImage);
        }

        public StatusPrivacyContactsPage(PrivacyType privacyType)
        {
            InitializeComponent();
            _privacyType = privacyType;

            // Set title based on privacy type
            PageTitleLabel.Text = privacyType == PrivacyType.Allowed ? "Allowed Contacts" : "Blocked Contacts";

            LoadContacts();
        }

        private async void LoadContacts()
        {
            try
            {
                // Show loading indicator
                ContactsCollectionView.IsVisible = false;

                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                var contacts = new List<ContactItem>();

                // Load device contacts
                var deviceContacts = await GetDeviceContactsAsync();

                foreach (var deviceContact in deviceContacts)
                {
                    // Get the first phone number from the contact
                    var phoneNumber = deviceContact.Phones?.FirstOrDefault()?.PhoneNumber ?? string.Empty;

                    if (!string.IsNullOrEmpty(phoneNumber))
                    {
                        // Clean phone number (remove spaces, dashes, etc.)
                        var cleanPhone = new string(phoneNumber.Where(c => char.IsDigit(c)).ToArray());

                        // Check if this contact is registered on Lock
                        var registeredUser = await GetRegisteredUserAsync(cleanPhone);

                        var contact = new ContactItem
                        {
                            Phone = cleanPhone,
                            Name = string.IsNullOrEmpty(deviceContact.DisplayName) ? cleanPhone : deviceContact.DisplayName,
                            ProfileImage = registeredUser?.ProfileImagePath ?? string.Empty
                        };

                        contacts.Add(contact);
                    }
                }

                // Also add users from conversations (they might not be in device contacts)
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var conversations = await ChatRepository.GetConversationsForUserAsync(currentUserPhone);

                foreach (var conv in conversations)
                {
                    var otherPhone = conv.ParticipantA == currentUserPhone
                        ? conv.ParticipantB
                        : conv.ParticipantA;

                    if (string.IsNullOrEmpty(otherPhone)) continue;

                    // Check if already added
                    if (contacts.Any(c => c.Phone == otherPhone)) continue;

                    // Get user details
                    var user = await db.Table<User>()
                        .Where(u => u.PhoneNumber == otherPhone)
                        .FirstOrDefaultAsync();

                    var contact = new ContactItem
                    {
                        Phone = otherPhone,
                        Name = user?.Name ?? otherPhone,
                        ProfileImage = user?.ProfileImagePath ?? string.Empty
                    };

                    contacts.Add(contact);
                }

                // Remove duplicates by phone number
                contacts = contacts
                    .GroupBy(c => c.Phone)
                    .Select(g => g.First())
                    .ToList();

                // Load saved selections
                var key = _privacyType == PrivacyType.Allowed
                    ? $"status_allowed_contacts_{currentUserPhone}"
                    : $"status_blocked_contacts_{currentUserPhone}";

                var savedJson = Preferences.Get(key, string.Empty);
                var savedPhones = string.IsNullOrEmpty(savedJson)
                    ? new HashSet<string>()
                    : System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(savedJson) ?? new HashSet<string>();

                foreach (var contact in contacts)
                {
                    contact.IsSelected = savedPhones.Contains(contact.Phone);
                }

                _allContacts = contacts.OrderBy(c => c.Name).ToList();
                _filteredContacts = new List<ContactItem>(_allContacts);

                ContactsCollectionView.ItemsSource = _filteredContacts;
                ContactsCollectionView.IsVisible = true;

                // Update the selected count badge
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading contacts: {ex}");
                ContactsCollectionView.IsVisible = true;
            }
        }

        private async Task<List<Microsoft.Maui.ApplicationModel.Communication.Contact>> GetDeviceContactsAsync()
        {
            try
            {
                // Request permission first
                var permissionStatus = await Permissions.RequestAsync<Permissions.ContactsRead>();

                if (permissionStatus != PermissionStatus.Granted)
                {
                    Debug.WriteLine("Contacts permission not granted");
                    return new List<Microsoft.Maui.ApplicationModel.Communication.Contact>();
                }

                var contacts = await Microsoft.Maui.ApplicationModel.Communication.Contacts.GetAllAsync();
                return contacts.ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting device contacts: {ex}");
                return new List<Microsoft.Maui.ApplicationModel.Communication.Contact>();
            }
        }

        private async Task<User> GetRegisteredUserAsync(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber)) return null;

            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                // Try to find user by exact match
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phoneNumber)
                    .FirstOrDefaultAsync();

                return user;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting registered user: {ex}");
                return null;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private void OnClearSearch(object sender, EventArgs e)
        {
            SearchBar.Text = string.Empty;
            OnSearchTextChanged(SearchBar, new TextChangedEventArgs(string.Empty, string.Empty));
        }

        private void OnContactTapped(object sender, EventArgs e)
        {
            try
            {
                ContactItem contact = null;

                // Get the contact from different possible sources
                if (sender is TapGestureRecognizer tap && tap.CommandParameter is ContactItem tappedContact)
                {
                    contact = tappedContact;
                }
                else if (sender is VisualElement ve && ve.BindingContext is ContactItem bindingContact)
                {
                    contact = bindingContact;
                }

                if (contact == null) return;

                // Toggle the selected state
                contact.IsSelected = !contact.IsSelected;

                // Find and update the contact in the main list
                var mainContact = _allContacts.FirstOrDefault(c => c.Phone == contact.Phone);
                if (mainContact != null)
                {
                    mainContact.IsSelected = contact.IsSelected;
                }

                // Find and update the contact in filtered list
                var filteredContact = _filteredContacts.FirstOrDefault(c => c.Phone == contact.Phone);
                if (filteredContact != null)
                {
                    filteredContact.IsSelected = contact.IsSelected;
                }

                // Update the selected count badge
                UpdateSelectedCount();

                Debug.WriteLine($"Contact {contact.Name} selected: {contact.IsSelected}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnContactTapped error: {ex}");
            }
        }
        private void UpdateSelectedCount()
        {
            var selectedCount = _allContacts.Count(c => c.IsSelected);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (SelectedCountBadge != null)
                {
                    if (selectedCount > 0)
                    {
                        SelectedCountLabel.Text = selectedCount.ToString();
                        SelectedCountBadge.IsVisible = true;
                    }
                    else
                    {
                        SelectedCountBadge.IsVisible = false;
                    }
                }
            });
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = e.NewTextValue?.ToLower() ?? string.Empty;

            // Show/hide clear button
            if (ClearSearchButton != null)
            {
                ClearSearchButton.IsVisible = !string.IsNullOrEmpty(searchText);
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredContacts = new List<ContactItem>(_allContacts);
            }
            else
            {
                _filteredContacts = _allContacts
                    .Where(c => c.Name.ToLower().Contains(searchText) ||
                               c.Phone.Contains(searchText))
                    .ToList();
            }

            ContactsCollectionView.ItemsSource = _filteredContacts;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                var currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
                if (string.IsNullOrEmpty(currentUserPhone)) return;

                // Collect selected contacts
                var selectedPhones = _allContacts
                    .Where(c => c.IsSelected)
                    .Select(c => c.Phone)
                    .ToHashSet();

                // Save to preferences
                var key = _privacyType == PrivacyType.Allowed
                    ? $"status_allowed_contacts_{currentUserPhone}"
                    : $"status_blocked_contacts_{currentUserPhone}";

                var json = System.Text.Json.JsonSerializer.Serialize(selectedPhones);
                Preferences.Set(key, json);

                // Notify that status settings changed
                MessagingCenter.Send(this, "StatusSettingsChanged");

                await DisplayAlert("Saved", "Privacy settings updated", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving contacts: {ex}");
                await DisplayAlert("Error", "Could not save settings", "OK");
            }
        }
    }
}