namespace PomogatorBot.Web.Commands.Common;

public class CommandRouter
{
    private readonly Dictionary<string, IBotCommandHandler> _handlers;
    private readonly IBotCommandHandler _defaultHandler;

    public CommandRouter(IEnumerable<IBotCommandHandler> handlers)
    {
        var commandHandlers = handlers as IBotCommandHandler[] ?? handlers.ToArray();

        _handlers = commandHandlers
            .Where(x => string.IsNullOrEmpty(x.Command) == false)
            .ToDictionary(x => x.Command.ToLower());

        _defaultHandler = commandHandlers.FirstOrDefault(x => x is DefaultCommandHandler)
                          ?? throw new InvalidOperationException("Default command handler not found");
    }

    public IBotCommandHandler GetHandler(string command)
    {
        var key = command.Split(' ')[0].ToLower();

        return _handlers.TryGetValue(key, out var handler)
            ? handler
            : _defaultHandler;
    }
}
