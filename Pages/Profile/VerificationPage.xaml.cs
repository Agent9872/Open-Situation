using Lock.Chat.Services;
using Lock.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Plugin.Maui.OCR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lock.Pages.Profile
{
    [QueryProperty(nameof(UserPhone), "phone")]
    public partial class VerificationPage : ContentPage
    {
        private readonly Plugin.Maui.OCR.IOcrService _ocrService;
        private byte[] _currentImageData;
        private string _selectedImagePath;
        private string _selectedIdType = "National ID";
        private User _profileUser;
        private double _similarityScore = 0;
        private bool _isProcessing = false;

        // Store extracted data from ID
        private string _extractedIdName = "";
        private string _extractedIdNumber = "";
        private string _extractedIdDob = "";
        private string _extractedIdAddress = "";

        // Store the raw extracted text for reference
        private string _lastExtractedRawText = "";

        // Property to receive phone number from ProfilePage
      private string _userPhone = string.Empty;
public string UserPhone
{
    get => _userPhone;
    set
    {
        Debug.WriteLine($"UserPhone SETTER called with: '{value}'"); // ADD THIS
        _userPhone = value ?? string.Empty;
        if (!string.IsNullOrEmpty(_userPhone))
        {
            _ = LoadUserProfileAsync(_userPhone);
        }
        else
        {
            Debug.WriteLine("UserPhone is null or empty!"); // ADD THIS
        }
    }
}

        public VerificationPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
            Shell.SetNavBarIsVisible(this, false);
            _ocrService = OcrPlugin.Default;
        }

        private async Task LoadUserProfileAsync(string phone)
        {
            try
            {
                // Remove this SQLite code:
                // await DatabaseService.InitializeAsync();
                // var db = DatabaseService.GetConnection();
                // _profileUser = await db.Table<User>().Where(u => u.PhoneNumber == phone).FirstOrDefaultAsync();

                // Replace with Supabase code:
                var users = await SupabaseService.GetAsync<User>("Users",
                    $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                _profileUser = users.FirstOrDefault();

                if (_profileUser != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Show profile info banner
                        ProfileInfoBanner.IsVisible = true;

                        // Set profile initials
                        string initials = GetInitials(_profileUser.Name);
                        ProfileInitials.Text = initials;

                        // Set profile name
                        ProfileNameLabel.Text = _profileUser.Name ?? "Unknown User";

                        // Set profile DOB
                        string dob = _profileUser.DateOfBirth.ToString("dd/MM/yyyy");
                        ProfileDobLabel.Text = $"DOB: {dob}";

                        // Set verification badge based on actual verification status
                        UpdateVerificationBadge();

                        // Show the extracted info container
                        ExtractedInfoContainer.IsVisible = true;

                        // Initially show profile data
                        ExtractedName.Text = _profileUser.Name ?? "Not detected";
                        ExtractedDob.Text = dob;
                        var age = CalculateAgeFromDob(dob);
                        ExtractedAge.Text = string.IsNullOrEmpty(age) ? "Not detected" : $"{age} years";
                        ExtractedIdNumber.Text = "Waiting for upload...";
                        ExtractedAddress.Text = "Waiting for upload...";

                        // Update confidence badge
                        ConfidenceBadge.Text = "Upload ID to verify";

                        // If already verified, show message and disable submission
                        if (_profileUser.IsVerified)
                        {
                            AlreadyVerifiedBanner.IsVisible = true;

                            var verifiedDate = _profileUser.VerifiedAt?.ToString("MMMM dd, yyyy") ?? "April 14, 2026";
                            VerifiedDateLabel.Text = $"Verified on {verifiedDate}";

                            // Hide upload and submission elements
                            UploadDropZone.IsVisible = false;
                            ImagePreviewContainer.IsVisible = false;
                            SubmitButton.IsVisible = false;           // Hide submit button completely
                            VerificationStatusContainer.IsVisible = false;

                            // Optional: Make extracted fields look "locked" or keep them visible
                            ExtractedIdNumber.Text = _profileUser.VerificationIdNumber ?? "Verified";
                            ExtractedAddress.Text = "Verified via ID upload";
                        }
                        else
                        {
                            AlreadyVerifiedBanner.IsVisible = false;
                            UploadDropZone.IsVisible = true;
                            SubmitButton.IsVisible = true;
                            SubmitButton.IsEnabled = false;
                            SubmitButton.Opacity = 0.5;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadUserProfileAsync error: {ex}");
                await DisplayAlert("Error", "Could not load profile data. Please try again.", "OK");
            }
        }


        private void UpdateVerificationBadge()
        {
            if (_profileUser == null) return;

            if (_profileUser.IsVerified)
            {
                // Show star icon
                VerifiedStarIcon.IsVisible = true;

                // Update status badge styling
                ProfileStatusBadge.BackgroundColor = Color.FromArgb("#00B5B520");
                ProfileStatusBadge.Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5"));
                ProfileVerifiedBadge.Text = "Verified";
                ProfileVerifiedBadge.TextColor = Color.FromArgb("#00B5B5");
            }
            else if (_profileUser.VerificationStatus == "pending")
            {
                VerifiedStarIcon.IsVisible = false;
                ProfileStatusBadge.BackgroundColor = Color.FromArgb("#FFA50020");
                ProfileStatusBadge.Stroke = new SolidColorBrush(Color.FromArgb("#FFA500"));
                ProfileVerifiedBadge.Text = "Pending Review";
                ProfileVerifiedBadge.TextColor = Color.FromArgb("#FFA500");
            }
            else
            {
                VerifiedStarIcon.IsVisible = false;
                ProfileStatusBadge.BackgroundColor = Color.FromArgb("#FF6B6B20");
                ProfileStatusBadge.Stroke = new SolidColorBrush(Color.FromArgb("#FF6B6B"));
                ProfileVerifiedBadge.Text = "Not Verified";
                ProfileVerifiedBadge.TextColor = Color.FromArgb("#FF6B6B");
            }
        }

        private string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpper();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpper();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnTakePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "Take ID Photo"
                });

                if (result != null)
                {
                    await LoadAndProcessImage(result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Take photo error: {ex}");
                await DisplayAlert("Error", "Could not take photo. Please check camera permissions.", "OK");
            }
        }

        private async void OnUploadClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select ID image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    await LoadAndProcessImage(result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Upload error: {ex}");
                await DisplayAlert("Error", "Could not upload image. Please check storage permissions.", "OK");
            }
        }

        private async Task LoadAndProcessImage(FileResult fileResult)
        {
            try
            {
                // Show processing indicator
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProcessingContainer.IsVisible = true;
                    UploadDropZone.IsVisible = false;
                    ImagePreviewContainer.IsVisible = false;

                    // Reset extracted data display
                    ExtractedIdNumber.Text = "Processing...";
                    ExtractedAddress.Text = "Processing...";
                    ConfidenceBadge.Text = "Processing...";
                });

                // Load the image
                var stream = await fileResult.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _currentImageData = memoryStream.ToArray();

                // Show preview
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_currentImageData));
                    ImagePreviewContainer.IsVisible = true;
                });

                // Auto-extract text immediately
                await AutoExtractText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load image error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    ProcessingContainer.IsVisible = false;
                    UploadDropZone.IsVisible = true;
                    ExtractedIdNumber.Text = "Upload failed";
                    ExtractedAddress.Text = "Upload failed";
                    await DisplayAlert("Error", "Could not load image", "OK");
                });
            }
        }

        private async Task AutoExtractText()
        {
            if (_currentImageData == null || _isProcessing)
            {
                return;
            }

            try
            {
                _isProcessing = true;

                var result = await _ocrService.RecognizeTextAsync(_currentImageData, tryHard: true);
                var extractedText = result.AllText?.Trim() ?? string.Empty;
                _lastExtractedRawText = extractedText;

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        ProcessingContainer.IsVisible = false;
                        UploadDropZone.IsVisible = true;  // Make sure upload zone shows
                        ImagePreviewContainer.IsVisible = true;  // Keep preview if exists
                        ExtractedIdNumber.Text = "No text found";
                        ExtractedAddress.Text = "No text found";

                        // Reset the submit button
                        SubmitButton.IsEnabled = false;
                        SubmitButton.Opacity = 0.5;

                        await DisplayAlert("No Text Found",
                            "Could not extract any text from your ID.\n\nTips for better results:\n" +
                            "• Use bright, even lighting (no shadows or glare)\n" +
                            "• Hold the camera straight above the ID\n" +
                            "• Make sure the entire document is clearly visible and in focus\n" +
                            "• Try uploading a higher quality photo", "OK");
                    });
                    return;
                }

                // Hide processing, show preview and extracted info
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProcessingContainer.IsVisible = false;
                    FullExtractedText.Text = extractedText;
                });

                // Parse with improved layout-aware logic
                ParseExtractedTextImproved(extractedText);

                // Update UI with extracted ID data
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Show extracted data from ID
                    ExtractedIdNumber.Text = string.IsNullOrEmpty(_extractedIdNumber) ? "Not detected" : _extractedIdNumber;
                    ExtractedAddress.Text = string.IsNullOrEmpty(_extractedIdAddress) ? "Not detected" : _extractedIdAddress;

                    // Note: Name and DOB remain from profile (locked)
                    // But we can show a comparison indicator
                    if (!string.IsNullOrEmpty(_extractedIdName))
                    {
                        // Add a subtle indicator that we extracted a name from the ID
                        ExtractedName.Text = _profileUser?.Name ?? "Not detected";
                    }
                });

                // Calculate similarity score and validate against profile
                await CalculateAndValidateSimilarity();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    ProcessingContainer.IsVisible = false;
                    UploadDropZone.IsVisible = true;
                    ExtractedIdNumber.Text = "Extraction failed";
                    ExtractedAddress.Text = "Extraction failed";
                    await DisplayAlert("Error", $"Failed to extract text: {ex.Message}", "OK");
                });
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task CalculateAndValidateSimilarity()
        {
            if (_profileUser == null) return;

            // Calculate individual match scores
            double nameScore = CalculateNameSimilarity(_extractedIdName, _profileUser.Name ?? "");
            double dobScore = CalculateDobSimilarity(_extractedIdDob, _profileUser.DateOfBirth.ToString("dd/MM/yyyy"));
            double idScore = CalculateIdSimilarity(_extractedIdNumber);

            // Weighted score: Name 50%, DOB 30%, ID presence 20%
            _similarityScore = (nameScore * 0.5) + (dobScore * 0.3) + (idScore * 0.2);

            Debug.WriteLine($"Similarity Score: {_similarityScore:F1}%");
            Debug.WriteLine($"Name: '{_extractedIdName}' vs '{_profileUser.Name}' - Score: {nameScore:F1}%");
            Debug.WriteLine($"DOB: '{_extractedIdDob}' vs '{_profileUser.DateOfBirth:dd/MM/yyyy}' - Score: {dobScore:F1}%");
            Debug.WriteLine($"ID: '{_extractedIdNumber}' - Score: {idScore:F1}%");

            // Update UI with score and detailed comparison
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ConfidenceBadge.Text = $"{_similarityScore:F0}% match";

                // Build detailed status message
                string statusMessage = "";

                if (_similarityScore >= 80)
                {
                    ConfidenceBadge.TextColor = Color.FromArgb("#00B5B5");
                    VerificationStatusIcon.Text = "?";
                    statusMessage = $"? VERIFIED: {_similarityScore:F0}% match with profile\n\n";
                    statusMessage += $"• Name: {(nameScore >= 80 ? "?" : "??")} {_extractedIdName}\n";
                    statusMessage += $"• DOB: {(dobScore >= 80 ? "?" : "??")} {_extractedIdDob}\n";
                    statusMessage += $"• ID Number: {(idScore >= 80 ? "?" : "??")} {_extractedIdNumber}\n";
                    statusMessage += $"\nID document matches your profile!";
                    VerificationStatusContainer.Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5"));
                    SubmitButton.IsEnabled = true;
                    SubmitButton.Opacity = 1;
                }
                else if (_similarityScore >= 60)
                {
                    ConfidenceBadge.TextColor = Color.FromArgb("#FFA500");
                    VerificationStatusIcon.Text = "??";
                    statusMessage = $"?? PARTIAL MATCH: {_similarityScore:F0}% confidence\n\n";
                    statusMessage += $"• Name: {(nameScore >= 60 ? "?" : "?")} {_extractedIdName}\n";
                    statusMessage += $"• DOB: {(dobScore >= 60 ? "?" : "?")} {_extractedIdDob}\n";
                    statusMessage += $"• ID Number: {(idScore >= 60 ? "?" : "?")} {_extractedIdNumber}\n";
                    statusMessage += $"\nPlease ensure your ID is clear and matches your profile.";
                    VerificationStatusContainer.Stroke = new SolidColorBrush(Color.FromArgb("#FFA500"));
                    SubmitButton.IsEnabled = false;
                    SubmitButton.Opacity = 0.5;
                }
                else
                {
                    ConfidenceBadge.TextColor = Color.FromArgb("#FF6B6B");
                    VerificationStatusIcon.Text = "?";
                    statusMessage = $"? VERIFICATION FAILED: {_similarityScore:F0}% match\n\n";
                    statusMessage += $"• Name: {_extractedIdName}\n";
                    statusMessage += $"• DOB: {_extractedIdDob}\n";
                    statusMessage += $"• ID Number: {_extractedIdNumber}\n";
                    statusMessage += $"\nThe information on your ID does not match your profile.\n\n";
                    statusMessage += $"Profile Name: {_profileUser.Name}\n";
                    statusMessage += $"Profile DOB: {_profileUser.DateOfBirth:dd/MM/yyyy}\n\n";
                    statusMessage += $"Please upload a clearer image or the correct ID document.";
                    VerificationStatusContainer.Stroke = new SolidColorBrush(Color.FromArgb("#FF6B6B"));
                    SubmitButton.IsEnabled = false;
                    SubmitButton.Opacity = 0.5;

                    // CRITICAL: Keep upload section visible for retry
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UploadDropZone.IsVisible = true;
                        ImagePreviewContainer.IsVisible = true;  // Keep preview visible
                        ProcessingContainer.IsVisible = false;

                        // Reset the upload zone to allow new uploads
                        // But keep the current image preview so user can see what was scanned
                    });
                }
                VerificationStatusMessage.Text = statusMessage;
                VerificationStatusContainer.IsVisible = true;
            });
        }

        private double CalculateNameSimilarity(string extractedName, string profileName)
        {
            if (string.IsNullOrEmpty(extractedName) || extractedName == "Not detected")
                return 0;

            if (string.IsNullOrEmpty(profileName))
                return 0;

            extractedName = extractedName.ToLower().Trim();
            profileName = profileName.ToLower().Trim();

            // Exact match (fast path)
            if (extractedName == profileName)
                return 100;

            // Split into word sets (order doesn't matter)
            var extractedWords = extractedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(w => w.Trim())
                                              .Where(w => !string.IsNullOrEmpty(w))
                                              .ToList();

            var profileWords = profileName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(w => w.Trim())
                                          .Where(w => !string.IsNullOrEmpty(w))
                                          .ToList();

            if (extractedWords.Count == 0 || profileWords.Count == 0)
                return 0;

            // Count matching words (order-independent)
            int matchedWords = 0;
            var matchedProfileIndices = new HashSet<int>();

            foreach (var extractedWord in extractedWords)
            {
                for (int i = 0; i < profileWords.Count; i++)
                {
                    if (!matchedProfileIndices.Contains(i))
                    {
                        // Check for exact word match OR one contains the other
                        if (profileWords[i] == extractedWord ||
                            profileWords[i].Contains(extractedWord) ||
                            extractedWord.Contains(profileWords[i]))
                        {
                            matchedWords++;
                            matchedProfileIndices.Add(i);
                            break;
                        }
                    }
                }
            }

            // Calculate match percentage based on the larger set of words
            int maxWords = Math.Max(extractedWords.Count, profileWords.Count);
            double wordMatchScore = (double)matchedWords / maxWords * 100;

            // If we have at least 2 words matched, also check Levenshtein on the full name as fallback
            double levenshteinScore = 0;
            if (wordMatchScore < 80)
            {
                int distance = LevenshteinDistance(extractedName, profileName);
                int maxLength = Math.Max(extractedName.Length, profileName.Length);
                levenshteinScore = (1.0 - (double)distance / maxLength) * 100;
            }

            // Return the better of the two scores
            double finalScore = Math.Max(wordMatchScore, levenshteinScore);

            Debug.WriteLine($"Name Match - Extracted: '{extractedName}', Profile: '{profileName}'");
            Debug.WriteLine($"  Word match: {wordMatchScore:F1}%, Levenshtein: {levenshteinScore:F1}%, Final: {finalScore:F1}%");
            Debug.WriteLine($"  Matched {matchedWords}/{maxWords} words");

            return Math.Max(0, Math.Min(100, finalScore));
        }
        private double CalculateDobSimilarity(string extractedDob, string profileDob)
        {
            if (string.IsNullOrEmpty(extractedDob) || extractedDob == "Not detected")
                return 0;

            if (extractedDob == profileDob)
                return 100;

            try
            {
                var extractedParts = extractedDob.Split('/');
                var profileParts = profileDob.Split('/');

                if (extractedParts.Length == 3 && profileParts.Length == 3)
                {
                    int extractedDay = int.Parse(extractedParts[0]);
                    int extractedMonth = int.Parse(extractedParts[1]);
                    int extractedYear = int.Parse(extractedParts[2]);

                    int profileDay = int.Parse(profileParts[0]);
                    int profileMonth = int.Parse(profileParts[1]);
                    int profileYear = int.Parse(profileParts[2]);

                    int matches = 0;
                    if (extractedDay == profileDay) matches++;
                    if (extractedMonth == profileMonth) matches++;
                    if (extractedYear == profileYear) matches++;

                    return (matches / 3.0) * 100;
                }
            }
            catch { }

            return 0;
        }

        private double CalculateIdSimilarity(string extractedId)
        {
            if (string.IsNullOrEmpty(extractedId) || extractedId == "Not detected" || extractedId == "Waiting for upload..." || extractedId == "Processing...")
                return 0;

            if (extractedId.Length >= 8)
                return 100;

            if (extractedId.Length >= 5)
                return 60;

            return 30;
        }

        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        

        private void ParseExtractedTextImproved(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Flatten all whitespace to single spaces for clean regex matching
            var flat = Regex.Replace(text, @"\s+", " ").Trim();

            // Reset all fields
            _extractedIdName = "";
            _extractedIdNumber = "";
            _extractedIdDob = "";
            _extractedIdAddress = "";

            Debug.WriteLine($"[OCR] Raw flat text: {flat}");

            // ???????????????????????????????????????????????????????????????
            // STEP 1 — VIN / ID NUMBER
            // Pattern: "VIN: wF5 BIAI 7896 236"
            // ???????????????????????????????????????????????????????????????
            var vinMatch = Regex.Match(flat, @"VIN[:\s]+([A-Z0-9]{2,6}(?:\s+[A-Z0-9]{2,6}){2,5})", RegexOptions.IgnoreCase);
            if (vinMatch.Success)
            {
                _extractedIdNumber = vinMatch.Groups[1].Value.Trim().ToUpper();
                Debug.WriteLine($"[VIN] {_extractedIdNumber}");
            }
            else
            {
                // Fallback: long alphanumeric code (NIN style — 11 digits)
                var ninMatch = Regex.Match(flat, @"\b(\d{11})\b");
                if (ninMatch.Success)
                {
                    _extractedIdNumber = ninMatch.Groups[1].Value;
                    Debug.WriteLine($"[NIN fallback] {_extractedIdNumber}");
                }
                else
                {
                    // Generic: any chunked alphanumeric that looks like an ID
                    var chunkMatch = Regex.Match(flat, @"\b([A-Z]{1,3}[0-9]{3,}\s+[A-Z0-9]{4,}\s+[A-Z0-9]{3,})\b", RegexOptions.IgnoreCase);
                    if (chunkMatch.Success)
                    {
                        _extractedIdNumber = chunkMatch.Groups[1].Value.Trim().ToUpper();
                        Debug.WriteLine($"[ID chunk fallback] {_extractedIdNumber}");
                    }
                }
            }

            // ???????????????????????????????????????????????????????????????
            // STEP 2 — NAME
            //
            // Nigerian Voter's Card structure after DELIM block:
            //   SURNAME, FIRSTNAME [MIDDLENAME]
            //   e.g.  ANGULA, ANENGE MALACHI
            //
            // Strategy A: Find "SURNAME, FIRSTNAME [MIDDLE]" — the comma is the key
            // ???????????????????????????????????????????????????????????????

            bool nameFound = false;

            // Strategy A — comma-surname format (most reliable for Voter's Card)
            // Matches:  ANGULA, ANENGE MALACHI
            // Stops before: a year (W1997), digits, keywords like OCCUPATION/DATE/ADDRESS
            var commaName = Regex.Match(
                flat,
                @"\b([A-Z]{2,}),\s+([A-Z]{2,}(?:\s+[A-Z]{2,}){0,3})\b(?=\s+(?:[A-Z]?\d{4}|OCCUPATION|DATE|ADDRESS|$))",
                RegexOptions.IgnoreCase);

            if (commaName.Success)
            {
                // SURNAME is before comma, GIVEN NAMES are after — build GIVEN SURNAME order
                string surname = commaName.Groups[1].Value.Trim();
                string givenNames = commaName.Groups[2].Value.Trim();

                // Exclude obvious non-name words that sometimes appear before the comma
                var noiseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "NORTH", "SOUTH", "EAST", "WEST", "LGA", "WARD", "DELIM", "STATE", "BENUE",
              "GBOKO", "LAGOS", "ABUJA", "KANO", "VIN", "CODE", "CARD" };

                if (!noiseWords.Contains(surname) && surname.Length >= 2)
                {
                    // Format: Firstname Middlename Surname (natural reading order)
                    string fullName = $"{givenNames} {surname}";

                    // Title-case each word
                    _extractedIdName = TitleCase(fullName);
                    nameFound = true;
                    Debug.WriteLine($"[Name A - comma format] surname='{surname}' given='{givenNames}' ? '{_extractedIdName}'");
                }
            }

            // Strategy B — three consecutive ALL-CAPS words NOT separated by comma
            // e.g.  JOHN MICHAEL SMITH  (for passports/DL with no comma)
            if (!nameFound)
            {
                // Look for a sequence of 2–4 uppercase words that aren't noise
                var capsBlock = Regex.Match(
                    flat,
                    @"\b([A-Z]{3,})(?:\s+([A-Z]{3,})){1,3}\b",
                    RegexOptions.None);   // NO IgnoreCase — we want ALLCAPS only

                if (capsBlock.Success)
                {
                    // Collect from full match
                    string candidate = capsBlock.Value.Trim();
                    var words = candidate.Split(' ');

                    var noiseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "NORTH","SOUTH","EAST","WEST","LGA","WARD","STATE","BENUE","GBOKO",
                  "LAGOS","ABUJA","KANO","VOTER","CARD","REPUBLIC","FEDERAL","NIGERIA",
                  "INDEPENDENT","ELECTORAL","COMMISSION","OCCUPATION","STUDENT","DATE",
                  "BIRTH","ADDRESS","DELIM","VIN","CODE","NAME","FULL" };

                    var nameWords = words.Where(w => w.Length >= 2 && !noiseWords.Contains(w)).Take(4).ToList();

                    if (nameWords.Count >= 2)
                    {
                        _extractedIdName = TitleCase(string.Join(" ", nameWords));
                        nameFound = true;
                        Debug.WriteLine($"[Name B - caps block] '{_extractedIdName}'");
                    }
                }
            }

            // Strategy C — look for text sandwiched between DELIM value and date/year
            if (!nameFound)
            {
                var sandwichMatch = Regex.Match(
                    flat,
                    @"(?:NORTH|SOUTH|EAST|WEST)\s+([A-Z]{2,}(?:\s+[A-Z]{2,}){1,4})\s+(?:[A-Z]?\d{4})",
                    RegexOptions.IgnoreCase);

                if (sandwichMatch.Success)
                {
                    _extractedIdName = TitleCase(sandwichMatch.Groups[1].Value.Trim());
                    nameFound = true;
                    Debug.WriteLine($"[Name C - sandwich] '{_extractedIdName}'");
                }
            }

            // ???????????????????????????????????????????????????????????????
            // STEP 3 — DATE OF BIRTH
            //
            // Voter's Card often has "W1997" meaning year of birth 1997
            // or a full date like "07/10/1997" or "07-10-1997"
            // ???????????????????????????????????????????????????????????????

            // Full date: DD/MM/YYYY or DD-MM-YYYY or DD.MM.YYYY
            var fullDate = Regex.Match(flat, @"\b(\d{2})[\/\-\.](\d{2})[\/\-\.](\d{4})\b");
            if (fullDate.Success)
            {
                _extractedIdDob = $"{fullDate.Groups[1].Value}/{fullDate.Groups[2].Value}/{fullDate.Groups[3].Value}";
                Debug.WriteLine($"[DOB full date] {_extractedIdDob}");
            }

            // W-year format: W1997 ? store as 01/01/1997 (best guess, year only)
            if (string.IsNullOrEmpty(_extractedIdDob))
            {
                var wYear = Regex.Match(flat, @"\bW(\d{4})\b");
                if (wYear.Success)
                {
                    _extractedIdDob = $"01/01/{wYear.Groups[1].Value}";
                    Debug.WriteLine($"[DOB W-year] {_extractedIdDob}");
                }
            }

            // DATE OF BIRTH keyword pattern
            if (string.IsNullOrEmpty(_extractedIdDob))
            {
                var dobKeyword = Regex.Match(flat,
                    @"(?:DATE\s+OF\s+BIRTH|DOB)[:\s]+(\d{2})[\/\-\.](\d{2})[\/\-\.](\d{4})",
                    RegexOptions.IgnoreCase);
                if (dobKeyword.Success)
                {
                    _extractedIdDob = $"{dobKeyword.Groups[1].Value}/{dobKeyword.Groups[2].Value}/{dobKeyword.Groups[3].Value}";
                    Debug.WriteLine($"[DOB keyword] {_extractedIdDob}");
                }
            }

            // ???????????????????????????????????????????????????????????????
            // STEP 4 — ADDRESS
            //
            // Voter's Card: DELIM contains LGA/Ward info = residential area
            // e.g.  DELIM: BENUE 1 GBOKO GBOKO NORTH WEST
            // Then ADDRESS keyword may also appear: ADDRESS GBOKO WEST
            // ???????????????????????????????????????????????????????????????

            // ADDRESS keyword first (most explicit)
            var addressKeyword = Regex.Match(flat,
                @"ADDRESS\s+([A-Z0-9]{2,}(?:\s+[A-Z0-9]{2,}){0,6})",
                RegexOptions.IgnoreCase);
            if (addressKeyword.Success)
            {
                _extractedIdAddress = TitleCase(addressKeyword.Groups[1].Value.Trim());
                Debug.WriteLine($"[Address keyword] {_extractedIdAddress}");
            }

            // DELIM block as fallback address (contains State + LGA + Ward)
            if (string.IsNullOrEmpty(_extractedIdAddress))
            {
                var delimMatch = Regex.Match(flat,
                    @"DELIM[:\s]+([A-Z]{2,}(?:\s+[A-Z0-9]{1,5})?(?:\s+[A-Z]{2,}){0,6})",
                    RegexOptions.IgnoreCase);
                if (delimMatch.Success)
                {
                    // Clean out noise numbers (ward numbers like "1")
                    string raw = delimMatch.Groups[1].Value.Trim();
                    raw = Regex.Replace(raw, @"\s+\d{1,2}\s+", " "); // remove lone digits
                    _extractedIdAddress = TitleCase(raw.Trim());
                    Debug.WriteLine($"[Address DELIM] {_extractedIdAddress}");
                }
            }

            Debug.WriteLine($"[FINAL] Name='{_extractedIdName}' | ID='{_extractedIdNumber}' | DOB='{_extractedIdDob}' | Address='{_extractedIdAddress}'");
        }


        // ??????????????????????????????????????????????????????????????????
        // HELPER — add this private helper method to the class (not inside
        // ParseExtractedTextImproved, but right below it in the same class)
        // ??????????????????????????????????????????????????????????????????
        private static string TitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var words = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
            return string.Join(" ", words);
        }
        private string CalculateAgeFromDob(string dob)
        {
            try
            {
                var parts = dob.Split('/');
                if (parts.Length == 3)
                {
                    int day = int.Parse(parts[0]);
                    int month = int.Parse(parts[1]);
                    int year = int.Parse(parts[2]);

                    var birthDate = new DateTime(year, month, day);
                    var today = DateTime.Today;
                    int age = today.Year - birthDate.Year;
                    if (birthDate.AddYears(age) > today) age--;
                    return age >= 0 && age <= 120 ? age.ToString() : "";
                }
            }
            catch { }
            return "";
        }

        private void OnIdTypeSelected(object sender, EventArgs e)
        {
            if (sender is not Border tapped) return;
            _selectedIdType = tapped.GestureRecognizers
                .OfType<TapGestureRecognizer>()
                .FirstOrDefault()?.CommandParameter?.ToString() ?? "National ID";

            var chips = new[] { ChipNational, ChipDriver, ChipPassport, ChipVoter };
            foreach (var chip in chips)
            {
                chip.BackgroundColor = Color.FromArgb("#0F2020");
                chip.Stroke = new SolidColorBrush(Color.FromArgb("#1E4040"));
                if (chip.Content is Label lbl)
                    lbl.TextColor = Color.FromArgb("#A8D8D4");
            }

            tapped.BackgroundColor = Color.FromArgb("#0E2E2E");
            tapped.Stroke = new SolidColorBrush(Color.FromArgb("#00B5B5"));
            if (tapped.Content is Label activeLabel)
            {
                activeLabel.TextColor = Color.FromArgb("#00B5B5");
                activeLabel.FontAttributes = FontAttributes.Bold;
            }
        }

        private async void OnSubmitVerificationClicked(object sender, EventArgs e)
        {
            if (_profileUser == null)
            {
                await DisplayAlert("Error", "Profile data not loaded. Please try again.", "OK");
                return;
            }

            if (_profileUser.IsVerified)
            {
                await DisplayAlert("Already Verified",
                    "Your account is already verified!\n\n" +
                    $"Verified on: {_profileUser.VerifiedAt:MMMM dd, yyyy}",
                    "OK");
                return;
            }

            if (_similarityScore < 80)
            {
                await DisplayAlert("Verification Required",
                    $"Your ID only matches your profile with {_similarityScore:F1}% confidence.\n\n" +
                    "Please upload a clearer image of your ID and try again.",
                    "OK");
                return;
            }

            if (string.IsNullOrEmpty(_extractedIdNumber) || _extractedIdNumber == "Not detected")
            {
                await DisplayAlert("Incomplete Information",
                    "ID Number could not be extracted from your document.\n\n" +
                    "Please ensure you uploaded a clear image of your ID and try again.",
                    "OK");
                return;
            }

            // Create and show modal
            var modal = new VerificationConfirmModal(
                _profileUser,
                _extractedIdName,
                _extractedIdNumber,
                _extractedIdDob,
                _extractedIdAddress,
                _selectedIdType,
                _similarityScore
            );

            // Handle verification completion
            modal.VerificationCompleted += async (s, success) =>
            {
                if (success)
                {
                    // Update UI on the main page
                    UpdateVerificationBadge();
                    MessagingCenter.Send(this, "ProfileUpdated");
                    MessagingCenter.Send(this, "UserVerified");

                    // Navigate back after a short delay
                    await Task.Delay(500);
                    await Navigation.PopAsync();
                }
            };

            await Navigation.PushModalAsync(modal);
        }
    }
}