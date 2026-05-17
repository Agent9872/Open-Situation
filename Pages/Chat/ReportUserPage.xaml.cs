using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class ReportUserPage : ContentPage, IQueryAttributable
    {
        private string _reportedUserPhone;
        private string _reportedUserName;
        private string _reportedUserProfileImage;
        private int? _reportedMessageId;
        private string _reportedMessageContent;
        private string _conversationId;
        private bool _isSubmitting = false;
        private bool _userHasEditedDescription = false;

        private ObservableCollection<ReportImageDisplay> _images = new();

        // 4 preset descriptions per category
        private static readonly Dictionary<string, List<string>> CategoryDescriptions = new()
        {
            ["Spam / Promotional"] = new()
            {
                "This user has been repeatedly sending me unsolicited promotional messages and advertisements without my consent.",
                "I am receiving constant spam messages from this user promoting products, services, or links I never asked for.",
                "This user is flooding my inbox with irrelevant promotional content and will not stop despite being ignored.",
                "This account appears to be a bot or spam account sending mass promotional messages to users on the platform."
            },
            ["Harassment / Bullying"] = new()
            {
                "This user has been sending me threatening and intimidating messages that make me feel unsafe.",
                "I am being continuously harassed by this user who refuses to stop contacting me after I asked them to.",
                "This user is bullying me by sending hurtful, degrading, and humiliating messages repeatedly.",
                "This person has been targeting me with persistent unwanted contact, insults, and aggressive behavior."
            },
            ["Hate Speech"] = new()
            {
                "This user is sending messages containing hate speech targeting my race, religion, or ethnicity.",
                "I have received content from this user that promotes discrimination and hatred toward a specific group of people.",
                "This user is using slurs and derogatory language designed to demean people based on their identity.",
                "This account is spreading hateful rhetoric and extremist content that violates community standards."
            },
            ["Inappropriate Content"] = new()
            {
                "This user sent me explicit sexual content without my consent and without any warning.",
                "I received graphic and disturbing content from this user that I did not ask for and find deeply offensive.",
                "This user is sharing inappropriate images or videos that violate the platform's content policies.",
                "This person keeps sending me adult content that is not permitted on this platform."
            },
            ["Impersonation"] = new()
            {
                "This user is pretending to be me or someone I know, using stolen photos and personal information.",
                "This account is impersonating a public figure or celebrity to deceive other users on the platform.",
                "I believe this profile is a fake account created to impersonate a real person and mislead others.",
                "This user is using someone else's identity, photos, and personal details to create a false persona."
            },
            ["Scam / Fraud"] = new()
            {
                "This user attempted to scam me by requesting money or personal financial information under false pretenses.",
                "I believe this account is running a fraud scheme, promising things in exchange for payments that never materialize.",
                "This person tried to trick me into sharing my bank details or sending money through a deceptive scheme.",
                "This account is operating a romance scam, building a fake emotional connection to eventually request money."
            },
            ["Underage User"] = new()
            {
                "Based on our conversations and their profile, I believe this user is under 18 years old.",
                "This user has explicitly mentioned or implied they are a minor, which violates the platform's age policy.",
                "The photos and information shared by this user strongly suggest they are not of legal age to use this platform.",
                "I have reasons to believe this account belongs to an underage person who has misrepresented their age."
            },
            ["Privacy Violation"] = new()
            {
                "This user shared my personal photos, phone number, or private information without my consent.",
                "I found that this person has been sharing private conversations and personal details about me publicly.",
                "This user obtained and is distributing my private information in a way that puts my safety at risk.",
                "My personal data including location, contacts, or images has been shared by this user without permission."
            },
            ["Violence / Threat"] = new()
            {
                "This user has sent me direct threats of physical violence that I am taking very seriously.",
                "I received a credible threat from this user that made me fear for my personal safety and wellbeing.",
                "This person has been sending increasingly aggressive messages that include explicit threats of harm.",
                "This user threatened to harm me or someone I know and I believe the threat should be taken seriously."
            },
            ["Other"] = new()
            {
                "This user's behavior is harmful and inappropriate in a way that does not fit the other categories.",
                "I want to report this account for conduct that violates community guidelines in a significant way.",
                "This user has been behaving in a way that I find harmful and that other users should be warned about.",
                "I am reporting this account for suspicious or harmful behavior that I believe warrants admin review."
            }
        };

        public ReportUserPage()
        {
            InitializeComponent();
            ImagesCollectionView.ItemsSource = _images;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("userPhone", out var phoneObj))
                _reportedUserPhone = Uri.UnescapeDataString(phoneObj?.ToString() ?? string.Empty);

            if (query.TryGetValue("userName", out var nameObj))
                _reportedUserName = Uri.UnescapeDataString(nameObj?.ToString() ?? string.Empty);

            if (query.TryGetValue("profileImage", out var profileObj))
                _reportedUserProfileImage = Uri.UnescapeDataString(profileObj?.ToString() ?? string.Empty);

            if (query.TryGetValue("messageId", out var msgIdObj) && int.TryParse(msgIdObj?.ToString(), out int msgId))
                _reportedMessageId = msgId;

            if (query.TryGetValue("messageContent", out var msgContentObj))
                _reportedMessageContent = Uri.UnescapeDataString(msgContentObj?.ToString() ?? string.Empty);

            if (query.TryGetValue("conversationId", out var convObj))
                _conversationId = Uri.UnescapeDataString(convObj?.ToString() ?? string.Empty);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadUserInfo();
            LoadMessageContext();

            // Wire up category change
            if (CategoryPicker != null)
                CategoryPicker.SelectedIndexChanged += OnCategorySelectedIndexChanged;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (CategoryPicker != null)
                CategoryPicker.SelectedIndexChanged -= OnCategorySelectedIndexChanged;
        }

        private async void OnCategorySelectedIndexChanged(object sender, EventArgs e)
        {
            if (CategoryPicker?.SelectedItem is not string selectedCategory) return;

            // Only prompt if description is empty or still a template
            bool descriptionIsEmpty = string.IsNullOrWhiteSpace(DescriptionEditor?.Text);
            bool descriptionIsTemplate = _categoryTemplates.Values
                .SelectMany(t => t)
                .Any(t => t == DescriptionEditor?.Text?.Trim());

            if (!descriptionIsEmpty && !descriptionIsTemplate)
            {
                bool replace = await DisplayAlert(
                    "Replace Description?",
                    "You already have a description written. Do you want to replace it with a template?",
                    "Use Template", "Keep Mine");
                if (!replace) return;
            }

            if (!_categoryTemplates.TryGetValue(selectedCategory, out var templates)) return;

            // Show 4 template options
            string chosen = await DisplayActionSheet(
                $"Quick fill for: {selectedCategory}",
                "Write my own",
                null,
                templates);

            if (chosen == "Write my own" || string.IsNullOrEmpty(chosen)) return;

            if (DescriptionEditor != null)
            {
                DescriptionEditor.Text = chosen;
                if (CharCountLabel != null)
                    CharCountLabel.Text = $"{chosen.Length} / 1000";
            }
        }

        private void LoadUserInfo()
        {
            if (UserNameLabel != null)
                UserNameLabel.Text = !string.IsNullOrEmpty(_reportedUserName)
                    ? _reportedUserName
                    : _reportedUserPhone ?? "Unknown User";

            if (UserPhoneLabel != null)
                UserPhoneLabel.Text = _reportedUserPhone ?? "";

            if (UserProfileImage != null)
            {
                if (!string.IsNullOrEmpty(_reportedUserProfileImage) && File.Exists(_reportedUserProfileImage))
                    UserProfileImage.Source = ImageSource.FromFile(_reportedUserProfileImage);
                else
                    UserProfileImage.Source = "default_avatar.png";
            }
        }

        private void LoadMessageContext()
        {
            if (_reportedMessageId.HasValue && !string.IsNullOrEmpty(_reportedMessageContent))
            {
                if (MessagePreviewLabel != null)
                    MessagePreviewLabel.Text = $"\"{_reportedMessageContent}\"";
                if (ClearMessageButton != null)
                    ClearMessageButton.IsVisible = true;
            }
            else
            {
                if (ClearMessageButton != null)
                    ClearMessageButton.IsVisible = false;
            }
        }

        // ?? Category changed — show preset picker ??????????????????????
        private async void OnCategoryChanged(object sender, EventArgs e)
        {
            if (CategoryPicker?.SelectedItem is not string selectedCategory) return;

            // Only auto-fill if user hasn't manually edited the description
            if (_userHasEditedDescription &&
                !string.IsNullOrWhiteSpace(DescriptionEditor?.Text))
            {
                bool replace = await DisplayAlert(
                    "Replace Description?",
                    "You have already written a description. Would you like to see preset descriptions for this category?",
                    "Show Presets", "Keep Mine");

                if (!replace) return;
            }

            if (!CategoryDescriptions.TryGetValue(selectedCategory, out var presets)) return;

            string chosen = await DisplayActionSheet(
                $"Quick fill for: {selectedCategory}",
                "Write my own",
                null,
                presets[0],
                presets[1],
                presets[2],
                presets[3]);

            if (chosen == "Write my own" || string.IsNullOrEmpty(chosen))
            {
                // Just clear and let them type
                if (DescriptionEditor != null)
                {
                    DescriptionEditor.Text = string.Empty;
                    DescriptionEditor.Focus();
                }
                _userHasEditedDescription = false;
                return;
            }

            if (DescriptionEditor != null)
                DescriptionEditor.Text = chosen;

            _userHasEditedDescription = false; // treat preset as not user-edited
        }

        private void OnDescriptionTextChanged(object sender, TextChangedEventArgs e)
        {
            if (CharCountLabel != null)
                CharCountLabel.Text = $"{e.NewTextValue?.Length ?? 0} / 1000";

            // Mark as user-edited only if they actually typed something new
            if (!string.IsNullOrEmpty(e.NewTextValue))
                _userHasEditedDescription = true;
        }

        private async void OnAddImageClicked(object sender, EventArgs e)
        {
            try
            {
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select evidence images",
                    FileTypes = FilePickerFileType.Images
                });

                if (results == null || !results.Any()) return;

                foreach (var result in results)
                {
                    try
                    {
                        using var stream = await result.OpenReadAsync();
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        var bytes = ms.ToArray();

                        string ext = Path.GetExtension(result.FileName);
                        if (string.IsNullOrEmpty(ext)) ext = ".jpg";

                        string savedPath = ReportService.SaveReportImage(bytes, ext);
                        if (!string.IsNullOrEmpty(savedPath))
                            _images.Add(new ReportImageDisplay { Path = savedPath, Bytes = bytes });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading image {result.FileName}: {ex.Message}");
                    }
                }

                RefreshImagesUI();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not load images: {ex.Message}", "OK");
            }
        }

        private void OnRemoveImageClicked(object sender, EventArgs e)
        {
            string imagePath = null;

            if (e is TappedEventArgs tapped && tapped.Parameter is string p)
                imagePath = p;
            else if (sender is Button btn && btn.CommandParameter is string bp)
                imagePath = bp;

            if (string.IsNullOrEmpty(imagePath)) return;

            var image = _images.FirstOrDefault(i => i.Path == imagePath);
            if (image != null)
            {
                _images.Remove(image);
                try { if (File.Exists(imagePath)) File.Delete(imagePath); } catch { }
                RefreshImagesUI();
            }
        }

        private void RefreshImagesUI()
        {
            bool hasImages = _images.Any();
            ImagesCollectionView.ItemsSource = null;
            ImagesCollectionView.ItemsSource = _images;
            ImagesCollectionView.IsVisible = hasImages;
            if (EmptyImagesPlaceholder != null) EmptyImagesPlaceholder.IsVisible = !hasImages;
            if (ImageCountLabel != null)
            {
                ImageCountLabel.IsVisible = hasImages;
                ImageCountLabel.Text = $"{_images.Count} image{(_images.Count == 1 ? "" : "s")} attached";
            }
        }

        private void OnClearMessageClicked(object sender, EventArgs e)
        {
            _reportedMessageId = null;
            _reportedMessageContent = null;
            if (MessagePreviewLabel != null)
                MessagePreviewLabel.Text = "No message selected — optional";
            if (ClearMessageButton != null)
                ClearMessageButton.IsVisible = false;
        }

        // Add this dictionary at the top of the class alongside other fields
        private static readonly Dictionary<string, string[]> _categoryTemplates = new()
        {
            ["Spam / Promotional"] = new[]
            {
        "This user is repeatedly sending me unsolicited promotional messages and advertisements without my consent.",
        "I keep receiving spam messages from this user containing links to external websites and promotional content.",
        "This user is mass-messaging people with promotional content and is not engaging in genuine conversation.",
        "This account appears to be a bot or automated account sending spam messages to multiple users."
    },
            ["Harassment / Bullying"] = new[]
            {
        "This user has been sending me repeated threatening and abusive messages that make me feel unsafe.",
        "I am being continuously harassed by this user who refuses to stop contacting me after I asked them to.",
        "This user is cyberbullying me by sending degrading, humiliating, and offensive messages.",
        "This user has been targeting me with hostile and aggressive messages across multiple conversations."
    },
            ["Hate Speech"] = new[]
            {
        "This user is using hate speech and slurs targeting my race, religion, gender, or sexual orientation.",
        "This user is promoting discrimination and sending messages that incite hatred toward a specific group.",
        "I have received messages from this user containing extremist rhetoric and hateful ideology.",
        "This user is sending content that dehumanizes people based on their identity or background."
    },
            ["Inappropriate Content"] = new[]
            {
        "This user sent me explicit and sexually inappropriate content without my consent.",
        "This user is sharing graphic and disturbing content that violates community standards.",
        "I received unsolicited adult content from this user that made me extremely uncomfortable.",
        "This user is distributing inappropriate media including explicit images or videos."
    },
            ["Impersonation"] = new[]
            {
        "This user is pretending to be me and using my name and photos to deceive other people.",
        "This account is impersonating a well-known public figure or celebrity to mislead users.",
        "This user is using a fake identity to gain my trust under false pretenses.",
        "This person is falsely claiming to represent an official organization or business."
    },
            ["Scam / Fraud"] = new[]
            {
        "This user is attempting to scam me by requesting money under false pretenses.",
        "I believe this is a romance scam. This user built trust over time and is now asking for financial help.",
        "This user sent me a fraudulent investment opportunity claiming guaranteed returns.",
        "This person is pretending to sell goods or services but I believe it is a financial scam."
    },
            ["Underage User"] = new[]
            {
        "Based on information shared in our conversation, I believe this user is under 18 years old.",
        "This user has disclosed their age and they appear to be a minor using the platform.",
        "From their profile and messages, I strongly suspect this user is underage.",
        "This user is exhibiting behavior and sharing information consistent with being a minor."
    },
            ["Privacy Violation"] = new[]
            {
        "This user shared my private photos or personal information without my permission.",
        "This user is threatening to expose my private information or intimate images.",
        "My personal contact details or location were shared by this user without my consent.",
        "This user obtained and is distributing my private data in violation of my privacy."
    },
            ["Violence / Threat"] = new[]
            {
        "This user sent me explicit threats of physical violence that I take seriously.",
        "I have received direct death threats from this user and fear for my safety.",
        "This user is threatening to harm me or people I know if I do not comply with their demands.",
        "This user sent messages describing specific violent acts they intend to carry out."
    },
            ["Other"] = new[]
            {
        "This user's behavior is violating community guidelines in a way not covered by other categories.",
        "I am reporting this user for conduct that I believe is harmful to the community.",
        "This user's actions are making the platform unsafe and I am requesting admin review.",
        "Please review this user's activity as their behavior appears to violate the terms of service."
    }
        };

        private async void OnSubmitClicked(object sender, EventArgs e)
            => await SubmitReportAsync();

        private async Task SubmitReportAsync()
        {
            if (_isSubmitting) return;

            if (CategoryPicker == null || CategoryPicker.SelectedIndex == -1)
            {
                await DisplayAlert("Missing Category",
                    "Please select a report category before submitting.", "OK");
                return;
            }

            string description = DescriptionEditor?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(description))
            {
                bool proceed = await DisplayAlert(
                    "No Description",
                    "Would you like to add a description? It helps our team review faster.",
                    "Add Description", "Submit Anyway");
                if (proceed) return;
            }

            string currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            string currentUserName = Preferences.Get("current_user_name", "User");

            if (string.IsNullOrEmpty(currentUserPhone))
            {
                await DisplayAlert("Error", "You must be logged in to submit a report.", "OK");
                return;
            }

            var report = new Report
            {
                ReporterPhone = currentUserPhone,
                ReporterName = currentUserName,
                ReportedUserPhone = _reportedUserPhone,
                ReportedUserName = _reportedUserName ?? _reportedUserPhone ?? "Unknown",
                ReportedUserProfileImage = _reportedUserProfileImage,
                Category = CategoryPicker.SelectedItem?.ToString(),
                Description = description,
                ReportedAt = DateTime.UtcNow,
                ConversationId = _conversationId,
                ReportedMessageId = _reportedMessageId,
                ReportedMessageContent = _reportedMessageContent,
                Status = ReportStatus.Pending
            };

            foreach (var img in _images)
                report.Images.Add(new ReportImage { LocalPath = img.Path, AddedAt = DateTime.UtcNow });

            _isSubmitting = true;
            if (SubmitLabel != null) SubmitLabel.Text = "Submitting...";

            try
            {
                await Lock.Chat.Services.DatabaseService.InitializeAsync();
                bool success = await ReportService.SubmitReportAsync(report);

                if (success)
                {
                    await DisplayAlert("Report Submitted",
                        "Thank you for helping keep our community safe. Our team will review your report within 24 hours.",
                        "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Submission Failed",
                        "We could not submit your report. Please try again.", "Try Again");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SubmitReportAsync error: {ex}");
                await DisplayAlert("Error", $"Something went wrong: {ex.Message}", "OK");
            }
            finally
            {
                _isSubmitting = false;
                if (SubmitLabel != null) SubmitLabel.Text = "Submit Report";
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            if (_images.Any() || !string.IsNullOrWhiteSpace(DescriptionEditor?.Text)
                || CategoryPicker?.SelectedIndex != -1)
            {
                bool confirm = await DisplayAlert("Discard Report?",
                    "You have unsaved changes. Are you sure you want to go back?",
                    "Discard", "Keep Editing");
                if (!confirm) return;
            }

            foreach (var img in _images)
            {
                try { if (File.Exists(img.Path)) File.Delete(img.Path); } catch { }
            }

            await Navigation.PopAsync();
        }
    }

    public class ReportImageDisplay
    {
        public string Path { get; set; }
        public byte[] Bytes { get; set; }
    }
}