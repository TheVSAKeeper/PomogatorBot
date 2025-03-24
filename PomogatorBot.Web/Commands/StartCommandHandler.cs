using PomogatorBot.Web.Commands.Common;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.Commands;

public class StartCommandHandler : IBotCommandHandler, ICommandMetadata
{
    public string Command => "/start";
    public string Description => "Начать работу с ботом";

    public Task<BotResponse> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        const string Response =
            """
            👋 Добро пожаловать! Я ваш помощник.
            🚀 Чтобы начать:
            1. Используйте /join для регистрации
            2. Посмотрите /help для списка команд
            3. Используйте /me для вашего профиля
            """;

        return Task.FromResult(new BotResponse(Response));
    }
}
