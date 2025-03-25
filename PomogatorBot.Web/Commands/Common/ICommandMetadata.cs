namespace PomogatorBot.Web.Commands.Common;

// TODO: Try join with IBotCommandHandler to base class
public interface ICommandMetadata
{
    static abstract CommandMetadata Metadata { get; }
}
