namespace Lock.Models
{
    public class EndorsementRequest
    {
        public int Id { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string RequestorPhone { get; set; } = string.Empty;
        public string RequestorName { get; set; } = string.Empty;
        public string RequestorProfileImage { get; set; } = string.Empty;
        public string FriendPhone { get; set; } = string.Empty;
        public string FriendName { get; set; } = string.Empty;
        public string Testimonial { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "pending"; // pending, accepted, declined
        public DateTime? RespondedAt { get; set; }
    }
}