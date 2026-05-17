// ════════════════════════════════════════════════════
// FILE 2 — NEW FILE
// Path: Lock/Services/Admin/PagePermissionDefinitions.cs
// ════════════════════════════════════════════════════

using System.Collections.Generic;

namespace Lock.Services.Admin
{
    public class PagePermissionGroup
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupIcon { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#00C9C9";
        public List<PagePermissionEntry> Pages { get; set; } = new();
    }

    public class PagePermissionEntry
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool DefaultUserAccess { get; set; } = true;
    }

    public static class PagePermissionDefinitions
    {
        public static readonly List<PagePermissionGroup> Groups = new()
        {
            new PagePermissionGroup
            {
                GroupName = "Core",
                GroupIcon = "🏠",
                AccentColor = "#00C9C9",
                Pages = new()
                {
                    new() { Key = "post",          DisplayName = "Home Feed",        Description = "View and create posts",        Icon = "📱", DefaultUserAccess = true  },
                    new() { Key = "profile",       DisplayName = "Profile",          Description = "View and edit own profile",    Icon = "👤", DefaultUserAccess = true  },
                    new() { Key = "conversations", DisplayName = "Messaging",        Description = "Send and receive messages",    Icon = "💬", DefaultUserAccess = true  },
                    new() { Key = "match",         DisplayName = "Match / Discover", Description = "Browse and match with users",  Icon = "❤️", DefaultUserAccess = true  },
                }
            },
            new PagePermissionGroup
            {
                GroupName = "Social",
                GroupIcon = "🌐",
                AccentColor = "#C084FC",
                Pages = new()
                {
                    new() { Key = "comments",    DisplayName = "Comments",      Description = "Comment on posts",           Icon = "💭", DefaultUserAccess = true  },
                    new() { Key = "chatsearch",  DisplayName = "Search Users",  Description = "Search for other users",     Icon = "🔍", DefaultUserAccess = true  },
                    new() { Key = "creategroup", DisplayName = "Create Groups", Description = "Create group conversations", Icon = "👥", DefaultUserAccess = true  },
                }
            },
            new PagePermissionGroup
            {
                GroupName = "Admin",
                GroupIcon = "🛡️",
                AccentColor = "#FF6B6B",
                Pages = new()
                {
                    new() { Key = "admin/users",     DisplayName = "User Management",   Description = "View and manage all users",    Icon = "👥", DefaultUserAccess = false },
                    new() { Key = "admin/roles",     DisplayName = "Role Manager",      Description = "Assign admin roles",           Icon = "🔑", DefaultUserAccess = false },
                    new() { Key = "userdetail",      DisplayName = "User Detail View",  Description = "View detailed user profiles",  Icon = "🔎", DefaultUserAccess = false },
                    new() { Key = "admin/dashboard", DisplayName = "Admin Dashboard",   Description = "View analytics dashboard",     Icon = "📊", DefaultUserAccess = false },
                    new() { Key = "admin/reports",   DisplayName = "Reports & Appeals", Description = "Handle reports and appeals",   Icon = "🚩", DefaultUserAccess = false },
                }
            },
        };

        public static HashSet<string> DefaultDeniedForUser()
        {
            var denied = new HashSet<string>();
            foreach (var group in Groups)
                foreach (var page in group.Pages)
                    if (!page.DefaultUserAccess)
                        denied.Add(page.Key);
            return denied;
        }
    }
}