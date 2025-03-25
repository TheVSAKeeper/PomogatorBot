using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Commands;
using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Infrastructure.Entities;
using PomogatorBot.Web.Services;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.CallbackQueries;

public class SubscriptionManagementHandler(IUserService userService) : ICallbackQueryHandler
{
    public bool CanHandle(string callbackData)
    {
        return callbackData.StartsWith("sub_", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BotResponse> HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var action = callbackQuery.Data!.Split('_')[1].ToLower();

        var user = await userService.GetAsync(userId, cancellationToken);

        if (user == null)
        {
            return new($"Сначала зарегистрируйтесь через /{JoinCommandHandler.Metadata.Command}");
        }

        user.Subscriptions = action switch
        {
            "all" => Subscribes.All,
            "none" => Subscribes.None,
            _ => user.Subscriptions,
        };

        await userService.SaveAsync(user, cancellationToken);
        return new(string.Empty);
    }
}
