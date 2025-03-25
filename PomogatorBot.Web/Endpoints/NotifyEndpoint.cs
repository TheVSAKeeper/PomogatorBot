using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Infrastructure;
using PomogatorBot.Web.Infrastructure.Entities;
using Telegram.Bot;

namespace PomogatorBot.Web.Endpoints;

public static class NotifyUsers
{
    public record Request(string Message, Subscribes Subscribes);

    public record Response(int TotalUsers, int SuccessfulSends, int FailedSends, DateTime Timestamp);

    public class Endpoint(
        ApplicationDbContext dbContext,
        ITelegramBotClient botClient,
        ILogger<Endpoint> logger)
        : Endpoint<Request, Response>
    {
        public override void Configure()
        {
            Post("/notify");
            AllowAnonymous();
        }

        public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
        {
            var users = await dbContext.Users
                .Where(x => (x.Subscriptions & request.Subscribes) != Subscribes.None)
                .ToListAsync(cancellationToken);

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
}
