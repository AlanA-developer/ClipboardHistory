using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardHistory.Models;

namespace ClipboardHistory.ViewModels
{
    /// <summary>
    /// Lightweight view model for displaying clipboard images in the UI.
    /// Holds a pre-generated thumbnail BitmapSource to avoid loading full-size
    /// images into the visual tree (critical for performance with large images).
    /// </summary>
    public class ClipboardImageViewModel
    {
        /// <summary>
        /// The underlying database entity
        /// </summary>
        public ClipboardImage Entity { get; }

        /// <summary>
        /// Pre-generated thumbnail for UI display (max 200px on longest side)
        /// </summary>
        public BitmapSource Thumbnail { get; }

        /// <summary>
        /// Human-readable file size string (e.g. "1.2 MB")
        /// </summary>
        public string FileSizeText { get; }

        /// <summary>
        /// Resolution string (e.g. "1920×1080")
        /// </summary>
        public string ResolutionText { get; }

        /// <summary>
        /// Timestamp from the entity
        /// </summary>
        public DateTime Timestamp => Entity.Timestamp;

        /// <summary>
        /// IsPinned from the entity
        /// </summary>
        public bool IsPinned => Entity.IsPinned;

        public ClipboardImageViewModel(ClipboardImage entity)
        {
            Entity = entity;
            Thumbnail = GenerateThumbnail(entity.ImageData, 200);
            FileSizeText = FormatFileSize(entity.FileSizeBytes);
            ResolutionText = $"{entity.Width}×{entity.Height}";
        }

        /// <summary>
        /// Creates a downscaled BitmapImage from raw PNG bytes.
        /// DecodePixelWidth/Height tells WPF to decode at reduced resolution,
        /// so we never hold the full-res bitmap in memory.
        /// </summary>
        private static BitmapSource GenerateThumbnail(byte[] imageData, int maxPixels)
        {
            if (imageData == null || imageData.Length == 0)
            {
                // Return a 1x1 transparent pixel as fallback
                return BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 0 }, 4);
            }

            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(imageData))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.DecodePixelWidth = maxPixels;
                bitmap.EndInit();
            }
            bitmap.Freeze(); // Make cross-thread accessible and reduce memory
            return bitmap;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        /// <summary>
        /// Static helper: Convert a BitmapSource to PNG byte array for storage
        /// </summary>
        public static byte[] BitmapSourceToPng(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
