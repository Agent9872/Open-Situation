using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lock.Models.Chat
{
    public class MediaDisplayItem
    {
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = "image";
        public DateTime SentAt { get; set; }
        // Optional: public string SenderPhone { get; set; } = string.Empty;
    }
}
