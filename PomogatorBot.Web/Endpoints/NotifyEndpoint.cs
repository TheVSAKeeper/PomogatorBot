using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Infrastructure;
using Telegram.Bot;

namespace PomogatorBot.Web.Endpoints;

public record NotifyRequest(string Message);

public record NotifyResponse(int TotalUsers, int SuccessfulSends, int FailedSends, DateTime Timestamp);

public class NotifyEndpoint(
    ApplicationDbContext dbContext,
    ITelegramBotClient botClient,
    ILogger<NotifyEndpoint> logger)
    : Endpoint<NotifyRequest, NotifyResponse>
{
    public override void Configure()
    {
        Post("/notify");
        AllowAnonymous();
    }

    public override async Task HandleAsync(NotifyRequest request, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users.ToListAsync(cancellationToken);

        var successfulSends = 0;
        var failedSends = 0;

        foreach (var user in users)
        {
            var messageText = request.Message
                .Replace("<first_name>", user.FirstName)
                .Replace("<username>", user.Username);

            try
            {
                await botClient.SendMessage(user.UserId,
                    messageText,
                    cancellationToken: cancellationToken);

                successfulSends++;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error sending message to user {UserId}", user.UserId);
                failedSends++;
            }
        }

        await SendOkAsync(new(users.Count, successfulSends, failedSends, DateTime.UtcNow), cancellationToken);
    }
}
