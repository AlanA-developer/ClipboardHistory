using Microsoft.EntityFrameworkCore;
using ClipboardHistory.Models;

namespace ClipboardHistory.Data
{
    public class ClipboardDbContext : DbContext
    {
        public DbSet<ClipboardItem> ClipboardItems { get; set; }
        public DbSet<ClipboardImage> ClipboardImages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=clipboard.db");
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
        }
    }
}
