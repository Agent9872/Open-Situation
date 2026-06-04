using Newtonsoft.Json;
using System;

namespace Lock.Models
{
    public class UserEndorsement
    {
        public int Id { get; set; }

        [JsonProperty("target_user_id")]
        public int TargetUserId { get; set; }

        [JsonProperty("target_user_phone")]
        public string TargetUserPhone { get; set; } = string.Empty;

        [JsonProperty("endorser_user_id")]
        public int EndorserUserId { get; set; }

        [JsonProperty("endorser_user_phone")]
        public string EndorserUserPhone { get; set; } = string.Empty;

        [JsonProperty("endorser_name")]
        public string EndorserName { get; set; } = string.Empty;

        [JsonProperty("endorser_profile_image")]
        public string EndorserProfileImage { get; set; } = string.Empty;

        [JsonProperty("testimonial")]
        public string Testimonial { get; set; } = string.Empty;

        [JsonProperty("rating")]
        public int Rating { get; set; } = 5;

        [JsonProperty("is_approved")]
        public bool IsApproved { get; set; } = true;

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // For display purposes (computed properties - not stored)
        public string RatingStars => new string('★', Rating) + new string('☆', 5 - Rating);
        public string DisplayDate => CreatedAt.ToString("MMMM dd, yyyy");
    }
}