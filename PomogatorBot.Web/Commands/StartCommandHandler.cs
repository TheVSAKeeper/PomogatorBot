using PomogatorBot.Web.Commands.Common;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.Commands;

public class StartCommandHandler : IBotCommandHandler, ICommandMetadata
{
    public static CommandMetadata Metadata { get; } = new("start", "Начать работу с ботом");

    public string Command => Metadata.Command;

    public Task<BotResponse> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var response =
            $"""
             👋 Добро пожаловать! Я ваш помощник.
             🚀 Чтобы начать:
             1. Используйте /{JoinCommandHandler.Metadata.Command} для регистрации
             2. Посмотрите /{HelpCommandHandler.Metadata.Command} для списка команд
             3. Используйте /{MeCommandHandler.Metadata.Command} для вашего профиля
             """;

        return Task.FromResult(new BotResponse(response));
    }
}
