using Lock.Models;
using Lock.Chat.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class GroupRepository
    {
        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        public static async Task InitializeAsync()
        {
            // Supabase is already initialized in App.xaml.cs
            await Task.CompletedTask;
        }

        // ═══════════════════════════════════════════════════════════════
        // GROUP CRUD
        // ═══════════════════════════════════════════════════════════════

        public static async Task<Group> CreateGroupAsync(
            string name,
            string description,
            GroupType groupType,
            GroupVisibility visibility,
            string createdByPhone,
            string coverImagePath = "",
            string category = "",
            List<string>? interestTags = null,
            int maxMembers = 0,
            string moodFilter = "",
            bool isAnonymousAllowed = false,
            bool isEncrypted = true,
            bool requireApproval = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Group name cannot be empty.");

            if (string.IsNullOrWhiteSpace(createdByPhone))
                throw new ArgumentException("Creator phone number is required.");

            // Prevent recent duplicate group creation
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            var allGroups = await SupabaseService.GetAsync<Group>("Groups",
                $"Name=eq.{Uri.EscapeDataString(name)}&CreatedByPhone=eq.{Uri.EscapeDataString(createdByPhone)}&CreatedAt=gt.{fiveMinutesAgo:yyyy-MM-ddTHH:mm:ssZ}");

            if (allGroups.Any())
            {
                Debug.WriteLine($"Duplicate group creation attempt detected for '{name}'. Returning existing.");
                return allGroups.First();
            }

            // Create the group
            var group = new Group
            {
                Id = Guid.NewGuid().ToString(),
                Name = name.Trim(),
                Description = description?.Trim() ?? string.Empty,
                CoverImagePath = coverImagePath ?? string.Empty,
                CreatedByPhone = createdByPhone,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                GroupType = groupType,
                Visibility = visibility,
                Category = category?.Trim() ?? string.Empty,
                InterestTags = interestTags ?? new List<string>(),
                MaxMembers = maxMembers,
                MoodFilter = moodFilter?.Trim() ?? string.Empty,
                IsAnonymousAllowed = isAnonymousAllowed,
                IsEncrypted = isEncrypted,
                RequireApproval = requireApproval,
                IsActive = true,
                MemberCount = 1
            };

            var insertedGroup = await SupabaseService.InsertAndReturnAsync<Group>("Groups", group);
            if (insertedGroup == null) throw new Exception("Failed to create group");

            // Auto-add creator as owner
            var creatorName = await GetUserNameAsync(createdByPhone);
            var creatorImg = await GetUserProfileImageAsync(createdByPhone);

            var creatorMember = new GroupMember
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = insertedGroup.Id,
                UserPhone = createdByPhone,
                UserName = creatorName,
                UserProfileImagePath = creatorImg,
                Role = GroupMemberRole.Creator,
                JoinedAt = DateTime.UtcNow,
                LastReadAt = DateTime.UtcNow,
                IsAnonymous = false,
                IsMuted = false,
                IsBanned = false
            };

            await SupabaseService.InsertAsync("GroupMembers", creatorMember);

            // Check for duplicate creation message
            var thirtySecondsAgo = DateTime.UtcNow.AddSeconds(-30);
            var existingCreationMessages = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                $"GroupId=eq.{Uri.EscapeDataString(insertedGroup.Id)}&IsSystemMessage=eq.true&SentAt=gt.{thirtySecondsAgo:yyyy-MM-ddTHH:mm:ssZ}&limit=10");

            if (!existingCreationMessages.Any(m => m.Content.Contains("created this group")))
            {
                await PostSystemMessageAsync(insertedGroup.Id, $"👋 {creatorName} created this group");
                Debug.WriteLine($"System creation message posted for group {insertedGroup.Id}");
            }
            else
            {
                Debug.WriteLine($"Creation message already exists — skipping duplicate.");
            }

            return insertedGroup;
        }

        public static async Task<Group?> GetGroupAsync(string groupId)
        {
            var groups = await SupabaseService.GetAsync<Group>("Groups", $"Id=eq.{Uri.EscapeDataString(groupId)}&limit=1");
            return groups.FirstOrDefault();
        }

        public static async Task UpdateGroupAsync(Group group)
        {
            group.LastActiveAt = DateTime.UtcNow;
            await SupabaseService.UpdateAsync("Groups", $"Id=eq.{Uri.EscapeDataString(group.Id)}", group);
        }

        public static async Task DeleteGroupAsync(string groupId)
        {
            await SupabaseService.DeleteAsync("Groups", $"Id=eq.{Uri.EscapeDataString(groupId)}");
            await SupabaseService.DeleteAsync("GroupMembers", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
            await SupabaseService.DeleteAsync("GroupMessages", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
            await SupabaseService.DeleteAsync("GroupInvites", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
            await SupabaseService.DeleteAsync("GroupJoinRequests", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
            await SupabaseService.DeleteAsync("GroupEvents", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
            await SupabaseService.DeleteAsync("GroupPinnedMessages", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
        }

        // ═══════════════════════════════════════════════════════════════
        // DISCOVERY — PUBLIC GROUPS
        // ═══════════════════════════════════════════════════════════════

        public static async Task<List<Group>> GetPublicGroupsAsync(
            string currentUserPhone,
            string? searchQuery = null,
            GroupType? filterType = null,
            string? filterMood = null)
        {
            var query = await SupabaseService.GetAsync<Group>("Groups",
                $"IsActive=eq.true&Visibility=eq.Public&order=LastActiveAt.desc");

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.ToLower();
                query = query.Where(g =>
                    g.Name.ToLower().Contains(q) ||
                    g.Description.ToLower().Contains(q) ||
                    g.Category.ToLower().Contains(q)).ToList();
            }

            if (filterType.HasValue)
                query = query.Where(g => g.GroupType == filterType.Value).ToList();

            if (!string.IsNullOrEmpty(filterMood))
                query = query.Where(g =>
                    string.IsNullOrEmpty(g.MoodFilter) ||
                    string.Equals(g.MoodFilter, filterMood, StringComparison.OrdinalIgnoreCase)).ToList();

            // Mark membership status
            var memberships = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&IsBanned=eq.false");
            var memberGroupIds = memberships.Select(m => m.GroupId).ToHashSet();

            foreach (var g in query)
            {
                g.IsMember = memberGroupIds.Contains(g.Id);
            }

            return query.OrderByDescending(g => g.LastActiveAt).ToList();
        }

        public static async Task<List<Group>> GetMyGroupsAsync(string userPhone)
        {
            var memberships = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&IsBanned=eq.false");

            var groupIds = memberships.Select(m => m.GroupId).ToList();
            if (!groupIds.Any()) return new List<Group>();

            var allGroups = await SupabaseService.GetAsync<Group>("Groups", $"IsActive=eq.true");
            var myGroups = allGroups.Where(g => groupIds.Contains(g.Id)).ToList();

            // Attach unread counts and role info
            foreach (var g in myGroups)
            {
                var membership = memberships.First(m => m.GroupId == g.Id);
                g.IsMember = true;
                g.IsCreator = membership.Role == GroupMemberRole.Creator;
                g.IsAdmin = membership.Role == GroupMemberRole.Creator || membership.Role == GroupMemberRole.Admin;

                var messages = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                    $"GroupId=eq.{Uri.EscapeDataString(g.Id)}&IsDeleted=eq.false");
                var unread = messages.Count(msg => msg.SentAt > membership.LastReadAt && msg.SenderPhone != userPhone);
                g.UnreadCount = unread;
            }

            return myGroups.OrderByDescending(g => g.LastMessageAt).ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        // MEMBERSHIP
        // ═══════════════════════════════════════════════════════════════

        public static async Task<(bool success, string message)> JoinGroupAsync(
            string groupId,
            string userPhone,
            bool anonymous = false)
        {
            var group = await GetGroupAsync(groupId);
            if (group == null) return (false, "Group not found");
            if (!group.IsActive) return (false, "This group is no longer active");

            var existing = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}&limit=1");

            if (existing.Any())
            {
                if (existing.First().IsBanned) return (false, "You have been banned from this group");
                return (false, "You are already a member");
            }

            // Mood filter check
            if (!string.IsNullOrEmpty(group.MoodFilter))
            {
                var userMood = await GetUserMoodAsync(userPhone);
                if (!string.Equals(userMood, group.MoodFilter, StringComparison.OrdinalIgnoreCase))
                    return (false, $"This Mood Room requires mood: {group.MoodFilter}");
            }

            // Capacity check
            var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsBanned=eq.false");
            if (group.MaxMembers > 0 && members.Count >= group.MaxMembers)
                return (false, "This group is full");

            if (group.RequireApproval)
            {
                var existingRequest = await SupabaseService.GetAsync<GroupJoinRequest>("GroupJoinRequests",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}&Status=eq.pending&limit=1");

                if (existingRequest.Any())
                    return (false, "Join request already pending");

                var pending = new GroupJoinRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = groupId,
                    UserPhone = userPhone,
                    UserName = await GetUserNameAsync(userPhone),
                    UserProfileImage = await GetUserProfileImageAsync(userPhone),
                    RequestedAt = DateTime.UtcNow,
                    Status = "pending"
                };
                await SupabaseService.InsertAsync("GroupJoinRequests", pending);
                return (true, "Join request sent — waiting for admin approval");
            }

            var userName = await GetUserNameAsync(userPhone);
            var userImg = await GetUserProfileImageAsync(userPhone);
            int anonNumber = members.Count + 1;

            var member = new GroupMember
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = groupId,
                UserPhone = userPhone,
                UserName = userName,
                UserProfileImagePath = userImg,
                AnonymousAlias = anonymous ? $"Member #{anonNumber}" : string.Empty,
                Role = GroupMemberRole.Member,
                JoinedAt = DateTime.UtcNow,
                LastReadAt = DateTime.UtcNow,
                IsAnonymous = anonymous,
                IsMuted = false,
                IsBanned = false
            };

            await SupabaseService.InsertAsync("GroupMembers", member);

            group.MemberCount = members.Count + 1;
            await SupabaseService.UpdateAsync("Groups", $"Id=eq.{Uri.EscapeDataString(groupId)}", new { MemberCount = group.MemberCount });

            var displayName = anonymous ? $"Member #{anonNumber}" : userName;

            // Check for duplicate join messages
            var recentJoinMessage = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsSystemMessage=eq.true&Content=eq.{Uri.EscapeDataString($"✨ {displayName} joined the group")}&limit=1");

            if (!recentJoinMessage.Any())
            {
                await PostSystemMessageAsync(groupId, $"✨ {displayName} joined the group");
            }
            else
            {
                Debug.WriteLine($"Skipped duplicate join message for {displayName}");
            }

            return (true, "Joined successfully");
        }

        public static async Task<bool> LeaveGroupAsync(string groupId, string userPhone)
        {
            try
            {
                var member = await GetMemberAsync(groupId, userPhone);
                if (member == null) return false;

                // BLOCK: Creator cannot leave the group
                if (member.Role == GroupMemberRole.Creator)
                {
                    return false; // Silently block — UI should handle the message
                }

                // Delete the member first
                await SupabaseService.DeleteAsync("GroupMembers",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}");

                var group = await GetGroupAsync(groupId);
                if (group != null)
                {
                    var currentMembers = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                        $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsBanned=eq.false");
                    group.MemberCount = currentMembers.Count;
                    await UpdateGroupAsync(group);
                }

                var displayName = member.IsAnonymous && !string.IsNullOrEmpty(member.AnonymousAlias)
                    ? member.AnonymousAlias
                    : member.UserName;

                // Check for duplicate leave messages
                var recentLeaveMessage = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsSystemMessage=eq.true&Content=eq.{Uri.EscapeDataString($"👋 {displayName} left the group")}&limit=1");

                if (!recentLeaveMessage.Any())
                {
                    await PostSystemMessageAsync(groupId, $"👋 {displayName} left the group");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LeaveGroupAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> RemoveMemberAsync(
            string groupId,
            string adminPhone,
            string targetPhone)
        {
            var adminMember = await GetMemberAsync(groupId, adminPhone);
            if (adminMember == null || !adminMember.IsPrivileged)
                return false;

            var target = await GetMemberAsync(groupId, targetPhone);
            if (target == null) return false;
            if (target.Role == GroupMemberRole.Creator) return false;

            target.IsBanned = true;
            await SupabaseService.UpdateAsync("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(targetPhone)}",
                target);

            var group = await GetGroupAsync(groupId);
            if (group != null)
            {
                var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsBanned=eq.false");
                group.MemberCount = members.Count;
                await UpdateGroupAsync(group);
            }

            await PostSystemMessageAsync(groupId, $"🚫 {target.UserName} was removed by an admin");
            return true;
        }

        public static async Task<bool> PromoteMemberAsync(
            string groupId,
            string adminPhone,
            string targetPhone,
            GroupMemberRole newRole)
        {
            var admin = await GetMemberAsync(groupId, adminPhone);
            if (admin == null || (admin.Role != GroupMemberRole.Creator && admin.Role != GroupMemberRole.Admin))
                return false;

            var target = await GetMemberAsync(groupId, targetPhone);
            if (target == null) return false;

            target.Role = newRole;
            await SupabaseService.UpdateAsync("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(targetPhone)}",
                target);

            string roleLabel = newRole == GroupMemberRole.Admin ? "Admin" : "Moderator";
            await PostSystemMessageAsync(groupId, $"⭐ {target.UserName} is now a {roleLabel}");
            return true;
        }

        public static async Task<List<GroupMember>> GetMembersAsync(string groupId)
        {
            return await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsBanned=eq.false&order=JoinedAt.asc");
        }

        public static async Task<bool> IsMemberAsync(string groupId, string userPhone)
        {
            var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}&IsBanned=eq.false&limit=1");
            return members.Any();
        }

        public static async Task<GroupMember?> GetMemberAsync(string groupId, string userPhone)
        {
            var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}&limit=1");
            return members.FirstOrDefault();
        }

        // ═══════════════════════════════════════════════════════════════
        // MESSAGES
        // ═══════════════════════════════════════════════════════════════

        public static async Task<GroupMessage> SendMessageAsync(
            string groupId,
            string senderPhone,
            string content,
            GroupMessageType type = GroupMessageType.Text,
            List<string>? mediaPaths = null,
            string? replyToMessageId = null,
            string voiceAudioPath = "",
            double voiceDurationSeconds = 0,
            string pollJson = "")
        {
            var member = await GetMemberAsync(groupId, senderPhone);
            if (member == null)
                throw new UnauthorizedAccessException("Not a member of this group");

            var group = await GetGroupAsync(groupId);

            // Handle Encryption
            string encryptedContent = string.Empty;
            string storeContent = content;

            if (group?.IsEncrypted == true && !string.IsNullOrEmpty(content))
            {
                encryptedContent = EncryptMessage(content, groupId);
                storeContent = encryptedContent;
            }

            // Reply preview with decrypted content
            string replyToSenderName = string.Empty;
            string replyToPreview = string.Empty;

            if (!string.IsNullOrEmpty(replyToMessageId))
            {
                var replyMsg = await GetMessageAsync(replyToMessageId);
                if (replyMsg != null)
                {
                    replyToSenderName = replyMsg.DisplaySenderName ?? "Someone";

                    string previewText = "";
                    switch (replyMsg.MessageType)
                    {
                        case GroupMessageType.Image:
                            previewText = "📷 Photo";
                            break;
                        case GroupMessageType.Voice:
                            previewText = "🎙️ Voice message";
                            break;
                        case GroupMessageType.Poll:
                            previewText = "📊 Poll";
                            break;
                        case GroupMessageType.Event:
                            previewText = "📅 Event";
                            break;
                        case GroupMessageType.SystemMessage:
                            previewText = replyMsg.Content ?? "System message";
                            break;
                        default:
                            if (replyMsg.IsEncrypted && !string.IsNullOrEmpty(replyMsg.EncryptedContent))
                            {
                                try
                                {
                                    previewText = DecryptMessage(replyMsg.EncryptedContent, groupId);
                                }
                                catch
                                {
                                    previewText = "🔒 Encrypted message";
                                }
                            }
                            else if (!string.IsNullOrEmpty(replyMsg.Content))
                            {
                                previewText = replyMsg.Content;
                            }
                            else
                            {
                                previewText = "Message";
                            }
                            break;
                    }

                    if (previewText.Length > 80)
                        previewText = previewText.Substring(0, 77) + "...";

                    replyToPreview = previewText;
                }
            }

            // Disappearing messages
            DateTime? disappearAt = group?.DisappearingMessageSeconds > 0
                ? DateTime.UtcNow.AddSeconds(group.DisappearingMessageSeconds)
                : null;

            // Normalize media paths
            var normalizedMediaPaths = new List<string>();
            if (mediaPaths != null)
            {
                foreach (var path in mediaPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        string cleanPath = path.Trim();
                        if (!cleanPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                            !cleanPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanPath = "file://" + cleanPath;
                        }
                        normalizedMediaPaths.Add(cleanPath);
                    }
                }
            }

            var message = new GroupMessage
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = groupId,
                SenderPhone = senderPhone,
                SenderName = member.UserName,
                SenderProfileImage = member.UserProfileImagePath,
                SenderAnonymousAlias = member.IsAnonymous ? member.AnonymousAlias : string.Empty,
                MessageType = type,
                Content = storeContent,
                EncryptedContent = encryptedContent,
                IsEncrypted = group?.IsEncrypted == true,
                MediaPaths = normalizedMediaPaths,
                VoiceAudioPath = voiceAudioPath,
                VoiceDurationSeconds = voiceDurationSeconds,
                PollJson = pollJson,
                ReplyToMessageId = replyToMessageId,
                ReplyToSenderName = replyToSenderName,
                ReplyToPreview = replyToPreview,
                SentAt = DateTime.UtcNow,
                DisappearAt = disappearAt,
                IsDeleted = false,
                IsPinned = false,
                ReadByCount = 1
            };

            var inserted = await SupabaseService.InsertAndReturnAsync<GroupMessage>("GroupMessages", message);

            // Update group last activity
            if (group != null)
            {
                group.LastMessagePreview = type == GroupMessageType.Image ? "📷 Photo" : message.ContentPreview;
                group.LastMessageSenderName = message.DisplaySenderName;
                group.LastMessageAt = DateTime.UtcNow;
                group.LastActiveAt = DateTime.UtcNow;
                await UpdateGroupAsync(group);
            }

            Debug.WriteLine($"Message sent: Type={type}, MediaCount={normalizedMediaPaths.Count}");
            return inserted ?? message;
        }

        public static async Task<List<GroupMessage>> GetMessagesAsync(
            string groupId,
            int take = 50,
            int skip = 0)
        {
            var msgs = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsDeleted=eq.false&order=SentAt.desc&limit={take}&offset={skip}");

            msgs.Reverse(); // chronological order

            // Decrypt if encrypted
            foreach (var msg in msgs.Where(m => m.IsEncrypted && !string.IsNullOrEmpty(m.EncryptedContent)))
            {
                try
                {
                    msg.Content = DecryptMessage(msg.EncryptedContent, groupId);
                }
                catch
                {
                    msg.Content = "🔒 Encrypted message";
                }
            }

            return msgs;
        }

        public static async Task<GroupMessage?> GetMessageAsync(string messageId)
        {
            var messages = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                $"Id=eq.{Uri.EscapeDataString(messageId)}&limit=1");
            return messages.FirstOrDefault();
        }

        public static async Task<bool> DeleteMessageAsync(
            string groupId,
            string messageId,
            string requestorPhone)
        {
            var msg = await GetMessageAsync(messageId);
            if (msg == null) return false;

            var member = await GetMemberAsync(groupId, requestorPhone);

            bool canDelete = msg.SenderPhone == requestorPhone || (member?.IsPrivileged == true);
            if (!canDelete) return false;

            msg.IsDeleted = true;
            msg.Content = string.Empty;
            msg.EncryptedContent = string.Empty;
            await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);
            return true;
        }

        public static async Task<bool> EditMessageAsync(
            string messageId,
            string requestorPhone,
            string newContent)
        {
            var msg = await GetMessageAsync(messageId);
            if (msg == null || msg.SenderPhone != requestorPhone) return false;

            // Get the group for encryption
            var group = await GetGroupAsync(msg.GroupId);

            // Update content with encryption if needed
            if (group?.IsEncrypted == true && !string.IsNullOrEmpty(newContent))
            {
                msg.EncryptedContent = EncryptMessage(newContent, msg.GroupId);
                msg.Content = msg.EncryptedContent;
                msg.IsEncrypted = true;
            }
            else
            {
                msg.Content = newContent;
                msg.EncryptedContent = string.Empty;
                msg.IsEncrypted = false;
            }

            msg.IsEdited = true;
            msg.EditedAt = DateTime.UtcNow;
            await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);
            return true;
        }

        public static async Task AddReactionAsync(
            string messageId,
            string userPhone,
            string emoji)
        {
            var msg = await GetMessageAsync(messageId);
            if (msg == null) return;

            var reactions = msg.Reactions;

            // Remove from any existing reaction by this user
            foreach (var key in reactions.Keys.ToList())
                reactions[key].Remove(userPhone);

            // Add new reaction
            if (!reactions.ContainsKey(emoji))
                reactions[emoji] = new List<string>();
            reactions[emoji].Add(userPhone);

            msg.Reactions = reactions;
            await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);
        }

        public static async Task MarkAsReadAsync(string groupId, string userPhone)
        {
            var member = await GetMemberAsync(groupId, userPhone);
            if (member == null) return;

            member.LastReadAt = DateTime.UtcNow;
            member.LastSeenAt = DateTime.UtcNow;
            await SupabaseService.UpdateAsync("GroupMembers",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}",
                member);
        }

        public static async Task<bool> PinMessageAsync(
            string groupId,
            string messageId,
            string adminPhone)
        {
            var admin = await GetMemberAsync(groupId, adminPhone);
            if (admin == null || !admin.IsPrivileged) return false;

            // Max 3 pinned messages
            var pinnedMessages = await SupabaseService.GetAsync<GroupPinnedMessage>("GroupPinnedMessages",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}");
            if (pinnedMessages.Count >= 3) return false;

            var msg = await GetMessageAsync(messageId);
            if (msg == null) return false;

            msg.IsPinned = true;
            await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);

            var pin = new GroupPinnedMessage
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = groupId,
                MessageId = messageId,
                PinnedByPhone = adminPhone,
                PinnedAt = DateTime.UtcNow,
                MessagePreview = msg.ContentPreview
            };
            await SupabaseService.InsertAsync("GroupPinnedMessages", pin);

            await PostSystemMessageAsync(groupId, "📌 A message was pinned");
            return true;
        }

        public static async Task<List<GroupPinnedMessage>> GetPinnedMessagesAsync(string groupId)
        {
            return await SupabaseService.GetAsync<GroupPinnedMessage>("GroupPinnedMessages",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&order=PinnedAt.desc");
        }

        // ═══════════════════════════════════════════════════════════════
        // INVITE LINKS
        // ═══════════════════════════════════════════════════════════════

        public static async Task<GroupInvite> CreateInviteLinkAsync(
            string groupId,
            string createdByPhone,
            DateTime? expiresAt = null,
            int maxUses = 0)
        {
            var code = GenerateInviteCode();
            var invite = new GroupInvite
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = groupId,
                InviteCode = code,
                CreatedByPhone = createdByPhone,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                MaxUses = maxUses,
                UseCount = 0,
                IsActive = true
            };

            var inserted = await SupabaseService.InsertAndReturnAsync<GroupInvite>("GroupInvites", invite);
            return inserted ?? invite;
        }

        public static async Task<(bool success, string message, Group? group)>
            JoinByInviteCodeAsync(string code, string userPhone)
        {
            var invites = await SupabaseService.GetAsync<GroupInvite>("GroupInvites",
                $"InviteCode=eq.{Uri.EscapeDataString(code)}&IsActive=eq.true&limit=1");

            var invite = invites.FirstOrDefault();
            if (invite == null) return (false, "Invalid invite link", null);
            
            if (!invite.IsUsable) return (false, "This invite link has expired or reached its limit", null);

            var group = await GetGroupAsync(invite.GroupId);
            if (group == null) return (false, "Group not found", null);

            var (success, msg) = await JoinGroupAsync(invite.GroupId, userPhone);

            if (success)
            {
                invite.UseCount++;
                await SupabaseService.UpdateAsync("GroupInvites", $"Id=eq.{Uri.EscapeDataString(invite.Id)}", invite);
            }

            return (success, msg, group);
        }

        public static async Task<bool> RevokeInviteAsync(string inviteId, string adminPhone)
        {
            var invites = await SupabaseService.GetAsync<GroupInvite>("GroupInvites",
                $"Id=eq.{Uri.EscapeDataString(inviteId)}&limit=1");
            
            var invite = invites.FirstOrDefault();
            if (invite == null) return false;

            invite.IsActive = false;
            await SupabaseService.UpdateAsync("GroupInvites", $"Id=eq.{Uri.EscapeDataString(inviteId)}", invite);
            return true;
        }

        public static async Task<(bool success, string message)> CancelJoinRequestAsync(
            string requestId,
            string userPhone)
        {
            try
            {
                var requests = await SupabaseService.GetAsync<GroupJoinRequest>("GroupJoinRequests",
                    $"Id=eq.{Uri.EscapeDataString(requestId)}&UserPhone=eq.{Uri.EscapeDataString(userPhone)}&Status=eq.pending&limit=1");

                var req = requests.FirstOrDefault();
                if (req == null)
                    return (false, "Request not found or already processed.");

                req.Status = "cancelled";
                await SupabaseService.UpdateAsync("GroupJoinRequests", $"Id=eq.{Uri.EscapeDataString(requestId)}", req);

                Debug.WriteLine($"CancelJoinRequestAsync: request {requestId} cancelled by {userPhone}");
                return (true, "Join request cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CancelJoinRequestAsync error: {ex}");
                return (false, $"Failed to cancel request: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // JOIN REQUESTS
        // ═══════════════════════════════════════════════════════════════

        public static async Task<List<GroupJoinRequest>> GetPendingJoinRequestsAsync(string groupId)
        {
            return await SupabaseService.GetAsync<GroupJoinRequest>("GroupJoinRequests",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&Status=eq.pending&order=RequestedAt.asc");
        }

        public static async Task<bool> ApproveJoinRequestAsync(
            string requestId,
            string adminPhone)
        {
            var requests = await SupabaseService.GetAsync<GroupJoinRequest>("GroupJoinRequests",
                $"Id=eq.{Uri.EscapeDataString(requestId)}&limit=1");

            var req = requests.FirstOrDefault();
            if (req == null) return false;

            var admin = await GetMemberAsync(req.GroupId, adminPhone);
            if (admin == null || !admin.IsPrivileged) return false;

            // Mark request approved
            req.Status = "approved";
            await SupabaseService.UpdateAsync("GroupJoinRequests", $"Id=eq.{Uri.EscapeDataString(requestId)}", req);

            // Check if already a member
            var existing = await GetMemberAsync(req.GroupId, req.UserPhone);

            if (existing != null)
            {
                if (existing.IsBanned)
                {
                    existing.IsBanned = false;
                    await SupabaseService.UpdateAsync("GroupMembers",
                        $"GroupId=eq.{Uri.EscapeDataString(req.GroupId)}&UserPhone=eq.{Uri.EscapeDataString(req.UserPhone)}",
                        existing);
                }
            }
            else
            {
                var group = await GetGroupAsync(req.GroupId);
                var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                    $"GroupId=eq.{Uri.EscapeDataString(req.GroupId)}&IsBanned=eq.false");
                int anonNumber = members.Count + 1;

                var member = new GroupMember
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = req.GroupId,
                    UserPhone = req.UserPhone,
                    UserName = req.UserName,
                    UserProfileImagePath = req.UserProfileImage,
                    AnonymousAlias = string.Empty,
                    Role = GroupMemberRole.Member,
                    JoinedAt = DateTime.UtcNow,
                    LastReadAt = DateTime.UtcNow,
                    IsAnonymous = false,
                    IsMuted = false,
                    IsBanned = false
                };

                await SupabaseService.InsertAsync("GroupMembers", member);

                if (group != null)
                {
                    group.MemberCount = members.Count + 1;
                    group.LastActiveAt = DateTime.UtcNow;
                    await UpdateGroupAsync(group);
                }

                await PostSystemMessageAsync(req.GroupId, $"✨ {req.UserName} joined the group");
            }

            MessagingCenter.Send<object>(new object(), "GroupsUpdated");
            return true;
        }

        public static async Task<bool> RejectJoinRequestAsync(string requestId, string adminPhone)
        {
            var requests = await SupabaseService.GetAsync<GroupJoinRequest>("GroupJoinRequests",
                $"Id=eq.{Uri.EscapeDataString(requestId)}&limit=1");

            var req = requests.FirstOrDefault();
            if (req == null) return false;

            req.Status = "rejected";
            await SupabaseService.UpdateAsync("GroupJoinRequests", $"Id=eq.{Uri.EscapeDataString(requestId)}", req);
            return true;
        }

        // Add these methods to your GroupRepository class:

        // ═══════════════════════════════════════════════════════════════
        // NEWER MESSAGES
        // ═══════════════════════════════════════════════════════════════

        public static async Task<List<GroupMessage>> GetNewerMessagesAsync(string groupId, string afterMessageId, string currentUserPhone)
        {
            try
            {
                if (string.IsNullOrEmpty(afterMessageId))
                {
                    return await GetMessagesAsync(groupId, 60);
                }

                var lastMessage = await GetMessageAsync(afterMessageId);
                if (lastMessage == null)
                {
                    return await GetMessagesAsync(groupId, 60);
                }

                var newerMessages = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsDeleted=eq.false&SentAt=gt.{lastMessage.SentAt:yyyy-MM-ddTHH:mm:ssZ}&order=SentAt.asc");

                // Decrypt if encrypted
                foreach (var msg in newerMessages.Where(m => m.IsEncrypted && !string.IsNullOrEmpty(m.EncryptedContent)))
                {
                    try
                    {
                        msg.Content = DecryptMessage(msg.EncryptedContent, groupId);
                    }
                    catch
                    {
                        msg.Content = "🔒 Encrypted message";
                    }
                }

                return newerMessages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetNewerMessagesAsync error: {ex}");
                return new List<GroupMessage>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DELETE MESSAGE FOR SELF
        // ═══════════════════════════════════════════════════════════════

        public static async Task<bool> DeleteMessageForSelfAsync(string groupId, string messageId, string userPhone)
        {
            try
            {
                var msg = await GetMessageAsync(messageId);
                if (msg == null) return false;

                // For "Delete for me", we mark it as deleted for this user
                // Since we don't have per-user deletion, we'll mark it as deleted but keep a flag
                // You might want to add a "DeletedForUsers" JSON field for this, but for now:
                msg.IsDeleted = true;
                msg.Content = string.Empty;
                msg.EncryptedContent = string.Empty;

                await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteMessageForSelfAsync error: {ex}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DELETE MESSAGE FOR EVERYONE
        // ═══════════════════════════════════════════════════════════════

        public static async Task<bool> DeleteMessageForEveryoneAsync(string groupId, string messageId, string userPhone)
        {
            try
            {
                var msg = await GetMessageAsync(messageId);
                if (msg == null) return false;

                var member = await GetMemberAsync(groupId, userPhone);
                bool canDelete = msg.SenderPhone == userPhone || (member?.IsPrivileged == true);

                if (!canDelete) return false;

                msg.IsDeleted = true;
                msg.Content = string.Empty;
                msg.EncryptedContent = string.Empty;

                await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteMessageForEveryoneAsync error: {ex}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE MEMBER
        // ═══════════════════════════════════════════════════════════════

        public static async Task<bool> UpdateMemberAsync(GroupMember member)
        {
            try
            {
                return await SupabaseService.UpdateAsync("GroupMembers",
                    $"GroupId=eq.{Uri.EscapeDataString(member.GroupId)}&UserPhone=eq.{Uri.EscapeDataString(member.UserPhone)}",
                    member);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateMemberAsync error: {ex}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GROUP EVENTS
        // ═══════════════════════════════════════════════════════════════

        public static async Task<GroupEvent> CreateGroupEventAsync(
            string groupId,
            string createdByPhone,
            string title,
            string description,
            string location,
            DateTime eventDate,
            int maxAttendees = 0)
        {
            var ev = new GroupEvent
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = groupId,
                CreatedByPhone = createdByPhone,
                Title = title,
                Description = description,
                Location = location,
                EventDate = eventDate,
                CreatedAt = DateTime.UtcNow,
                MaxAttendees = maxAttendees,
                AttendeePhones = new List<string> { createdByPhone }
            };

            var inserted = await SupabaseService.InsertAndReturnAsync<GroupEvent>("GroupEvents", ev);

            var creatorName = await GetUserNameAsync(createdByPhone);
            await PostSystemMessageAsync(groupId, $"📅 {creatorName} created an event: {title}", GroupMessageType.Event);

            return inserted ?? ev;
        }

        public static async Task<bool> RsvpGroupEventAsync(
            string eventId,
            string userPhone,
            bool attending)
        {
            var events = await SupabaseService.GetAsync<GroupEvent>("GroupEvents",
                $"Id=eq.{Uri.EscapeDataString(eventId)}&limit=1");

            var ev = events.FirstOrDefault();
            if (ev == null) return false;

            var phones = ev.AttendeePhones;

            if (attending)
            {
                if (!phones.Contains(userPhone))
                    phones.Add(userPhone);
            }
            else
            {
                phones.Remove(userPhone);
            }

            ev.AttendeePhones = phones;
            await SupabaseService.UpdateAsync("GroupEvents", $"Id=eq.{Uri.EscapeDataString(eventId)}", ev);
            return true;
        }

        public static async Task<List<GroupEvent>> GetGroupEventsAsync(string groupId)
        {
            return await SupabaseService.GetAsync<GroupEvent>("GroupEvents",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&order=EventDate.asc");
        }

        // ═══════════════════════════════════════════════════════════════
        // POLLS
        // ═══════════════════════════════════════════════════════════════

        public static async Task<GroupMessage> CreatePollAsync(
            string groupId,
            string creatorPhone,
            string question,
            List<string> options,
            bool allowMultiple = false,
            DateTime? expiresAt = null)
        {
            var poll = new GroupPoll
            {
                Question = question,
                AllowMultipleVotes = allowMultiple,
                ExpiresAt = expiresAt,
                Options = options.Select(o => new GroupPollOption { Text = o }).ToList()
            };

            var pollJson = JsonSerializer.Serialize(poll);

            return await SendMessageAsync(
                groupId,
                creatorPhone,
                $"📊 Poll: {question}",
                GroupMessageType.Poll,
                pollJson: pollJson);
        }

        public static async Task<bool> VoteOnPollAsync(
            string messageId,
            string userPhone,
            int optionIndex)
        {
            var msg = await GetMessageAsync(messageId);
            if (msg == null || string.IsNullOrEmpty(msg.PollJson)) return false;

            var poll = JsonSerializer.Deserialize<GroupPoll>(msg.PollJson);
            if (poll == null || optionIndex >= poll.Options.Count) return false;
            if (poll.IsExpired) return false;

            if (!poll.AllowMultipleVotes)
            {
                foreach (var opt in poll.Options)
                    opt.VoterPhones.Remove(userPhone);
            }

            if (!poll.Options[optionIndex].VoterPhones.Contains(userPhone))
                poll.Options[optionIndex].VoterPhones.Add(userPhone);

            msg.PollJson = JsonSerializer.Serialize(poll);
            await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", msg);
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // POLL EDIT & DELETE
        // ═══════════════════════════════════════════════════════════════

        public static async Task<bool> EditPollAsync(string messageId, string groupId, string newQuestion, List<string> newOptions)
        {
            try
            {
                var message = await GetMessageAsync(messageId);
                if (message == null) return false;

                // Update the poll question in the message content
                message.Content = $"📊 Poll: {newQuestion}";

                // Get existing poll to preserve voter data
                var existingPoll = JsonSerializer.Deserialize<GroupPoll>(message.PollJson);

                // Create updated poll with preserved voter data where possible
                var updatedPoll = new GroupPoll
                {
                    Question = newQuestion,
                    Options = new List<GroupPollOption>(),
                    AllowMultipleVotes = existingPoll?.AllowMultipleVotes ?? false,
                    ExpiresAt = existingPoll?.ExpiresAt
                };

                // Map options - preserve votes for options that have the same text
                foreach (var newOpt in newOptions)
                {
                    var existingOption = existingPoll?.Options.FirstOrDefault(o => o.Text == newOpt);
                    updatedPoll.Options.Add(new GroupPollOption
                    {
                        Text = newOpt,
                        VoterPhones = existingOption?.VoterPhones ?? new List<string>()
                    });
                }

                message.PollJson = JsonSerializer.Serialize(updatedPoll);
                message.IsEdited = true;

                await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", message);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EditPollAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> DeletePollAsync(string messageId, string groupId)
        {
            try
            {
                var message = await GetMessageAsync(messageId);
                if (message == null) return false;

                message.IsDeleted = true;
                await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(messageId)}", message);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeletePollAsync error: {ex}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DISAPPEARING MESSAGES CLEANUP
        // ═══════════════════════════════════════════════════════════════

        public static async Task CleanupDisappearingMessagesAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var messages = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                    $"DisappearAt=lt.{now:yyyy-MM-ddTHH:mm:ssZ}&IsDeleted=eq.false");

                foreach (var msg in messages)
                {
                    msg.IsDeleted = true;
                    msg.Content = string.Empty;
                    msg.EncryptedContent = string.Empty;
                    await SupabaseService.UpdateAsync("GroupMessages", $"Id=eq.{Uri.EscapeDataString(msg.Id)}", msg);
                }

                Debug.WriteLine($"Cleaned up {messages.Count} disappearing messages");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupDisappearingMessages error: {ex}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INTERNAL HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static async Task PostSystemMessageAsync(
            string groupId,
            string content,
            GroupMessageType type = GroupMessageType.SystemMessage)
        {
            try
            {
                // Check for duplicate system message
                var recentDuplicate = await SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                    $"GroupId=eq.{Uri.EscapeDataString(groupId)}&IsSystemMessage=eq.true&Content=eq.{Uri.EscapeDataString(content)}&limit=1");

                if (recentDuplicate.Any())
                {
                    Debug.WriteLine($"Skipped duplicate system message: {content}");
                    return;
                }

                var msg = new GroupMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = groupId,
                    SenderPhone = "system",
                    SenderName = "System",
                    MessageType = type,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    IsSystemMessage = true,
                    ShowAvatar = false
                };

                await SupabaseService.InsertAsync("GroupMessages", msg);
                Debug.WriteLine($"System message posted: {content}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PostSystemMessage error: {ex}");
            }
        }

        private static async Task<string> GetUserNameAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                return users.FirstOrDefault()?.Name ?? phone;
            }
            catch { return phone; }
        }

        private static async Task<string> GetUserProfileImageAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                return users.FirstOrDefault()?.ProfileImagePath ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static async Task<string> GetUserMoodAsync(string phone)
        {
            try
            {
                var users = await SupabaseService.GetAsync<User>("Users", $"PhoneNumber=eq.{Uri.EscapeDataString(phone)}&limit=1");
                return users.FirstOrDefault()?.Mood ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        public static async Task<List<Group>> GetAllPublicGroupsAsync()
        {
            try
            {
                var allGroups = await SupabaseService.GetAsync<Group>("Groups",
                    $"IsActive=eq.true&Visibility=eq.Public&order=LastActiveAt.desc");

                Debug.WriteLine($"Total public groups: {allGroups.Count}");

                // Update member counts
                foreach (var group in allGroups)
                {
                    var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                        $"GroupId=eq.{Uri.EscapeDataString(group.Id)}&IsBanned=eq.false");
                    group.MemberCount = members.Count;
                }

                return allGroups;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllPublicGroupsAsync ERROR: {ex.Message}");
                return new List<Group>();
            }
        }

        public static async Task<List<string>> GetUserGroupIdsAsync(string userPhone)
        {
            try
            {
                var memberships = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                    $"UserPhone=eq.{Uri.EscapeDataString(userPhone)}&IsBanned=eq.false");
                return memberships.Select(m => m.GroupId).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserGroupIdsAsync error: {ex.Message}");
                return new List<string>();
            }
        }

        public static async Task<List<Group>> GetAllGroupsForExploreAsync(string currentUserPhone)
        {
            try
            {
                var allGroups = await SupabaseService.GetAsync<Group>("Groups",
                    $"IsActive=eq.true&Visibility=eq.Public&order=LastActiveAt.desc");

                Debug.WriteLine($"GetAllGroupsForExploreAsync: found {allGroups.Count} public groups");

                // Confirmed memberships
                var memberships = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&IsBanned=eq.false");
                var memberGroupIds = memberships.Select(m => m.GroupId).ToHashSet();

                // Pending join requests
                var pendingRequests = await SupabaseService.GetAsync<GroupJoinRequest>("GroupJoinRequests",
                    $"UserPhone=eq.{Uri.EscapeDataString(currentUserPhone)}&Status=eq.pending");
                var pendingByGroupId = pendingRequests.ToDictionary(r => r.GroupId, r => r.Id);

                // Stamp each group with live member count + membership flags
                foreach (var group in allGroups)
                {
                    group.IsMember = memberGroupIds.Contains(group.Id);
                    group.IsPendingJoin = !group.IsMember && pendingByGroupId.ContainsKey(group.Id);
                    group.PendingJoinRequestId = group.IsPendingJoin ? pendingByGroupId[group.Id] : string.Empty;

                    var members = await SupabaseService.GetAsync<GroupMember>("GroupMembers",
                        $"GroupId=eq.{Uri.EscapeDataString(group.Id)}&IsBanned=eq.false");
                    group.MemberCount = members.Count;
                }

                return allGroups.OrderByDescending(g => g.LastActiveAt).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllGroupsForExploreAsync ERROR: {ex.Message}");
                return new List<Group>();
            }
        }

        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(
                Enumerable.Repeat(chars, 8)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());
        }

        private static string EncryptMessage(string plainText, string groupId)
        {
            try
            {
                using var aes = Aes.Create();
                var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(groupId + "_lock_group_key"));
                aes.Key = keyBytes;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                var result = new byte[aes.IV.Length + encrypted.Length];
                aes.IV.CopyTo(result, 0);
                encrypted.CopyTo(result, aes.IV.Length);
                return Convert.ToBase64String(result);
            }
            catch { return plainText; }
        }

        private static string DecryptMessage(string cipherText, string groupId)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);
                using var aes = Aes.Create();
                var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(groupId + "_lock_group_key"));
                aes.Key = keyBytes;

                var iv = new byte[aes.BlockSize / 8];
                var encrypted = new byte[fullCipher.Length - iv.Length];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                Array.Copy(fullCipher, iv.Length, encrypted, 0, encrypted.Length);

                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return "🔒 Encrypted message"; }
        }
    }
}