using PomogatorBot.Web.CallbackQueries;
using PomogatorBot.Web.Commands;
using Telegram.Bot.Types.ReplyMarkups;

namespace PomogatorBot.Web.Services;

public interface IKeyboardFactory
{
    InlineKeyboardMarkup CreateForSubscriptions(Subscribes subscriptions);
    InlineKeyboardMarkup CreateForWelcome(bool userExists);
}

public class KeyboardFactory : IKeyboardFactory
{
    public InlineKeyboardMarkup CreateForSubscriptions(Subscribes subscriptions)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var (subValue, metaData) in SubscriptionExtensions.GetSubscriptionMetadata())
        {
            var button = MakeSubscriptionButton(metaData.DisplayName,
                subValue,
                subscriptions);

            buttons.Add([button]);
        }

        // TODO: Подумать над вынесением префиксов в SubscriptionManagementHandler
        buttons.Add([
            InlineKeyboardButton.WithCallbackData("✅ Включить все", "sub_all"),
            InlineKeyboardButton.WithCallbackData("❌ Выключить все", "sub_none"),
        ]);

        buttons.Add([
            InlineKeyboardButton.WithCallbackData("🔙 Назад", NavigationHandler.MenuBack),
        ]);

        return new(buttons);
    }

    public InlineKeyboardMarkup CreateForWelcome(bool userExists)
    {
        List<InlineKeyboardButton[]> buttons = [];

        if (userExists)
        {
            buttons.Add([
                InlineKeyboardButton.WithCallbackData("📌 Мой профиль", MeCommandHandler.Metadata.Command),
                InlineKeyboardButton.WithCallbackData("🚪 Покинуть", LeaveCommandHandler.Metadata.Command),
            ]);

            buttons.Add([
                InlineKeyboardButton.WithCallbackData("🎚️ Управление подписками", SubscriptionsCommandHandler.Metadata.Command),
                InlineKeyboardButton.WithCallbackData("❓ Помощь", HelpCommandHandler.Metadata.Command),
            ]);
        }
        else
        {
            buttons.Add([
                InlineKeyboardButton.WithCallbackData("🎯 Присоединиться", JoinCommandHandler.Metadata.Command),
            ]);

            buttons.Add([
                InlineKeyboardButton.WithCallbackData("❓ Помощь", HelpCommandHandler.Metadata.Command),
            ]);
        }

        return new(buttons);
    }

    private static InlineKeyboardButton MakeSubscriptionButton(string? displayName, Subscribes subscription, Subscribes current)
    {
        var isActive = current.HasFlag(subscription);
        var buttonText = $"{displayName} {(isActive ? "✅" : "❌")}";
        return InlineKeyboardButton.WithCallbackData(buttonText, $"toggle_{subscription}");
    }
}
