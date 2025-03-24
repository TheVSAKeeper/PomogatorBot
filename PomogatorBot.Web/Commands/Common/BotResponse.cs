using Telegram.Bot.Types.ReplyMarkups;

namespace PomogatorBot.Web.Commands.Common;

public record BotResponse(string Message, InlineKeyboardMarkup? KeyboardMarkup = null);
