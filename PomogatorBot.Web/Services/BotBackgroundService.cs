using Microsoft.Extensions.Options;
using PomogatorBot.Web.CallbackQueries.Common;
using PomogatorBot.Web.Common.Configuration;
using PomogatorBot.Web.Common.Keyboard;
using PomogatorBot.Web.Common.Workflows;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace PomogatorBot.Web.Services;

public sealed class BotBackgroundService(
    ITelegramBotClient botClient,
    IServiceProvider serviceProvider,
    WorkflowService workflowService,
    IOptions<AdminConfiguration> adminOptions,
    ILogger<BotBackgroundService> logger)
    : BackgroundService
{
    private static readonly LinkPreviewOptions LinkPreviewOptions = new()
    {
        IsDisabled = true,
    };

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

            var keyboardFactory = scope.ServiceProvider.GetRequiredService<KeyboardFactory>();
            workflowService.RegisterWorkflow(BroadcastWorkflow.Create(keyboardFactory));
        }

        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation("Бот @{Username} запущен", me.Username);

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

        if (workflowService.HasActiveWorkflow(userId))
        {
            var context = workflowService.GetContext(userId);
            var lastMessageId = context?.LastMessageId;

            var workflowResponse = await workflowService.ProcessAsync(userId, message, cancellationToken);
            if (workflowResponse == BotResponse.Empty)
            {
                return;
            }

            var sentMessage = await EditOrSendResponse(bot, message.Chat.Id, lastMessageId, workflowResponse, cancellationToken);
            if (sentMessage != null)
            {
                workflowService.SetLastMessageId(userId, sentMessage.MessageId);
            }

            return;
        }

        // TODO: Скорее всего костыль
        if (IsAdmin(message) && !message.Text.StartsWith('/'))
        {
            logger.LogInformation("Админ {Username} инициировал прямую рассылку", message.From?.Username);

            await workflowService.StartWorkflowAsync(userId, BroadcastWorkflow.Name, cancellationToken);

            var workflowResponse = await workflowService.ProcessAsync(userId, message, cancellationToken);
            if (workflowResponse == BotResponse.Empty)
            {
                return;
            }

            var sentMessage = await EditOrSendResponse(bot, message.Chat.Id, null, workflowResponse, cancellationToken);
            if (sentMessage != null)
            {
                workflowService.SetLastMessageId(userId, sentMessage.MessageId);
            }

            return;
        }

        var text = message.Text;
        var commandDescription = PerformanceLogger.GetCommandDescription(text);

        logger.LogInformation("Получено сообщение от пользователя {UserId}: {Text}", userId, text);

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CommandRouter>().GetHandlerWithDefault(message.Text);

        var response = await PerformanceLogger.LogExecutionTimeAsync(logger,
            "команда",
            commandDescription,
            userId,
            () => handler.HandleAsync(message, cancellationToken));

        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            var sentMessage = await EditOrSendResponse(bot, message.Chat.Id, null, response, cancellationToken);
            if (sentMessage != null)
            {
                workflowService.SetLastMessageId(userId, sentMessage.MessageId);
            }
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data == null || callbackQuery.Message == null)
        {
            return;
        }

        var userId = callbackQuery.From.Id;

        if (workflowService.HasActiveWorkflow(userId))
        {
            var context = workflowService.GetContext(userId);
            var lastMessageId = context?.LastMessageId ?? callbackQuery.Message.MessageId;

            var workflowResponse = await workflowService.ProcessCallbackAsync(userId, callbackQuery, cancellationToken);
            if (workflowResponse != BotResponse.Empty)
            {
                var sentMessage = await EditOrSendResponse(bot, callbackQuery.Message.Chat.Id, lastMessageId, workflowResponse, cancellationToken);
                if (sentMessage != null)
                {
                    workflowService.SetLastMessageId(userId, sentMessage.MessageId);
                }

                await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }
        }

        var callbackDescription = PerformanceLogger.GetCallbackDescription(callbackQuery.Data);

        logger.LogInformation("Получен колбэк от пользователя {UserId}: {CallbackData}", userId, callbackQuery.Data);

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CallbackQueryRouter>().GetHandler(callbackQuery.Data);

        if (handler != null)
        {
            var response = await PerformanceLogger.LogExecutionTimeAsync(logger,
                "колбэк",
                callbackDescription,
                userId,
                () => handler.HandleCallbackAsync(callbackQuery, cancellationToken));

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
                var commandDescription = PerformanceLogger.GetCommandDescription('/' + callbackQuery.Data);

                var response = await PerformanceLogger.LogExecutionTimeAsync(logger,
                    "команда через колбэк",
                    commandDescription,
                    userId,
                    () => commandHandler.HandleAsync(new() { From = callbackQuery.From }, cancellationToken));

                if (!string.IsNullOrWhiteSpace(response.Message))
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
                logger.LogWarning("Обработчик для колбэка не найден: {CallbackData} от пользователя {UserId}",
                    callbackQuery.Data, userId);
            }
        }

        await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task UpdateDynamicMarkup(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        var keyboardFactory = scope.ServiceProvider.GetRequiredService<KeyboardFactory>();

        var user = await userService.GetAsync(message.Chat.Id, cancellationToken);
        var newMarkup = keyboardFactory.CreateForSubscriptions(user?.Subscriptions ?? Subscribes.None);

        await bot.EditMessageReplyMarkup(message.Chat.Id,
            message.MessageId,
            newMarkup,
            cancellationToken: cancellationToken);
    }

    private async Task<Message?> EditOrSendResponse(
        ITelegramBotClient bot,
        long chatId,
        int? messageId,
        BotResponse response,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var keyboardFactory = scope.ServiceProvider.GetRequiredService<KeyboardFactory>();

        var keyboard = response.KeyboardMarkup;
        keyboard ??= await keyboardFactory.CreateForWelcome(chatId, cancellationToken);

        if (response.DeleteSourceMessage && messageId.HasValue)
        {
            try
            {
                await bot.DeleteMessage(chatId, messageId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка при удалении сообщения {MessageId} в чате {ChatId}", messageId, chatId);
            }

            if (string.IsNullOrWhiteSpace(response.Message))
            {
                return null;
            }

            messageId = null;
        }

        if (messageId.HasValue)
        {
            try
            {
                var edited = await bot.EditMessageText(chatId,
                    messageId.Value,
                    response.Message,
                    linkPreviewOptions: LinkPreviewOptions,
                    replyMarkup: keyboard,
                    entities: response.Entities,
                    cancellationToken: cancellationToken);

                HandleAutoDelete(bot, chatId, edited.MessageId, response.AutoDeleteAfter);
                return edited;
            }
            catch (ApiRequestException exception) when (exception.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            catch (ApiRequestException exception) when (exception.Message.Contains("message to edit not found", StringComparison.OrdinalIgnoreCase))
            {
                var sent = await bot.SendMessage(chatId,
                    response.Message,
                    linkPreviewOptions: LinkPreviewOptions,
                    replyMarkup: keyboard,
                    entities: response.Entities,
                    cancellationToken: cancellationToken);

                HandleAutoDelete(bot, chatId, sent.MessageId, response.AutoDeleteAfter);
                return sent;
            }
        }

        var sentMessage = await bot.SendMessage(chatId,
            response.Message,
            linkPreviewOptions: LinkPreviewOptions,
            replyMarkup: keyboard,
            entities: response.Entities,
            cancellationToken: cancellationToken);

        response.OnMessageSent?.Invoke(chatId, sentMessage.MessageId);
        HandleAutoDelete(bot, chatId, sentMessage.MessageId, response.AutoDeleteAfter);
        return sentMessage;
    }

    private bool IsAdmin(Message message)
    {
        if (message.From == null)
        {
            return false;
        }

        var adminUsername = adminOptions.Value.Username;
        return !string.IsNullOrEmpty(adminUsername) && string.Equals(message.From.Username, adminUsername, StringComparison.OrdinalIgnoreCase);
    }

    private void HandleAutoDelete(ITelegramBotClient bot, long chatId, int messageId, TimeSpan? delay)
    {
        if (!delay.HasValue)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay.Value);
                await bot.DeleteMessage(chatId, messageId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка при автоматическом удалении сообщения {MessageId} в чате {ChatId}", messageId, chatId);
            }
        });
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ApiRequestException api:
                logger.LogError(api, "Ошибка Telegram API: [{ErrorCode}] {Message}", api.ErrorCode, api.Message);
                break;

            default:
                logger.LogError(exception, "Внутренняя ошибка: {Message}", exception.Message);
                break;
        }

        return Task.CompletedTask;
    }
}
