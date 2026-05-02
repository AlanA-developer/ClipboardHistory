using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ClipboardHistory.Models;
using ClipboardHistory.Services;

namespace ClipboardHistory
{
    public partial class MainWindow
    {
        // Key capture state for the shortcut form
        private uint _capturedModifiers;
        private uint _capturedVKey;
        private string _capturedModDisplay = "";
        private string _capturedKeyDisplay = "";
        private List<InstalledApp> _installedApps = new();

        private const string CLIPBOARD_TARGET = "__CLIPBOARD__";

        #region Shortcut Data Management

        /// <summary>
        /// Seeds default shortcuts on first run: Alt+V (clipboard), Alt+W (WhatsApp), Alt+T (Telegram)
        /// </summary>
        private void SeedDefaultShortcuts()
        {
            if (_db.KeyboardShortcuts.Any()) return;

            var defaults = new[]
            {
                new KeyboardShortcut
                {
                    Name = "Portapapeles",
                    Target = CLIPBOARD_TARGET,
                    Modifiers = Win32Api.MOD_ALT,
                    VirtualKey = Win32Api.VK_V,
                    ModifierDisplay = "Alt",
                    KeyDisplay = "V",
                    IsBuiltIn = true
                },
                new KeyboardShortcut
                {
                    Name = "WhatsApp",
                    Target = FindAppPath("WhatsApp") ?? "whatsapp://",
                    Modifiers = Win32Api.MOD_ALT,
                    VirtualKey = Win32Api.VK_W,
                    ModifierDisplay = "Alt",
                    KeyDisplay = "W",
                    IsBuiltIn = false
                },
                new KeyboardShortcut
                {
                    Name = "Telegram",
                    Target = FindAppPath("Telegram") ?? "tg://",
                    Modifiers = Win32Api.MOD_ALT,
                    VirtualKey = Win32Api.VK_T,
                    ModifierDisplay = "Alt",
                    KeyDisplay = "T",
                    IsBuiltIn = false
                }
            };

            _db.KeyboardShortcuts.AddRange(defaults);
            _db.SaveChanges();
        }

        /// <summary>
        /// Try to find an installed app by partial name match
        /// </summary>
        private static string? FindAppPath(string partialName)
        {
            try
            {
                var apps = InstalledAppsService.GetInstalledApps();
                var match = apps.FirstOrDefault(a =>
                    a.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase));
                return match?.ExecutablePath;
            }
            catch { return null; }
        }

        private void LoadShortcuts()
        {
            var list = _db.KeyboardShortcuts.OrderBy(s => s.Id).ToList();
            Shortcuts.Clear();
            foreach (var s in list) Shortcuts.Add(s);
        }

        #endregion

        #region Hotkey Registration

        private void RegisterAllHotkeys()
        {
            if (_hwndSource == null) return;

            var shortcuts = _db.KeyboardShortcuts.ToList();
            foreach (var s in shortcuts)
            {
                int id = 9000 + s.Id;
                if (Win32Api.RegisterHotKey(_hwndSource.Handle, id, s.Modifiers, s.VirtualKey))
                {
                    _registeredHotkeyIds.Add(id);
                }
            }
        }

        private void UnregisterAllHotkeys()
        {
            if (_hwndSource == null) return;
            foreach (int id in _registeredHotkeyIds)
            {
                Win32Api.UnregisterHotKey(_hwndSource.Handle, id);
            }
            _registeredHotkeyIds.Clear();
        }

        private void RegisterSingleHotkey(KeyboardShortcut shortcut)
        {
            if (_hwndSource == null) return;
            int id = 9000 + shortcut.Id;
            if (Win32Api.RegisterHotKey(_hwndSource.Handle, id, shortcut.Modifiers, shortcut.VirtualKey))
            {
                _registeredHotkeyIds.Add(id);
            }
        }

        private void UnregisterSingleHotkey(KeyboardShortcut shortcut)
        {
            if (_hwndSource == null) return;
            int id = 9000 + shortcut.Id;
            Win32Api.UnregisterHotKey(_hwndSource.Handle, id);
            _registeredHotkeyIds.Remove(id);
        }

        #endregion

        #region Hotkey Execution

        private void HandleHotkeyPressed(int hotkeyId)
        {
            int shortcutId = hotkeyId - 9000;
            var shortcut = _db.KeyboardShortcuts.FirstOrDefault(s => s.Id == shortcutId);
            if (shortcut == null) return;

            if (shortcut.Target == CLIPBOARD_TARGET)
            {
                ShowAndFocus();
            }
            else
            {
                LaunchApplication(shortcut.Target);
            }
        }

        private static void LaunchApplication(string target)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { /* Target not found or access denied */ }
        }

        #endregion

        #region Shortcut Form UI Handlers

        private void AddShortcut_Click(object sender, RoutedEventArgs e)
        {
            // Load installed apps for the ComboBox
            _installedApps = InstalledAppsService.GetInstalledApps();
            ProgramComboBox.Items.Clear();
            foreach (var app in _installedApps)
            {
                ProgramComboBox.Items.Add(app.Name);
            }

            // Reset form
            ProgramComboBox.Text = "";
            KeyCaptureBox.Text = "\U0001f3b9 Haz clic aqu\u00ed y presiona las teclas...";
            _capturedModifiers = 0;
            _capturedVKey = 0;
            _capturedModDisplay = "";
            _capturedKeyDisplay = "";

            ShortcutFormPanel.Visibility = Visibility.Visible;
            AddShortcutButton.IsEnabled = false;
        }

        private void CancelShortcut_Click(object sender, RoutedEventArgs e)
        {
            ShortcutFormPanel.Visibility = Visibility.Collapsed;
            AddShortcutButton.IsEnabled = true;
        }

        private void SaveShortcut_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            string selectedName = ProgramComboBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(selectedName))
            {
                ProgramComboBox.Focus();
                return;
            }
            if (_capturedVKey == 0)
            {
                KeyCaptureBox.Focus();
                return;
            }

            // Check for conflicts
            var conflict = _db.KeyboardShortcuts.FirstOrDefault(s =>
                s.Modifiers == _capturedModifiers && s.VirtualKey == _capturedVKey);
            if (conflict != null)
            {
                KeyCaptureBox.Text = $"\u26a0\ufe0f Conflicto con: {conflict.Name}";
                return;
            }

            // Find target path
            string target = "";
            var matchedApp = _installedApps.FirstOrDefault(a =>
                a.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
            if (matchedApp != null)
            {
                target = matchedApp.ExecutablePath;
            }
            else
            {
                // User typed a custom path or name
                target = selectedName;
            }

            var shortcut = new KeyboardShortcut
            {
                Name = matchedApp?.Name ?? selectedName,
                Target = target,
                Modifiers = _capturedModifiers,
                VirtualKey = _capturedVKey,
                ModifierDisplay = _capturedModDisplay,
                KeyDisplay = _capturedKeyDisplay,
                IsBuiltIn = false
            };

            _db.KeyboardShortcuts.Add(shortcut);
            _db.SaveChanges();

            RegisterSingleHotkey(shortcut);
            LoadShortcuts();

            ShortcutFormPanel.Visibility = Visibility.Collapsed;
            AddShortcutButton.IsEnabled = true;
        }

        private void DeleteShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is KeyboardShortcut shortcut)
            {
                if (shortcut.IsBuiltIn) return;

                UnregisterSingleHotkey(shortcut);
                _db.KeyboardShortcuts.Remove(shortcut);
                _db.SaveChanges();
                LoadShortcuts();
            }
        }

        #endregion

        #region Key Capture

        private void KeyCaptureBox_GotFocus(object sender, RoutedEventArgs e)
        {
            KeyCaptureBox.Text = "\u23f3 Esperando combinaci\u00f3n de teclas...";
            _capturedModifiers = 0;
            _capturedVKey = 0;
        }

        private void KeyCaptureBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            // Get the actual key (Alt sends Key.System)
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore modifier-only presses
            if (key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            // Build modifiers
            uint mods = 0;
            var modParts = new List<string>();

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                mods |= Win32Api.MOD_CONTROL;
                modParts.Add("Ctrl");
            }
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                mods |= Win32Api.MOD_ALT;
                modParts.Add("Alt");
            }
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                mods |= Win32Api.MOD_SHIFT;
                modParts.Add("Shift");
            }

            // Require at least one modifier
            if (mods == 0)
            {
                KeyCaptureBox.Text = "\u26a0\ufe0f Usa al menos un modificador (Ctrl, Alt, Shift)";
                return;
            }

            // Get VK code
            int vk = KeyInterop.VirtualKeyFromKey(key);
            string keyName = key.ToString();

            // Store captured values
            _capturedModifiers = mods;
            _capturedVKey = (uint)vk;
            _capturedModDisplay = string.Join("+", modParts);
            _capturedKeyDisplay = keyName;

            KeyCaptureBox.Text = $"\u2705 {_capturedModDisplay} + {_capturedKeyDisplay}";
        }

        #endregion

        #region Auto-Start

        /// <summary>
        /// Enables auto-start on first run so the app always runs in background
        /// </summary>
        private static void EnableAutoStartOnFirstRun()
        {
            if (!IsStartupEnabled())
            {
                ToggleStartup(); // Enables it
            }
        }

        #endregion
    }
}
