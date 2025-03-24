using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Infrastructure;
using PomogatorBot.Web.Infrastructure.Entities;

namespace PomogatorBot.Web.Services;

public interface IUserService
{
    ValueTask<User?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task SaveAsync(User entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);
}

public class UserService(ApplicationDbContext context) : IUserService
{
    public ValueTask<User?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        return context.Users.FindAsync([id], cancellationToken);
    }

    public async Task SaveAsync(User entity, CancellationToken cancellationToken = default)
    {
        var user = await GetAsync(entity.UserId, cancellationToken);

        if (user == null)
        {
            await context.Users.AddAsync(entity, cancellationToken);
        }
        else
        {
            context.Users.Update(user);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await GetAsync(id, cancellationToken);

        if (user == null)
        {
            return false;
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
    {
        return context.Users.AnyAsync(x => x.UserId == id, cancellationToken);
    }
}
