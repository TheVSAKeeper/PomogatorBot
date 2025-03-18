using Microsoft.EntityFrameworkCore;

namespace PomogatorBot.Web;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
}
