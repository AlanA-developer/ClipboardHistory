using System;
using System.Runtime.InteropServices;

namespace ClipboardHistory.Services
{
    public static class Win32Api
    {
        // Window messages
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_HOTKEY = 0x0312;

        // Clipboard listener
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        // Hotkey registration
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier constants
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        // Common virtual key codes
        public const uint VK_V = 0x56;
        public const uint VK_W = 0x57;
        public const uint VK_T = 0x54;
    }
}
