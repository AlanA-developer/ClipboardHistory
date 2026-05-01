using System;
using System.ComponentModel.DataAnnotations;

namespace ClipboardHistory.Models
{
    public class ClipboardItem
    {
        [Key]
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; } = false;
    }
}
