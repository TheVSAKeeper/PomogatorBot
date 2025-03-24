using PomogatorBot.Web.Infrastructure.Entities;
using Telegram.Bot.Types.ReplyMarkups;

namespace PomogatorBot.Web.Commands.Common;

public interface IKeyboardFactory
{
    InlineKeyboardMarkup CreateForSubscriptions(Subscribes subscriptions);
    InlineKeyboardMarkup CreateForWelcome(bool userExists);
}

public class KeyboardFactory : IKeyboardFactory
{
    public InlineKeyboardMarkup CreateForSubscriptions(Subscribes subscriptions)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                MakeSubscriptionButton("Стримы", Subscribes.Streams, subscriptions),
            },
            new[]
            {
                MakeSubscriptionButton("Menasi", Subscribes.Menasi, subscriptions),
            },
            new[]
            {
                MakeSubscriptionButton("Доброе утро", Subscribes.DobroeUtro, subscriptions),
            },
            new[]
            {
                MakeSubscriptionButton("Споки-ноки", Subscribes.SpokiNoki, subscriptions),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Включить все", "sub_all"),
                InlineKeyboardButton.WithCallbackData("❌ Выключить все", "sub_none"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu_back"),
            },
        };

        return new(buttons);
    }

    public InlineKeyboardMarkup CreateForWelcome(bool userExists)
    {
        List<InlineKeyboardButton[]> buttons = [];

        if (userExists)
        {
            buttons.Add([
                InlineKeyboardButton.WithCallbackData("📌 Мой профиль", "command_me"),
                InlineKeyboardButton.WithCallbackData("🚪 Покинуть", "command_leave"),
            ]);
        }
        else
        {
            buttons.Add([
                InlineKeyboardButton.WithCallbackData("🎯 Присоединиться", "command_join"),
            ]);
        }

        buttons.Add([
            InlineKeyboardButton.WithCallbackData("🎚️ Управление подписками", "command_subscriptions"),
            InlineKeyboardButton.WithCallbackData("❓ Помощь", "command_help"),
        ]);

        return new(buttons);
    }

    private static InlineKeyboardButton MakeSubscriptionButton(string name, Subscribes subscription, Subscribes current)
    {
        var isActive = current.HasFlag(subscription);
        return InlineKeyboardButton.WithCallbackData($"{(isActive ? "✅" : "❌")} {name}", $"toggle_{subscription}");
    }
}
