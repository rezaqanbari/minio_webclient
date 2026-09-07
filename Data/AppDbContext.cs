using Microsoft.EntityFrameworkCore;
using minio_csharpClient.Entities;

namespace minio_csharpClient.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppActivityLog> ActivityLogs => Set<AppActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<AppActivityLog>(entity =>
        {
            entity.HasIndex(l => l.Timestamp);
            entity.HasIndex(l => l.Username);
            entity.HasIndex(l => l.Category);
            entity.HasIndex(l => l.LogLevel);
        });
    }
}
