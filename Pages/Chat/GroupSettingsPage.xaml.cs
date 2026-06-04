using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class GroupSettingsPage : ContentPage
    {
        private readonly string _groupId;
        private readonly string _currentUserPhone;
        private Group? _group;
        private bool _isCreator;
        private bool _isAdmin;
        private string _groupImagePath = string.Empty;
        private ObservableCollection<string> _interestTags = new();
        private List<string> _customMoods = new();
        private bool _hasChanges = false;
        private bool _isSaving = false;
        private bool _isLoading = true;

        public GroupSettingsPage(string groupId, string currentUserPhone)
        {
            InitializeComponent();
            _groupId = groupId;
            _currentUserPhone = currentUserPhone;
            Shell.SetNavBarIsVisible(this, false);

            // Wire change events
            GroupNameEntry.TextChanged += OnFieldChanged;
            GroupDescriptionEditor.TextChanged += OnFieldChanged;
            GroupTypePicker.SelectedIndexChanged += OnFieldChanged;
            VisibilityPicker.SelectedIndexChanged += OnFieldChanged;
            CategoryEntry.TextChanged += OnFieldChanged;
            MaxMembersEntry.TextChanged += OnFieldChanged;
            MoodFilterPicker.SelectedIndexChanged += OnFieldChanged;
            AnonymousModeSwitch.Toggled += OnFieldChanged;
            EncryptionSwitch.Toggled += OnFieldChanged;
            RequireApprovalSwitch.Toggled += OnFieldChanged;

            LoadGroupDetails();
            LoadCustomMoods();
        }

        // ?? Change tracking ??????????????????????????????????????????????????

        private void OnFieldChanged(object? sender, EventArgs e)
        {
            if (_isLoading) return;
            _hasChanges = true;
            SaveFrame.IsVisible = true;
        }

        // ?? Load ?????????????????????????????????????????????????????????????

        private async void LoadGroupDetails()
        {
            try
            {
                _isLoading = true;

                await GroupDatabaseService.InitializeAsync();
                _group = await GroupRepository.GetGroupAsync(_groupId);

                if (_group == null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DisplayAlert("Error", "Group not found", "OK");
                        await Navigation.PopAsync();
                    });
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    // Subtitle
                    GroupSubtitleLabel.Text = _group.GroupTypeDisplay;

                    // Basic
                    GroupNameEntry.Text = _group.Name;
                    GroupDescriptionEditor.Text = _group.Description;
                    CategoryEntry.Text = _group.Category;
                    MaxMembersEntry.Text = _group.MaxMembers > 0
                        ? _group.MaxMembers.ToString() : "0";

                    // Image
                    if (!string.IsNullOrEmpty(_group.CoverImagePath) && File.Exists(_group.CoverImagePath))
                    {
                        _groupImagePath = _group.CoverImagePath;
                        GroupImage.Source = ImageSource.FromFile(_group.CoverImagePath);
                        GroupImage.IsVisible = true;
                        CameraIcon.IsVisible = false;
                    }

                    // Group type picker
                    GroupTypePicker.SelectedIndex = _group.GroupType switch
                    {
                        GroupType.CommunityCircle => 0,
                        GroupType.InterestBased => 1,
                        GroupType.SquadDating => 2,
                        GroupType.MoodRoom => 3,
                        GroupType.PrivateGroup => 4,
                        GroupType.EventGroup => 5,
                        GroupType.SupportCircle => 6,
                        _ => 0
                    };

                    // Visibility picker
                    VisibilityPicker.SelectedIndex = _group.Visibility switch
                    {
                        GroupVisibility.Public => 0,
                        GroupVisibility.Private => 1,
                        GroupVisibility.Secret => 2,
                        _ => 0
                    };

                    // Mood filter
                    if (_group.GroupType == GroupType.MoodRoom &&
                        !string.IsNullOrEmpty(_group.MoodFilter))
                    {
                        if (MoodFilterPicker.Items.Contains(_group.MoodFilter))
                            MoodFilterPicker.SelectedItem = _group.MoodFilter;
                        else
                        {
                            MoodFilterPicker.Items.Insert(
                                MoodFilterPicker.Items.Count - 1, _group.MoodFilter);
                            MoodFilterPicker.SelectedItem = _group.MoodFilter;
                        }
                    }

                    // Switches
                    AnonymousModeSwitch.IsToggled = _group.IsAnonymousAllowed;
                    EncryptionSwitch.IsToggled = _group.IsEncrypted;
                    RequireApprovalSwitch.IsToggled = _group.RequireApproval;

                    // Interest tags
                    _interestTags = new ObservableCollection<string>(_group.InterestTags);
                    LoadInterestTags();

                    // Conditional UI
                    UpdateConditionalUI();
                });

                // Members + permissions
                var members = await GroupRepository.GetMembersAsync(_groupId);
                var currentMember = await GroupRepository.GetMemberAsync(_groupId, _currentUserPhone);

                _isCreator = currentMember?.Role == GroupMemberRole.Creator;
                _isAdmin = currentMember?.IsPrivileged ?? false;
                var canEdit = _isCreator || _isAdmin;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    MemberCountLabel.Text = $"{members.Count} member{(members.Count == 1 ? "" : "s")}";

                    // ?? NEW: Show Add Admin button only for creator ??
                    AddAdminFrame.IsVisible = _isCreator;

                    GroupNameEntry.IsEnabled = canEdit;
                    GroupDescriptionEditor.IsEnabled = canEdit;
                    GroupTypePicker.IsEnabled = canEdit;
                    VisibilityPicker.IsEnabled = canEdit;
                    CategoryEntry.IsEnabled = canEdit;
                    MaxMembersEntry.IsEnabled = canEdit;
                    MoodFilterPicker.IsEnabled = canEdit && _group.GroupType == GroupType.MoodRoom;
                    AnonymousModeSwitch.IsEnabled = canEdit && _group.GroupType == GroupType.SupportCircle;
                    EncryptionSwitch.IsEnabled = canEdit;
                    RequireApprovalSwitch.IsEnabled = canEdit;

                    DangerZoneLayout.IsVisible = true;
                    DeleteGroupFrame.IsVisible = _isCreator;
                    DeleteGroupButton.IsVisible = _isCreator;

                    if (!canEdit)
                        SaveFrame.IsVisible = false;

                    // ?? NEW: Load admins list ??
                    await LoadAdminsAsync();
                });

                _isLoading = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGroupDetails error: {ex}");
                _isLoading = false;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", "Could not load group details", "OK");
                    await Navigation.PopAsync();
                });
            }
        }

        private async Task LoadAdminsAsync()
        {
            try
            {
                var members = await GroupRepository.GetMembersAsync(_groupId);
                var admins = members
                    .Where(m => m.Role == GroupMemberRole.Creator ||
                                m.Role == GroupMemberRole.Admin)
                    .OrderBy(m => m.Role)
                    .ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AdminsContainer.Children.Clear();

                    foreach (var admin in admins)
                    {
                        var row = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                            ColumnSpacing = 12
                        };

                        // Avatar
                        var avatar = new Border
                        {
                            WidthRequest = 40,
                            HeightRequest = 40,
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = 20 },
                            BackgroundColor = Color.FromArgb("#1E1E1E"),
                            Content = new Label
                            {
                                Text = admin.UserName?.Length > 0
                                    ? admin.UserName[0].ToString().ToUpper()
                                    : "?",
                                TextColor = Color.FromArgb("#008080"),
                                FontSize = 16,
                                FontAttributes = FontAttributes.Bold,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            }
                        };

                        if (!string.IsNullOrEmpty(admin.UserProfileImagePath) &&
                            File.Exists(admin.UserProfileImagePath))
                        {
                            avatar.Content = new Image
                            {
                                Source = ImageSource.FromFile(admin.UserProfileImagePath),
                                Aspect = Aspect.AspectFill
                            };
                        }

                        Grid.SetColumn(avatar, 0);
                        row.Children.Add(avatar);

                        // Name + role
                        var nameStack = new VerticalStackLayout
                        {
                            Spacing = 2,
                            VerticalOptions = LayoutOptions.Center
                        };

                        nameStack.Children.Add(new Label
                        {
                            Text = admin.UserPhone == _currentUserPhone
                                ? $"{admin.UserName} (You)"
                                : admin.UserName,
                            FontSize = 14,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#FFFFFF")
                        });

                        var (roleText, roleColor) = admin.Role switch
                        {
                            GroupMemberRole.Creator => ("?? Creator", "#FFD700"),
                            _ => ("? Admin", "#008080")
                        };

                        nameStack.Children.Add(new Label
                        {
                            Text = roleText,
                            FontSize = 11,
                            TextColor = Color.FromArgb(roleColor)
                        });

                        Grid.SetColumn(nameStack, 1);
                        row.Children.Add(nameStack);

                        // Remove button — only creator can remove admins,
                        // and cannot remove themselves (creator)
                        bool canRemove = _isCreator &&
                                         admin.Role != GroupMemberRole.Creator &&
                                         admin.UserPhone != _currentUserPhone;

                        if (canRemove)
                        {
                            var removeBtn = new Border
                            {
                                StrokeThickness = 1,
                                Stroke = Color.FromArgb("#FF3B6F"),
                                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                                BackgroundColor = Colors.Transparent,
                                Padding = new Thickness(10, 4),
                                VerticalOptions = LayoutOptions.Center,
                                Content = new Label
                                {
                                    Text = "Remove",
                                    FontSize = 11,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#FF3B6F")
                                }
                            };

                            var tap = new TapGestureRecognizer();
                            var capturedAdmin = admin;
                            tap.Tapped += async (s, e) =>
                                await RemoveAdminAsync(capturedAdmin);
                            removeBtn.GestureRecognizers.Add(tap);

                            Grid.SetColumn(removeBtn, 2);
                            row.Children.Add(removeBtn);
                        }

                        AdminsContainer.Children.Add(row);

                        // Divider
                        if (admins.IndexOf(admin) < admins.Count - 1)
                        {
                            AdminsContainer.Children.Add(new BoxView
                            {
                                HeightRequest = 1,
                                BackgroundColor = Color.FromArgb("#1E1E1E")
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAdminsAsync error: {ex}");
            }
        }

        private async void OnAddAdminClicked(object sender, EventArgs e)
        {
            try
            {
                // Only creator can add admins
                if (!_isCreator)
                {
                    await DisplayAlert("Permission Denied",
                        "Only the group creator can add admins.", "OK");
                    return;
                }

                var members = await GroupRepository.GetMembersAsync(_groupId);

                // Only show regular members (not already admins/creator)
                var regularMembers = members
                    .Where(m => m.Role == GroupMemberRole.Member &&
                                m.UserPhone != _currentUserPhone)
                    .ToList();

                if (!regularMembers.Any())
                {
                    await DisplayAlert("No Members",
                        "There are no regular members to promote.", "OK");
                    return;
                }

                var options = regularMembers
                    .Select(m => m.UserName)
                    .ToArray();

                var selected = await DisplayActionSheet(
                    "Promote to Admin", "Cancel", null, options);

                if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

                var target = regularMembers.First(m => m.UserName == selected);

                bool confirm = await DisplayAlert(
                    "Add Admin",
                    $"Promote {target.UserName} to admin? They will be able to manage members and messages.",
                    "Promote", "Cancel");

                if (!confirm) return;

                bool success = await GroupRepository.PromoteMemberAsync(
                    _groupId, _currentUserPhone,
                    target.UserPhone, GroupMemberRole.Admin);

                if (success)
                {
                    await DisplayAlert("Done",
                        $"{target.UserName} is now an admin.", "OK");
                    await LoadAdminsAsync();
                }
                else
                {
                    await DisplayAlert("Error",
                        "Could not promote member.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAddAdminClicked error: {ex}");
                await DisplayAlert("Error", "Could not add admin.", "OK");
            }
        }

        private async Task RemoveAdminAsync(GroupMember admin)
        {
            try
            {
                // Double-check: only creator can remove admins
                if (!_isCreator)
                {
                    await DisplayAlert("Permission Denied",
                        "Only the group creator can remove admins.", "OK");
                    return;
                }

                // Cannot remove the creator
                if (admin.Role == GroupMemberRole.Creator)
                {
                    await DisplayAlert("Cannot Remove",
                        "The group creator cannot be removed as admin.", "OK");
                    return;
                }

                bool confirm = await DisplayAlert(
                    "Remove Admin",
                    $"Remove {admin.UserName} as admin? They will remain a regular member.",
                    "Remove", "Cancel");

                if (!confirm) return;

                // Remove this SQLite code:
                // var db = GroupDatabaseService.GetConnection();
                // admin.Role = GroupMemberRole.Member;
                // await db.UpdateAsync(admin);

                // Replace with Supabase code:
                admin.Role = GroupMemberRole.Member;
                await SupabaseService.UpdateAsync("GroupMembers",
                    $"GroupId=eq.{Uri.EscapeDataString(admin.GroupId)}&UserPhone=eq.{Uri.EscapeDataString(admin.UserPhone)}",
                    admin);

                await DisplayAlert("Done",
                    $"{admin.UserName} is no longer an admin.", "OK");

                await LoadAdminsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemoveAdminAsync error: {ex}");
                await DisplayAlert("Error", "Could not remove admin.", "OK");
            }
        }

        // ?? Conditional cards ????????????????????????????????????????????????

        private void UpdateConditionalUI()
        {
            var type = GroupTypePicker.SelectedItem?.ToString() ?? string.Empty;

            MoodFilterCard.IsVisible = type == "Mood Room";
            AnonymousModeGrid.IsVisible = type == "Support Circle";
            AnonymousDivider.IsVisible = type == "Support Circle";
            SquadInfoBanner.IsVisible = type == "Squad Dating";
        }

        private void OnGroupTypeChanged(object sender, EventArgs e)
        {
            UpdateConditionalUI();
            OnFieldChanged(sender, e);
        }

        // ?? Interest Tags ????????????????????????????????????????????????????

        private void LoadInterestTags()
        {
            TagsContainer.Children.Clear();
            foreach (var tag in _interestTags)
                AddTagToContainer(tag);
            AddTagButton();
        }

        private void AddTagToContainer(string tag)
        {
            // Same teal pill as MatchPage CommonInterests
            var pill = new Border
            {
                BackgroundColor = Color.FromArgb("#0D2626"),
                StrokeThickness = 0.5f,
                Stroke = Color.FromArgb("#008080"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(10, 5),
                Margin = new Thickness(0, 0, 8, 8)
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 6
            };

            var label = new Label
            {
                Text = $"#{tag}",
                TextColor = Color.FromArgb("#008080"),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };

            var removeLabel = new Label
            {
                Text = "  ?",
                TextColor = Color.FromArgb("#FF3B6F"),
                FontSize = 11,
                IsVisible = _isCreator || _isAdmin,
                VerticalOptions = LayoutOptions.Center
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                TagsContainer.Children.Remove(pill);
                _interestTags.Remove(tag);
                _hasChanges = true;
                SaveFrame.IsVisible = true;
            };
            removeLabel.GestureRecognizers.Add(tap);

            Grid.SetColumn(label, 0);
            Grid.SetColumn(removeLabel, 1);
            grid.Children.Add(label);
            grid.Children.Add(removeLabel);
            pill.Content = grid;
            TagsContainer.Children.Add(pill);
        }

        private void AddTagButton()
        {
            if (!(_isCreator || _isAdmin)) return;

            var btn = new Border
            {
                BackgroundColor = Color.FromArgb("#1E1E1E"),
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#2E2E2E"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(12, 5),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var lbl = new Label
            {
                Text = "+ Add Tag",
                TextColor = Color.FromArgb("#008080"),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnAddTagTapped;
            lbl.GestureRecognizers.Add(tap);
            btn.Content = lbl;
            TagsContainer.Children.Add(btn);
        }

        private async void OnAddTagTapped(object sender, EventArgs e)
        {
            var tag = await DisplayPromptAsync(
                "Add Interest Tag",
                "e.g. Hiking, Music, Food",
                maxLength: 20,
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(tag)) return;
            tag = tag.Trim();

            if (!_interestTags.Contains(tag))
            {
                _interestTags.Add(tag);
                await MainThread.InvokeOnMainThreadAsync(() => AddTagToContainer(tag));
                _hasChanges = true;
                SaveFrame.IsVisible = true;
            }
        }

        // ?? Mood ?????????????????????????????????????????????????????????????

        private void LoadCustomMoods()
        {
            var saved = Preferences.Get("custom_moods", string.Empty);
            if (string.IsNullOrEmpty(saved)) return;

            _customMoods = saved.Split('|').ToList();
            foreach (var mood in _customMoods)
                if (!MoodFilterPicker.Items.Contains(mood))
                    MoodFilterPicker.Items.Insert(MoodFilterPicker.Items.Count - 1, mood);
        }

        private void OnMoodFilterSelectedIndexChanged(object sender, EventArgs e)
        {
            var sel = MoodFilterPicker.SelectedItem?.ToString();
            CustomMoodContainer.IsVisible = sel == "? Custom Mood";
            if (CustomMoodContainer.IsVisible)
                MoodFilterPicker.SelectedIndex = -1;
            OnFieldChanged(sender, e);
        }

        private void OnAddCustomMoodClicked(object sender, EventArgs e)
        {
            var mood = CustomMoodEntry?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(mood)) return;

            if (!MoodFilterPicker.Items.Contains(mood))
            {
                MoodFilterPicker.Items.Insert(MoodFilterPicker.Items.Count - 1, mood);
                _customMoods.Add(mood);
                Preferences.Set("custom_moods", string.Join("|", _customMoods));
            }

            MoodFilterPicker.SelectedItem = mood;
            CustomMoodEntry.Text = string.Empty;
            CustomMoodContainer.IsVisible = false;
            _hasChanges = true;
            SaveFrame.IsVisible = true;
        }

        // ?? Image ????????????????????????????????????????????????????????????

        private async void OnEditImageTapped(object sender, EventArgs e)
        {
            if (!(_isCreator || _isAdmin))
            {
                await DisplayAlert("Permission Denied", "Only admins can change the group image.", "OK");
                return;
            }

            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select group image",
                    FileTypes = FilePickerFileType.Images
                });
                if (result == null) return;

                var dest = System.IO.Path.Combine(FileSystem.AppDataDirectory,
                    $"group_{_groupId}_{Guid.NewGuid():N}{System.IO.Path.GetExtension(result.FileName)}");

                using var src = await result.OpenReadAsync();
                using var dstStream = File.Open(dest, FileMode.Create);
                await src.CopyToAsync(dstStream);

                _groupImagePath = dest;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    GroupImage.Source = ImageSource.FromFile(dest);
                    GroupImage.IsVisible = true;
                    CameraIcon.IsVisible = false;
                });

                _hasChanges = true;
                SaveFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PickImage error: {ex}");
                await DisplayAlert("Error", "Could not select image", "OK");
            }
        }

        // ?? Save ?????????????????????????????????????????????????????????????

        private async void OnSaveClicked(object sender, EventArgs e)
            => await SaveChangesAsync();

        private async Task SaveChangesAsync()
        {
            if (_isSaving || _group == null) return;
            _isSaving = true;

            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SaveButton.IsEnabled = false;
                    SaveButton.Text = "Saving…";
                });

                _group.Name = GroupNameEntry.Text?.Trim() ?? _group.Name;
                _group.Description = GroupDescriptionEditor.Text?.Trim() ?? string.Empty;
                _group.Category = CategoryEntry.Text?.Trim() ?? string.Empty;
                _group.InterestTags = _interestTags.ToList();

                if (int.TryParse(MaxMembersEntry.Text, out int max))
                    _group.MaxMembers = max;

                _group.GroupType = GroupTypePicker.SelectedItem?.ToString() switch
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

                _group.Visibility = VisibilityPicker.SelectedItem?.ToString() switch
                {
                    "?? Private" => GroupVisibility.Private,
                    "?? Secret" => GroupVisibility.Secret,
                    _ => GroupVisibility.Public
                };

                if (_group.GroupType == GroupType.MoodRoom &&
                    MoodFilterPicker.SelectedIndex >= 0)
                    _group.MoodFilter = MoodFilterPicker.SelectedItem?.ToString() ?? string.Empty;

                _group.IsAnonymousAllowed = AnonymousModeSwitch.IsToggled;
                _group.IsEncrypted = EncryptionSwitch.IsToggled;
                _group.RequireApproval = RequireApprovalSwitch.IsToggled;

                if (!string.IsNullOrEmpty(_groupImagePath) &&
                    _groupImagePath != _group.CoverImagePath)
                {
                    if (!string.IsNullOrEmpty(_group.CoverImagePath) &&
                        File.Exists(_group.CoverImagePath))
                        File.Delete(_group.CoverImagePath);
                    _group.CoverImagePath = _groupImagePath;
                }

                await GroupRepository.UpdateGroupAsync(_group);
                MessagingCenter.Send(this, "GroupsUpdated");
                MessagingCenter.Send(this, "GroupSettingsUpdated", _groupId);

                _hasChanges = false;
                await MainThread.InvokeOnMainThreadAsync(() => SaveFrame.IsVisible = false);
                await DisplayAlert("Saved ?", "Group settings updated successfully.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveChangesAsync error: {ex}");
                await DisplayAlert("Error", $"Could not save changes: {ex.Message}", "OK");
            }
            finally
            {
                _isSaving = false;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SaveButton.IsEnabled = true;
                    SaveButton.Text = "Save";
                });
            }
        }

        // ?? Members ??????????????????????????????????????????????????????????

        private async void OnViewMembersClicked(object sender, EventArgs e)
            => await Navigation.PushAsync(
                new GroupMembersPage(_groupId, _currentUserPhone));

        // ?? Leave / Delete ???????????????????????????????????????????????????

        private async void OnLeaveGroupClicked(object sender, EventArgs e)
        {
            try
            {
                var msg = _isCreator
                    ? "You are the creator. Leaving will archive (not delete) the group. Continue?"
                    : "Are you sure you want to leave this group?";

                if (!await DisplayAlert("Leave Group", msg, "Yes", "No")) return;

                if (await GroupRepository.LeaveGroupAsync(_groupId, _currentUserPhone))
                {
                    MessagingCenter.Send(this, "GroupsUpdated");
                    MessagingCenter.Send(this, "ConversationsUpdated");
                    await DisplayAlert("Done", "You left the group.", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Error", "Could not leave group.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LeaveGroup error: {ex}");
                await DisplayAlert("Error", "Could not leave group.", "OK");
            }
        }

        private async void OnDeleteGroupClicked(object sender, EventArgs e)
        {
            DeleteGroupButton.IsEnabled = false;
            try
            {
                var current = await GroupRepository.GetMemberAsync(_groupId, _currentUserPhone);
                if (current?.Role != GroupMemberRole.Creator)
                {
                    await DisplayAlert("Permission Denied", "Only the group creator can delete this group.", "OK");
                    return;
                }

                if (!await DisplayAlert(
                    "?? DELETE GROUP",
                    $"Permanently delete \"{_group?.Name}\"?\n\nAll messages and data will be lost. This cannot be undone.",
                    "Yes, Delete", "Cancel")) return;

                var confirm = await DisplayPromptAsync(
                    "Final Confirmation",
                    "Type DELETE to confirm:",
                    "DELETE", "Cancel");

                if (confirm != "DELETE") return;

                DeleteGroupButton.Text = "Deleting…";
                await GroupRepository.DeleteGroupAsync(_groupId);

                MessagingCenter.Send(this, "GroupsUpdated");
                MessagingCenter.Send(this, "ConversationsUpdated");
                MessagingCenter.Send(this, "GroupDeleted", _groupId);

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteGroup error: {ex}");
                await DisplayAlert("Error", $"Could not delete group: {ex.Message}", "OK");
            }
            finally
            {
                DeleteGroupButton.IsEnabled = true;
                DeleteGroupButton.Text = "Delete";
            }
        }

        // ?? Back ?????????????????????????????????????????????????????????????

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (_isSaving) return;

            if (_hasChanges)
            {
                var save = await DisplayAlert(
                    "Unsaved Changes",
                    "You have unsaved changes. Save before leaving?",
                    "Save", "Discard");

                if (save)
                {
                    await SaveChangesAsync();
                    if (!_isSaving)
                        await Navigation.PopAsync();
                }
                else
                {
                    await Navigation.PopAsync();
                }
            }
            else
            {
                await Navigation.PopAsync();
            }
        }
    }
}