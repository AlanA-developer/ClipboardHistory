using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ClipboardHistory.Services
{
    /// <summary>
    /// Represents an installed application discovered on the system.
    /// </summary>
    public class InstalledApp
    {
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Scans Start Menu shortcuts to enumerate installed applications.
    /// </summary>
    public static class InstalledAppsService
    {
        private static List<InstalledApp>? _cachedApps;

        /// <summary>
        /// Returns a cached list of installed applications.
        /// Call RefreshCache() to force a rescan.
        /// </summary>
        public static List<InstalledApp> GetInstalledApps()
        {
            if (_cachedApps != null) return _cachedApps;
            _cachedApps = ScanInstalledApps();
            return _cachedApps;
        }

        public static void RefreshCache()
        {
            _cachedApps = null;
        }

        private static List<InstalledApp> ScanInstalledApps()
        {
            var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

            // Scan both system and user Start Menu folders
            string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);

            ScanStartMenuFolder(apps, Path.Combine(commonStartMenu, "Programs"));
            ScanStartMenuFolder(apps, Path.Combine(userStartMenu, "Programs"));

            return apps.Values
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ScanStartMenuFolder(Dictionary<string, InstalledApp> apps, string folder)
        {
            if (!Directory.Exists(folder)) return;

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                foreach (var lnkFile in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                {
                    try
                    {
                        dynamic shortcut = shell.CreateShortcut(lnkFile);
                        string target = shortcut.TargetPath ?? "";
                        Marshal.ReleaseComObject(shortcut);

                        if (string.IsNullOrWhiteSpace(target)) continue;
                        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                        string name = Path.GetFileNameWithoutExtension(lnkFile);

                        // Filter out unwanted entries
                        if (IsUnwantedEntry(name)) continue;

                        if (!apps.ContainsKey(name))
                        {
                            apps[name] = new InstalledApp
                            {
                                Name = name,
                                ExecutablePath = target
                            };
                        }
                    }
                    catch { /* Skip broken shortcuts */ }
                }
            }
            catch { /* Folder access denied */ }
            finally
            {
                if (shell != null)
                {
                    try { Marshal.ReleaseComObject(shell); } catch { }
                }
            }
        }

        private static bool IsUnwantedEntry(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("uninstall") ||
                   lower.Contains("desinstalar") ||
                   lower.Contains("readme") ||
                   lower.Contains("license") ||
                   lower.Contains("changelog") ||
                   lower.Contains("release notes") ||
                   lower.Contains("manual") ||
                   lower.Contains("documentation");
        }
    }
}
