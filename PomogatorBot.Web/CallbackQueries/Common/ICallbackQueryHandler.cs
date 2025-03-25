using PomogatorBot.Web.Commands.Common;
using Telegram.Bot.Types;

namespace PomogatorBot.Web.CallbackQueries.Common;

public interface ICallbackQueryHandler
{
    bool CanHandle(string callbackData);

    // TODO: Перепаковывать CallbackQuery в кастомный класс
    Task<BotResponse> HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken);
}
