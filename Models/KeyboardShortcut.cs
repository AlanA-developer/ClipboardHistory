using System.ComponentModel.DataAnnotations;

namespace ClipboardHistory.Models
{
    public class KeyboardShortcut
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Display name for the shortcut (e.g., "WhatsApp", "Telegram")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Target executable path or "__CLIPBOARD__" for the built-in clipboard shortcut
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Win32 modifier flags (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN)
        /// </summary>
        public uint Modifiers { get; set; }

        /// <summary>
        /// Win32 virtual key code (e.g., 0x57 for 'W')
        /// </summary>
        public uint VirtualKey { get; set; }

        /// <summary>
        /// Human-readable modifier display (e.g., "Alt", "Ctrl+Shift")
        /// </summary>
        public string ModifierDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable key display (e.g., "W", "V")
        /// </summary>
        public string KeyDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Built-in shortcuts cannot be deleted (e.g., the clipboard shortcut)
        /// </summary>
        public bool IsBuiltIn { get; set; } = false;

        /// <summary>
        /// Computed display string for the shortcut combination
        /// </summary>
        public string ShortcutDisplay =>
            string.IsNullOrEmpty(ModifierDisplay) ? KeyDisplay : $"{ModifierDisplay} + {KeyDisplay}";
    }
}
