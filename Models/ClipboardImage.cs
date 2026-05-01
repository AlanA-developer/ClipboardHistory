using System;
using System.ComponentModel.DataAnnotations;

namespace ClipboardHistory.Models
{
    public class ClipboardImage
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Raw PNG image data stored as BLOB in SQLite
        /// </summary>
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// Width of the original image in pixels (metadata for display)
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the original image in pixels (metadata for display)
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// File size of the stored image data in bytes (metadata for display)
        /// </summary>
        public long FileSizeBytes { get; set; }
    }
}
