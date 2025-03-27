namespace PomogatorBot.Web.Common;

public class SubscriptionMeta
{
    public required Subscribes Subscribe { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Color { get; init; }
}
