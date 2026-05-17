using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class GroupInfoPage : ContentPage
    {
        private readonly string _groupId;
        private readonly string _currentUserPhone;
        private Group? _group;

        public GroupInfoPage(string groupId, string currentUserPhone)
        {
            InitializeComponent();
            _groupId = groupId;
            _currentUserPhone = currentUserPhone;
            Shell.SetNavBarIsVisible(this, false);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadGroupInfoAsync();
        }

        private async Task LoadGroupInfoAsync()
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                _group = await GroupRepository.GetGroupAsync(_groupId);

                if (_group == null)
                {
                    await Navigation.PopAsync();
                    return;
                }

                // ?? Header subtitle ??????????????????????????????????????????
                GroupTypeChipLabel.Text = _group.GroupTypeDisplay;

                // ?? Name + description ???????????????????????????????????????
                GroupNameLabel.Text = _group.Name;
                GroupDescLabel.Text = string.IsNullOrEmpty(_group.Description)
                    ? "No description provided."
                    : _group.Description;

                // ?? Type + visibility pills ??????????????????????????????????
                GroupTypePillLabel.Text = $"{_group.GroupTypeIcon}  {_group.GroupTypeDisplay}";
                VisibilityLabel.Text = $"??  {_group.Visibility}";

                // ?? Stats row ????????????????????????????????????????????????
                MemberCountLabel.Text = _group.MaxMembers > 0
                    ? $"{_group.MemberCount}/{_group.MaxMembers}"
                    : _group.MemberCount.ToString();

                GroupCreatedLabel.Text = _group.CreatedAt.ToString("MMM yyyy");

                MaxMembersStatLabel.Text = _group.MaxMembers > 0
                    ? _group.MaxMembers.ToString()
                    : "?";

                // ?? Details rows ?????????????????????????????????????????????

                // Category
                if (!string.IsNullOrEmpty(_group.Category))
                {
                    CategoryRow.IsVisible = true;
                    CategoryDivider.IsVisible = true;
                    CategoryLabel.Text = _group.Category;
                }

                // Mood filter (Mood Room only)
                if (_group.GroupType == GroupType.MoodRoom && !string.IsNullOrEmpty(_group.MoodFilter))
                {
                    MoodRow.IsVisible = true;
                    MoodDivider.IsVisible = true;
                    MoodLabel.Text = _group.MoodFilter;
                }

                // Encryption
                EncryptionLabel.Text = _group.IsEncrypted ? "Enabled ?" : "Disabled";
                EncryptionLabel.TextColor = _group.IsEncrypted
                    ? Color.FromArgb("#008080")
                    : Color.FromArgb("#666666");

                // Approval
                ApprovalLabel.Text = _group.RequireApproval ? "Required" : "Open";
                ApprovalLabel.TextColor = _group.RequireApproval
                    ? Color.FromArgb("#FF3B6F")
                    : Color.FromArgb("#008080");

                // Anonymous mode (Support Circle only)
                if (_group.GroupType == GroupType.SupportCircle)
                {
                    AnonymousRow.IsVisible = true;
                    AnonymousDivider.IsVisible = true;
                    AnonymousLabel.Text = _group.IsAnonymousAllowed ? "Enabled ?" : "Disabled";
                    AnonymousLabel.TextColor = _group.IsAnonymousAllowed
                        ? Color.FromArgb("#008080")
                        : Color.FromArgb("#666666");
                }

                // Max members detail row
                if (_group.MaxMembers > 0)
                {
                    MaxMembersRow.IsVisible = true;
                    MaxMembersLabel.Text = _group.MaxMembers.ToString();
                }

                // ?? Cover image ??????????????????????????????????????????????
                if (_group.HasCoverImage && !string.IsNullOrEmpty(_group.CoverImagePath))
                {
                    GroupCoverImage.Source = ImageSource.FromFile(_group.CoverImagePath);
                    CoverImageFrame.IsVisible = true;
                }
                else
                {
                    CoverImageFrame.IsVisible = false;
                }

                // ?? Interest tags ????????????????????????????????????????????
                LoadInterestTags();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadGroupInfo error: {ex}");
                await DisplayAlert("Error", "Could not load group information", "OK");
            }
        }

        private void LoadInterestTags()
        {
            if (_group?.InterestTags == null || !_group.InterestTags.Any())
            {
                TagsCard.IsVisible = false;
                return;
            }

            TagsCard.IsVisible = true;
            TagsContainer.Children.Clear();

            foreach (var tag in _group.InterestTags)
            {
                // Same teal pill style as MatchPage CommonInterests chips
                var pill = new Border
                {
                    BackgroundColor = Color.FromArgb("#0D2626"),
                    StrokeThickness = 0.5f,
                    Stroke = Color.FromArgb("#008080"),
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(10, 4),
                    Margin = new Thickness(0, 0, 6, 6)
                };

                pill.Content = new Label
                {
                    Text = $"#{tag}",
                    TextColor = Color.FromArgb("#008080"),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center
                };

                TagsContainer.Children.Add(pill);
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
            => await Navigation.PopAsync();
    }
}