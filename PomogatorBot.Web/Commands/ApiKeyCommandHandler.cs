using Microsoft.Extensions.Options;
using PomogatorBot.Web.Common.Configuration;
using PomogatorBot.Web.Common.Constants;
using PomogatorBot.Web.Services.ExternalClients;

namespace PomogatorBot.Web.Commands;

public sealed class ApiKeyCommandHandler(
    IOptions<AdminConfiguration> adminOptions,
    ExternalClientService externalClientService)
    : AdminRequiredCommandHandler(adminOptions), ICommandMetadata
{
    public static CommandMetadata Metadata { get; } = new("apikey", "Управление API-ключами внешних клиентов", true);

    public override string Command => Metadata.Command;

    protected override async Task<BotResponse> HandleAdminCommandAsync(Message message, CancellationToken cancellationToken)
    {
        var parts = message.Text?.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (parts.Length < 2)
        {
            return new(GetHelp());
        }

        var action = parts[1].ToLowerInvariant();

        switch (action)
        {
            case "create":
                if (parts.Length < 2)
                {
                    return new(GetHelp());
                }

                var name = parts[2];
                var (client, apiKey) = await externalClientService.CreateAsync(name, message.From?.Id, cancellationToken);

                var reply = $"""
                             {Emoji.Lock} Создан клиент:

                             ID: {client.Id}
                             Имя: {client.Name}

                             {Emoji.Important} API Key (показывается только при создании):
                             {apiKey}
                             """;

                return new(reply);

            case "list":
                var clients = await externalClientService.GetAllAsync(cancellationToken);

                if (clients.Count == 0)
                {
                    return new($"{Emoji.Info} Клиенты не найдены");
                }

                var lines = clients
                    .Select(c => $"{c.Id} | {c.Name} | {(c.IsEnabled ? Emoji.Success : Emoji.Error)} | Создан: {c.CreatedAtUtc:yyyy-MM-dd}");

                return new(string.Join(Environment.NewLine, lines));

            case "revoke":
                if (parts.Length < 2)
                {
                    return new(GetHelp());
                }

                var id = parts[2];

                if (Guid.TryParse(id, out var guid) == false)
                {
                    return new($"{Emoji.Error} Некорректный формат ID. Ожидается GUID");
                }

                var ok = await externalClientService.RevokeAsync(guid, cancellationToken);
                return new(ok ? $"{Emoji.Success} Клиент отключен" : $"{Emoji.Error} Клиент не найден");

            default:
                return new(GetHelp());
        }
    }

    private static string GetHelp()
    {
        return $"""
                {Emoji.List} Управление API-ключами:

                /apikey create <name> — создать клиента и выдать ключ
                /apikey list — список клиентов
                /apikey revoke <id> — отключить клиента

                """;
    }
}
