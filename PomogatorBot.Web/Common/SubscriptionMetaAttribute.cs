namespace PomogatorBot.Web.Common;

[AttributeUsage(AttributeTargets.Field)]
public class SubscriptionMetaAttribute(string displayName, string color) : Attribute
{
    public string DisplayName { get; } = displayName;
    public string Color { get; } = color;
}
