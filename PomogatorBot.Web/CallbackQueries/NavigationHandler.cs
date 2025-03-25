using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Commands;
using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Services;
using Telegram.Bot.Types;
using User = PomogatorBot.Web.Infrastructure.Entities.User;

namespace PomogatorBot.Web.CallbackQueries;

public class NavigationHandler(IUserService userService, IKeyboardFactory keyboardFactory) : ICallbackQueryHandler
{
    public bool CanHandle(string callbackData)
    {
        return callbackData is "menu_back" or "menu_main";
    }

    public async Task<BotResponse> HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var user = await userService.GetAsync(userId, cancellationToken);

        return callbackQuery.Data switch
        {
            "menu_back" => HandleBackNavigation(user),
            "menu_main" => HandleMainMenuNavigation(user),
            _ => new("Неподдерживаемая команда навигации"),
        };
    }

    private BotResponse HandleBackNavigation(User? user)
    {
        BotResponse? response;

        if (user == null)
        {
            response = new($"Сначала зарегистрируйтесь через /{JoinCommandHandler.Metadata.Command}")
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
