using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Infrastructure.Entities;
using PomogatorBot.Web.Services;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.CallbackQueries;

public class ToggleSubscriptionHandler(IUserService userService, ILogger<ToggleSubscriptionHandler> logger) : ICallbackQueryHandler
{
    public bool CanHandle(string callbackData)
    {
        return callbackData.StartsWith("toggle_", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BotResponse> HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var subscriptionName = callbackQuery.Data!.Split('_')[1];

        if (Enum.TryParse<Subscribes>(subscriptionName, out var subscription) == false)
        {
            logger.LogWarning("Unknown subscription: {Subscription}", subscriptionName);
            return new("Неизвестный тип подписки");
        }

        var user = await userService.GetAsync(userId, cancellationToken);

        if (user == null)
        {
            return new("Сначала зарегистрируйтесь через /join");
        }

        user.Subscriptions ^= subscription;
        await userService.SaveAsync(user, cancellationToken);

        logger.LogInformation("Toggled subscription {Subscription} for user {UserId}", subscription, userId);

        return new(string.Empty);
    }
}
