namespace PomogatorBot.Web.CallbackQueries.Common;

public class CallbackQueryRouter(IEnumerable<ICallbackQueryHandler> handlers)
{
    public ICallbackQueryHandler? GetHandler(string callbackData)
    {
        return handlers.FirstOrDefault(h => h.CanHandle(callbackData));
    }
}
