using SQLite;
using System;

namespace Lock.Models
{
    public class UserEndorsement
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // The person being endorsed (target)
        public int TargetUserId { get; set; }
        public string TargetUserPhone { get; set; } = string.Empty;

        // The person giving the endorsement (endorser)
        public int EndorserUserId { get; set; }
        public string EndorserUserPhone { get; set; } = string.Empty;
        public string EndorserName { get; set; } = string.Empty;
        public string EndorserProfileImage { get; set; } = string.Empty;  // ADD THIS PROPERTY

        // Endorsement content
        public string Testimonial { get; set; } = string.Empty;
        public int Rating { get; set; } = 5;

        // Status and timestamps
        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // For display purposes (not stored in DB)
        [Ignore]
        public string RatingStars => new string('★', Rating) + new string('☆', 5 - Rating);

        [Ignore]
        public string DisplayDate => CreatedAt.ToString("MMMM dd, yyyy");
    }
}