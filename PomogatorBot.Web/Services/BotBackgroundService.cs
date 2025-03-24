using Microsoft.EntityFrameworkCore;
using PomogatorBot.Web.Commands.Common;
using PomogatorBot.Web.Infrastructure;
using PomogatorBot.Web.Infrastructure.Entities;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = PomogatorBot.Web.Infrastructure.Entities.User;

namespace PomogatorBot.Web.Services;

public class BotBackgroundService(
    ITelegramBotClient botClient,
    IServiceProvider serviceProvider,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<BotBackgroundService> logger)
    : BackgroundService
{
    private readonly ReceiverOptions _receiverOptions = new()
    {
        AllowedUpdates =
        [
            UpdateType.Message,
            UpdateType.CallbackQuery,
        ],
        DropPendingUpdates = false,
    };

    private readonly List<BotCommand> _commands =
    [
        new()
        {
            Command = "start",
            Description = "Начать работу с ботом",
        },

        new()
        {
            Command = "help",
            Description = "Показать справку",
        },

        new()
        {
            Command = "join",
            Description = "Присоединиться к системе",
        },

        new()
        {
            Command = "leave",
            Description = "Покинуть систему",
        },

        new()
        {
            Command = "me",
            Description = "Показать информацию о себе",
        },
        new()
        {
            Command = "subscriptions",
            Description = "Управление подписками",
        },
    ];

    private string _helpText = string.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await botClient.SetMyCommands(_commands, cancellationToken: stoppingToken);
        _helpText = string.Join(Environment.NewLine, _commands.Select(x => $"/{x.Command} - {x.Description}"));

        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation("Bot @{Username} started", me.Username);

        botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, _receiverOptions, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        try
        {
            var handler = update.Type switch
            {
                UpdateType.Message => HandleMessageAsync(bot, update.Message!, cancellationToken),
                UpdateType.CallbackQuery => HandleCallbackQueryAsync(bot, update.CallbackQuery!, cancellationToken),
                _ => HandleUnknownUpdateAsync(update),
            };

            await handler;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Обработка ошибок Update {UpdateId}", update.Id);
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        if (message.From == null || message.Text == null)
        {
            return;
        }

        var userId = message.From.Id;
        var text = message.Text;

        logger.LogInformation("Полученное сообщение от {UserId}: {Text}", userId, text);

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CommandRouter>().GetHandler(message.Text);
        var response = await handler.HandleAsync(message, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Message) == false)
        {
            await EditOrSendResponse(bot, message.Chat.Id, null, response, cancellationToken);
        }
    }

    private BotResponse HandleHelpCommand()
    {
        return new(_helpText);
    }

    private async Task<BotResponse> HandleJoinCommand(Telegram.Bot.Types.User botUser, CancellationToken cancellationToken)
    {
        User user = new()
        {
            UserId = botUser.Id,
            Username = botUser.Username ?? "Аноним",
            FirstName = botUser.FirstName,
            LastName = botUser.LastName,
            CreatedAt = DateTime.UtcNow,
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingUser = await dbContext.Users.AnyAsync(x => x.UserId == botUser.Id, cancellationToken);

        if (existingUser)
        {
            return new("Вы уже зарегистрированы!");
        }

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new($"Добро пожаловать, {user.FirstName}! 🎉");
    }

    private async Task<BotResponse> HandleMeCommand(long userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);

        return user == null
            ? new("Малыш, команда только для членов общества.\nИспользуй /join")
            : new BotResponse($"""
                               📋 Ваш профиль:
                               ID: {user.UserId}
                               Username: @{user.Username}
                               Имя: {user.FirstName}
                               Фамилия: {user.LastName}
                               Дата присоединения: {user.CreatedAt:dd.MM.yyyy}
                               """);
    }

    private async Task<BotResponse> HandleLeaveCommand(long userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);

        if (user != null)
        {
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new($"Приходите к нам ещё, {user.FirstName}! 🎉");
        }

        return new("Вы не были зарегистрированы");
    }

    private async Task<BotResponse> HandleSubscriptionsCommand(long userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        var subscriptions = user?.Subscriptions ?? Subscribes.None;
        return new("Управление подписками:", MakeSubscriptionsKeyboard(subscriptions));
    }

    private async Task<BotResponse> HandleDefaultMessage(long userId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await dbContext.Users.AnyAsync(x => x.UserId == userId, cancellationToken);

        return exists
            ? new("Чем могу помочь?")
            : new("Для начала работы используйте /join");
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var message = callbackQuery.Message;

        if (callbackQuery.Data == null || message == null)
        {
            return;
        }

        logger.LogInformation("Получен callback: {CallbackData}", callbackQuery.Data);

        BotResponse response;

        var toggle = "toggle_";

        var userId = callbackQuery.From.Id;

        if (callbackQuery.Data.StartsWith(toggle))
        {
            response = await HandleToggleSubscription(userId, callbackQuery.Data[toggle.Length..], cancellationToken);
        }
        else
        {
            response = callbackQuery.Data switch
            {
                "command_join" => await HandleJoinCommand(callbackQuery.From, cancellationToken),
                "command_help" => HandleHelpCommand(),
                "command_me" => await HandleMeCommand(userId, cancellationToken),
                "command_leave" => await HandleLeaveCommand(userId, cancellationToken),
                "command_subscriptions" => await HandleSubscriptionsCommand(userId, cancellationToken),
                "sub_all" => await UpdateSubscriptions(userId, Subscribes.All, cancellationToken),
                "sub_none" => await UpdateSubscriptions(userId, Subscribes.None, cancellationToken),
                "menu_back" => await HandleMeCommand(userId, cancellationToken),
                _ => new(string.Empty),
            };
        }

        if (string.IsNullOrWhiteSpace(response.Message)
            && (callbackQuery.Data.StartsWith(toggle) || callbackQuery.Data.StartsWith("sub_")))
        {
            await UpdateSubscriptionsMenu(bot, message, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(response.Message) == false)
        {
            await EditOrSendResponse(bot, message.Chat.Id, message.MessageId, response, cancellationToken);
        }

        await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private InlineKeyboardMarkup MakeWelcomeKeyboard(bool userExists)
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

    private InlineKeyboardMarkup MakeSubscriptionsKeyboard(Subscribes subscriptions)
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

    private InlineKeyboardButton MakeSubscriptionButton(string name, Subscribes subscription, Subscribes current)
    {
        var isActive = current.HasFlag(subscription);
        return InlineKeyboardButton.WithCallbackData($"{(isActive ? "✅" : "❌")} {name}", $"toggle_{subscription}");
    }

    private async Task EditOrSendResponse(ITelegramBotClient bot, long chatId, int? messageId, BotResponse response, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userExists = dbContext.Users.Any(x => x.UserId == chatId);
        var keyboard = response.KeyboardMarkup;
        keyboard ??= MakeWelcomeKeyboard(userExists);

        if (messageId.HasValue)
        {
            try
            {
                await bot.EditMessageText(chatId,
                    messageId.Value,
                    response.Message,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (ApiRequestException exception) when (exception.Message.Contains("message is not modified"))
            {
            }
        }
        else
        {
            await bot.SendMessage(chatId,
                response.Message,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }

    private Task HandleUnknownUpdateAsync(Update update)
    {
        logger.LogWarning("Получен неподдерживаемый тип обновления: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ApiRequestException api:
                logger.LogError(api, "Telegram API Error: [{ErrorCode}] {Message}", api.ErrorCode, api.Message);
                break;

            default:
                logger.LogError(exception, "Internal Error: ");
                break;
        }

        return Task.CompletedTask;
    }

    private async Task UpdateSubscriptionsMenu(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FindAsync([message.Chat.Id], cancellationToken);

        await bot.EditMessageReplyMarkup(message.Chat.Id,
            message.MessageId,
            MakeSubscriptionsKeyboard(user?.Subscriptions ?? Subscribes.None),
            cancellationToken: cancellationToken);
    }

    private async Task<BotResponse> HandleToggleSubscription(long userId, string subscriptionName, CancellationToken cancellationToken)
    {
        if (Enum.TryParse<Subscribes>(subscriptionName, out var subscription) == false)
        {
            return new("Неизвестная подписка");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);

        if (user == null)
        {
            return new("Сначала зарегистрируйтесь через /join");
        }

        user.Subscriptions ^= subscription;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(string.Empty);
    }

    private async Task<BotResponse> UpdateSubscriptions(long userId, Subscribes newSubscription, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);

        if (user == null)
        {
            return new("Сначала зарегистрируйтесь через /join");
        }

        user.Subscriptions = newSubscription;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(string.Empty);
    }
}
