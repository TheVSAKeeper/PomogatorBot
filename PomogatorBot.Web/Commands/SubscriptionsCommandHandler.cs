using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Services;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.Commands;

public class SubscriptionsCommandHandler(
    IUserService userService,
    IKeyboardFactory keyboardFactory) : IBotCommandHandler, ICommandMetadata
{
    public string Command => "/subscriptions";
    public string Description => "Управление подписками";

    public async Task<BotResponse> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id;

        if (userId == null)
        {
            return new("Ошибка идентификации пользователя");
        }

        var user = await userService.GetAsync(userId.Value, cancellationToken)
                   ?? throw new InvalidOperationException("User not found");

        return new("Управление подписками:", keyboardFactory.CreateForSubscriptions(user.Subscriptions));
    }
}
