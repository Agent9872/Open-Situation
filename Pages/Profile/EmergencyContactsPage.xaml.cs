using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lock.Pages.Profile
{
    public partial class EmergencyContactsPage : ContentPage
    {
        private string _userPhone = string.Empty;
        private List<EmergencyContact> _contacts = new();

        public EmergencyContactsPage(string userPhone)
        {
            InitializeComponent();
            _userPhone = userPhone;

            // Hide navigation bar
            NavigationPage.SetHasNavigationBar(this, false);
            Shell.SetNavBarIsVisible(this, false);

            // Wire up events
            AddContactButton.Clicked += OnAddContactClicked;
            SOSButton.Clicked += OnSOSButtonClicked;

            // Load contacts
            _ = LoadContactsAsync();
        }

        private async Task LoadContactsAsync()
        {
            try
            {
                _contacts = await EmergencyContactService.GetEmergencyContactsAsync(_userPhone);
                ContactsCollectionView.ItemsSource = _contacts;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load contacts: {ex.Message}", "OK");
            }
        }

        private async void OnAddContactClicked(object sender, EventArgs e)
        {
            await ShowContactDialog(null);
        }

        private async void OnEditContactClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is EmergencyContact contact)
            {
                await ShowContactDialog(contact);
            }
        }

        private async void OnDeleteContactClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is EmergencyContact contact)
            {
                var confirm = await DisplayAlert(
                    "Delete Contact",
                    $"Are you sure you want to delete {contact.Name} from emergency contacts?",
                    "Delete",
                    "Cancel");

                if (confirm)
                {
                    bool success = await EmergencyContactService.DeleteEmergencyContactAsync(contact.Id);
                    if (success)
                    {
                        _contacts.Remove(contact);
                        ContactsCollectionView.ItemsSource = null;
                        ContactsCollectionView.ItemsSource = _contacts;
                        await DisplayAlert("Deleted", "Contact removed", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to delete contact", "OK");
                    }
                }
            }
        }

        private async void OnSOSButtonClicked(object sender, EventArgs e)
        {
            if (_contacts.Count == 0)
            {
                await DisplayAlert("No Contacts", "Please add emergency contacts first", "OK");
                return;
            }

            // Request permissions first
            bool permissionsGranted = await PermissionService.RequestSOSPermissionsAsync();

            if (!permissionsGranted)
            {
                bool retry = await DisplayAlert(
                    "Permissions Required",
                    "Lock needs location and SMS permissions to send SOS alerts. Without these, your contacts won't receive your location or message.\n\nDo you want to try again?",
                    "Retry",
                    "Cancel"
                );

                if (retry)
                {
                    permissionsGranted = await PermissionService.RequestSOSPermissionsAsync();
                }

                if (!permissionsGranted)
                {
                    await DisplayAlert("Cannot Send SOS", "SOS alert cannot be sent without the required permissions.", "OK");
                    return;
                }
            }

            // Show loading indicator
            var loadingOverlay = new Grid
            {
                BackgroundColor = Color.FromArgb("#80000000"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            var loadingActivity = new ActivityIndicator
            {
                IsRunning = true,
                Color = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var loadingLabel = new Label
            {
                Text = "Sending SOS alert...",
                TextColor = Colors.White,
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };

            loadingOverlay.Children.Add(loadingActivity);
            loadingOverlay.Children.Add(loadingLabel);

            var mainLayout = this.Content as Grid;
            if (mainLayout != null)
            {
                mainLayout.Children.Add(loadingOverlay);
            }

            try
            {
                // Get custom message from user
                string customMessage = await GetCustomSOSMessageAsync();

                // If user cancelled, exit
                if (customMessage == null)
                {
                    return;
                }

                // Show confirmation dialog with message preview
                string contactNames = string.Join(", ", _contacts.Take(3).Select(c => c.Name));
                if (_contacts.Count > 3)
                {
                    contactNames += $" and {_contacts.Count - 3} more";
                }

                bool confirm = await DisplayAlert(
                    "?? SOS Alert Confirmation",
                    $"Send emergency alert to:\n{contactNames}\n\n" +
                    $"Message: \"{customMessage}\"\n\n" +
                    $"?? This will:\n" +
                    $"1. Send an in-app SOS message to your contacts\n" +
                    $"2. Open SMS composition for you to send the alert\n\n" +
                    $"Your location will be included automatically.",
                    "Send SOS",
                    "Cancel");

                if (!confirm)
                {
                    return;
                }

                // Send the alert (in-app messages will be sent automatically, SMS composition will open)
                var result = await EmergencyContactService.SendEmergencyAlertAsync(_userPhone, _contacts, customMessage);

                // Show result
                if (result.Success)
                {
                    string successMessage = $"? SOS alert sent!\n\n";

                    if (result.SuccessfulContacts.Count > 0)
                    {
                        successMessage += "? In-app messages sent to:\n";
                        foreach (var contactResult in result.SuccessfulContacts)
                        {
                            successMessage += $"  • {contactResult.Contact.Name}\n";
                        }

                        successMessage += $"\n?? SMS composition opened for each contact to send the alert manually.";
                    }

                    if (result.FailedContacts.Count > 0)
                    {
                        successMessage += $"\n\n?? Failed to send in-app messages to:\n";
                        foreach (var contactResult in result.FailedContacts)
                        {
                            successMessage += $"  • {contactResult.Contact.Name}\n";
                        }
                    }

                    successMessage += $"\n\n?? Your current location has been included in the alert.";

                    await DisplayAlert("SOS Alert Sent", successMessage, "OK");

                    // Play a success sound/vibration
                    try
                    {
                        Vibration.Vibrate(TimeSpan.FromMilliseconds(500));
                    }
                    catch { }
                }
                else
                {
                    await DisplayAlert("Error", $"Failed to send SOS alert: {result.ErrorMessage ?? "Unknown error"}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to send SOS alert: {ex.Message}", "OK");
            }
            finally
            {
                // Remove loading overlay
                if (mainLayout != null && loadingOverlay.Parent != null)
                {
                    mainLayout.Children.Remove(loadingOverlay);
                }
            }
        }
        private async Task<string> GetCustomSOSMessageAsync()
        {
            var tcs = new TaskCompletionSource<string>();

            var messageEntry = new Editor
            {
                Text = "I need immediate assistance. Please contact me!",
                FontSize = 14,
                HeightRequest = 100,
                BackgroundColor = Color.FromArgb("#2F3337"),
                TextColor = Colors.White,
                Placeholder = "Type your emergency message...",
                PlaceholderColor = Color.FromArgb("#888888")
            };

            var locationSwitch = new Switch
            {
                IsToggled = true,
                HorizontalOptions = LayoutOptions.End
            };

            var locationGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
        {
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = GridLength.Auto }
        }
            };

            var locationLabel = new Label
            {
                Text = "Share my current location",
                FontSize = 13,
                TextColor = Color.FromArgb("#E6E6E6"),
                VerticalOptions = LayoutOptions.Center
            };

            Grid.SetColumn(locationLabel, 0);
            Grid.SetColumn(locationSwitch, 1);
            locationGrid.Children.Add(locationLabel);
            locationGrid.Children.Add(locationSwitch);

            var sendButton = new Button
            {
                Text = "SEND SOS ALERT",
                BackgroundColor = Color.FromArgb("#C05050"),
                TextColor = Colors.White,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 50,
                CornerRadius = 25,
                Margin = new Thickness(20, 10, 20, 10)
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#333333"),
                TextColor = Color.FromArgb("#AAAAAA"),
                FontSize = 14,
                HeightRequest = 44,
                CornerRadius = 22,
                Margin = new Thickness(20, 0, 20, 20)
            };

            var contentLayout = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
        {
            new Label
            {
                Text = "?? EMERGENCY SOS ??",
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#C05050"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 0, 10)
            },

            new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#333333")
            },

            new Label
            {
                Text = "Custom Message",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E6E6E6")
            },

            messageEntry,

            new Label
            {
                Text = "This message will be sent to your emergency contacts along with your location and timestamp.",
                FontSize = 11,
                TextColor = Color.FromArgb("#AAAAAA"),
                LineBreakMode = LineBreakMode.WordWrap
            },

            new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#333333")
            },

            new Label
            {
                Text = "Location Sharing",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E6E6E6")
            },

            locationGrid,

            new Label
            {
                Text = "Your location will be included in the SOS alert to help contacts find you.",
                FontSize = 11,
                TextColor = Color.FromArgb("#AAAAAA"),
                LineBreakMode = LineBreakMode.WordWrap
            },

            new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = Color.FromArgb("#333333")
            },

            new Label
            {
                Text = "? Important",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFA500")
            },

            new Label
            {
                Text = "• This alert will be sent via in-app message to your emergency contacts\n" +
                       "• Your contacts will receive your message, location, and timestamp\n" +
                       "• SMS will also be opened for contacts outside the app\n" +
                       "• Only use this in genuine emergencies",
                FontSize = 11,
                TextColor = Color.FromArgb("#AAAAAA"),
                LineBreakMode = LineBreakMode.WordWrap
            },

            sendButton,
            cancelButton
        }
            };

            var customMessagePage = new ContentPage
            {
                Title = "Emergency SOS",
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                Content = new ScrollView
                {
                    Content = contentLayout
                }
            };

            sendButton.Clicked += async (s, args) =>
            {
                string message = messageEntry.Text?.Trim();
                if (string.IsNullOrEmpty(message))
                {
                    message = "I need immediate assistance. Please contact me!";
                }

                tcs.TrySetResult(message);
                await Navigation.PopModalAsync();
            };

            cancelButton.Clicked += async (s, args) =>
            {
                tcs.TrySetResult(null);
                await Navigation.PopModalAsync();
            };

            await Navigation.PushModalAsync(new NavigationPage(customMessagePage)
            {
                BarBackgroundColor = Color.FromArgb("#1E1E1E"),
                BarTextColor = Colors.White
            });

            return await tcs.Task;
        }

        private async Task ShowContactDialog(EmergencyContact? existingContact)
        {
            string title = existingContact == null ? "Add Emergency Contact" : "Edit Emergency Contact";

            // Create input fields
            var nameEntry = new Entry
            {
                Text = existingContact?.Name ?? "",
                Placeholder = "Full name",
                FontSize = 14
            };

            var phoneEntry = new Entry
            {
                Text = existingContact?.PhoneNumber ?? "",
                Placeholder = "Phone number",
                Keyboard = Keyboard.Telephone,
                FontSize = 14
            };

            var relationshipEntry = new Entry
            {
                Text = existingContact?.Relationship ?? "",
                Placeholder = "Relationship (e.g., Parent, Sibling, Friend)",
                FontSize = 14
            };

            var notesEntry = new Entry
            {
                Text = existingContact?.Notes ?? "",
                Placeholder = "Notes (optional)",
                FontSize = 14
            };

            var primarySwitch = new Switch
            {
                IsToggled = existingContact?.IsPrimary ?? false
            };

            // Create the dialog content
            var contentLayout = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12
            };

            contentLayout.Children.Add(new Label
            {
                Text = "Name",
                FontSize = 12,
                TextColor = Color.FromArgb("#888880")
            });
            contentLayout.Children.Add(nameEntry);

            contentLayout.Children.Add(new Label
            {
                Text = "Phone Number",
                FontSize = 12,
                TextColor = Color.FromArgb("#888880")
            });
            contentLayout.Children.Add(phoneEntry);

            contentLayout.Children.Add(new Label
            {
                Text = "Relationship",
                FontSize = 12,
                TextColor = Color.FromArgb("#888880")
            });
            contentLayout.Children.Add(relationshipEntry);

            contentLayout.Children.Add(new Label
            {
                Text = "Notes",
                FontSize = 12,
                TextColor = Color.FromArgb("#888880")
            });
            contentLayout.Children.Add(notesEntry);

            var primaryLayout = new HorizontalStackLayout
            {
                Spacing = 10
            };
            primaryLayout.Children.Add(new Label
            {
                Text = "Set as Primary Contact",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            });
            primaryLayout.Children.Add(primarySwitch);
            contentLayout.Children.Add(primaryLayout);

            var scrollView = new ScrollView
            {
                Orientation = ScrollOrientation.Vertical,
                Content = contentLayout
            };

            var page = new ContentPage
            {
                Title = title,
                Content = scrollView
            };

            // Create buttons
            var saveButton = new Button
            {
                Text = "Save",
                BackgroundColor = Color.FromArgb("#C05050"),
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 10)
            };

            saveButton.Clicked += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameEntry.Text))
                {
                    await page.DisplayAlert("Error", "Please enter a name", "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(phoneEntry.Text))
                {
                    await page.DisplayAlert("Error", "Please enter a phone number", "OK");
                    return;
                }

                try
                {
                    if (existingContact == null)
                    {
                        var newContact = await EmergencyContactService.AddEmergencyContactAsync(
                            _userPhone,
                            nameEntry.Text.Trim(),
                            phoneEntry.Text.Trim(),
                            relationshipEntry.Text.Trim(),
                            primarySwitch.IsToggled,
                            notesEntry.Text?.Trim());

                        if (newContact != null)
                        {
                            _contacts.Add(newContact);
                        }
                    }
                    else
                    {
                        existingContact.Name = nameEntry.Text.Trim();
                        existingContact.PhoneNumber = phoneEntry.Text.Trim();
                        existingContact.Relationship = relationshipEntry.Text.Trim();
                        existingContact.Notes = notesEntry.Text?.Trim();
                        existingContact.IsPrimary = primarySwitch.IsToggled;

                        await EmergencyContactService.UpdateEmergencyContactAsync(existingContact);
                    }

                    // Refresh the list
                    _contacts = await EmergencyContactService.GetEmergencyContactsAsync(_userPhone);
                    ContactsCollectionView.ItemsSource = null;
                    ContactsCollectionView.ItemsSource = _contacts;

                    await Navigation.PopModalAsync();
                }
                catch (Exception ex)
                {
                    await page.DisplayAlert("Error", ex.Message, "OK");
                }
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Colors.Gray,
                TextColor = Colors.White,
                FontSize = 14,
                HeightRequest = 40,
                Margin = new Thickness(20, 0, 20, 20)
            };

            cancelButton.Clicked += async (s, args) => await Navigation.PopModalAsync();

            // Add buttons to the content
            contentLayout.Children.Add(saveButton);
            contentLayout.Children.Add(cancelButton);

            await Navigation.PushModalAsync(new NavigationPage(page));
        }
    }
}