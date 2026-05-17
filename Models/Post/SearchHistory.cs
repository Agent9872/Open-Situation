// File: Models/SearchHistory.cs (not in Post folder)
using System;

namespace Lock.Models  // This is the key - use Lock.Models, not Lock.Models.Post
{
    public class SearchHistory
    {
        public string Query { get; set; } = string.Empty;
        public DateTime SearchTime { get; set; }
        public int ResultCount { get; set; }

        // Formatted properties for UI binding
        public string DisplayTime => SearchTime.ToString("hh:mm tt");
        public string DisplayDate => SearchTime.ToString("MMM d, yyyy");
        public string DisplayResultCount => ResultCount > 0 ? $"({ResultCount})" : "(no results)";
        public bool HasResults => ResultCount > 0;
    }
}