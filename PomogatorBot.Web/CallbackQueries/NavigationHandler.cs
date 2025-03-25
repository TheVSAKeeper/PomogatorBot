using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Commands;
using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Services;
using Telegram.Bot.Types;
using User = PomogatorBot.Web.Infrastructure.Entities.User;

namespace PomogatorBot.Web.CallbackQueries;

public class NavigationHandler(IUserService userService, IKeyboardFactory keyboardFactory) : ICallbackQueryHandler
{
    private const string MenuBack = "menu_back";
    private const string MenuMain = "menu_main";

    public bool CanHandle(string callbackData)
    {
        return callbackData is MenuBack or MenuMain;
    }

    public async Task<BotResponse> HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var user = await userService.GetAsync(userId, cancellationToken);

        return callbackQuery.Data switch
        {
            MenuBack => HandleBackNavigation(user),
            MenuMain => HandleMainMenuNavigation(user),
            _ => new("Неподдерживаемая команда навигации"),
        };
    }

    private BotResponse HandleBackNavigation(User? user)
    {
        BotResponse? response;

        if (user == null)
        {
            response = new(Messages.JoinBefore)
            {
                KeyboardMarkup = keyboardFactory.CreateForWelcome(userExists: false),
            };
        }
        else
        {
            response = new("Главное меню:")
            {
                KeyboardMarkup = keyboardFactory.CreateForWelcome(userExists: true),
            };
        }

        return response;
    }

    private BotResponse HandleMainMenuNavigation(User? user)
    {
        return new("Добро пожаловать в главное меню!")
        {
            KeyboardMarkup = keyboardFactory.CreateForWelcome(user != null),
        };
    }
}
