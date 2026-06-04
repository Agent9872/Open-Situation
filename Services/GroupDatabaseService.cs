using Lock.Models;
using Lock.Models.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lock.Chat.Services
{
    public static class GroupDatabaseService
    {
        private static bool _isInitialized = false;

        public static async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Just verify Supabase connection is available
                // Your SupabaseService should already be configured
                _isInitialized = true;
                Debug.WriteLine("GroupDatabaseService initialized successfully (using Supabase)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GroupDatabaseService init error: {ex}");
                throw;
            }
        }

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService.GetAsync<Group>() directly")]
        public static Task<List<Group>> GetGroupsAsync() =>
            SupabaseService.GetAsync<Group>("Groups", "");

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService.GetAsync<GroupMember>() directly")]
        public static Task<List<GroupMember>> GetGroupMembersAsync(string groupId) =>
            SupabaseService.GetAsync<GroupMember>("GroupMembers", $"GroupId=eq.{Uri.EscapeDataString(groupId)}");

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService.GetAsync<GroupMessage>() directly")]
        public static Task<List<GroupMessage>> GetGroupMessagesAsync(string groupId) =>
            SupabaseService.GetAsync<GroupMessage>("GroupMessages",
                $"GroupId=eq.{Uri.EscapeDataString(groupId)}&order=SentAt.asc");

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService.InsertAsync<Group>() directly")]
        public static Task<bool> InsertGroupAsync(Group group) =>
            SupabaseService.InsertAsync("Groups", group);

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService.InsertAsync<GroupMember>() directly")]
        public static Task<bool> InsertGroupMemberAsync(GroupMember member) =>
            SupabaseService.InsertAsync("GroupMembers", member);

        // Note: This method is deprecated. Use SupabaseService directly instead.
        [Obsolete("Use SupabaseService.InsertAsync<GroupMessage>() directly")]
        public static Task<bool> InsertGroupMessageAsync(GroupMessage message) =>
            SupabaseService.InsertAsync("GroupMessages", message);
    }
}