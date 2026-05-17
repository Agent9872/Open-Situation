using Lock.Chat.Services;
using Lock.Models;
using Lock.Pages.Profile;
using Lock.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Lock.Services.Admin;

namespace Lock.Pages.Chat
{
    [QueryProperty(nameof(GroupId), "groupId")]
    public partial class GroupChatPage : ContentPage
    {
        private string _groupId = string.Empty;
        private string _currentUserPhone = string.Empty;
        private Group? _group;
        private GroupMember? _currentMember;
        private ObservableCollection<GroupMessage> _messages = new();
        private int? _replyToMessageId;
        private System.Timers.Timer? _pollTimer;
        private Dictionary<int, PollData> _pollCache = new();
        private bool _attachmentPanelOpen = false;

        public string GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                if (!string.IsNullOrEmpty(_groupId))
                    _ = InitializePageAsync();
            }
        }

        public GroupChatPage()
        {
            InitializeComponent();
            _currentUserPhone = Preferences.Get("current_user_phone", string.Empty);
            MessagesCollectionView.ItemsSource = _messages;

            MessagingCenter.Subscribe<GroupPollsPage, PollUpdateMessage>(
                this, "PollUpdated", async (sender, msg) =>
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var pollMsg = _messages.FirstOrDefault(m => m.Id == msg.MessageId);
                        if (pollMsg == null) return;

                        if (msg.IsDeleted)
                        {
                            _messages.Remove(pollMsg);
                            return;
                        }

                        _pollCache.Remove(pollMsg.Id);
                        var db = GroupDatabaseService.GetConnection();
                        var fresh = await db.Table<GroupMessage>()
                            .Where(m => m.Id == msg.MessageId)
                            .FirstOrDefaultAsync();

                        if (fresh != null)
                        {
                            pollMsg.PollJson = fresh.PollJson;
                            pollMsg.Content = fresh.Content;
                            await LoadPollDataAsync(pollMsg);
                            var idx = _messages.IndexOf(pollMsg);
                            if (idx >= 0)
                            {
                                _messages[idx] = pollMsg;
                                MessagesCollectionView.ScrollTo(idx,
                                    position: ScrollToPosition.MakeVisible, animate: false);
                            }
                        }
                    });
                });
        }

        // ?? Init ?????????????????????????????????????????????????????????????

        private async Task InitializePageAsync()
        {
            try
            {
                _isPageActive = true;

                await GroupDatabaseService.InitializeAsync();
                _group = await GroupRepository.GetGroupAsync(_groupId);

                if (_group == null)
                {
                    await DisplayAlert("Error", "Group not found", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                _currentMember = await GroupRepository.GetMemberAsync(_groupId, _currentUserPhone);

                if (_currentMember == null || _currentMember.IsBanned)
                {
                    await DisplayAlert("Access Denied", "You are not a member of this group", "OK");
                    await Navigation.PopAsync();
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(PopulateHeader);
                await LoadMessagesAsync();
                await GroupRepository.MarkAsReadAsync(_groupId, _currentUserPhone);

                if (PollAttachFrame != null)
                    PollAttachFrame.IsVisible = _currentMember.IsPrivileged;

                StartMessagePolling();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GroupChatPage init error: {ex}");
                await DisplayAlert("Error", "Could not load group chat", "OK");
            }
        }
        private void PopulateHeader()
        {
            if (_group == null) return;

            GroupNameLabel.Text = _group.Name;
            GroupTypeIconLabel.Text = _group.GroupTypeIcon;

            var mood = string.IsNullOrEmpty(_group.MoodFilter) ? "" : $" · {_group.MoodFilter}";
            GroupSubtitleLabel.Text = $"{_group.MemberCount} members{mood}";

            if (_group.HasCoverImage && !string.IsNullOrEmpty(_group.CoverImagePath))
            {
                GroupAvatarImage.Source = ImageSource.FromFile(_group.CoverImagePath);
                GroupAvatarImage.IsVisible = true;
                GroupAvatarFallback.IsVisible = false;
            }
            else
            {
                GroupAvatarFallback.IsVisible = true;
                GroupAvatarImage.IsVisible = false;
                GroupAvatarInitial.Text = _group.Name.Length > 0
                    ? _group.Name[0].ToString().ToUpper()
                    : "G";
            }
        }

        // ?? Messages ?????????????????????????????????????????????????????????

        private async Task LoadMessagesAsync()
        {
            try
            {
                var msgs = await GroupRepository.GetMessagesAsync(_groupId, take: 60);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _messages.Clear();

                    for (int i = 0; i < msgs.Count; i++)
                    {
                        var msg = msgs[i];
                        DecryptIfNeeded(msg);

                        msg.IsOutgoing = msg.SenderPhone == _currentUserPhone;
                        msg.ShowAvatar = !msg.IsSystemMessage && !msg.IsOutgoing &&
                                          (i == 0 || msgs[i - 1].SenderPhone != msg.SenderPhone);
                        msg.ShowSenderName = !msg.IsSystemMessage && !msg.IsOutgoing &&
                                              (i == 0 || msgs[i - 1].SenderPhone != msg.SenderPhone);
                        msg.SenderInitial = !string.IsNullOrEmpty(msg.SenderName) && msg.SenderName.Length > 0
                            ? msg.SenderName[0].ToString().ToUpper()
                            : string.Empty;

                        if (msg.MessageType == GroupMessageType.Poll)
                            _ = LoadPollDataAsync(msg);

                        _messages.Add(msg);
                    }

                    ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadMessages error: {ex}");
            }
        }
        private void DecryptIfNeeded(GroupMessage msg)
        {
            // For system messages or non-encrypted messages with content
            if (msg.IsSystemMessage || (!msg.IsEncrypted && !string.IsNullOrEmpty(msg.Content)))
            {
                if (string.IsNullOrEmpty(msg.DisplayContent) && !string.IsNullOrEmpty(msg.Content))
                    msg.SetDecryptedContent(msg.Content);
                return;
            }

            // For encrypted messages
            if (msg.IsEncrypted && !string.IsNullOrEmpty(msg.EncryptedContent))
            {
                try
                {
                    var dec = DecryptMessage(msg.EncryptedContent, _groupId);
                    msg.SetDecryptedContent(dec);
                    if (string.IsNullOrEmpty(msg.Content)) msg.Content = dec;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Decrypt failed for {msg.Id}: {ex.Message}");
                    msg.SetDecryptedContent("?? Encrypted");
                }
            }
            // For non-encrypted but empty content
            else if (!msg.IsEncrypted && string.IsNullOrEmpty(msg.Content) && msg.MessageType == GroupMessageType.Image)
            {
                msg.SetDecryptedContent(string.Empty);
            }
        }
        private void StartMessagePolling()
        {
            try
            {
                StopPolling(); // Ensure any existing timer is cleaned up

                _pollTimer = new System.Timers.Timer(3000);
                _pollTimer.Elapsed += async (s, e) =>
                {
                    if (_isPageActive && _currentMember != null)
                        await PollForNewMessagesAsync();
                };
                _pollTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StartMessagePolling error: {ex}");
            }
        }

        private bool _isPageActive = true;

        private async Task PollForNewMessagesAsync()
        {
            // Don't poll if page is no longer active
            if (!_isPageActive || _pollTimer == null) return;

            try
            {
                // Check if we're still a member before polling
                var isStillMember = await GroupRepository.IsMemberAsync(_groupId, _currentUserPhone);
                if (!isStillMember)
                {
                    Debug.WriteLine("User is no longer a member, stopping polling");
                    StopPolling();
                    return;
                }

                var lastId = _messages.LastOrDefault()?.Id ?? 0;
                var db = GroupDatabaseService.GetConnection();

                var newMsgs = await db.Table<GroupMessage>()
      .Where(m => m.GroupId == _groupId && m.Id > lastId && !m.IsDeleted && !m.IsSystemMessage)
      .OrderBy(m => m.SentAt)
      .ToListAsync();

                if (!newMsgs.Any()) return;

                var lastMsg = _messages.LastOrDefault(m => !m.IsSystemMessage);

                foreach (var msg in newMsgs)
                {
                    DecryptIfNeeded(msg);
                    msg.IsOutgoing = msg.SenderPhone == _currentUserPhone;

                    bool isDifferentSender = lastMsg == null || lastMsg.SenderPhone != msg.SenderPhone;
                    msg.ShowSenderName = !msg.IsSystemMessage && !msg.IsOutgoing && isDifferentSender;
                    msg.ShowAvatar = msg.ShowSenderName;

                    msg.SenderInitial = msg.SenderName?.Length > 0
                        ? msg.SenderName[0].ToString().ToUpper()
                        : "?";

                    if (msg.MessageType == GroupMessageType.Poll)
                        _ = LoadPollDataAsync(msg);

                    lastMsg = msg;
                }

                // Only update UI if page is still active
                if (_isPageActive)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foreach (var msg in newMsgs)
                            _messages.Add(msg);
                        ScrollToBottom();
                    });
                }

                await GroupRepository.MarkAsReadAsync(_groupId, _currentUserPhone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PollForNewMessages error: {ex}");
                // If we get an error that might indicate the user is no longer a member, stop polling
                if (ex.Message.Contains("no such table") || ex.Message.Contains("Group not found"))
                {
                    StopPolling();
                }
            }
        }
        private void ScrollToBottom()
        {
            if (_messages.Count == 0) return;
            try
            {
                MessagesCollectionView.ScrollTo(
                    _messages.Last(), position: ScrollToPosition.End, animate: false);
            }
            catch { }
        }

        // ?? Send ?????????????????????????????????????????????????????????????

        private async void OnSendMessageTapped(object sender, EventArgs e)
        {
            try
            {
                var text = MessageEditor?.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(text)) return;

                if (MessageEditor != null) MessageEditor.Text = string.Empty;

                var msg = await GroupRepository.SendMessageAsync(
                    _groupId, _currentUserPhone, text,
                    replyToMessageId: _replyToMessageId ?? 0);

                if (msg.IsEncrypted && !string.IsNullOrEmpty(msg.EncryptedContent))
                {
                    try
                    {
                        var dec = DecryptMessage(msg.EncryptedContent, _groupId);
                        msg.SetDecryptedContent(dec);
                        msg.Content = dec;
                    }
                    catch { msg.SetDecryptedContent("?? Sent encrypted"); }
                }
                else if (!string.IsNullOrEmpty(msg.Content))
                {
                    msg.SetDecryptedContent(msg.Content);
                }

                msg.IsOutgoing = true;
                msg.ShowAvatar = false;
                msg.ShowSenderName = false;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _messages.Add(msg);
                    ScrollToBottom();
                    ClearReply();
                });

                MessagingCenter.Send(this, "GroupsUpdated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnSendMessage error: {ex}");
                await DisplayAlert("Error", "Could not send message: " + ex.Message, "OK");
            }
        }

        // Track message sent
        private async Task TrackGroupMessageSentAsync(GroupMessage message)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupMembershipAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    message.SenderPhone,
                    "Sent Message",
                    message.SenderPhone);
                Debug.WriteLine($"[TRACKING] Group message sent: GroupId={_groupId}, User={message.SenderPhone}, MessageId={message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackGroupMessageSentAsync error: {ex}");
            }
        }

        // Track message edit
        private async Task TrackGroupMessageEditAsync(GroupMessage message, string oldContent)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupUpdateAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    "Message Edited",
                    oldContent,
                    message.DisplayContent,
                    message.SenderPhone);
                Debug.WriteLine($"[TRACKING] Group message edited: GroupId={_groupId}, User={message.SenderPhone}, MessageId={message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackGroupMessageEditAsync error: {ex}");
            }
        }

        // Track message deletion
        private async Task TrackGroupMessageDeletionAsync(GroupMessage message, string action)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupMembershipAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    message.SenderPhone,
                    action,
                    _currentUserPhone);
                Debug.WriteLine($"[TRACKING] Group message {action}: GroupId={_groupId}, User={message.SenderPhone}, MessageId={message.Id}, DeletedBy={_currentUserPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackGroupMessageDeletionAsync error: {ex}");
            }
        }

        // Track poll creation
        private async Task TrackPollCreationAsync(GroupMessage message, GroupPoll poll)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupUpdateAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    "Poll Created",
                    string.Empty,
                    $"{poll.Question} with {poll.Options.Count} options",
                    message.SenderPhone);
                Debug.WriteLine($"[TRACKING] Poll created: GroupId={_groupId}, User={message.SenderPhone}, Question={poll.Question}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPollCreationAsync error: {ex}");
            }
        }

        // Track poll vote
        private async Task TrackPollVoteAsync(GroupMessage message, string optionText)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupMembershipAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    _currentUserPhone,
                    "Voted in Poll",
                    _currentUserPhone);
                Debug.WriteLine($"[TRACKING] Poll vote: GroupId={_groupId}, User={_currentUserPhone}, Option={optionText}, PollId={message.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackPollVoteAsync error: {ex}");
            }
        }

        // Track image upload
        private async Task TrackImageUploadAsync(List<string> imagePaths)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupMembershipAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    _currentUserPhone,
                    "Uploaded Images",
                    _currentUserPhone);
                Debug.WriteLine($"[TRACKING] Images uploaded: GroupId={_groupId}, User={_currentUserPhone}, Count={imagePaths.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackImageUploadAsync error: {ex}");
            }
        }

        // Track event creation
        private async Task TrackEventCreationAsync(string eventTitle)
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupUpdateAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    "Event Created",
                    string.Empty,
                    eventTitle,
                    _currentUserPhone);
                Debug.WriteLine($"[TRACKING] Event created: GroupId={_groupId}, User={_currentUserPhone}, Event={eventTitle}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackEventCreationAsync error: {ex}");
            }
        }

        // Track member leaving group
        private async Task TrackMemberLeaveAsync()
        {
            try
            {
                await UserTrackingService.Instance.TrackGroupMembershipAsync(
                    _groupId,
                    _group?.Name ?? "Unknown",
                    _currentUserPhone,
                    "Left Group",
                    _currentUserPhone);
                Debug.WriteLine($"[TRACKING] Member left: GroupId={_groupId}, User={_currentUserPhone}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrackMemberLeaveAsync error: {ex}");
            }
        }


        // ?? Attachment panel ?????????????????????????????????????????????????

        private void OnAttachmentTapped(object sender, EventArgs e)
        {
            _attachmentPanelOpen = !_attachmentPanelOpen;
            AttachmentPanel.IsVisible = _attachmentPanelOpen;
        }

        private void OnCloseAttachmentPanel(object sender, EventArgs e)
        {
            _attachmentPanelOpen = false;
            AttachmentPanel.IsVisible = false;
        }

        private async void OnSendPhotosTapped(object sender, EventArgs e)
        {
            OnCloseAttachmentPanel(sender, e);
            await SendImagesAsync();
        }

        private async void OnCreatePollFromPanelTapped(object sender, EventArgs e)
        {
            OnCloseAttachmentPanel(sender, e);
            await Navigation.PushAsync(
                new GroupPollsPage(_groupId, _currentUserPhone,
                                   _currentMember?.IsPrivileged == true));
        }

        private async void OnCreateEventFromPanelTapped(object sender, EventArgs e)
        {
            OnCloseAttachmentPanel(sender, e);
            await ShowCreateEventAsync();
        }

        // ?? Image send ???????????????????????????????????????????????????????

        private async Task SendImagesAsync()
        {
            try
            {
                var results = await FilePicker.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select photos",
                    FileTypes = FilePickerFileType.Images
                });

                if (results == null || !results.Any()) return;

                var savedPaths = new List<string>();
                foreach (var r in results)
                {
                    var dest = Path.Combine(FileSystem.AppDataDirectory,
                        $"group_img_{Guid.NewGuid():N}{Path.GetExtension(r.FileName)}");
                    using var src = await r.OpenReadAsync();
                    using var dst = File.Open(dest, FileMode.Create);
                    await src.CopyToAsync(dst);
                    savedPaths.Add(dest);
                }

                if (!savedPaths.Any()) return;

                // ========== TRACK IMAGE UPLOAD ==========
                await TrackImageUploadAsync(savedPaths);

                // Show preview page with captions option
                var previewPage = new ImagePreviewPage(savedPaths, _groupId, _currentUserPhone, _replyToMessageId);

                // Subscribe to the ImagesSent message
                MessagingCenter.Subscribe<ImagePreviewPage>(this, "ImagesSent", async (sender) =>
                {
                    MessagingCenter.Unsubscribe<ImagePreviewPage>(this, "ImagesSent");
                    await LoadMessagesAsync();
                    ClearReply();
                });

                await Navigation.PushModalAsync(previewPage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendImages error: {ex}");
                await DisplayAlert("Error", "Could not select images: " + ex.Message, "OK");
            }
        }
        
        // ?? Poll ?????????????????????????????????????????????????????????????

        private async Task LoadPollDataAsync(GroupMessage message)
        {
            if (string.IsNullOrEmpty(message.PollJson)) return;

            try
            {
                var poll = System.Text.Json.JsonSerializer
                    .Deserialize<GroupPoll>(message.PollJson);
                if (poll == null) return;

                var pollData = new PollData
                {
                    Question = poll.Question,
                    TotalVotes = poll.TotalVotes,
                    AllowMultipleVotes = poll.AllowMultipleVotes,
                    ExpiresAt = poll.ExpiresAt,
                    IsExpired = poll.ExpiresAt.HasValue &&
                                         DateTime.UtcNow > poll.ExpiresAt.Value,
                    CurrentUserPhone = _currentUserPhone
                };

                double pageWidth = Width > 0 ? Width - 96 : 240;

                for (int i = 0; i < poll.Options.Count; i++)
                {
                    var opt = poll.Options[i];
                    double pct = pollData.TotalVotes > 0
                        ? (double)opt.VoterPhones.Count / pollData.TotalVotes * 100.0
                        : 0;

                    pollData.Options.Add(new PollOptionData
                    {
                        Text = opt.Text,
                        VoteCount = opt.VoterPhones.Count,
                        VoterPhones = opt.VoterPhones.ToList(),
                        VotePercentage = $"{pct:F0}%",
                        FillWidthRequest = pollData.TotalVotes > 0
                            ? Math.Max(4, pageWidth * pct / 100.0)
                            : 0
                    });
                }

                pollData.HasUserVoted = pollData.Options.Any(
                    o => o.VoterPhones.Contains(_currentUserPhone));

                _pollCache[message.Id] = pollData;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    message.PollQuestion = pollData.Question;
                    message.PollOptions = pollData.Options;
                    message.TotalVotesDisplay = pollData.TotalVotes == 1
                        ? "1 vote" : $"{pollData.TotalVotes} votes";
                    message.IsPollExpired = pollData.IsExpired;
                    message.ShowVoteButton = !pollData.IsExpired &&
                                               (!pollData.HasUserVoted || pollData.AllowMultipleVotes);
                    message.VoteButtonText = pollData.HasUserVoted ? "Change Vote" : "Vote Now";
                    message.VoteButtonColor = pollData.HasUserVoted ? "#FF3B6F" : "#008080";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPollData error: {ex}");
            }
        }

        private async void OnPollVoteClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not GroupMessage message)
                return;

            try
            {
                btn.IsEnabled = false;

                if (!_pollCache.TryGetValue(message.Id, out var pollData))
                {
                    await LoadPollDataAsync(message);
                    pollData = _pollCache.GetValueOrDefault(message.Id);
                }
                if (pollData == null) { btn.IsEnabled = true; return; }

                if (pollData.IsExpired)
                {
                    await DisplayAlert("Poll Closed", "This poll has ended.", "OK");
                    btn.IsEnabled = true;
                    return;
                }

                if (pollData.HasUserVoted && !pollData.AllowMultipleVotes)
                {
                    var change = await DisplayAlert("Change Vote?",
                        "You already voted. Change your selection?",
                        "Yes, Change", "Keep My Vote");
                    if (!change) { btn.IsEnabled = true; return; }
                }

                var options = pollData.Options.Select(o => o.Text).ToArray();
                var selected = await DisplayActionSheet(pollData.Question, "Cancel", null, options);

                if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                { btn.IsEnabled = true; return; }

                var idx = Array.IndexOf(options, selected);
                if (idx < 0) { btn.IsEnabled = true; return; }

                btn.Text = "Voting…";
                var ok = await GroupRepository.VoteOnPollAsync(message.Id, _currentUserPhone, idx);

                if (ok)
                {
                    // ========== TRACK POLL VOTE ==========
                    await TrackPollVoteAsync(message, selected);

                    _pollCache.Remove(message.Id);
                    var db = GroupDatabaseService.GetConnection();
                    var fresh = await db.Table<GroupMessage>()
                        .Where(m => m.Id == message.Id).FirstOrDefaultAsync();
                    if (fresh != null)
                    {
                        message.PollJson = fresh.PollJson;
                        await LoadPollDataAsync(message);
                        var mIdx = _messages.IndexOf(message);
                        if (mIdx >= 0)
                            await MainThread.InvokeOnMainThreadAsync(() =>
                                _messages[mIdx] = message);
                    }
                }
                else
                {
                    await DisplayAlert("Error", "Could not record your vote.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPollVoteClicked error: {ex}");
            }
            finally
            {
                btn.Text = message.VoteButtonText;
                btn.IsEnabled = true;
            }
        }
        // ?? Message menu ?????????????????????????????????????????????????????

        private async void OnMessageMenuTapped(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not GroupMessage message)
                return;

            try
            {
                bool isMine = message.SenderPhone == _currentUserPhone;

                var options = isMine
                    ? new[] { "Reply", "Edit", "Forward", "Copy", "Delete for me", "Delete for everyone", "Info" }
                    : new[] { "Reply", "Forward", "Copy", "Delete for me", "Report", "Block", "Info" };

                var action = await DisplayActionSheet("Message", "Cancel", null, options);

                switch (action)
                {
                    case "Reply": SetReplyTo(message); break;
                    case "Edit": await EditMessageAsync(message); break;
                    case "Forward": await ForwardMessageAsync(message); break;
                    case "Copy": await CopyMessageAsync(message); break;
                    case "Delete for me": await DeleteForMeAsync(message); break;
                    case "Delete for everyone": await DeleteForEveryoneAsync(message); break;
                    case "Report": await ReportMessageAsync(message); break;
                    case "Block": await BlockUserAsync(message.SenderPhone, message.SenderName); break;
                    case "Info": await ShowMessageInfoAsync(message); break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MessageMenu error: {ex}");
            }
        }

        private async Task EditMessageAsync(GroupMessage message)
        {
            var newText = await DisplayPromptAsync("Edit Message", "Update your message:",
                "Save", "Cancel", message.DisplayContent, maxLength: 2000);
            if (string.IsNullOrWhiteSpace(newText)) return;

            string oldContent = message.DisplayContent;

            if (await GroupRepository.EditMessageAsync(message.Id, _currentUserPhone, newText))
            {
                // ========== TRACK MESSAGE EDIT ==========
                await TrackGroupMessageEditAsync(message, oldContent);

                // Refresh the specific message instead of reloading all messages
                await RefreshSingleMessageAsync(message);
            }
            else
                await DisplayAlert("Error", "Could not edit message.", "OK");
        }
        private async Task RefreshSingleMessageAsync(GroupMessage oldMessage)
        {
            try
            {
                var db = GroupDatabaseService.GetConnection();
                var freshMsg = await db.Table<GroupMessage>()
                    .Where(m => m.Id == oldMessage.Id)
                    .FirstOrDefaultAsync();

                if (freshMsg != null)
                {
                    // Decrypt if needed
                    DecryptIfNeeded(freshMsg);

                    // Preserve UI properties
                    freshMsg.IsOutgoing = oldMessage.IsOutgoing;
                    freshMsg.ShowAvatar = oldMessage.ShowAvatar;
                    freshMsg.ShowSenderName = oldMessage.ShowSenderName;
                    freshMsg.SenderInitial = oldMessage.SenderInitial;

                    // Find and replace in the collection
                    var index = _messages.IndexOf(oldMessage);
                    if (index >= 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            _messages[index] = freshMsg;
                            // Force refresh of that specific item
                            var temp = _messages.ToList();
                            _messages.Clear();
                            foreach (var msg in temp)
                                _messages.Add(msg);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshSingleMessage error: {ex}");
                // Fallback to reload all messages
                await LoadMessagesAsync();
            }
        }
        private async Task ForwardMessageAsync(GroupMessage message)
        {
            var groups = await GroupRepository.GetMyGroupsAsync(_currentUserPhone);
            var others = groups.Where(g => g.Id != _groupId).ToList();
            if (!others.Any())
            {
                await DisplayAlert("No Groups", "No other groups to forward to.", "OK");
                return;
            }

            var pick = await DisplayActionSheet("Forward to", "Cancel", null,
                others.Select(g => g.Name).ToArray());
            if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;

            var target = others.First(g => g.Name == pick);
            await GroupRepository.SendMessageAsync(target.Id, _currentUserPhone,
                $"? Forwarded: {message.DisplayContent}");
            await DisplayAlert("Forwarded", $"Message forwarded to {pick}", "OK");
        }

        private async Task CopyMessageAsync(GroupMessage message)
        {
            await Clipboard.Default.SetTextAsync(message.DisplayContent);
            await DisplayAlert("Copied", "Message copied to clipboard.", "OK");
        }

        private async Task DeleteForMeAsync(GroupMessage message)
        {
            if (!await DisplayAlert("Delete", "Delete for yourself only?", "Delete", "Cancel")) return;
            if (await GroupRepository.DeleteMessageAsync(_groupId, message.Id, _currentUserPhone))
            {
                // ========== TRACK MESSAGE DELETION ==========
                await TrackGroupMessageDeletionAsync(message, "Deleted for self");
                await LoadMessagesAsync();
            }
            else
                await DisplayAlert("Error", "Could not delete.", "OK");
        }

        private async Task DeleteForEveryoneAsync(GroupMessage message)
        {
            bool isAdmin = _currentMember?.IsPrivileged == true;
            if (!isAdmin && message.SenderPhone != _currentUserPhone)
            {
                await DisplayAlert("Permission Denied", "Only admins can delete others' messages.", "OK");
                return;
            }
            if (!isAdmin && (DateTime.UtcNow - message.SentAt).TotalMinutes > 5)
            {
                await DisplayAlert("Too Late", "Messages can only be deleted for everyone within 5 minutes.", "OK");
                return;
            }
            if (!await DisplayAlert("Delete for Everyone",
                "This cannot be undone.", "Delete", "Cancel")) return;

            if (await GroupRepository.DeleteMessageAsync(_groupId, message.Id, _currentUserPhone))
            {
                // ========== TRACK MESSAGE DELETION ==========
                await TrackGroupMessageDeletionAsync(message, "Deleted for everyone");
                await LoadMessagesAsync();
            }
            else
                await DisplayAlert("Error", "Could not delete.", "OK");
        }

        private async Task ReportMessageAsync(GroupMessage message)
        {
            var reason = await DisplayPromptAsync("Report", "Reason:", "Submit", "Cancel");
            if (string.IsNullOrWhiteSpace(reason)) return;
            await DisplayAlert("Report Submitted", "Our team will review the message.", "OK");
        }

        private async Task BlockUserAsync(string phone, string name)
        {
            if (!await DisplayAlert("Block", $"Block {name}?", "Block", "Cancel")) return;
            if (await ChatRepository.BlockUserAsync(_currentUserPhone, phone))
                await DisplayAlert("Blocked", $"{name} has been blocked.", "OK");
            else
                await DisplayAlert("Error", "Could not block user.", "OK");
        }

        private async Task ShowMessageInfoAsync(GroupMessage message)
        {
            var info = $"Sent: {message.SentAt:MMM dd, yyyy 'at' h:mm tt}\n" +
                       $"Type: {message.MessageType}" +
                       (message.IsEdited ? "\nEdited" : "") +
                       (message.IsDeleted ? "\nDeleted" : "");
            await DisplayAlert("Message Info", info, "OK");
        }

        public static async Task<bool> DeleteMessageAsync(
        string groupId,
        int messageId,
        string requestorPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var msg = await db.Table<GroupMessage>()
                .Where(m => m.Id == messageId)
                .FirstOrDefaultAsync();
            if (msg == null) return false;

            var member = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == requestorPhone)
                .FirstOrDefaultAsync();

            // CRITICAL FIX: Only allow deletion if:
            // 1. User is the message sender, OR
            // 2. User is an admin/creator (privileged)
            bool canDelete = msg.SenderPhone == requestorPhone || (member?.IsPrivileged == true);

            if (!canDelete)
            {
                Debug.WriteLine($"User {requestorPhone} attempted to delete message {messageId} but is not authorized");
                return false;
            }

            msg.IsDeleted = true;
            msg.Content = string.Empty;
            msg.EncryptedContent = string.Empty; // Also clear encrypted content
            await db.UpdateAsync(msg);

            Debug.WriteLine($"Message {messageId} deleted by {requestorPhone} (IsSender: {msg.SenderPhone == requestorPhone}, IsAdmin: {member?.IsPrivileged})");
            return true;
        }

        // ?? Image viewer ?????????????????????????????????????????????????????

        private async void OnImageTapped(object sender, TappedEventArgs e)
        {
            try
            {
                var paths = new List<string>();
                if (e.Parameter is string s) paths.Add(s);
                else if (sender is VisualElement ve &&
                         ve.BindingContext is GroupMessage gm &&
                         gm.MediaPaths?.Any() == true)
                    paths = gm.MediaPaths.ToList();

                if (paths.Any())
                    await Navigation.PushModalAsync(new FullScreenMediaPage(paths, 0));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnImageTapped error: {ex}");
            }
        }

        // ?? Reply ?????????????????????????????????????????????????????????????

        private void SetReplyTo(GroupMessage message)
        {
            _replyToMessageId = message.Id;

            // FIX: Ensure we use the decrypted content for preview
            string decryptedContent = message.DisplayContent;

            // If DisplayContent is empty or still encrypted, try to decrypt
            if (string.IsNullOrEmpty(decryptedContent) || decryptedContent == "?? Encrypted")
            {
                if (message.IsEncrypted && !string.IsNullOrEmpty(message.EncryptedContent))
                {
                    try
                    {
                        decryptedContent = DecryptMessage(message.EncryptedContent, _groupId);
                    }
                    catch
                    {
                        decryptedContent = "Encrypted message";
                    }
                }
                else if (!string.IsNullOrEmpty(message.Content))
                {
                    decryptedContent = message.Content;
                }
            }

            string preview = message.MessageType switch
            {
                GroupMessageType.Poll => $"?? {message.PollQuestion ?? "Poll"}",
                GroupMessageType.Image => "?? Photo",
                GroupMessageType.Voice => "?? Voice message",
                GroupMessageType.Event => "?? Event",
                _ => decryptedContent
            };

            if (preview.Length > 80) preview = preview[..77] + "…";

            ReplyToNameLabel.Text = message.DisplaySenderName;
            ReplyToPreviewLabel.Text = preview;
            ReplyPreviewBanner.IsVisible = true;
        }
        private void ClearReply()
        {
            _replyToMessageId = null;
            ReplyPreviewBanner.IsVisible = false;
        }

        private void OnCancelReplyTapped(object sender, EventArgs e) => ClearReply();

        // ?? Events ????????????????????????????????????????????????????????????

        private async Task ShowCreateEventAsync()
        {
            var title = await DisplayPromptAsync("Event Title", "What is the event?", maxLength: 100);
            if (string.IsNullOrWhiteSpace(title)) return;

            var location = await DisplayPromptAsync("Location", "Where is it? (optional)", maxLength: 200);

            var ev = await GroupRepository.CreateGroupEventAsync(
                _groupId, _currentUserPhone,
                title.Trim(), string.Empty, location?.Trim() ?? string.Empty,
                DateTime.UtcNow.AddDays(7));

            // ========== TRACK EVENT CREATION ==========
            await TrackEventCreationAsync(title.Trim());

            await DisplayAlert("Event Created", $"'{ev.Title}' has been posted to the group!", "OK");
        }


        // ?? Encryption ????????????????????????????????????????????????????????

        private string DecryptMessage(string enc, string groupId)
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
                using var ms = new MemoryStream(cipher);
                using var cs = new System.Security.Cryptography.CryptoStream(
                    ms, dec, System.Security.Cryptography.CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DecryptMessage error: {ex.Message}");
                return "?? Encrypted message";
            }
        }

        // ?? Header actions ????????????????????????????????????????????????????

        private async void OnBackTapped(object sender, EventArgs e)
            => await Navigation.PopAsync();

        private async void OnGroupInfoTapped(object sender, EventArgs e)
        {
            if (_group == null) return;
            await Navigation.PushAsync(new GroupInfoPage(_groupId, _currentUserPhone));
        }

        private async void OnSearchTapped(object sender, EventArgs e)
            => await DisplayAlert("Search", "Search in group messages — coming soon", "OK");

        private async void OnMenuTapped(object sender, EventArgs e)
        {
            if (_group == null) return;

            bool isAdmin = _currentMember?.IsPrivileged == true;
            var opts = new List<string>
        { "Group Info", "Polls", "Mute Notifications", "Search Messages", "Members" }; // <-- Add "Members" here

            if (isAdmin)
            {
                opts.Add("Manage Members");
                opts.Add("Create Invite Link");
                opts.Add("Group Settings");
            }
            opts.Add("Leave Group");

            var action = await DisplayActionSheet(_group.Name, "Cancel", null, opts.ToArray());

            switch (action)
            {
                case "Group Info":
                    await Navigation.PushAsync(new GroupInfoPage(_groupId, _currentUserPhone));
                    break;
                case "Members": // <-- Add this case
                    await Navigation.PushAsync(new GroupMembersPage(_groupId, _currentUserPhone));
                    break;
                case "Polls":
                    await Navigation.PushAsync(
                        new GroupPollsPage(_groupId, _currentUserPhone, isAdmin));
                    break;
                case "Mute Notifications":
                    await ToggleMuteAsync();
                    break;
                case "Group Settings":
                    await Navigation.PushAsync(
                        new GroupSettingsPage(_groupId, _currentUserPhone));
                    break;
                case "Manage Members":
                    await Navigation.PushAsync(
                        new GroupMembersPage(_groupId, _currentUserPhone));
                    break;
                case "Create Invite Link":
                    await CreateInviteLinkAsync();
                    break;
                case "Leave Group":
                    await LeaveGroupAsync();
                    break;
            }
        }
        private async Task ToggleMuteAsync()
        {
            if (_currentMember == null) return;
            var db = GroupDatabaseService.GetConnection();
            _currentMember.IsMuted = !_currentMember.IsMuted;
            await db.UpdateAsync(_currentMember);
            await DisplayAlert(
                _currentMember.IsMuted ? "Muted" : "Unmuted",
                _currentMember.IsMuted
                    ? "Notifications disabled for this group."
                    : "Notifications enabled for this group.",
                "OK");
        }

        private async Task CreateInviteLinkAsync()
        {
            try
            {
                var expiry = await DisplayActionSheet(
                    "Invite link expiry", "Cancel", null,
                    "Never", "24 hours", "7 days");
                if (expiry == "Cancel") return;

                DateTime? exp = expiry switch
                {
                    "24 hours" => DateTime.UtcNow.AddHours(24),
                    "7 days" => DateTime.UtcNow.AddDays(7),
                    _ => null
                };

                var invite = await GroupRepository.CreateInviteLinkAsync(
                    _groupId, _currentUserPhone, exp);
                var link = $"lock://group/join/{invite.InviteCode}";

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = $"Join '{_group?.Name}' on Lock!\n\n{link}",
                    Title = "Invite to group"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateInviteLink error: {ex}");
            }
        }

        private async Task LeaveGroupAsync()
        {
            try
            {
                var member = await GroupRepository.GetMemberAsync(_groupId, _currentUserPhone);
                if (member?.Role == GroupMemberRole.Creator)
                {
                    await DisplayAlert(
                        "Cannot Leave",
                        "You created this group. Transfer ownership to another member first, or delete the group.",
                        "OK");
                    return;
                }

                StopPolling();

                bool confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
                    await DisplayAlert("Leave Group",
                        $"Leave '{_group?.Name}'? You can rejoin later if it's public.",
                        "Leave", "Cancel"));

                if (!confirm)
                {
                    StartMessagePolling();
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (LoadingOverlay != null) LoadingOverlay.IsVisible = true;
                });

                bool success = await GroupRepository.LeaveGroupAsync(_groupId, _currentUserPhone);

                if (success)
                {
                    await TrackMemberLeaveAsync();
                    MessagingCenter.Send(this, "GroupsUpdated");

                    // Full cleanup only on actual leave
                    _messages.Clear();
                    _pollCache.Clear();

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            while (Navigation.ModalStack.Count > 0)
                                await Navigation.PopModalAsync();
                            await Navigation.PopAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Navigation error: {ex}");
                            Application.Current?.MainPage?.Navigation.PopAsync();
                        }
                    });
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        DisplayAlert("Error", "Could not leave group. Please try again.", "OK"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LeaveGroupAsync error: {ex}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                    DisplayAlert("Error", "An error occurred while leaving the group.", "OK"));
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (LoadingOverlay != null) LoadingOverlay.IsVisible = false;
                });
            }
        }
        private void StopPolling()
        {
            try
            {
                if (_pollTimer != null)
                {
                    _pollTimer.Stop();
                    _pollTimer.Dispose();
                    _pollTimer = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopPolling error: {ex}");
            }
        }

        // ?? Lifecycle ?????????????????????????????????????????????????????????

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (string.IsNullOrEmpty(_groupId)) return;

            _isPageActive = true;

            // Unsubscribe before re-subscribing to avoid duplicate handlers
            MessagingCenter.Unsubscribe<GroupPollsPage, PollUpdateMessage>(this, "PollUpdated");
            MessagingCenter.Unsubscribe<ImagePreviewPage>(this, "ImagesSent");

            MessagingCenter.Subscribe<GroupPollsPage, PollUpdateMessage>(
                this, "PollUpdated", async (sender, msg) =>
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var pollMsg = _messages.FirstOrDefault(m => m.Id == msg.MessageId);
                        if (pollMsg == null) return;
                        if (msg.IsDeleted) { _messages.Remove(pollMsg); return; }
                        _pollCache.Remove(pollMsg.Id);
                        var db = GroupDatabaseService.GetConnection();
                        var fresh = await db.Table<GroupMessage>()
                            .Where(m => m.Id == msg.MessageId).FirstOrDefaultAsync();
                        if (fresh != null)
                        {
                            pollMsg.PollJson = fresh.PollJson;
                            pollMsg.Content = fresh.Content;
                            await LoadPollDataAsync(pollMsg);
                            var idx = _messages.IndexOf(pollMsg);
                            if (idx >= 0)
                                _messages[idx] = pollMsg;
                        }
                    });
                });

            _ = Task.Run(async () =>
            {
                await GroupRepository.MarkAsReadAsync(_groupId, _currentUserPhone);
                await LoadMessagesAsync();
                await MainThread.InvokeOnMainThreadAsync(StartMessagePolling);
            });
        }

        protected override void OnDisappearing()
        {
            try
            {
                _isPageActive = false;
                StopPolling();

                // Unsubscribe from messages
                MessagingCenter.Unsubscribe<GroupPollsPage, PollUpdateMessage>(this, "PollUpdated");
                MessagingCenter.Unsubscribe<ImagePreviewPage>(this, "ImagesSent");

                base.OnDisappearing();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnDisappearing error: {ex}");
            }
        }
    }

    // ?? Supporting classes ????????????????????????????????????????????????????

    public class PollData
    {
        public string Question { get; set; } = string.Empty;
        public List<PollOptionData> Options { get; set; } = new();
        public int TotalVotes { get; set; }
        public bool IsExpired { get; set; }
        public bool AllowMultipleVotes { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string CurrentUserPhone { get; set; } = string.Empty;
        public bool HasUserVoted { get; set; }
    }

    public class PollOptionData
    {
        public string Text { get; set; } = string.Empty;
        public int VoteCount { get; set; }
        public List<string> VoterPhones { get; set; } = new();
        public string VotePercentage { get; set; } = "0%";
        public double FillWidthRequest { get; set; } = 0;
    }

    public class PollUpdateMessage
    {
        public int MessageId { get; set; }
        public bool IsDeleted { get; set; }
    }
}