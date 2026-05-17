using System;

namespace Lock.Models
{
    public class LinkPreview
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public bool IsLoading { get; set; } = true;
        public bool HasError { get; set; } = false;
    }
}