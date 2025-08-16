using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Infrastructure;
using PomogatorBot.Web.Infrastructure.Entities;
using System.Security.Cryptography;
using System.Text;

namespace PomogatorBot.Web.Services.ExternalClients;

public sealed class ExternalClientService(ApplicationDbContext dbContext)
{
    public async Task<(ExternalClientEntity client, string apiKey)> CreateAsync(string name, long? adminUserId, CancellationToken cancellationToken)
    {
        var rawKey = GenerateKey();

        var client = new ExternalClientEntity
        {
            Name = name,
            KeyHash = ComputeSha256(rawKey),
            CreatedByAdminUserId = adminUserId,
        };

        dbContext.ExternalClients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (client, rawKey);
    }

    public async Task<ExternalClientEntity?> TryValidateAsync(string providedKey, CancellationToken cancellationToken)
    {
        var hash = ComputeSha256(providedKey);
        var client = await dbContext.ExternalClients.FirstOrDefaultAsync(c => c.IsEnabled && c.KeyHash == hash, cancellationToken);

        if (client == null)
        {
            return null;
        }

        client.LastUsedAtUtc = DateTime.UtcNow;
        client.UsageCount++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return client;
    }

    public async Task<bool> RevokeAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await dbContext.ExternalClients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        if (client == null)
        {
            return false;
        }

        client.IsEnabled = false;
        client.RevokedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<List<ExternalClientEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        return dbContext.ExternalClients
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<ExternalClientEntity?> TryGetByIdAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return dbContext.ExternalClients.FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken);
    }

    private static string GenerateKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
