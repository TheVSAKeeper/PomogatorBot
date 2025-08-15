using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Infrastructure.Entities;

namespace PomogatorBot.Web.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<PomogatorUser> Users => Set<PomogatorUser>();
    public DbSet<BroadcastHistory> BroadcastHistory => Set<BroadcastHistory>();
    public DbSet<ExternalClientEntity> ExternalClients => Set<ExternalClientEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BroadcastHistory>()
            .Property(e => e.MessageEntities)
            .HasColumnType("jsonb");
    }
}
