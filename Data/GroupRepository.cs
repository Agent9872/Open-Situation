using Lock.Models;
using Lock.Chat.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Lock.Services
{
    public static class GroupRepository
    {

        public static async Task InitializeAsync()
        {
            await GroupDatabaseService.InitializeAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // GROUP CRUD
        // ═══════════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════════
        // GROUP CRUD - FIXED VERSION
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
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Group name cannot be empty.");

            if (string.IsNullOrWhiteSpace(createdByPhone))
                throw new ArgumentException("Creator phone number is required.");

            // Prevent recent duplicate group creation
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

            var existingGroup = await db.Table<Group>()
                .Where(g => g.Name == name
                         && g.CreatedByPhone == createdByPhone
                         && g.CreatedAt > fiveMinutesAgo)
                .FirstOrDefaultAsync();

            if (existingGroup != null)
            {
                Debug.WriteLine($"Duplicate group creation attempt detected for '{name}'. Returning existing.");
                return existingGroup;
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

            await db.InsertAsync(group);

            // Auto-add creator as owner
            var creatorName = await GetUserNameAsync(createdByPhone);
            var creatorImg = await GetUserProfileImageAsync(createdByPhone);

            var creatorMember = new GroupMember
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = group.Id,
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

            await db.InsertAsync(creatorMember);

            // FIXED: Check for duplicate creation message
            var thirtySecondsAgo = DateTime.UtcNow.AddSeconds(-30);
            var existingCreationCount = await db.Table<GroupMessage>()
                .Where(m => m.GroupId == group.Id &&
                            m.IsSystemMessage &&
                            m.Content.Contains("created this group") &&
                            m.SentAt > thirtySecondsAgo)
                .CountAsync();

            if (existingCreationCount == 0)
            {
                await PostSystemMessageAsync(group.Id, $"👋 {creatorName} created this group");
                Debug.WriteLine($"System creation message posted for group {group.Id}");
            }
            else
            {
                Debug.WriteLine($"Creation message already exists — skipping duplicate.");
            }

            return group;
        }


        public static async Task<Group?> GetGroupAsync(string groupId)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();
            return await db.Table<Group>()
                .Where(g => g.Id == groupId)
                .FirstOrDefaultAsync();
        }

        public static async Task UpdateGroupAsync(Group group)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();
            group.LastActiveAt = DateTime.UtcNow;
            await db.UpdateAsync(group);
        }

        public static async Task DeleteGroupAsync(string groupId)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            await db.ExecuteAsync("DELETE FROM Groups WHERE Id = ?", groupId);
            await db.ExecuteAsync("DELETE FROM GroupMembers WHERE GroupId = ?", groupId);
            await db.ExecuteAsync("DELETE FROM GroupMessages WHERE GroupId = ?", groupId);
            await db.ExecuteAsync("DELETE FROM GroupInvites WHERE GroupId = ?", groupId);
            await db.ExecuteAsync("DELETE FROM GroupJoinRequests WHERE GroupId = ?", groupId);
            await db.ExecuteAsync("DELETE FROM GroupEvents WHERE GroupId = ?", groupId);
            await db.ExecuteAsync("DELETE FROM GroupPinnedMessages WHERE GroupId = ?", groupId);
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
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var query = await db.Table<Group>()
                .Where(g => g.IsActive &&
                            (int)g.Visibility == (int)GroupVisibility.Public)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.ToLower();
                query = query.Where(g =>
                    g.Name.ToLower().Contains(q) ||
                    g.Description.ToLower().Contains(q) ||
                    g.Category.ToLower().Contains(q))
                    .ToList();
            }

            if (filterType.HasValue)
                query = query.Where(g => g.GroupType == filterType.Value).ToList();

            if (!string.IsNullOrEmpty(filterMood))
                query = query.Where(g =>
                    string.IsNullOrEmpty(g.MoodFilter) ||
                    string.Equals(g.MoodFilter, filterMood,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // Mark membership status
            var memberGroupIds = (await db.Table<GroupMember>()
                .Where(m => m.UserPhone == currentUserPhone && !m.IsBanned)
                .ToListAsync())
                .Select(m => m.GroupId)
                .ToHashSet();

            foreach (var g in query)
            {
                g.IsMember = memberGroupIds.Contains(g.Id);
            }

            return query.OrderByDescending(g => g.LastActiveAt).ToList();
        }

        public static async Task<List<Group>> GetMyGroupsAsync(string userPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var memberships = await db.Table<GroupMember>()
                .Where(m => m.UserPhone == userPhone && !m.IsBanned)
                .ToListAsync();

            var groupIds = memberships.Select(m => m.GroupId).ToList();
            if (!groupIds.Any()) return new List<Group>();

            var groups = await db.Table<Group>()
                .Where(g => g.IsActive)
                .ToListAsync();

            var myGroups = groups
                .Where(g => groupIds.Contains(g.Id))
                .ToList();

            // Attach unread counts and role info
            foreach (var g in myGroups)
            {
                var membership = memberships.First(m => m.GroupId == g.Id);
                g.IsMember = true;
                g.IsCreator = membership.Role == GroupMemberRole.Creator;
                g.IsAdmin = membership.Role == GroupMemberRole.Creator ||
                               membership.Role == GroupMemberRole.Admin;

                var unread = await db.Table<GroupMessage>()
                    .Where(msg => msg.GroupId == g.Id &&
                                  msg.SentAt > membership.LastReadAt &&
                                  msg.SenderPhone != userPhone &&
                                  !msg.IsDeleted)
                    .CountAsync();
                g.UnreadCount = unread;
            }

            return myGroups.OrderByDescending(g => g.LastMessageAt).ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        // MEMBERSHIP
        // ═══════════════════════════════════════════════════════════════

        // FIXED JoinGroupAsync - prevent duplicate system messages
        public static async Task<(bool success, string message)> JoinGroupAsync(
      string groupId,
      string userPhone,
      bool anonymous = false)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var group = await GetGroupAsync(groupId);
            if (group == null) return (false, "Group not found");
            if (!group.IsActive) return (false, "This group is no longer active");

            var existing = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == userPhone)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                if (existing.IsBanned) return (false, "You have been banned from this group");
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
            if (group.MaxMembers > 0 && group.MemberCount >= group.MaxMembers)
                return (false, "This group is full");

            if (group.RequireApproval)
            {
                var existingRequest = await db.Table<GroupJoinRequest>()
                    .Where(r => r.GroupId == groupId && r.UserPhone == userPhone && r.Status == "pending")
                    .FirstOrDefaultAsync();

                if (existingRequest != null)
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
                await db.InsertAsync(pending);
                return (true, "Join request sent — waiting for admin approval");
            }

            var userName = await GetUserNameAsync(userPhone);
            var userImg = await GetUserProfileImageAsync(userPhone);

            int anonNumber = group.MemberCount + 1;
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

            await db.InsertAsync(member);

            group.MemberCount++;
            await db.UpdateAsync(group);

            var displayName = anonymous ? $"Member #{anonNumber}" : userName;

            // FIXED: Check for duplicate join messages - look in last 10 seconds only
            var recentJoinMessage = await db.Table<GroupMessage>()
       .Where(m => m.GroupId == groupId &&
                   m.IsSystemMessage &&
                   m.Content == $"✨ {displayName} joined the group")
       .FirstOrDefaultAsync();

            if (recentJoinMessage == null)
            {
                await PostSystemMessageAsync(groupId, $"✨ {displayName} joined the group");
            }
            else
            {
                Debug.WriteLine($"Skipped duplicate join message for {displayName}");
            }

            return (true, "Joined successfully");
        }

        // FIXED LeaveGroupAsync - prevent duplicate system messages
        public static async Task<bool> LeaveGroupAsync(string groupId, string userPhone)
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var member = await db.Table<GroupMember>()
                    .Where(m => m.GroupId == groupId && m.UserPhone == userPhone)
                    .FirstOrDefaultAsync();

                if (member == null) return false;

                // BLOCK: Creator cannot leave the group
                if (member.Role == GroupMemberRole.Creator)
                {
                    return false; // Silently block — UI should handle the message
                }

                // If creator is leaving, transfer or dissolve
                if (member.Role == GroupMemberRole.Creator)
                {
                    var nextAdmin = await db.Table<GroupMember>()
                        .Where(m => m.GroupId == groupId &&
                                    m.UserPhone != userPhone &&
                                    !m.IsBanned)
                        .FirstOrDefaultAsync();

                    if (nextAdmin != null)
                    {
                        nextAdmin.Role = GroupMemberRole.Creator;
                        await db.UpdateAsync(nextAdmin);
                        await PostSystemMessageAsync(groupId,
                            $"👑 {nextAdmin.UserName} is now the group creator");
                    }
                    else
                    {
                        // No members left — archive group
                        var group2 = await GetGroupAsync(groupId);
                        if (group2 != null)
                        {
                            group2.IsActive = false;
                            await db.UpdateAsync(group2);
                        }
                    }
                }

                // Delete the member first to prevent any further operations
                await db.DeleteAsync(member);

                var group = await GetGroupAsync(groupId);
                if (group != null)
                {
                    group.MemberCount = Math.Max(0, group.MemberCount - 1);
                    await db.UpdateAsync(group);
                }

                var displayName = member.IsAnonymous && !string.IsNullOrEmpty(member.AnonymousAlias)
                    ? member.AnonymousAlias
                    : member.UserName;

                // Check for duplicate leave messages
                var recentLeaveMessage = await db.Table<GroupMessage>()
      .Where(m => m.GroupId == groupId &&
                  m.IsSystemMessage &&
                  m.Content == $"👋 {displayName} left the group")
      .FirstOrDefaultAsync();

                if (recentLeaveMessage == null)
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
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var adminMember = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == adminPhone)
                .FirstOrDefaultAsync();

            if (adminMember == null || !adminMember.IsPrivileged)
                return false;

            var target = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == targetPhone)
                .FirstOrDefaultAsync();

            if (target == null) return false;
            if (target.Role == GroupMemberRole.Creator) return false;

            target.IsBanned = true;
            await db.UpdateAsync(target);

            var group = await GetGroupAsync(groupId);
            if (group != null)
            {
                group.MemberCount = Math.Max(0, group.MemberCount - 1);
                await db.UpdateAsync(group);
            }

            await PostSystemMessageAsync(groupId,
                $"🚫 {target.UserName} was removed by an admin");
            return true;
        }

        public static async Task<bool> PromoteMemberAsync(
            string groupId,
            string adminPhone,
            string targetPhone,
            GroupMemberRole newRole)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var admin = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == adminPhone)
                .FirstOrDefaultAsync();

            if (admin == null ||
                (admin.Role != GroupMemberRole.Creator && admin.Role != GroupMemberRole.Admin))
                return false;

            var target = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == targetPhone)
                .FirstOrDefaultAsync();

            if (target == null) return false;

            target.Role = newRole;
            await db.UpdateAsync(target);

            string roleLabel = newRole == GroupMemberRole.Admin ? "Admin" : "Moderator";
            await PostSystemMessageAsync(groupId,
                $"⭐ {target.UserName} is now a {roleLabel}");
            return true;
        }

        public static async Task<List<GroupMember>> GetMembersAsync(string groupId)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            return await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && !m.IsBanned)
                .ToListAsync();
        }

        public static async Task<bool> IsMemberAsync(string groupId, string userPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var m = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId &&
                            m.UserPhone == userPhone &&
                            !m.IsBanned)
                .FirstOrDefaultAsync();
            return m != null;
        }

        public static async Task<GroupMember?> GetMemberAsync(string groupId, string userPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();
            return await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == userPhone)
                .FirstOrDefaultAsync();
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
     int replyToMessageId = 0,
     string voiceAudioPath = "",
     double voiceDurationSeconds = 0,
     string pollJson = "")
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var member = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == senderPhone && !m.IsBanned)
                .FirstOrDefaultAsync();

            if (member == null)
                throw new UnauthorizedAccessException("Not a member of this group");

            var group = await GetGroupAsync(groupId);

            // === Handle Encryption ===
            string encryptedContent = string.Empty;
            string storeContent = content;

            if (group?.IsEncrypted == true && !string.IsNullOrEmpty(content))
            {
                encryptedContent = EncryptMessage(content, groupId);
                storeContent = encryptedContent;
            }

            // === FIXED: Better Reply Preview with DECRYPTED content ===
            string replyToSenderName = string.Empty;
            string replyToPreview = string.Empty;

            if (replyToMessageId > 0)
            {
                var replyMsg = await db.Table<GroupMessage>()
                    .Where(m => m.Id == replyToMessageId)
                    .FirstOrDefaultAsync();

                if (replyMsg != null)
                {
                    replyToSenderName = replyMsg.DisplaySenderName ?? "Someone";

                    // FIX: Get DECRYPTED content for preview
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
                            // For text messages, get the decrypted content
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

            // === Disappearing messages ===
            DateTime? disappearAt = group?.DisappearingMessageSeconds > 0
                ? DateTime.UtcNow.AddSeconds(group.DisappearingMessageSeconds)
                : null;

            // === Normalize media paths for MAUI (important fix) ===
            var normalizedMediaPaths = new List<string>();
            if (mediaPaths != null)
            {
                foreach (var path in mediaPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        string cleanPath = path.Trim();
                        // Ensure it's a proper file path (add file:// if needed on some platforms)
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
                ReplyToPreview = replyToPreview,  // ← Now uses DECRYPTED content
                SentAt = DateTime.UtcNow,
                DisappearAt = disappearAt,
                IsDeleted = false,
                IsPinned = false,
                ReadByCount = 1
            };

            await db.InsertAsync(message);

            // Update group last activity
            if (group != null)
            {
                group.LastMessagePreview = type == GroupMessageType.Image ? "📷 Photo" : message.ContentPreview;
                group.LastMessageSenderName = message.DisplaySenderName;
                group.LastMessageAt = DateTime.UtcNow;
                group.LastActiveAt = DateTime.UtcNow;
                await db.UpdateAsync(group);
            }

            Debug.WriteLine($"Message sent: Type={type}, MediaCount={normalizedMediaPaths.Count}");
            return message;
        }
        public static async Task<List<GroupMessage>> GetMessagesAsync(
            string groupId,
            int take = 50,
            int skip = 0)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var msgs = await db.Table<GroupMessage>()
                .Where(m => m.GroupId == groupId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

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

            bool canDelete = msg.SenderPhone == requestorPhone ||
                             (member?.IsPrivileged == true);
            if (!canDelete) return false;

            msg.IsDeleted = true;
            msg.Content = string.Empty;
            await db.UpdateAsync(msg);
            return true;
        }

        public static async Task<bool> EditMessageAsync(
       int messageId,
       string requestorPhone,
       string newContent)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var msg = await db.Table<GroupMessage>()
                .Where(m => m.Id == messageId && m.SenderPhone == requestorPhone)
                .FirstOrDefaultAsync();
            if (msg == null) return false;

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
            await db.UpdateAsync(msg);

            return true;
        }
        public static async Task AddReactionAsync(
            int messageId,
            string userPhone,
            string emoji)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var msg = await db.Table<GroupMessage>()
                .Where(m => m.Id == messageId)
                .FirstOrDefaultAsync();
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
            await db.UpdateAsync(msg);
        }

        public static async Task MarkAsReadAsync(string groupId, string userPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var member = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == userPhone)
                .FirstOrDefaultAsync();

            if (member == null) return;

            member.LastReadAt = DateTime.UtcNow;
            member.LastSeenAt = DateTime.UtcNow;
            await db.UpdateAsync(member);
        }

        public static async Task<bool> PinMessageAsync(
            string groupId,
            int messageId,
            string adminPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var admin = await db.Table<GroupMember>()
                .Where(m => m.GroupId == groupId && m.UserPhone == adminPhone)
                .FirstOrDefaultAsync();
            if (admin == null || !admin.IsPrivileged) return false;

            // Max 3 pinned messages
            var pinCount = await db.Table<GroupPinnedMessage>()
                .Where(p => p.GroupId == groupId)
                .CountAsync();
            if (pinCount >= 3) return false;

            var msg = await db.Table<GroupMessage>()
                .Where(m => m.Id == messageId)
                .FirstOrDefaultAsync();
            if (msg == null) return false;

            msg.IsPinned = true;
            await db.UpdateAsync(msg);

            var pin = new GroupPinnedMessage
            {
                GroupId = groupId,
                MessageId = messageId,
                PinnedByPhone = adminPhone,
                PinnedAt = DateTime.UtcNow,
                MessagePreview = msg.ContentPreview
            };
            await db.InsertAsync(pin);

            await PostSystemMessageAsync(groupId, "📌 A message was pinned");
            return true;
        }

        public static async Task<List<GroupPinnedMessage>> GetPinnedMessagesAsync(string groupId)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();
            return await db.Table<GroupPinnedMessage>()
                .Where(p => p.GroupId == groupId)
                .OrderByDescending(p => p.PinnedAt)
                .ToListAsync();
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
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

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

            await db.InsertAsync(invite);
            return invite;
        }

        public static async Task<(bool success, string message, Group? group)>
            JoinByInviteCodeAsync(string code, string userPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var invite = await db.Table<GroupInvite>()
                .Where(i => i.InviteCode == code && i.IsActive)
                .FirstOrDefaultAsync();

            if (invite == null) return (false, "Invalid invite link", null);
            if (!invite.IsUsable) return (false, "This invite link has expired or reached its limit", null);

            var group = await GetGroupAsync(invite.GroupId);
            if (group == null) return (false, "Group not found", null);

            var (success, msg) = await JoinGroupAsync(invite.GroupId, userPhone);

            if (success)
            {
                invite.UseCount++;
                await db.UpdateAsync(invite);
            }

            return (success, msg, group);
        }

        public static async Task<bool> RevokeInviteAsync(string inviteId, string adminPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var invite = await db.Table<GroupInvite>()
                .Where(i => i.Id == inviteId)
                .FirstOrDefaultAsync();
            if (invite == null) return false;

            invite.IsActive = false;
            await db.UpdateAsync(invite);
            return true;
        }

        public static async Task<(bool success, string message)> CancelJoinRequestAsync(
    string requestId,
    string userPhone)
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var req = await db.Table<GroupJoinRequest>()
                    .Where(r => r.Id == requestId &&
                                r.UserPhone == userPhone &&
                                r.Status == "pending")
                    .FirstOrDefaultAsync();

                if (req == null)
                    return (false, "Request not found or already processed.");

                // Mark as cancelled — keeps a record for audit; admins won't see it
                // because GetPendingJoinRequestsAsync filters Status == "pending"
                req.Status = "cancelled";
                await db.UpdateAsync(req);

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
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();
            return await db.Table<GroupJoinRequest>()
                .Where(r => r.GroupId == groupId && r.Status == "pending")
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();
        }

        public static async Task<bool> ApproveJoinRequestAsync(
     string requestId,
     string adminPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var req = await db.Table<GroupJoinRequest>()
                .Where(r => r.Id == requestId)
                .FirstOrDefaultAsync();
            if (req == null) return false;

            var admin = await db.Table<GroupMember>()
                .Where(m => m.GroupId == req.GroupId && m.UserPhone == adminPhone)
                .FirstOrDefaultAsync();
            if (admin == null || !admin.IsPrivileged) return false;

            // Mark request approved
            req.Status = "approved";
            await db.UpdateAsync(req);

            // Check if already a member (guard against double-approval)
            var existing = await db.Table<GroupMember>()
                .Where(m => m.GroupId == req.GroupId && m.UserPhone == req.UserPhone)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // Already exists — just unban if banned
                if (existing.IsBanned)
                {
                    existing.IsBanned = false;
                    await db.UpdateAsync(existing);
                }
            }
            else
            {
                // Directly insert member — bypass RequireApproval logic in JoinGroupAsync
                var group = await GetGroupAsync(req.GroupId);
                int anonNumber = (group?.MemberCount ?? 0) + 1;

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

                await db.InsertAsync(member);

                if (group != null)
                {
                    group.MemberCount++;
                    group.LastActiveAt = DateTime.UtcNow;
                    await db.UpdateAsync(group);
                }

                // Post system message (PostSystemMessageAsync deduplicates internally)
                await PostSystemMessageAsync(req.GroupId, $"✨ {req.UserName} joined the group");
            }

            MessagingCenter.Send<object>(new object(), "GroupsUpdated");
            return true;
        }

        public static async Task<bool> RejectJoinRequestAsync(string requestId, string adminPhone)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var req = await db.Table<GroupJoinRequest>()
                .Where(r => r.Id == requestId)
                .FirstOrDefaultAsync();
            if (req == null) return false;

            req.Status = "rejected";
            await db.UpdateAsync(req);
            return true;
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
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

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

            await db.InsertAsync(ev);

            var creatorName = await GetUserNameAsync(createdByPhone);
            await PostSystemMessageAsync(groupId,
                $"📅 {creatorName} created an event: {title}",
                GroupMessageType.Event);

            return ev;
        }

        public static async Task<bool> RsvpGroupEventAsync(
            string eventId,
            string userPhone,
            bool attending)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var ev = await db.Table<GroupEvent>()
                .Where(e => e.Id == eventId)
                .FirstOrDefaultAsync();
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
            await db.UpdateAsync(ev);
            return true;
        }

        public static async Task<List<GroupEvent>> GetGroupEventsAsync(string groupId)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();
            return await db.Table<GroupEvent>()
                .Where(e => e.GroupId == groupId)
                .OrderBy(e => e.EventDate)
                .ToListAsync();
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

            var pollJson = System.Text.Json.JsonSerializer.Serialize(poll);

            return await SendMessageAsync(
                groupId,
                creatorPhone,
                $"📊 Poll: {question}",
                GroupMessageType.Poll,
                pollJson: pollJson);
        }

        public static async Task<bool> VoteOnPollAsync(
            int messageId,
            string userPhone,
            int optionIndex)
        {
            await GroupDatabaseService.InitializeAsync();
            var db = GroupDatabaseService.GetConnection();

            var msg = await db.Table<GroupMessage>()
                .Where(m => m.Id == messageId)
                .FirstOrDefaultAsync();
            if (msg == null || string.IsNullOrEmpty(msg.PollJson)) return false;

            var poll = System.Text.Json.JsonSerializer.Deserialize<GroupPoll>(msg.PollJson);
            if (poll == null || optionIndex >= poll.Options.Count) return false;
            if (poll.IsExpired) return false;

            if (!poll.AllowMultipleVotes)
            {
                foreach (var opt in poll.Options)
                    opt.VoterPhones.Remove(userPhone);
            }

            if (!poll.Options[optionIndex].VoterPhones.Contains(userPhone))
                poll.Options[optionIndex].VoterPhones.Add(userPhone);

            msg.PollJson = System.Text.Json.JsonSerializer.Serialize(poll);
            await db.UpdateAsync(msg);
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // POLL EDIT & DELETE
        // ═══════════════════════════════════════════════════════════════

        public static async Task<bool> EditPollAsync(int messageId, string groupId, string newQuestion, List<string> newOptions)
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var message = await db.Table<GroupMessage>().Where(m => m.Id == messageId).FirstOrDefaultAsync();
                if (message == null) return false;

                // Update the poll question in the message content
                message.Content = $"📊 Poll: {newQuestion}";

                // Get existing poll to preserve voter data
                var existingPoll = System.Text.Json.JsonSerializer.Deserialize<GroupPoll>(message.PollJson);

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

                message.PollJson = System.Text.Json.JsonSerializer.Serialize(updatedPoll);
                message.IsEdited = true;

                await db.UpdateAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EditPollAsync error: {ex}");
                return false;
            }
        }

        public static async Task<bool> DeletePollAsync(int messageId, string groupId)
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var message = await db.Table<GroupMessage>().Where(m => m.Id == messageId).FirstOrDefaultAsync();
                if (message == null) return false;

                message.IsDeleted = true;
                await db.UpdateAsync(message);
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
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var now = DateTime.UtcNow;
                var expired = await db.Table<GroupMessage>()
                    .Where(m => m.DisappearAt != null && !m.IsDeleted)
                    .ToListAsync();

                foreach (var msg in expired.Where(m => m.DisappearAt < now))
                {
                    msg.IsDeleted = true;
                    msg.Content = string.Empty;
                    await db.UpdateAsync(msg);
                }

                Debug.WriteLine($"Cleaned up {expired.Count(m => m.DisappearAt < now)} disappearing messages");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupDisappearingMessages error: {ex}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // COMPATIBILITY SCORE FOR GROUPS
        // ═══════════════════════════════════════════════════════════════

        public static async Task<int> CalculateGroupCompatibilityAsync(
            string groupId, string userPhone)
        {
            try
            {
                var members = await GetMembersAsync(groupId);
                if (!members.Any()) return 0;

                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();

                var currentUser = await db.Table<User>()
                    .Where(u => u.PhoneNumber == userPhone)
                    .FirstOrDefaultAsync();
                if (currentUser == null) return 0;

                var userInterests = (currentUser.Interests ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim().ToLower())
                    .ToHashSet();

                int matchCount = 0;

                foreach (var member in members.Take(10))
                {
                    var memberUser = await db.Table<User>()
                        .Where(u => u.PhoneNumber == member.UserPhone)
                        .FirstOrDefaultAsync();

                    if (memberUser == null) continue;

                    var memberInterests = (memberUser.Interests ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Trim().ToLower())
                        .ToHashSet();

                    if (userInterests.Intersect(memberInterests).Any())
                        matchCount++;
                }

                int sampleSize = Math.Min(members.Count, 10);
                return sampleSize > 0
                    ? (int)((double)matchCount / sampleSize * 100)
                    : 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalculateGroupCompatibility error: {ex}");
                return 0;
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
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                // FIXED: Better duplicate detection with longer window and exact content match
                var recentDuplicate = await db.Table<GroupMessage>()
       .Where(m => m.GroupId == groupId &&
                   m.IsSystemMessage &&
                   m.Content == content)
       .FirstOrDefaultAsync();

                if (recentDuplicate != null)
                {
                    Debug.WriteLine($"Skipped duplicate system message: {content}");
                    return;
                }

                var msg = new GroupMessage
                {
                    GroupId = groupId,
                    SenderPhone = "system",
                    SenderName = "System",
                    MessageType = type,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    IsSystemMessage = true,
                    ShowAvatar = false
                };

                await db.InsertAsync(msg);
                Debug.WriteLine($"System message posted: {content}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PostSystemMessage error: {ex}");
            }
        }

        public static async Task AddSystemMessageIndexAsync()
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                // Add unique index to prevent duplicate system messages within short time window
                await db.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_system_message_dedup 
            ON GroupMessages(GroupId, Content, SentAt) 
            WHERE IsSystemMessage = 1;
        ");

                Debug.WriteLine("System message deduplication index created");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddSystemMessageIndexAsync error: {ex}");
            }
        }



        private static async Task<string> GetUserNameAsync(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
                return user?.Name ?? phone;
            }
            catch { return phone; }
        }

        private static async Task<string> GetUserProfileImageAsync(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
                return user?.ProfileImagePath ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static async Task<string> GetUserMoodAsync(string phone)
        {
            try
            {
                await DatabaseService.InitializeAsync();
                var db = DatabaseService.GetConnection();
                var user = await db.Table<User>()
                    .Where(u => u.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
                return user?.Mood ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        public static async Task<List<Group>> GetAllPublicGroupsAsync()
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                // Get ALL groups for debugging
                var allGroups = await db.Table<Group>().ToListAsync();
                Debug.WriteLine($"Total groups in database: {allGroups.Count}");

                foreach (var g in allGroups)
                {
                    Debug.WriteLine($"Group: '{g.Name}' | Visibility: {g.Visibility} | Active: {g.IsActive}");
                }

                // Get only Public + Active groups
                var publicGroups = allGroups
                    .Where(g => g.IsActive && g.Visibility == GroupVisibility.Public)
                    .OrderByDescending(g => g.LastActiveAt)
                    .ToList();

                Debug.WriteLine($"Returning {publicGroups.Count} PUBLIC groups to Explore page");

                // Update member count
                foreach (var group in publicGroups)
                {
                    var count = await db.Table<GroupMember>()
                        .Where(m => m.GroupId == group.Id && !m.IsBanned)
                        .CountAsync();
                    group.MemberCount = count;
                }

                return publicGroups;
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
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                var memberships = await db.Table<GroupMember>()
                    .Where(m => m.UserPhone == userPhone && !m.IsBanned)
                    .ToListAsync();

                return memberships.Select(m => m.GroupId).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserGroupIdsAsync error: {ex.Message}");
                return new List<string>();
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

        // Simple AES encryption reusing your app's pattern
        private static string EncryptMessage(string plainText, string groupId)
        {
            try
            {
                using var aes = System.Security.Cryptography.Aes.Create();
                var keyBytes = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(groupId + "_lock_group_key"));
                aes.Key = keyBytes;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                var result = new byte[aes.IV.Length + encrypted.Length];
                aes.IV.CopyTo(result, 0);
                encrypted.CopyTo(result, aes.IV.Length);
                return Convert.ToBase64String(result);
            }
            catch { return plainText; }
        }
        public static async Task<List<Group>> GetAllGroupsForExploreAsync(string currentUserPhone)
        {
            try
            {
                await GroupDatabaseService.InitializeAsync();
                var db = GroupDatabaseService.GetConnection();

                // All active public groups
                var allGroups = await db.Table<Group>()
                    .Where(g => g.IsActive && g.Visibility == GroupVisibility.Public)
                    .ToListAsync();

                Debug.WriteLine($"GetAllGroupsForExploreAsync: found {allGroups.Count} public groups");

                // Confirmed memberships
                var memberships = await db.Table<GroupMember>()
                    .Where(m => m.UserPhone == currentUserPhone && !m.IsBanned)
                    .ToListAsync();
                var memberGroupIds = memberships.Select(m => m.GroupId).ToHashSet();

                // Pending join requests — keyed by GroupId so we can get the RequestId too
                var pendingRequests = await db.Table<GroupJoinRequest>()
                    .Where(r => r.UserPhone == currentUserPhone && r.Status == "pending")
                    .ToListAsync();
                var pendingByGroupId = pendingRequests.ToDictionary(r => r.GroupId, r => r.Id);

                // Stamp each group with live member count + membership flags
                foreach (var group in allGroups)
                {
                    group.IsMember = memberGroupIds.Contains(group.Id);
                    group.IsPendingJoin = !group.IsMember && pendingByGroupId.ContainsKey(group.Id);
                    group.PendingJoinRequestId = group.IsPendingJoin
                        ? pendingByGroupId[group.Id]
                        : string.Empty;

                    var count = await db.Table<GroupMember>()
                        .Where(m => m.GroupId == group.Id && !m.IsBanned)
                        .CountAsync();
                    group.MemberCount = count;
                }

                return allGroups.OrderByDescending(g => g.LastActiveAt).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllGroupsForExploreAsync ERROR: {ex.Message}");
                return new List<Group>();
            }
        }


        private static string DecryptMessage(string cipherText, string groupId)
        {
            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);
                using var aes = System.Security.Cryptography.Aes.Create();
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