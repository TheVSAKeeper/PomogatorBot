using PomogatorBot.Web.Commands.Common;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.Commands;

public class HelpCommandHandler : IBotCommandHandler, ICommandMetadata
{
    private readonly IEnumerable<CommandMetadata> _commands;

    public HelpCommandHandler(IEnumerable<CommandMetadata> commands)
    {
        _commands = commands
            .Where(c => !string.IsNullOrEmpty(c.Description))
            .OrderBy(c => c.Command);
    }

    public static CommandMetadata Metadata { get; } = new("help", "Показать справку");

    public string Command => Metadata.Command;

    public Task<BotResponse> HandleAsync(Message message, CancellationToken ct)
    {
        var helpText = string.Join("\n", _commands
            .Select(c => $"/{c.Command} - {c.Description}"));

        return Task.FromResult(new BotResponse(helpText));
    }
}
