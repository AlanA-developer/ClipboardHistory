using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardHistory.Data;
using ClipboardHistory.Models;
using ClipboardHistory.Services;
using ClipboardHistory.ViewModels;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui.Appearance;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;
using FormsApp = System.Windows.Forms;

namespace ClipboardHistory
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private HwndSource? _hwndSource;
        private readonly ClipboardDbContext _db;
        private FormsApp.NotifyIcon? _trayIcon;
        private bool _isReallyClosing = false;

        // Keyboard navigation: selected index per tab (-1 = none)
        private int _selectedTextIndex = -1;
        private int _selectedImageIndex = -1;
        private int _selectedPinnedIndex = -1;

        // Text collections
        public ObservableCollection<ClipboardItem> Items { get; set; } = new();
        public ObservableCollection<ClipboardItem> PinnedItems { get; set; } = new();

        // Image collection
        public ObservableCollection<ClipboardImageViewModel> ImageItems { get; set; } = new();
        public ObservableCollection<KeyboardShortcut> Shortcuts { get; set; } = new();
        private readonly List<int> _registeredHotkeyIds = new();

        public MainWindow()
        {
            InitializeComponent();

            // Ensure dark theme is applied to this window
            ApplicationThemeManager.Apply(ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica);

            _db = new ClipboardDbContext();
            _db.Database.EnsureCreated();
            _db.EnsureShortcutsTableExists();

            ClipboardListView.ItemsSource = Items;
            PinnedListView.ItemsSource = PinnedItems;
            ImageListView.ItemsSource = ImageItems;
            ShortcutsListView.ItemsSource = Shortcuts;

            // Seed mock data if the database is empty
            SeedMockDataIfEmpty();
            SeedMockImagesIfEmpty();
            SeedDefaultShortcuts();

            LoadData();
            LoadShortcuts();

            // Initialize system tray icon
            InitializeTrayIcon();

            this.Loaded += MainWindow_Loaded;
            this.Deactivated += (s, e) => this.Hide();
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            EnableAutoStartOnFirstRun();
        }

        #region System Tray Icon

        private void InitializeTrayIcon()
        {
            _trayIcon = new FormsApp.NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "Clipboard History — Alt+V",
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => ShowAndFocus();

            var contextMenu = new FormsApp.ContextMenuStrip();
            
            var openItem = new FormsApp.ToolStripMenuItem("📋  Abrir Historial");
            openItem.Click += (s, e) => ShowAndFocus();
            openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(openItem);

            contextMenu.Items.Add(new FormsApp.ToolStripSeparator());

            var startupItem = new FormsApp.ToolStripMenuItem("🚀  Inicio con Windows");
            startupItem.Checked = IsStartupEnabled();
            startupItem.Click += (s, e) =>
            {
                ToggleStartup();
                startupItem.Checked = IsStartupEnabled();
            };
            contextMenu.Items.Add(startupItem);

            contextMenu.Items.Add(new FormsApp.ToolStripSeparator());

            var exitItem = new FormsApp.ToolStripMenuItem("❌  Salir");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Creates a clipboard-themed tray icon programmatically (no .ico file needed at runtime).
        /// </summary>
        private static System.Drawing.Icon CreateTrayIcon()
        {
            using var bitmap = new System.Drawing.Bitmap(32, 32);
            using var g = System.Drawing.Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            // Clipboard body (rounded rectangle)
            using var bodyBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(76, 144, 217));
            using var bodyPath = CreateRoundedRect(4, 6, 24, 22, 3);
            g.FillPath(bodyBrush, bodyPath);

            // Clipboard clip (top center)
            using var clipBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(52, 100, 160));
            g.FillRectangle(clipBrush, 10, 2, 12, 6);
            using var clipInnerBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(76, 144, 217));
            g.FillRectangle(clipInnerBrush, 12, 4, 8, 3);

            // Text lines on clipboard
            using var lineBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 230, 245));
            g.FillRectangle(lineBrush, 8, 13, 16, 2);
            g.FillRectangle(lineBrush, 8, 18, 12, 2);
            g.FillRectangle(lineBrush, 8, 23, 14, 2);

            IntPtr hIcon = bitmap.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRect(int x, int y, int w, int h, int r)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Check if the app is registered in Windows startup (HKCU Run key)
        /// </summary>
        private static bool IsStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("ClipboardHistory") != null;
            }
            catch { return false; }
        }

        /// <summary>
        /// Toggle auto-start registration in Windows Registry
        /// </summary>
        private static void ToggleStartup()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (key.GetValue("ClipboardHistory") != null)
                {
                    key.DeleteValue("ClipboardHistory", false);
                }
                else
                {
                    string exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue("ClipboardHistory", $"\"{exePath}\"");
                }
            }
            catch { /* May fail without admin rights on some systems */ }
        }

        private void ExitApplication()
        {
            _isReallyClosing = true;
            _trayIcon?.Dispose();
            _trayIcon = null;
            Application.Current.Shutdown();
        }

        #endregion

        #region Close → Hide (instead of terminate)

        /// <summary>
        /// Intercepts the close event. If the user clicks X, we just hide the window.
        /// The app keeps running in the system tray. Only ExitApplication() truly closes.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isReallyClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnClosing(e);
        }

        #endregion

        #region Initialization & Seed Data

        private void SeedMockDataIfEmpty()
        {
            if (!_db.ClipboardItems.Any())
            {
                var mockItems = new[]
                {
                    new ClipboardItem { Content = "SELECT * FROM Users WHERE active = 1 ORDER BY created_at DESC;", Timestamp = DateTime.Now.AddMinutes(-2) },
                    new ClipboardItem { Content = "https://github.com/lepoco/wpfui", Timestamp = DateTime.Now.AddMinutes(-5) },
                    new ClipboardItem { Content = "Este es un texto copiado de ejemplo para probar el historial del portapapeles.", Timestamp = DateTime.Now.AddMinutes(-8) },
                    new ClipboardItem { Content = "dotnet build -c Release && dotnet run", Timestamp = DateTime.Now.AddMinutes(-12) },
                    new ClipboardItem { Content = "La contraseña temporal es: Abc123!@#", Timestamp = DateTime.Now.AddMinutes(-15), IsPinned = true },
                    new ClipboardItem { Content = "192.168.1.100:8080", Timestamp = DateTime.Now.AddMinutes(-20), IsPinned = true },
                    new ClipboardItem { Content = "Buenos días equipo, les comparto el documento actualizado con las correcciones solicitadas.", Timestamp = DateTime.Now.AddMinutes(-30) },
                    new ClipboardItem { Content = "npm install && npm run dev", Timestamp = DateTime.Now.AddMinutes(-45) },
                };

                _db.ClipboardItems.AddRange(mockItems);
                _db.SaveChanges();
            }
        }

        /// <summary>
        /// Seeds the database with procedurally generated mock images
        /// so the Images tab has content for preview on first run.
        /// </summary>
        private void SeedMockImagesIfEmpty()
        {
            if (!_db.ClipboardImages.Any())
            {
                var mockImages = new[]
                {
                    CreateMockImage(320, 240, "#1E3A5F", "#4A90D9", "Mock 1", DateTime.Now.AddMinutes(-3)),
                    CreateMockImage(640, 480, "#2D1B4E", "#8B5CF6", "Mock 2", DateTime.Now.AddMinutes(-10)),
                    CreateMockImage(400, 300, "#1B4332", "#40C057", "Mock 3", DateTime.Now.AddMinutes(-18)),
                    CreateMockImage(800, 600, "#4A1C1C", "#E74C3C", "Mock 4", DateTime.Now.AddMinutes(-25), isPinned: true),
                };

                _db.ClipboardImages.AddRange(mockImages);
                _db.SaveChanges();
            }
        }

        /// <summary>
        /// Creates a procedurally generated gradient image for mock data.
        /// No external files needed — generates a real PNG in memory.
        /// </summary>
        private static ClipboardImage CreateMockImage(int width, int height, string color1Hex, string color2Hex, string label, DateTime timestamp, bool isPinned = false)
        {
            // Create a DrawingVisual with a gradient + text
            var dv = new DrawingVisual();
            var c1 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color1Hex);
            var c2 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color2Hex);

            using (var dc = dv.RenderOpen())
            {
                // Gradient background
                var gradientBrush = new LinearGradientBrush(c1, c2, 45);
                dc.DrawRectangle(gradientBrush, null, new Rect(0, 0, width, height));

                // Center label text
                var typeface = new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var formattedText = new FormattedText(
                    $"{width}×{height}",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    Math.Min(width, height) * 0.12,
                    System.Windows.Media.Brushes.White,
                    96);
                formattedText.TextAlignment = TextAlignment.Center;

                dc.DrawText(formattedText, new System.Windows.Point(width / 2.0, height / 2.0 - formattedText.Height / 2));

                // Subtitle
                var subtitleText = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    Math.Min(width, height) * 0.06,
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 255)),
                    96);
                subtitleText.TextAlignment = TextAlignment.Center;

                dc.DrawText(subtitleText, new System.Windows.Point(width / 2.0, height / 2.0 + formattedText.Height / 2 + 4));
            }

            // Render to bitmap
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            // Encode to PNG
            byte[] pngData = ClipboardImageViewModel.BitmapSourceToPng(rtb);

            return new ClipboardImage
            {
                ImageData = pngData,
                Width = width,
                Height = height,
                FileSizeBytes = pngData.Length,
                Timestamp = timestamp,
                IsPinned = isPinned,
            };
        }

        #endregion

        #region Win32 / Clipboard Listener

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource?.AddHook(HwndHandler);

            Win32Api.AddClipboardFormatListener(_hwndSource!.Handle);
            RegisterAllHotkeys();
        }

        private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == Win32Api.WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
            }
            else if (msg == Win32Api.WM_HOTKEY)
            {
                int hotkeyId = wparam.ToInt32();
                HandleHotkeyPressed(hotkeyId);
            }
            return IntPtr.Zero;
        }

        private void ShowAndFocus()
        {
            this.Show();
            this.Activate();
            this.Focus();
        }

        /// <summary>
        /// Called when the system clipboard content changes.
        /// Checks for text first, then images (sequential priority).
        /// </summary>
        private void OnClipboardChanged()
        {
            try
            {
                // Priority 1: Text content
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        SaveTextClipboard(text);
                        return;
                    }
                }

                // Priority 2: Image content
                if (Clipboard.ContainsImage())
                {
                    var bitmapSource = Clipboard.GetImage();
                    if (bitmapSource != null)
                    {
                        SaveImageClipboard(bitmapSource);
                    }
                }
            }
            catch { /* Clipboard access can throw if another process has it locked */ }
        }

        private void SaveTextClipboard(string text)
        {
            var existing = _db.ClipboardItems.FirstOrDefault(i => i.Content == text);
            if (existing != null)
            {
                existing.Timestamp = DateTime.Now;
                _db.Update(existing);
            }
            else
            {
                var newItem = new ClipboardItem { Content = text, Timestamp = DateTime.Now };
                _db.ClipboardItems.Add(newItem);
            }
            _db.SaveChanges();
            LoadData();
        }

        /// <summary>
        /// Converts a BitmapSource to PNG bytes and saves to the database.
        /// Generates a hash-like check to avoid storing exact duplicates.
        /// </summary>
        private void SaveImageClipboard(BitmapSource source)
        {
            byte[] pngData = ClipboardImageViewModel.BitmapSourceToPng(source);

            // Simple duplicate check: same size + same dimensions = likely duplicate
            int w = source.PixelWidth;
            int h = source.PixelHeight;
            long size = pngData.Length;

            var existing = _db.ClipboardImages
                .FirstOrDefault(i => i.Width == w && i.Height == h && i.FileSizeBytes == size);

            if (existing != null)
            {
                // Update timestamp (bring to top)
                existing.Timestamp = DateTime.Now;
                _db.Update(existing);
            }
            else
            {
                var newImage = new ClipboardImage
                {
                    ImageData = pngData,
                    Width = w,
                    Height = h,
                    FileSizeBytes = size,
                    Timestamp = DateTime.Now,
                };
                _db.ClipboardImages.Add(newImage);
            }
            _db.SaveChanges();
            LoadData();
        }

        #endregion

        #region Data Loading

        private void LoadData(string filter = "")
        {
            LoadTextData(filter);
            LoadImageData();
            UpdateEmptyStates();
            ClearAllHighlights();
        }

        private void LoadTextData(string filter)
        {
            // Load all text items
            var query = _db.ClipboardItems.AsQueryable();
            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(i => i.Content.Contains(filter));
            }

            var allList = query.OrderByDescending(i => i.Timestamp)
                               .Take(50)
                               .ToList();

            Items.Clear();
            foreach (var item in allList) Items.Add(item);

            // Load pinned text items
            var pinnedQuery = _db.ClipboardItems.Where(i => i.IsPinned);
            if (!string.IsNullOrEmpty(filter))
            {
                pinnedQuery = pinnedQuery.Where(i => i.Content.Contains(filter));
            }
            var pinnedList = pinnedQuery.OrderByDescending(i => i.Timestamp).ToList();

            PinnedItems.Clear();
            foreach (var item in pinnedList) PinnedItems.Add(item);
        }

        /// <summary>
        /// Loads image entities from SQLite and wraps them in ViewModels
        /// that generate thumbnails for efficient UI display.
        /// </summary>
        private void LoadImageData()
        {
            var imageList = _db.ClipboardImages
                .OrderByDescending(i => i.IsPinned)
                .ThenByDescending(i => i.Timestamp)
                .Take(30) // Limit for performance
                .ToList();

            ImageItems.Clear();
            foreach (var img in imageList)
            {
                ImageItems.Add(new ClipboardImageViewModel(img));
            }
        }

        private void UpdateEmptyStates()
        {
            ImagesEmptyState.Visibility = ImageItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #endregion

        #region Keyboard Navigation

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Don't intercept when typing in search box (except arrows/enter/escape)
            if (SearchBox.IsFocused && e.Key != Key.Up && e.Key != Key.Down 
                && e.Key != Key.Left && e.Key != Key.Right 
                && e.Key != Key.Enter && e.Key != Key.Escape)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Left:
                    NavigateTab(-1);
                    e.Handled = true;
                    break;

                case Key.Right:
                    NavigateTab(1);
                    e.Handled = true;
                    break;

                case Key.Up:
                    NavigateItem(-1);
                    e.Handled = true;
                    break;

                case Key.Down:
                    NavigateItem(1);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ConfirmSelection();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    this.Hide();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Switch tabs using Left/Right arrows
        /// </summary>
        private void NavigateTab(int direction)
        {
            int tabCount = MainTabControl.Items.Count;
            int current = MainTabControl.SelectedIndex;
            int next = (current + direction + tabCount) % tabCount;
            MainTabControl.SelectedIndex = next;

            // Highlight the first item in the new tab
            ResetSelectionForCurrentTab();
        }

        /// <summary>
        /// Navigate items within current tab using Up/Down arrows
        /// </summary>
        private void NavigateItem(int direction)
        {
            int tabIndex = MainTabControl.SelectedIndex;

            switch (tabIndex)
            {
                case 0: // Text tab
                    if (Items.Count == 0) return;
                    _selectedTextIndex = ClampIndex(_selectedTextIndex + direction, Items.Count);
                    HighlightItem(ClipboardListView, _selectedTextIndex, TextScrollViewer);
                    break;

                case 1: // Images tab
                    if (ImageItems.Count == 0) return;
                    _selectedImageIndex = ClampIndex(_selectedImageIndex + direction, ImageItems.Count);
                    HighlightItem(ImageListView, _selectedImageIndex, ImageScrollViewer);
                    break;

                case 2: // Pinned tab
                    if (PinnedItems.Count == 0) return;
                    _selectedPinnedIndex = ClampIndex(_selectedPinnedIndex + direction, PinnedItems.Count);
                    HighlightItem(PinnedListView, _selectedPinnedIndex, PinnedScrollViewer);
                    break;
            }
        }

        /// <summary>
        /// Enter key: copy the currently selected item
        /// </summary>
        private void ConfirmSelection()
        {
            int tabIndex = MainTabControl.SelectedIndex;

            switch (tabIndex)
            {
                case 0: // Text
                    if (_selectedTextIndex >= 0 && _selectedTextIndex < Items.Count)
                        CopyTextToClipboard(Items[_selectedTextIndex]);
                    break;

                case 1: // Images
                    if (_selectedImageIndex >= 0 && _selectedImageIndex < ImageItems.Count)
                        CopyImageToClipboard(ImageItems[_selectedImageIndex]);
                    break;

                case 2: // Pinned
                    if (_selectedPinnedIndex >= 0 && _selectedPinnedIndex < PinnedItems.Count)
                        CopyTextToClipboard(PinnedItems[_selectedPinnedIndex]);
                    break;
            }
        }

        /// <summary>
        /// Reset selection to first item when switching tabs
        /// </summary>
        private void ResetSelectionForCurrentTab()
        {
            // Use Dispatcher to ensure the tab content is rendered before highlighting
            Dispatcher.InvokeAsync(() =>
            {
                int tabIndex = MainTabControl.SelectedIndex;
                switch (tabIndex)
                {
                    case 0:
                        _selectedTextIndex = Items.Count > 0 ? 0 : -1;
                        if (_selectedTextIndex >= 0) HighlightItem(ClipboardListView, _selectedTextIndex, TextScrollViewer);
                        break;
                    case 1:
                        _selectedImageIndex = ImageItems.Count > 0 ? 0 : -1;
                        if (_selectedImageIndex >= 0) HighlightItem(ImageListView, _selectedImageIndex, ImageScrollViewer);
                        break;
                    case 2:
                        _selectedPinnedIndex = PinnedItems.Count > 0 ? 0 : -1;
                        if (_selectedPinnedIndex >= 0) HighlightItem(PinnedListView, _selectedPinnedIndex, PinnedScrollViewer);
                        break;
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static int ClampIndex(int index, int count)
        {
            if (index < 0) return 0;
            if (index >= count) return count - 1;
            return index;
        }

        /// <summary>
        /// Visually highlight the selected item in an ItemsControl by modifying
        /// the Border of each item container. The selected item gets an accent border.
        /// </summary>
        private void HighlightItem(ItemsControl itemsControl, int selectedIndex, ScrollViewer? scrollViewer)
        {
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                if (container == null) continue;

                // The first child of ContentPresenter is our Border from the DataTemplate
                var border = System.Windows.Media.VisualTreeHelper.GetChild(container, 0) as System.Windows.Controls.Border;
                if (border == null) continue;

                if (i == selectedIndex)
                {
                    // Accent highlight: brighter border + subtle glow background
                    border.BorderBrush = (System.Windows.Media.Brush)FindResource("SystemFillColorAttentionBrush");
                    border.BorderThickness = new Thickness(2);
                    border.Opacity = 1.0;
                }
                else
                {
                    // Reset to default style
                    border.BorderBrush = (System.Windows.Media.Brush)FindResource("ControlStrokeColorDefaultBrush");
                    border.BorderThickness = new Thickness(1);
                    border.Opacity = 0.85;
                }
            }

            // Scroll the selected item into view
            if (scrollViewer != null && selectedIndex >= 0)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(selectedIndex) as ContentPresenter;
                if (container != null)
                {
                    container.BringIntoView();
                }
            }
        }

        /// <summary>
        /// Clear all visual highlights (called when data reloads)
        /// </summary>
        private void ClearAllHighlights()
        {
            _selectedTextIndex = -1;
            _selectedImageIndex = -1;
            _selectedPinnedIndex = -1;
        }

        #endregion

        #region Text Event Handlers

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadData(SearchBox.Text);
        }

        private void CardClick_Handler(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardItem item)
            {
                CopyTextToClipboard(item);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardItem item)
            {
                CopyTextToClipboard(item);
            }
        }

        private void CopyTextToClipboard(ClipboardItem item)
        {
            Win32Api.RemoveClipboardFormatListener(_hwndSource!.Handle);
            Clipboard.SetText(item.Content);
            Win32Api.AddClipboardFormatListener(_hwndSource.Handle);
            this.Hide();
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardItem item)
            {
                item.IsPinned = !item.IsPinned;
                _db.Update(item);
                _db.SaveChanges();
                LoadData(SearchBox.Text);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardItem item)
            {
                _db.ClipboardItems.Remove(item);
                _db.SaveChanges();
                LoadData(SearchBox.Text);
            }
        }

        #endregion

        #region Image Event Handlers

        /// <summary>
        /// Click on image card → copy image to clipboard and hide
        /// </summary>
        private void ImageCardClick_Handler(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardImageViewModel vm)
            {
                CopyImageToClipboard(vm);
            }
        }

        private void ImageCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardImageViewModel vm)
            {
                CopyImageToClipboard(vm);
            }
        }

        /// <summary>
        /// Reconstructs the full-resolution BitmapImage from stored PNG bytes
        /// and places it on the system clipboard.
        /// </summary>
        private void CopyImageToClipboard(ClipboardImageViewModel vm)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(vm.Entity.ImageData))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();

                Win32Api.RemoveClipboardFormatListener(_hwndSource!.Handle);
                Clipboard.SetImage(bitmap);
                Win32Api.AddClipboardFormatListener(_hwndSource.Handle);
                this.Hide();
            }
            catch { /* Clipboard may be locked */ }
        }

        private void ImagePinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardImageViewModel vm)
            {
                vm.Entity.IsPinned = !vm.Entity.IsPinned;
                _db.Update(vm.Entity);
                _db.SaveChanges();
                LoadData(SearchBox.Text);
            }
        }

        private void ImageDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ClipboardImageViewModel vm)
            {
                _db.ClipboardImages.Remove(vm.Entity);
                _db.SaveChanges();
                LoadData(SearchBox.Text);
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            if (_hwndSource != null)
            {
                Win32Api.RemoveClipboardFormatListener(_hwndSource.Handle);
                UnregisterAllHotkeys();
            }
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}
