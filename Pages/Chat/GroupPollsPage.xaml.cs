using Lock.Chat.Services;
using Lock.Models;
using Lock.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lock.Pages.Chat
{
    public partial class GroupPollsPage : ContentPage
    {
        private readonly string _groupId;
        private readonly string _currentUserPhone;
        private readonly bool _isAdmin;
        private ObservableCollection<GroupPollViewModel> _polls = new();
        private TimeSpan _selectedDuration = TimeSpan.FromDays(7);

        // Page width used to calculate fill bar pixel widths
        private double _pageWidth = 360;

        public GroupPollsPage(string groupId, string currentUserPhone, bool isAdmin)
        {
            InitializeComponent();
            _groupId = groupId;
            _currentUserPhone = currentUserPhone;
            _isAdmin = isAdmin;

            Shell.SetNavBarIsVisible(this, false);
            PollsCollectionView.ItemsSource = _polls;
            CreatePollButton.IsVisible = isAdmin;

            // Capture real page width once layout is done
            this.SizeChanged += (s, e) =>
            {
                if (Width > 0) _pageWidth = Width - 64; // 16+16 outer + 16+16 card
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadPollsAsync();
        }

        // ?? Load ?????????????????????????????????????????????????????????????

        private async Task LoadPollsAsync()
        {
            try
            {
                ShowSkeleton(true);

                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var pollMessages = await db.Table<GroupMessage>()
                    .Where(m => m.GroupId == _groupId &&
                                m.MessageType == GroupMessageType.Poll &&
                                !m.IsDeleted)
                    .OrderByDescending(m => m.SentAt)
                    .ToListAsync();

                _polls.Clear();

                foreach (var msg in pollMessages)
                {
                    var vm = await CreatePollViewModelAsync(msg);
                    _polls.Add(vm);
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ShowSkeleton(false);
                    PollCountLabel.Text = _polls.Count == 1
                        ? "1 poll active"
                        : $"{_polls.Count} polls";
                    EmptyState.IsVisible = _polls.Count == 0;
                    PollsCollectionView.IsVisible = _polls.Count > 0;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPollsAsync error: {ex}");
                ShowSkeleton(false);
            }
        }

        private void ShowSkeleton(bool show)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SkeletonView.IsVisible = show;
                if (show)
                {
                    PollsCollectionView.IsVisible = false;
                    EmptyState.IsVisible = false;
                }
            });
        }

        // ?? Build ViewModel ???????????????????????????????????????????????????

        private async Task<GroupPollViewModel> CreatePollViewModelAsync(GroupMessage message)
        {
            string question = message.Content?.Replace("?? Poll: ", "")
                                              .Replace("Poll: ", "") ?? "Untitled Poll";

            // Try decrypt if looks like base64
            if (question.Length > 20 && !question.Contains(" ") && !question.Contains("?"))
            {
                try
                {
                    var dec = DecryptPollQuestion(question, _groupId);
                    if (!string.IsNullOrEmpty(dec)) question = dec;
                }
                catch { }
            }

            var vm = new GroupPollViewModel
            {
                MessageId = message.Id,
                Question = question,
                CreatedAt = message.SentAt,
                CreatorName = message.SenderName,
                CreatorPhone = message.SenderPhone,
                IsCreator = message.SenderPhone == _currentUserPhone
            };

            if (!string.IsNullOrEmpty(message.PollJson))
            {
                try
                {
                    var poll = System.Text.Json.JsonSerializer
                        .Deserialize<GroupPoll>(message.PollJson);

                    if (poll != null)
                    {
                        vm.AllowMultipleVotes = poll.AllowMultipleVotes;
                        vm.TotalVotes = poll.TotalVotes;

                        if (poll.ExpiresAt.HasValue)
                        {
                            vm.IsExpired = DateTime.UtcNow > poll.ExpiresAt.Value;
                            vm.ExpiresAt = poll.ExpiresAt.Value;
                        }

                        var letters = new[] { "A", "B", "C", "D" };
                        vm.Options = new ObservableCollection<PollOptionViewModel>();

                        for (int i = 0; i < poll.Options.Count; i++)
                        {
                            var opt = poll.Options[i];
                            vm.Options.Add(new PollOptionViewModel
                            {
                                Text = opt.Text,
                                VoterPhones = opt.VoterPhones.ToList(),
                                OptionLetter = letters[Math.Min(i, 3)],
                                OptionIndex = i
                            });
                        }

                        vm.HasUserVoted = vm.Options.Any(
                            o => o.VoterPhones.Contains(_currentUserPhone));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Parse poll error: {ex}");
                }
            }

            vm.UpdatePercentages(_currentUserPhone, _pageWidth);
            return vm;
        }

        private string DecryptPollQuestion(string enc, string groupId)
        {
            try
            {
                using var aes = System.Security.Cryptography.Aes.Create();
                using var sha = System.Security.Cryptography.SHA256.Create();
                aes.Key = sha.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(groupId + "_lock_group_key"));

                var full = Convert.FromBase64String(enc);
                var iv = new byte[aes.BlockSize / 8];
                var cipher = new byte[full.Length - iv.Length];
                Array.Copy(full, 0, iv, 0, iv.Length);
                Array.Copy(full, iv.Length, cipher, 0, cipher.Length);
                aes.IV = iv;

                using var dec = aes.CreateDecryptor();
                using var ms = new System.IO.MemoryStream(cipher);
                using var cs = new System.Security.Cryptography.CryptoStream(
                    ms, dec, System.Security.Cryptography.CryptoStreamMode.Read);
                using var sr = new System.IO.StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch { return enc; }
        }

        // ?? Create Poll UI ????????????????????????????????????????????????????

        private void OnToggleCreatePoll(object sender, EventArgs e)
        {
            CreatePollCard.IsVisible = !CreatePollCard.IsVisible;
            if (CreatePollCard.IsVisible) ResetCreateForm();
        }

        private void ResetCreateForm()
        {
            PollQuestionEntry.Text = string.Empty;
            Option1Entry.Text = string.Empty;
            Option2Entry.Text = string.Empty;
            Option3Entry.Text = string.Empty;
            Option4Entry.Text = string.Empty;
            Option3Frame.IsVisible = false;
            Option4Frame.IsVisible = false;
            AddOptionLabel.IsVisible = true;
            AllowMultipleSwitch.IsToggled = false;
            _selectedDuration = TimeSpan.FromDays(7);
            DurationLabel.Text = "7 days";
            DurationOptions.IsVisible = false;
            CreatePollSubmitButton.IsEnabled = false;
        }

        private void OnCancelCreatePollTapped(object sender, EventArgs e)
            => CreatePollCard.IsVisible = false;

        private void OnCancelCreatePollClicked(object sender, EventArgs e)
            => CreatePollCard.IsVisible = false;

        private void OnQuestionTextChanged(object sender, TextChangedEventArgs e)
            => ValidateForm();

        private void OnOptionTextChanged(object sender, TextChangedEventArgs e)
            => ValidateForm();

        private void ValidateForm()
        {
            bool ok = !string.IsNullOrWhiteSpace(PollQuestionEntry?.Text) &&
                      !string.IsNullOrWhiteSpace(Option1Entry?.Text) &&
                      !string.IsNullOrWhiteSpace(Option2Entry?.Text);
            if (CreatePollSubmitButton != null)
                CreatePollSubmitButton.IsEnabled = ok;
        }

        private void OnAddOptionTapped(object sender, EventArgs e)
        {
            if (!Option3Frame.IsVisible)
            {
                Option3Frame.IsVisible = true;
            }
            else if (!Option4Frame.IsVisible)
            {
                Option4Frame.IsVisible = true;
                AddOptionLabel.IsVisible = false;
            }
        }

        private void OnRemoveOption3Tapped(object sender, EventArgs e)
        {
            Option3Frame.IsVisible = false;
            Option3Entry.Text = string.Empty;
            AddOptionLabel.IsVisible = true;
        }

        private void OnRemoveOption4Tapped(object sender, EventArgs e)
        {
            Option4Frame.IsVisible = false;
            Option4Entry.Text = string.Empty;
            AddOptionLabel.IsVisible = true;
        }

        private void OnDurationTapped(object sender, EventArgs e)
            => DurationOptions.IsVisible = !DurationOptions.IsVisible;

        private void OnDurationPreset(object sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            var p = btn.CommandParameter as string ?? "";

            (_selectedDuration, DurationLabel.Text) = p switch
            {
                "1h" => (TimeSpan.FromHours(1), "1 hour"),
                "6h" => (TimeSpan.FromHours(6), "6 hours"),
                "12h" => (TimeSpan.FromHours(12), "12 hours"),
                "1d" => (TimeSpan.FromDays(1), "1 day"),
                "3d" => (TimeSpan.FromDays(3), "3 days"),
                "7d" => (TimeSpan.FromDays(7), "7 days"),
                "14d" => (TimeSpan.FromDays(14), "14 days"),
                "0" => (TimeSpan.Zero, "Never expires"),
                _ => (_selectedDuration, DurationLabel.Text)
            };

            DurationOptions.IsVisible = false;
        }

        private void OnSetCustomDuration(object sender, EventArgs e)
        {
            if (!int.TryParse(CustomDurationEntry.Text, out int mins) || mins <= 0) return;
            mins = Math.Min(mins, 10080);
            _selectedDuration = TimeSpan.FromMinutes(mins);

            DurationLabel.Text = mins < 60
                ? $"{mins} min{(mins != 1 ? "s" : "")}"
                : mins < 1440
                    ? $"{mins / 60} hour{(mins / 60 != 1 ? "s" : "")}"
                    : $"{mins / 1440} day{(mins / 1440 != 1 ? "s" : "")}";

            DurationOptions.IsVisible = false;
            CustomDurationEntry.Text = string.Empty;
        }

        private async void OnCreatePollSubmit(object sender, EventArgs e)
        {
            try
            {
                var q = PollQuestionEntry.Text?.Trim();
                if (string.IsNullOrWhiteSpace(q))
                {
                    await DisplayAlert("Oops", "Please enter a question", "OK");
                    return;
                }

                var opts = new List<string>
                {
                    Option1Entry.Text?.Trim() ?? "",
                    Option2Entry.Text?.Trim() ?? ""
                };
                if (!string.IsNullOrWhiteSpace(Option3Entry.Text))
                    opts.Add(Option3Entry.Text.Trim());
                if (!string.IsNullOrWhiteSpace(Option4Entry.Text))
                    opts.Add(Option4Entry.Text.Trim());

                if (opts.Any(string.IsNullOrWhiteSpace))
                {
                    await DisplayAlert("Oops", "Please fill in all option fields", "OK");
                    return;
                }

                DateTime? expiresAt = _selectedDuration > TimeSpan.Zero
                    ? DateTime.UtcNow.Add(_selectedDuration)
                    : null;

                await GroupRepository.CreatePollAsync(
                    _groupId, _currentUserPhone, q, opts,
                    AllowMultipleSwitch.IsToggled, expiresAt);

                CreatePollCard.IsVisible = false;
                await LoadPollsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnCreatePollSubmit error: {ex}");
                await DisplayAlert("Error", "Could not create poll", "OK");
            }
        }

        // ?? Voting ????????????????????????????????????????????????????????????

        // Tapping directly on an option row
        private async void OnOptionTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not PollOptionViewModel opt) return;

            // Find the parent poll
            var poll = _polls.FirstOrDefault(p => p.Options.Contains(opt));
            if (poll == null || poll.IsExpired) return;

            await CastVoteAsync(poll, opt.OptionIndex);
        }

        // "Vote" / "Change Vote" button in footer
        private async void OnVoteButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is GroupPollViewModel poll)
                await ShowVoteSheetAsync(poll);
        }

        private async Task ShowVoteSheetAsync(GroupPollViewModel poll)
        {
            if (poll.IsExpired)
            {
                await DisplayAlert("Poll Closed", "This poll has already ended.", "OK");
                return;
            }

            if (poll.HasUserVoted && !poll.AllowMultipleVotes)
            {
                var change = await DisplayAlert(
                    "Change Your Vote?",
                    "You've already voted. Do you want to change your selection?",
                    "Yes, Change", "Keep My Vote");
                if (!change) return;
            }

            var options = poll.Options.Select(o => o.Text).ToArray();
            var picked = await DisplayActionSheet(poll.Question, "Cancel", null, options);

            if (string.IsNullOrEmpty(picked) || picked == "Cancel") return;

            var idx = Array.IndexOf(options, picked);
            if (idx >= 0) await CastVoteAsync(poll, idx);
        }

        private async Task CastVoteAsync(GroupPollViewModel poll, int optionIndex)
        {
            try
            {
                var ok = await GroupRepository.VoteOnPollAsync(
                    poll.MessageId, _currentUserPhone, optionIndex);

                if (ok)
                    await LoadPollsAsync();
                else
                    await DisplayAlert("Error", "Could not record your vote.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CastVoteAsync error: {ex}");
            }
        }

        // ?? Poll menu (admin) ?????????????????????????????????????????????????

        private async void OnPollMenuTapped(object sender, TappedEventArgs e)
        {
            if (!_isAdmin) return;
            if (e.Parameter is not GroupPollViewModel poll) return;

            var action = await DisplayActionSheet(
                poll.Question, "Cancel", null, "Edit Poll", "Delete Poll");

            switch (action)
            {
                case "Edit Poll": await ShowEditPollAsync(poll); break;
                case "Delete Poll": await DeletePollAsync(poll); break;
            }
        }

        private async Task ShowEditPollAsync(GroupPollViewModel poll)
        {
            var q = await DisplayPromptAsync("Edit Poll", "Update the question:",
                "Save", "Cancel", poll.Question);
            if (string.IsNullOrWhiteSpace(q)) return;

            var oldOpts = string.Join("\n", poll.Options.Select((o, i) => $"{i + 1}. {o.Text}"));
            var newOptsRaw = await DisplayPromptAsync("Edit Options",
                "One option per line:", "Save", "Cancel", oldOpts, maxLength: 500);
            if (string.IsNullOrWhiteSpace(newOptsRaw)) return;

            var newOpts = newOptsRaw.Split('\n')
                .Select(l => System.Text.RegularExpressions.Regex.Replace(l.Trim(), @"^\d+\.\s*", ""))
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            if (newOpts.Count < 2)
            {
                await DisplayAlert("Error", "Please provide at least 2 options.", "OK"); return;
            }

            if (await GroupRepository.EditPollAsync(poll.MessageId, _groupId, q, newOpts))
                await LoadPollsAsync();
            else
                await DisplayAlert("Error", "Could not update poll.", "OK");
        }

        private async Task DeletePollAsync(GroupPollViewModel poll)
        {
            if (!await DisplayAlert("Delete Poll",
                $"Permanently delete this poll?\n\n\"{poll.Question}\"",
                "Delete", "Cancel")) return;

            if (await GroupRepository.DeletePollAsync(poll.MessageId, _groupId))
                await LoadPollsAsync();
            else
                await DisplayAlert("Error", "Could not delete poll.", "OK");
        }

        // ?? Navigation ????????????????????????????????????????????????????????

        private async void OnBackClicked(object sender, EventArgs e)
            => await Navigation.PopAsync();
    }

    // ?? ViewModels ????????????????????????????????????????????????????????????

    public class GroupPollViewModel : BindableObject
    {
        public int MessageId { get; set; }
        public string Question { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public string CreatorPhone { get; set; } = string.Empty;
        public bool IsExpired { get; set; }
        public bool AllowMultipleVotes { get; set; }
        public int TotalVotes { get; set; }
        public bool HasUserVoted { get; set; }
        public bool IsCreator { get; set; }
        public DateTime ExpiresAt { get; set; }
        public ObservableCollection<PollOptionViewModel> Options { get; set; } = new();

        // ?? Display helpers ??????????????????????????????????????????????????

        public string CreatorInitial =>
            CreatorName.Length > 0 ? CreatorName[0].ToString().ToUpper() : "?";

        public string CreatedRelative
        {
            get
            {
                var d = DateTime.UtcNow - CreatedAt;
                if (d.TotalMinutes < 1) return "just now";
                if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m ago";
                if (d.TotalHours < 24) return $"{(int)d.TotalHours}h ago";
                if (d.TotalDays < 7) return $"{(int)d.TotalDays}d ago";
                return CreatedAt.ToString("MMM d");
            }
        }

        public string ExpiryDisplay
        {
            get
            {
                if (IsExpired) return "ended";
                if (ExpiresAt == default) return "no expiry";
                var left = ExpiresAt - DateTime.UtcNow;
                if (left.TotalMinutes < 60) return $"{(int)left.TotalMinutes}m left";
                if (left.TotalHours < 24) return $"{(int)left.TotalHours}h left";
                return $"{(int)left.TotalDays}d left";
            }
        }

        // Status pill
        public string StatusText => IsExpired ? "Closed" : "Live";
        public Color StatusBgColor => IsExpired
            ? Color.FromArgb("#3A1A1A")
            : Color.FromArgb("#0D2626");
        public Color StatusTextColor => IsExpired
            ? Color.FromArgb("#FF3B6F")
            : Color.FromArgb("#008080");

        // Footer
        public string TotalVotesDisplay =>
            TotalVotes == 1 ? "1 vote" : $"{TotalVotes} votes";

        // Show the Vote / Change Vote button only if poll is open
        public bool ShowVoteButton => !IsExpired && (!HasUserVoted || AllowMultipleVotes);
        public bool ShowVotedBadge => HasUserVoted && (IsExpired || !AllowMultipleVotes);

        public string VoteButtonText => HasUserVoted ? "Change Vote" : "Vote Now";
        public Color VoteButtonBgColor => HasUserVoted
            ? Color.FromArgb("#FF3B6F")
            : Color.FromArgb("#008080");

        // ?? Percentages + fill widths ????????????????????????????????????????

        public void UpdatePercentages(string currentUserPhone, double pageWidth)
        {
            HasUserVoted = Options.Any(o => o.VoterPhones.Contains(currentUserPhone));
            bool showPercent = HasUserVoted || IsExpired;

            for (int i = 0; i < Options.Count; i++)
            {
                var opt = Options[i];
                opt.UserVotedThis = opt.VoterPhones.Contains(currentUserPhone);
                opt.ShowPercent = showPercent;

                double pct = TotalVotes > 0
                    ? (double)opt.VoteCount / TotalVotes * 100.0
                    : 0;

                opt.VotePercentage = $"{pct:F0}%";

                // Pixel width for fill bar (min 4px so bar is always visible when voted)
                double fillPx = showPercent && pct > 0
                    ? Math.Max(4, pageWidth * pct / 100.0)
                    : 0;

                opt.FillWidthRequest = fillPx;

                // Whether this option is "winning" (highest votes) — teal, otherwise pink dim
                bool isLeading = TotalVotes > 0 &&
                    opt.VoteCount == Options.Max(o => o.VoteCount) &&
                    opt.VoteCount > 0;

                opt.FillColor = isLeading
                    ? Color.FromArgb(opt.UserVotedThis ? "#1A3A3A" : "#112222")
                    : Color.FromArgb("#1A1818");

                opt.OptionBorderColor = opt.UserVotedThis
                    ? Color.FromArgb("#008080")
                    : Color.FromArgb("#2A2A2A");

                opt.LetterColor = opt.UserVotedThis
                    ? Color.FromArgb("#008080")
                    : Color.FromArgb("#FF3B6F");

                opt.PercentColor = isLeading
                    ? Color.FromArgb("#008080")
                    : Color.FromArgb("#AAAAAA");
            }
        }
    }

    public class PollOptionViewModel : BindableObject
    {
        public string Text { get; set; } = string.Empty;
        public List<string> VoterPhones { get; set; } = new();
        public string OptionLetter { get; set; } = "A";
        public int OptionIndex { get; set; }

        public int VoteCount => VoterPhones.Count;
        public string VotePercentage { get; set; } = "0%";
        public double FillWidthRequest { get; set; } = 0;
        public Color FillColor { get; set; } = Color.FromArgb("#1A1A1A");
        public Color OptionBorderColor { get; set; } = Color.FromArgb("#2A2A2A");
        public Color LetterColor { get; set; } = Color.FromArgb("#FF3B6F");
        public Color PercentColor { get; set; } = Color.FromArgb("#AAAAAA");
        public bool UserVotedThis { get; set; }
        public bool ShowPercent { get; set; }
    }
}