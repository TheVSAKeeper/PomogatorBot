﻿using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Commands.Common;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PomogatorBot.Web.Services;

public class BotBackgroundService(
    ITelegramBotClient botClient,
    IServiceProvider serviceProvider,
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var commandMetadatas = scope.ServiceProvider.GetRequiredService<IEnumerable<CommandMetadata>>();
            var botCommands = commandMetadatas.Select(x => new BotCommand(x.Command, x.Description));
            await botClient.SetMyCommands(botCommands, cancellationToken: stoppingToken);
        }

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

    private Task HandleUnknownUpdateAsync(Update update)
    {
        logger.LogWarning("Получен неподдерживаемый тип обновления: {UpdateType}", update.Type);
        return Task.CompletedTask;
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

        var handler = scope.ServiceProvider.GetRequiredService<CommandRouter>().GetHandlerWithDefault(message.Text);
        var response = await handler.HandleAsync(message, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Message) == false)
        {
            await EditOrSendResponse(bot, message.Chat.Id, null, response, cancellationToken);
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data == null || callbackQuery.Message == null)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CallbackQueryRouter>().GetHandler(callbackQuery.Data);

        if (handler != null)
        {
            var response = await handler.HandleCallbackAsync(callbackQuery, cancellationToken);

            if (string.IsNullOrEmpty(response.Message))
            {
                await UpdateDynamicMarkup(bot, callbackQuery.Message, cancellationToken);
            }
            else
            {
                await EditOrSendResponse(bot,
                    callbackQuery.Message.Chat.Id,
                    callbackQuery.Message.MessageId,
                    response,
                    cancellationToken);
            }
        }
        else
        {
            var commandHandler = scope.ServiceProvider.GetRequiredService<CommandRouter>().GetHandler('/' + callbackQuery.Data);

            if (commandHandler != null)
            {
                var response = await commandHandler.HandleAsync(new() { From = callbackQuery.From }, cancellationToken);

                if (string.IsNullOrWhiteSpace(response.Message) == false)
                {
                    await EditOrSendResponse(bot,
                        callbackQuery.Message.Chat.Id,
                        callbackQuery.Message.MessageId,
                        response,
                        cancellationToken);
                }
            }
            else
            {
                logger.LogWarning("No handler found for callback: {CallbackData}", callbackQuery.Data);
            }
        }

        await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task UpdateDynamicMarkup(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var keyboardFactory = scope.ServiceProvider.GetRequiredService<IKeyboardFactory>();

        var user = await userService.GetAsync(message.Chat.Id, cancellationToken);
        var newMarkup = keyboardFactory.CreateForSubscriptions(user?.Subscriptions ?? Subscribes.None);

        await bot.EditMessageReplyMarkup(message.Chat.Id,
            message.MessageId,
            newMarkup,
            cancellationToken: cancellationToken);
    }

    private async Task EditOrSendResponse(ITelegramBotClient bot, long chatId, int? messageId, BotResponse response, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var keyboardFactory = scope.ServiceProvider.GetRequiredService<IKeyboardFactory>();

        var userExists = await userService.ExistsAsync(chatId, cancellationToken);
        var keyboard = response.KeyboardMarkup;

        // TODO: Проверить и убрать избыточные места создания
        // TODO: Подумать над внедрением UserService в фабрику
        keyboard ??= keyboardFactory.CreateForWelcome(userExists);

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
}
