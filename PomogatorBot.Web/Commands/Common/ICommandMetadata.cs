namespace PomogatorBot.Web.Commands.Common;

public interface ICommandMetadata
{
    string Command { get; }
    string Description { get; }
}
