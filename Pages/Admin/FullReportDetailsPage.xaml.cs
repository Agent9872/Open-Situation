using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Admin
{
    public partial class FullReportDetailsPage : ContentPage
    {
        private readonly Report _report;
        private readonly ReportAdminPage _parentPage;

        // Users fetched from DB
        private Lock.Models.User? _reportedUser;
        private Lock.Models.User? _reporter;

        public FullReportDetailsPage(Report report, ReportAdminPage parentPage)
        {
            InitializeComponent();
            _report = report;
            _parentPage = parentPage;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDetailsAsync();
        }

        // ??????????????????????????????????????
        // LOAD & POPULATE
        // ??????????????????????????????????????

        private async Task LoadDetailsAsync()
        {
            try
            {
                // Remove these lines:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();

                // Fetch reported user - using Supabase
                if (!string.IsNullOrEmpty(_report.ReportedUserPhone))
                {
                    var reportedUsers = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(_report.ReportedUserPhone)}&limit=1");
                    _reportedUser = reportedUsers.FirstOrDefault();
                }

                // Fetch reporter - using Supabase
                if (!string.IsNullOrEmpty(_report.ReporterPhone))
                {
                    var reporters = await SupabaseService.GetAsync<Lock.Models.User>("Users",
                        $"PhoneNumber=eq.{Uri.EscapeDataString(_report.ReporterPhone)}&limit=1");
                    _reporter = reporters.FirstOrDefault();
                }

                PopulateHeader();
                PopulateReportedUserCard();
                PopulateReporterCard();
                PopulateReportDetailsCard();
                PopulateReportedMessageCard();
                PopulateEvidenceCard();
                PopulateAdminNotesCard();
                SetupActionPanel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FullReportDetailsPage.LoadDetailsAsync error: {ex}");
                await DisplayAlert("Error", $"Failed to load report details: {ex.Message}", "OK");
            }
        }

        private void PopulateHeader()
        {
            HeaderSubtitle.Text = $"#{_report.Id} · {_report.ReportedAt:MMM dd, yyyy}";

            HeaderStatusLabel.Text = _report.Status switch
            {
                ReportStatus.Pending => "PENDING",
                ReportStatus.UnderReview => "REVIEWING",
                ReportStatus.Resolved => "RESOLVED",
                ReportStatus.Dismissed => "DISMISSED",
                ReportStatus.ActionTaken => "ACTION TAKEN",
                _ => "PENDING"
            };

            HeaderStatusBadge.BackgroundColor = _report.Status switch
            {
                ReportStatus.Pending => Color.FromArgb("#FF9800"),
                ReportStatus.UnderReview => Color.FromArgb("#2196F3"),
                ReportStatus.Resolved => Color.FromArgb("#4CAF50"),
                ReportStatus.Dismissed => Color.FromArgb("#9E9E9E"),
                ReportStatus.ActionTaken => Color.FromArgb("#FF6B6B"),
                _ => Color.FromArgb("#FF9800")
            };
        }

        private void PopulateReportedUserCard()
        {
            bool hasUser = _reportedUser != null;

            string avatarUrl = (hasUser && !string.IsNullOrEmpty(_reportedUser!.ProfileImagePath) && File.Exists(_reportedUser.ProfileImagePath))
                ? _reportedUser.ProfileImagePath
                : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(_report.ReportedUserName ?? "U")}&background=FF3B6F&color=FFFFFF&size=128";

            ReportedUserAvatarImg.Source = avatarUrl;
            ReportedUserNameLabel.Text = _reportedUser?.Name ?? _report.ReportedUserName ?? "Unknown";
            ReportedUserPhoneLabel.Text = _report.ReportedUserPhone ?? "—";
            ReportedUserGenderLabel.Text = _reportedUser?.Gender ?? "—";
            ReportedUserAgeLabel.Text = hasUser ? $"{_reportedUser!.GetAge()} yrs" : "—";
            ReportedUserJoinedLabel.Text = hasUser ? _reportedUser!.JoinDate.ToString("MMM dd, yyyy") : "—";
            ReportedUserLastActiveLabel.Text = hasUser ? _reportedUser!.LastActive.ToString("MMM dd, yyyy") : "—";
            ReportedUserCountryLabel.Text = _reportedUser?.Country ?? "—";

            if (hasUser && _reportedUser!.IsBanned)
            {
                ReportedUserBanCard.IsVisible = true;
                ReportedUserBanLabel.Text = $"{_reportedUser.BanType?.ToUpper() ?? "BAN"}";
            }
        }

        private void PopulateReporterCard()
        {
            bool hasReporter = _reporter != null;

            string avatarUrl = (hasReporter && !string.IsNullOrEmpty(_reporter!.ProfileImagePath) && File.Exists(_reporter.ProfileImagePath))
                ? _reporter.ProfileImagePath
                : $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(_report.ReporterName ?? "U")}&background=2A2A38&color=AAAAAA&size=128";

            ReporterAvatarImg.Source = avatarUrl;
            ReporterNameLabel.Text = _reporter?.Name ?? _report.ReporterName ?? "Anonymous";
            ReporterPhoneLabel.Text = _report.ReporterPhone ?? "—";
            ReporterGenderLabel.Text = _reporter?.Gender ?? "—";
            ReporterAgeLabel.Text = hasReporter ? $"{_reporter!.GetAge()} yrs" : "—";
            ReporterJoinedLabel.Text = hasReporter ? _reporter!.JoinDate.ToString("MMM dd, yyyy") : "—";
        }

        private void PopulateReportDetailsCard()
        {
            CategoryLabel.Text = _report.Category ?? "Uncategorized";
            DateLabel.Text = _report.ReportedAt.ToString("MMM dd, yyyy h:mm tt");
            ConversationIdLabel.Text = string.IsNullOrEmpty(_report.ConversationId) ? "N/A" : _report.ConversationId;
            DescriptionLabel.Text = string.IsNullOrWhiteSpace(_report.Description)
                                         ? "(No description provided)"
                                         : _report.Description;
        }

        private void PopulateReportedMessageCard()
        {
            if (!string.IsNullOrWhiteSpace(_report.ReportedMessageContent))
            {
                ReportedMessageCard.IsVisible = true;
                ReportedMessageLabel.Text = _report.ReportedMessageContent;
            }
        }

        private void PopulateEvidenceCard()
        {
            var images = _report.Images ?? new();
            if (images.Count == 0) return;

            EvidenceCard.IsVisible = true;
            EvidenceHeaderLabel.Text = $"EVIDENCE — {images.Count} image{(images.Count == 1 ? "" : "s")}";

            EvidenceItemsContainer.Children.Clear();
            foreach (var img in images)
            {
                bool exists = !string.IsNullOrEmpty(img.LocalPath) && File.Exists(img.LocalPath);
                var row = new Border
                {
                    BackgroundColor = Color.FromArgb("#0D0D14"),
                    Padding = new Thickness(10, 8)
                };
                row.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 };

                var inner = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(), new ColumnDefinition { Width = GridLength.Auto } } };
                inner.Add(new Label
                {
                    Text = img.LocalPath ?? "Unknown path",
                    TextColor = Color.FromArgb("#888888"),
                    FontSize = 12,
                    LineBreakMode = LineBreakMode.MiddleTruncation,
                    VerticalOptions = LayoutOptions.Center
                }, 0, 0);

                var badge = new Border
                {
                    BackgroundColor = exists ? Color.FromArgb("#1A4A1A") : Color.FromArgb("#4A1A1A"),
                    Padding = new Thickness(8, 4)
                };
                badge.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 };
                badge.Content = new Label
                {
                    Text = exists ? "Available" : "Missing",
                    TextColor = exists ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF3B6F"),
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold
                };
                inner.Add(badge, 1, 0);
                row.Content = inner;
                EvidenceItemsContainer.Children.Add(row);
            }

            // Show/hide Open button only if at least one image exists
            bool anyExist = images.Any(i => !string.IsNullOrEmpty(i.LocalPath) && File.Exists(i.LocalPath));
            OpenImagesButton.IsVisible = anyExist;
        }

        private void PopulateAdminNotesCard()
        {
            if (!string.IsNullOrWhiteSpace(_report.AdminNotes))
            {
                AdminNotesCard.IsVisible = true;
                AdminNotesLabel.Text = _report.AdminNotes;
            }
        }

        private void SetupActionPanel()
        {
            bool isClosed = _report.Status == ReportStatus.Resolved
                         || _report.Status == ReportStatus.Dismissed
                         || _report.Status == ReportStatus.ActionTaken;

            if (isClosed)
            {
                ActionButtonsPanel.IsVisible = false;
                ClosedBanner.IsVisible = true;
                ClosedBannerText.Text = $"??  This report is {_report.Status} — no further actions available.";
            }
        }

        // ??????????????????????????????????????
        // NAVIGATION
        // ??????????????????????????????????????

        private async void OnBackTapped(object sender, EventArgs e)
            => await Navigation.PopAsync();

        // ??????????????????????????????????????
        // ACTION HANDLERS
        // ??????????????????????????????????????

        private async void OnResolveClicked(object sender, EventArgs e)
        {
            string preset = await DisplayActionSheet("Select a Resolve Reason", "Cancel", null,
                "We reviewed your account and found no further issues.",
                "The reported content has been removed and the issue is now resolved.",
                "Our team has investigated and taken the necessary corrective action.",
                "This matter has been resolved in line with our community guidelines.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Resolve Note", "Edit if needed, or tap OK:", initialValue: preset);
            string msg = string.IsNullOrWhiteSpace(note) ? preset : note;

            await UserService.ResolveReportAsync(_report.ReportedUserPhone, msg);
            await _parentPage.UpdateStatusAsync(_report, ReportStatus.Resolved, $"Resolved — Note: {msg}");
            await Navigation.PopAsync();
        }

        private async void OnUnderReviewClicked(object sender, EventArgs e)
        {
            string preset = await DisplayActionSheet("Select an Under Review Reason", "Cancel", null,
                "Your account is currently under review by our moderation team.",
                "We have received a report and are actively investigating your account activity.",
                "Our team is reviewing your recent interactions to ensure guideline compliance.",
                "A review has been initiated. We will notify you once it is complete.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Under Review Note", "Edit if needed, or tap OK:", initialValue: preset);
            string msg = string.IsNullOrWhiteSpace(note) ? preset : note;

            await UserService.ResolveReportAsync(_report.ReportedUserPhone, msg);
            await _parentPage.UpdateStatusAsync(_report, ReportStatus.UnderReview, $"Under review — Note: {msg}");
            await Navigation.PopAsync();
        }

        private async void OnDismissClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Dismiss Report",
                "No action will be taken against the reported user.", "Dismiss", "Cancel");
            if (!confirm) return;

            string preset = await DisplayActionSheet("Select a Dismiss Reason", "Cancel", null,
                "After review, no violation of our community guidelines was found.",
                "This report did not meet the threshold for a policy violation.",
                "The reported content was reviewed and found to be within acceptable use.",
                "Our team found insufficient evidence to support this report.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Dismiss Note", "Edit if needed, or tap OK:", initialValue: preset);
            string msg = string.IsNullOrWhiteSpace(note) ? preset : note;

            await UserService.DismissReportAsync(_report.ReportedUserPhone);
            await UserService.ResolveReportAsync(_report.ReportedUserPhone, msg);
            await _parentPage.UpdateStatusAsync(_report, ReportStatus.Dismissed, $"Dismissed — Note: {msg}");
            await Navigation.PopAsync();
        }

        private async void OnWarnClicked(object sender, EventArgs e)
        {
            string preset = await DisplayActionSheet("Select a Warning Reason", "Cancel", null,
                "Your recent behavior has violated our community guidelines. Please review them to avoid further action.",
                "We have received reports about your interactions. Continued violations may result in a suspension.",
                "Sending unsolicited or inappropriate content is not permitted on this platform.",
                "Harassment or disrespectful behavior toward other users will not be tolerated.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Warning Message", "Edit if needed, or tap OK:", initialValue: preset);
            string warningText = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool warned = await UserService.IssueWarningAsync(_report.ReportedUserPhone, warningText);
            if (warned)
            {
                await _parentPage.UpdateStatusAsync(_report, ReportStatus.ActionTaken, $"Warning issued — Message: {warningText}");
                await DisplayAlert("Warning Issued", $"Warning sent to {_report.ReportedUserName}.", "OK");
                await Navigation.PopAsync();
            }
            else
                await DisplayAlert("Error", $"Could not issue warning. Phone [{_report.ReportedUserPhone}] not found.", "OK");
        }

        private async void OnTempBanClicked(object sender, EventArgs e)
        {
            string durationCategory = await DisplayActionSheet("Select Ban Duration", "Cancel", null, "Hours", "Days");
            if (durationCategory == "Cancel" || string.IsNullOrEmpty(durationCategory)) return;

            string duration;
            DateTime expiresAt;

            if (durationCategory == "Hours")
            {
                duration = await DisplayActionSheet("Select Hours", "Cancel", null,
                    "1 hour", "6 hours", "12 hours", "24 hours");
                if (duration == "Cancel" || string.IsNullOrEmpty(duration)) return;
                expiresAt = duration switch
                {
                    "1 hour" => DateTime.UtcNow.AddHours(1),
                    "6 hours" => DateTime.UtcNow.AddHours(6),
                    "12 hours" => DateTime.UtcNow.AddHours(12),
                    _ => DateTime.UtcNow.AddHours(24)
                };
            }
            else
            {
                duration = await DisplayActionSheet("Select Days", "Cancel", null,
                    "2 days", "3 days", "7 days", "14 days", "30 days");
                if (duration == "Cancel" || string.IsNullOrEmpty(duration)) return;
                expiresAt = duration switch
                {
                    "2 days" => DateTime.UtcNow.AddDays(2),
                    "3 days" => DateTime.UtcNow.AddDays(3),
                    "7 days" => DateTime.UtcNow.AddDays(7),
                    "14 days" => DateTime.UtcNow.AddDays(14),
                    _ => DateTime.UtcNow.AddDays(30)
                };
            }

            string preset = await DisplayActionSheet("Select a Suspension Reason", "Cancel", null,
                "Repeated violations of our community guidelines.",
                "Sending spam, unsolicited messages, or inappropriate content.",
                "Harassment or threatening behavior toward another user.",
                "Sharing content that violates our terms of service.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Suspension Reason", "Edit if needed, or tap OK:", initialValue: preset);
            string reason = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Temporary Ban",
                $"Suspend {_report.ReportedUserName} for {duration}?\n" +
                $"Ends: {expiresAt:MMM dd, yyyy 'at' h:mm tt} UTC\nReason: {reason}",
                "Ban", "Cancel");
            if (!confirm) return;

            bool banned = await UserService.BanUserAsync(_report.ReportedUserPhone, "temporary", reason, expiresAt);
            if (banned)
            {
                await _parentPage.UpdateStatusAsync(_report, ReportStatus.ActionTaken,
                    $"Temp ban for {duration} until {expiresAt:MMM dd, yyyy HH:mm} UTC — Reason: {reason}");
                await DisplayAlert("Ban Applied", $"{_report.ReportedUserName} suspended for {duration}.", "OK");
                await Navigation.PopAsync();
            }
            else
                await DisplayAlert("Error", $"Could not ban user. Phone [{_report.ReportedUserPhone}] not found.", "OK");
        }

        private async void OnPermBanClicked(object sender, EventArgs e)
        {
            string preset = await DisplayActionSheet("Select a Ban Reason", "Cancel", null,
                "Severe and repeated violations of our community guidelines.",
                "Distribution of illegal, explicit, or harmful content.",
                "Predatory, abusive, or threatening behavior toward other users.",
                "Creating a fake identity or impersonating another person.");
            if (preset == "Cancel" || string.IsNullOrEmpty(preset)) return;

            string note = await DisplayPromptAsync("Ban Reason", "Edit if needed, or tap OK:", initialValue: preset);
            string reason = string.IsNullOrWhiteSpace(note) ? preset : note;

            bool confirm = await DisplayAlert("Confirm Permanent Ban",
                $"PERMANENTLY ban {_report.ReportedUserName}?\n" +
                $"Phone: {_report.ReportedUserPhone}\nReason: {reason}\n\nThis CANNOT be undone.",
                "Permanently Ban", "Cancel");
            if (!confirm) return;

            bool banned = await UserService.BanUserAsync(_report.ReportedUserPhone, "permanent", reason, null);
            if (banned)
            {
                await _parentPage.UpdateStatusAsync(_report, ReportStatus.ActionTaken, $"Permanent ban — Reason: {reason}");
                await DisplayAlert("Ban Applied", $"{_report.ReportedUserName} has been permanently banned.", "OK");
                await Navigation.PopAsync();
            }
            else
                await DisplayAlert("Error", $"Could not ban user. Phone [{_report.ReportedUserPhone}] not found.", "OK");
        }

        private async void OnOpenImagesClicked(object sender, EventArgs e)
        {
            var images = _report.Images ?? new();
            var existing = images.Where(i => !string.IsNullOrEmpty(i.LocalPath) && File.Exists(i.LocalPath)).ToList();

            foreach (var img in existing)
            {
                try
                {
                    await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(img.LocalPath) });
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not open image: {ex.Message}");
                    await DisplayAlert("Image Error", $"Could not open:\n{img.LocalPath}", "OK");
                }
            }
        }
    }
}