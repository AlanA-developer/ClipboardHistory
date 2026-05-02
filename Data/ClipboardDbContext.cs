using Microsoft.EntityFrameworkCore;
using ClipboardHistory.Models;

namespace ClipboardHistory.Data
{
    public class ClipboardDbContext : DbContext
    {
        public DbSet<ClipboardItem> ClipboardItems { get; set; }
        public DbSet<ClipboardImage> ClipboardImages { get; set; }
        public DbSet<KeyboardShortcut> KeyboardShortcuts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "clipboard.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ClipboardImage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ImageData).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
            });

            modelBuilder.Entity<KeyboardShortcut>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Target).IsRequired();
                entity.Ignore(e => e.ShortcutDisplay);
            });
        }

        /// <summary>
        /// Ensures the KeyboardShortcuts table exists in an already-created database.
        /// EnsureCreated() won't add new tables to an existing DB, so we do it manually.
        /// </summary>
        public void EnsureShortcutsTableExists()
        {
            try
            {
                Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS KeyboardShortcuts (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL DEFAULT '',
                        Target TEXT NOT NULL DEFAULT '',
                        Modifiers INTEGER NOT NULL DEFAULT 0,
                        VirtualKey INTEGER NOT NULL DEFAULT 0,
                        ModifierDisplay TEXT NOT NULL DEFAULT '',
                        KeyDisplay TEXT NOT NULL DEFAULT '',
                        IsBuiltIn INTEGER NOT NULL DEFAULT 0
                    )
                ");
            }
            catch { /* Table may already exist */ }
        }
    }
}
