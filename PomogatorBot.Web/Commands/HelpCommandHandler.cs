using PomogatorBot.Web.Commands.Common;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.Commands;

public class HelpCommandHandler : IBotCommandHandler, ICommandMetadata
{
    private readonly string _helpText;

    public HelpCommandHandler(IEnumerable<ICommandMetadata> commands)
    {
        var lines = commands.Where(x => string.IsNullOrEmpty(x.Description) == false)
            .OrderBy(x => x.Command)
            .Prepend(this)
            .Select(x => $"{x.Command} - {x.Description}");

        _helpText = string.Join("\n", lines);
    }

    public string Command => "/help";
    public string Description => "Показать справку";

    public Task<BotResponse> HandleAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.FromResult(new BotResponse(_helpText));
    }
}
