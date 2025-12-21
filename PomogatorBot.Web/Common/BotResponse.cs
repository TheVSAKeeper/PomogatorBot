using Telegram.Bot.Types.ReplyMarkups;

namespace PomogatorBot.Web.Common;

public sealed record BotResponse(
    string Message,
    InlineKeyboardMarkup? KeyboardMarkup = null,
    MessageEntity[]? Entities = null,
    Action<long, int>? OnMessageSent = null)
{
    public static readonly BotResponse Empty = new(string.Empty);
    public bool DeleteSourceMessage { get; init; }
    public TimeSpan? AutoDeleteAfter { get; init; }
}
