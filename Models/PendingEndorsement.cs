using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lock.Models
{
    public class PendingEndorsement
    {
        public string RequestId { get; set; } = string.Empty;
        public string FriendPhone { get; set; } = string.Empty;
        public string FriendName { get; set; } = string.Empty;
        public string FriendProfileImage { get; set; } = string.Empty;
        public string Testimonial { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "pending";

        // For display
        public string TimeAgo => GetTimeAgo(CreatedAt);
        public string DisplayDate => CreatedAt.ToString("MMM dd, yyyy 'at' h:mm tt");

        private string GetTimeAgo(DateTime date)
        {
            var diff = DateTime.UtcNow - date;

            if (diff.TotalMinutes < 1)
                return "Just now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} minute(s) ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} hour(s) ago";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} day(s) ago";

            return date.ToString("MMM dd, yyyy");
        }
    }
}