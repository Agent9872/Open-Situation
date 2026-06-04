using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lock.Services.Admin;

namespace Lock.Pages.Chat
{
    public partial class CreateGroupPage : ContentPage
    {
        private ObservableCollection<ContactInfo> _selectedMembers = new();
        private string _groupImagePath = string.Empty;
        private ObservableCollection<string> _interestTags = new();
        private string _currentUserPhone = string.Empty;
        private List<string> _customMoods = new();
        private List<string> _predefinedTags = new()
        {
            "Music", "Gaming", "Travel", "Reading", "Cooking",
            "Fitness", "Photography", "Art", "Tech", "Wellness",
            "Wine", "Coffee", "Pets", "Gardening", "Movies",
            "TV Shows", "Sports", "Basketball", "Podcasts",
            "Business", "Finance", "Learning", "Debate", "Networking"
        };

        public class ContactInfo
        {
            public string Phone { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public CreateGroupPage()
        {
            InitializeComponent();
            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            SelectedMembersView.ItemsSource = _selectedMembers;

            GroupTypePicker.SelectedIndexChanged += OnGroupTypeChanged;
            GroupTypePicker.SelectedIndex = 0;
            VisibilityPicker.SelectedIndex = 0;

            LoadCustomMoods();
            LoadPredefinedTags();
            // Remove AddTagButton() call - we'll add it only once
            UpdateSelectedMembersVisibility();
        }

        private void LoadCustomMoods()
        {
            var savedMoods = Preferences.Get("custom_moods", string.Empty);
            if (!string.IsNullOrEmpty(savedMoods))
            {
                _customMoods = savedMoods.Split('|').ToList();
                foreach (var mood in _customMoods)
                {
                    if (!MoodFilterPicker.Items.Contains(mood))
                    {
                        MoodFilterPicker.Items.Insert(MoodFilterPicker.Items.Count - 1, mood);
                    }
                }
            }
        }

        private void LoadPredefinedTags()
        {
            PredefinedTagsContainer?.Children.Clear();

            foreach (var tag in _predefinedTags)
            {
                AddPredefinedTagChip(tag);
            }
        }

        private void AddPredefinedTagChip(string tag)
        {
            var tagBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#008080"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => OnPredefinedTagTapped(tag);

            var tagLabel = new Label
            {
                Text = tag,
                TextColor = Color.FromArgb("#008080"),
                FontSize = 12
            };
            tagLabel.GestureRecognizers.Add(tap);
            tagBorder.Content = tagLabel;

            PredefinedTagsContainer.Children.Add(tagBorder);
        }

        private void OnPredefinedTagTapped(string tag)
        {
            if (!_interestTags.Contains(tag))
            {
                AddTagToContainer(tag);
                _interestTags.Add(tag);
            }
        }

        private void AddTagToContainer(string tag)
        {
            // Remove the add button temporarily
            var addBtn = TagsContainer.Children.LastOrDefault();
            if (addBtn != null) TagsContainer.Children.Remove(addBtn);

            var tagBorder = new Border
            {
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#008080"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var tagGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
        {
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Auto)
        },
                ColumnSpacing = 6
            };

            var tagLabel = new Label
            {
                Text = tag,
                TextColor = Color.FromArgb("#FFFFFF"),
                FontSize = 12
            };

            // Change this from "?" to "X"
            var removeLabel = new Label
            {
                Text = "?",
                TextColor = Color.FromArgb("#FF3B6F"),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            };

            var removeTap = new TapGestureRecognizer();
            removeTap.Tapped += (s, _) =>
            {
                TagsContainer.Children.Remove(tagBorder);
                _interestTags.Remove(tag);
                // Only add the add button back if there are no tags and it's not already there
                if (_interestTags.Count == 0 && !TagsContainer.Children.Any(c =>
                    (c as Border)?.Content is Label label && label.Text == "+ Add Tag"))
                {
                    AddTagButton();
                }
            };
            removeLabel.GestureRecognizers.Add(removeTap);

            Grid.SetColumn(tagLabel, 0);
            Grid.SetColumn(removeLabel, 1);
            tagGrid.Children.Add(tagLabel);
            tagGrid.Children.Add(removeLabel);
            tagBorder.Content = tagGrid;

            TagsContainer.Children.Add(tagBorder);

            // Only add the add button back if it was removed and we need it
            if (!TagsContainer.Children.Contains(addBtn) && addBtn != null)
            {
                TagsContainer.Children.Add(addBtn);
            }
            else if (addBtn == null)
            {
                AddTagButton();
            }
        }
        private void SaveCustomMood(string mood)
        {
            if (!_customMoods.Contains(mood))
            {
                _customMoods.Add(mood);
                Preferences.Set("custom_moods", string.Join("|", _customMoods));
            }
        }

        private void AddTagButton()
        {
            // Check if add button already exists
            bool alreadyExists = TagsContainer.Children.Any(c =>
                (c as Border)?.Content is Label label && label.Text == "+ Add Tag");

            if (alreadyExists) return;

            var border = new Border
            {
                BackgroundColor = Color.FromArgb("#2A2A2A"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#008080"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var label = new Label
            {
                Text = "+ Add Tag",
                TextColor = Color.FromArgb("#008080"),
                FontSize = 12
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnAddTagTapped;
            label.GestureRecognizers.Add(tap);
            border.Content = label;
            TagsContainer.Children.Add(border);
        }

        private void OnGroupTypeChanged(object sender, EventArgs e)
        {
            var type = GroupTypePicker.SelectedItem?.ToString() ?? string.Empty;

            // Hide all banners first
            SquadInfoBanner.IsVisible = false;
            MoodInfoBanner.IsVisible = false;
            InterestInfoBanner.IsVisible = false;
            SupportInfoBanner.IsVisible = false;
            EventInfoBanner.IsVisible = false;
            CommunityInfoBanner.IsVisible = false;
            PrivateInfoBanner.IsVisible = false;

            // Show specific UI elements based on group type
            MoodFilterGrid.IsVisible = type == "Mood Room";
            LookingForLayout.IsVisible = type == "Squad Dating";
            AnonymousModeGrid.IsVisible = type == "Support Circle";

            // Show info banners based on group type
            switch (type)
            {
                case "Squad Dating":
                    SquadInfoBanner.IsVisible = true;
                    break;
                case "Mood Room":
                    MoodInfoBanner.IsVisible = true;
                    // Populate mood options
                    if (MoodFilterPicker != null && MoodFilterPicker.Items.Count == 0)
                    {
                        var moods = new List<string>
                {
                    "Serious relationship", "Long-term potential", "Marriage minded",
                    "Life partner", "Just vibes / casual fun", "Something casual",
                     "ENM / Open to non-monogamy", "Polyamorous",
                    "Deep talks and connection", "Let's see where it goes",
                    "Networking / collabs / friends first", "Friendship only",
                    "Activity partner", "Travel buddy", "Dating but not rushing",
                    "Figuring it out", "OS (open situationship)", "Chalance (all-in effort)",
                    "Traditional relationship", "Short-term fun", "Texting/online connection",
                    "Other (Add your own)"
                };
                        foreach (var mood in moods)
                        {
                            MoodFilterPicker.Items.Add(mood);
                        }
                    }
                    break;
                case "Interest Based":
                    InterestInfoBanner.IsVisible = true;
                    break;
                case "Support Circle":
                    SupportInfoBanner.IsVisible = true;
                    break;
                case "Event Group":
                    EventInfoBanner.IsVisible = true;
                    break;
                case "Community Circle":
                    CommunityInfoBanner.IsVisible = true;
                    break;
                case "Private Group":
                    PrivateInfoBanner.IsVisible = true;
                    break;
            }
        }
        private void OnMoodFilterSelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = MoodFilterPicker.SelectedItem?.ToString();
            if (selected == "? Custom Mood")
            {
                if (CustomMoodContainer != null)
                    CustomMoodContainer.IsVisible = true;
                MoodFilterPicker.SelectedIndex = -1;
            }
            else
            {
                if (CustomMoodContainer != null)
                    CustomMoodContainer.IsVisible = false;
            }
        }

        private void OnLookingForSelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = LookingForPicker.SelectedItem?.ToString();
            if (selected == "Other (Add your own)")
            {
                CustomLookingForLayout.IsVisible = true;
                LookingForPicker.SelectedIndex = -1;
            }
            else
            {
                CustomLookingForLayout.IsVisible = false;
            }
        }

        private void OnAddCustomLookingForClicked(object sender, EventArgs e)
        {
            var customLookingFor = CustomLookingForEntry?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(customLookingFor)) return;

            if (!LookingForPicker.Items.Contains(customLookingFor))
            {
                LookingForPicker.Items.Insert(LookingForPicker.Items.Count - 1, customLookingFor);
            }

            LookingForPicker.SelectedItem = customLookingFor;
            if (CustomLookingForEntry != null)
                CustomLookingForEntry.Text = string.Empty;
            if (CustomLookingForLayout != null)
                CustomLookingForLayout.IsVisible = false;
        }


        private void OnAddCustomMoodClicked(object sender, EventArgs e)
        {
            var customMood = CustomMoodEntry?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(customMood)) return;

            if (!MoodFilterPicker.Items.Contains(customMood))
            {
                MoodFilterPicker.Items.Insert(MoodFilterPicker.Items.Count - 1, customMood);
                SaveCustomMood(customMood);
            }

            MoodFilterPicker.SelectedItem = customMood;
            if (CustomMoodEntry != null)
                CustomMoodEntry.Text = string.Empty;
            if (CustomMoodContainer != null)
                CustomMoodContainer.IsVisible = false;
        }

        private async void OnAddTagTapped(object sender, EventArgs e)
        {
            try
            {
                var tag = await DisplayPromptAsync(
                    "Add Interest Tag",
                    "e.g., Hiking, Music, Food, Dating, Gaming, Travel\n\nTap a predefined tag below to add it quickly!",
                    maxLength: 30,
                    keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(tag)) return;

                tag = tag.Trim();

                if (!_interestTags.Contains(tag))
                {
                    _interestTags.Add(tag);
                    AddTagToContainer(tag);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddTag error: {ex}");
            }
        }

        private async void OnAddImageTapped(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select group image",
                    FileTypes = FilePickerFileType.Images
                });
                if (result == null) return;

                var destName = $"group_{Guid.NewGuid():N}{System.IO.Path.GetExtension(result.FileName)}";
                var destPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, destName);

                using var src = await result.OpenReadAsync();
                using var dest = File.Open(destPath, FileMode.Create);
                await src.CopyToAsync(dest);

                _groupImagePath = destPath;
                GroupImage.Source = ImageSource.FromFile(destPath);
                GroupImage.IsVisible = true;
                CameraIcon.IsVisible = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PickImage error: {ex}");
                await DisplayAlert("Error", "Could not select image", "OK");
            }
        }

        private async void OnAddParticipantsClicked(object sender, EventArgs e)
        {
            try
            {
                var phone = await DisplayPromptAsync(
                    "Add Member",
                    "Enter phone number:",
                    keyboard: Keyboard.Telephone);

                if (string.IsNullOrWhiteSpace(phone)) return;

                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // var user = await db.Table<User>()
                //     .Where(u => u.PhoneNumber == phone.Trim())
                //     .FirstOrDefaultAsync();

                // Replace with Supabase code:
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone.Trim())}&limit=1");
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    if (_selectedMembers.Any(m => m.Phone == user.PhoneNumber))
                    {
                        await DisplayAlert("Already Added", $"{user.Name} is already in the list", "OK");
                        return;
                    }
                    _selectedMembers.Add(new ContactInfo
                    { Phone = user.PhoneNumber, Name = user.Name });
                }
                else
                {
                    var name = await DisplayPromptAsync(
                        "Not Found",
                        "User not found. Enter their name anyway:",
                        keyboard: Keyboard.Text);

                    if (!string.IsNullOrWhiteSpace(name))
                        _selectedMembers.Add(new ContactInfo
                        { Phone = phone.Trim(), Name = name.Trim() });
                }

                UpdateSelectedMembersVisibility();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddParticipants error: {ex}");
            }
        }

        private void OnRemoveMemberTapped(object sender, EventArgs e)
        {
            if (sender is TapGestureRecognizer { CommandParameter: ContactInfo contact })
            {
                _selectedMembers.Remove(contact);
                UpdateSelectedMembersVisibility();
            }
        }

        private void UpdateSelectedMembersVisibility()
        {
            if (SelectedMembersBorder != null)
            {
                SelectedMembersBorder.IsVisible = _selectedMembers.Count > 0;
            }
        }

        private async void OnCreateClicked(object sender, EventArgs e)
        {
            try
            {
                var groupName = GroupNameEntry.Text?.Trim();
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    await DisplayAlert("Error", "Please enter a group name", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(_currentUserPhone))
                {
                    await DisplayAlert("Error", "You must be logged in to create a group", "OK");
                    return;
                }

                var loadingOverlay = new Grid
                {
                    BackgroundColor = Color.FromArgb("#80000000"),
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand
                };

                var activityIndicator = new ActivityIndicator
                {
                    IsRunning = true,
                    Color = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                loadingOverlay.Children.Add(activityIndicator);
                (Content as Grid)?.Children.Add(loadingOverlay);

                var groupTypeText = GroupTypePicker.SelectedItem?.ToString() ?? "Community Circle";
                var groupType = groupTypeText switch
                {
                    "Community Circle" => GroupType.CommunityCircle,
                    "Interest Based" => GroupType.InterestBased,
                    "Squad Dating" => GroupType.SquadDating,
                    "Mood Room" => GroupType.MoodRoom,
                    "Private Group" => GroupType.PrivateGroup,
                    "Event Group" => GroupType.EventGroup,
                    "Support Circle" => GroupType.SupportCircle,
                    _ => GroupType.CommunityCircle
                };

                var visibilityText = VisibilityPicker.SelectedItem?.ToString() ?? "Public";
                var visibility = visibilityText switch
                {
                    "Private" => GroupVisibility.Private,
                    "Secret" => GroupVisibility.Secret,
                    _ => GroupVisibility.Public
                };

                Debug.WriteLine($"=== GROUP CREATION DEBUG ===");
                Debug.WriteLine($"Group Name: {groupName}");
                Debug.WriteLine($"Visibility: {visibility}");
                Debug.WriteLine($"Group Type: {groupType}");

                int.TryParse(MaxMembersEntry.Text, out int maxMembers);

                string moodFilter = string.Empty;
                if (groupType == GroupType.MoodRoom && MoodFilterPicker.SelectedIndex >= 0)
                    moodFilter = MoodFilterPicker.SelectedItem?.ToString() ?? string.Empty;

                string lookingFor = string.Empty;
                if (groupType == GroupType.SquadDating && LookingForPicker.SelectedIndex >= 0)
                    lookingFor = LookingForPicker.SelectedItem?.ToString() ?? string.Empty;

                bool anonymous = groupType == GroupType.SupportCircle && AnonymousModeSwitch.IsToggled;
                bool encrypted = EncryptionSwitch.IsToggled;
                bool requireApproval = RequireApprovalSwitch?.IsToggled ?? false;

                if (!string.IsNullOrEmpty(lookingFor))
                {
                    moodFilter = string.IsNullOrEmpty(moodFilter) ? lookingFor : $"{moodFilter} | {lookingFor}";
                }

                var group = await GroupRepository.CreateGroupAsync(
                    groupName,
                    GroupDescriptionEditor.Text?.Trim() ?? string.Empty,
                    groupType,
                    visibility,
                    _currentUserPhone,
                    _groupImagePath,
                    CategoryEntry.Text?.Trim() ?? string.Empty,
                    _interestTags.ToList(),
                    maxMembers,
                    moodFilter,
                    anonymous,
                    encrypted,
                    requireApproval);

                if (group == null)
                {
                    throw new Exception("Group creation failed - returned null");
                }

                Debug.WriteLine($"Group created successfully: {group.Id}");

                // ========== TRACK GROUP CREATION ==========
                await UserTrackingService.Instance.TrackGroupCreationAsync(group, _currentUserPhone);
                Debug.WriteLine($"[TRACKING] Group creation tracked: GroupId={group.Id}, Name={group.Name}, Creator={_currentUserPhone}");

                foreach (var contact in _selectedMembers)
                {
                    await GroupRepository.JoinGroupAsync(group.Id, contact.Phone);
                    Debug.WriteLine($"Added member: {contact.Name} ({contact.Phone})");

                    // ========== TRACK MEMBER ADDITION ==========
                    await UserTrackingService.Instance.TrackGroupMembershipAsync(
                        group.Id, group.Name, contact.Phone, "Added", _currentUserPhone);
                    Debug.WriteLine($"[TRACKING] Member added to group: {contact.Phone} added to {group.Name}");
                }

                (Content as Grid)?.Children.Remove(loadingOverlay);

                MessagingCenter.Send(this, "GroupsUpdated");
                MessagingCenter.Send(this, "ConversationsUpdated");

                var visibilityMessage = visibility == GroupVisibility.Public
                    ? "?? This group is public and will appear in Explore"
                    : visibility == GroupVisibility.Private
                        ? "?? This group is private - only invited members can join"
                        : "?? This group is secret - only members can find it";

                var tagsMessage = _interestTags.Any()
                    ? $"\n\n??? Tags: {string.Join(", ", _interestTags)}"
                    : "";

                var moodMessage = !string.IsNullOrEmpty(moodFilter)
                    ? $"\n\n?? Mood: {moodFilter}"
                    : "";

                await DisplayAlert(
                    "Group Created! ??",
                    $"'{groupName}' is ready with {_selectedMembers.Count + 1} members.\n\n{visibilityMessage}{tagsMessage}{moodMessage}",
                    "Open Group");

                await Navigation.PopModalAsync();

                var chatPage = new GroupChatPage();
                chatPage.GroupId = group.Id;

                if (Shell.Current?.Navigation != null)
                    await Shell.Current.Navigation.PushAsync(chatPage);
                else
                    await Navigation.PushAsync(chatPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnCreateClicked error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                if ((Content as Grid)?.Children.Count > 0)
                {
                    var overlay = (Content as Grid)?.Children.LastOrDefault(c => c is Grid);
                    if (overlay != null)
                        (Content as Grid)?.Children.Remove(overlay);
                }

                await DisplayAlert("Error", $"Could not create group: {ex.Message}", "OK");
            }
        }
        private async void OnCancelClicked(object sender, EventArgs e)
            => await Navigation.PopModalAsync();
    }
}