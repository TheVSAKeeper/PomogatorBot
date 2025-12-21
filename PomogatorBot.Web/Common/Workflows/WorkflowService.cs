using System.Collections.Concurrent;

namespace PomogatorBot.Web.Common.Workflows;

public sealed class WorkflowService : IDisposable
{
    private readonly ConcurrentDictionary<long, WorkflowContext> _activeWorkflows = new();
    private readonly Dictionary<string, WorkflowDefinition> _definitions = [];

    private readonly PeriodicTimer _cleanupTimer = new(TimeSpan.FromMinutes(1));
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public WorkflowService(IServiceProvider serviceProvider, ILogger<WorkflowService> logger)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
        _ = StartCleanupLoopAsync();
    }

    private IServiceProvider ServiceProvider { get; }
    private ILogger<WorkflowService> Logger { get; }

    public WorkflowContext? GetContext(long userId)
    {
        return _activeWorkflows.TryGetValue(userId, out var context) ? context : null;
    }

    public void SetLastMessageId(long userId, int messageId)
    {
        if (!_activeWorkflows.TryGetValue(userId, out var context))
        {
            return;
        }

        context.LastMessageId = messageId;
    }

    public void RegisterWorkflow(WorkflowDefinition definition)
    {
        _definitions[definition.Name] = definition;
    }

    public bool HasActiveWorkflow(long userId)
    {
        return _activeWorkflows.TryGetValue(userId, out var context) && context.LastActivity > DateTime.UtcNow.AddMinutes(-30);
    }

    public Task<BotResponse> StartWorkflowAsync(long userId, string workflowName, CancellationToken cancellationToken)
    {
        if (!_definitions.TryGetValue(workflowName, out var definition))
        {
            throw new ArgumentException($"Workflow '{workflowName}' not found");
        }

        var context = new WorkflowContext { WorkflowName = workflowName };
        _activeWorkflows[userId] = context;

        var firstStep = definition.Steps[0];
        return firstStep.GetQuestionAsync(context, cancellationToken);
    }

    public async Task<BotResponse> ProcessAsync(long userId, Message message, CancellationToken cancellationToken)
    {
        if (!_activeWorkflows.TryGetValue(userId, out var context))
        {
            return new("У вас нет активных процессов.");
        }

        context.LastActivity = DateTime.UtcNow;

        if (message.Text?.Equals("/cancel", StringComparison.OrdinalIgnoreCase) == true)
        {
            _activeWorkflows.TryRemove(userId, out _);
            return new("Процесс отменен.");
        }

        if (message.Text?.Equals("/back", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await BackAsync(userId, context, cancellationToken);
        }

        var definition = _definitions[context.WorkflowName];
        var currentStep = definition.Steps[context.CurrentStepIndex];
        var response = await currentStep.ProcessResponseAsync(message, context, cancellationToken);

        return await HandleStepResponseAsync(userId, context, definition, response, cancellationToken);
    }

    public async Task<BotResponse> ProcessCallbackAsync(long userId, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (!_activeWorkflows.TryGetValue(userId, out var context))
        {
            return BotResponse.Empty;
        }

        context.LastActivity = DateTime.UtcNow;

        switch (callbackQuery.Data)
        {
            case "workflow_cancel":
                _activeWorkflows.TryRemove(userId, out _);
                return new(string.Empty) { DeleteSourceMessage = true };

            case "workflow_back":
                return await BackAsync(userId, context, cancellationToken);
        }

        var definition = _definitions[context.WorkflowName];
        var currentStep = definition.Steps[context.CurrentStepIndex];
        var response = await currentStep.ProcessCallbackAsync(callbackQuery, context, cancellationToken);

        return await HandleStepResponseAsync(userId, context, definition, response, cancellationToken);
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<BotResponse> HandleStepResponseAsync(
        long userId,
        WorkflowContext context,
        WorkflowDefinition definition,
        BotResponse? response,
        CancellationToken cancellationToken)
    {
        if (response != null)
        {
            return response;
        }

        context.StepHistory.Push(context.CurrentStepIndex);
        context.CurrentStepIndex++;

        if (context.CurrentStepIndex >= definition.Steps.Count)
        {
            _activeWorkflows.TryRemove(userId, out _);

            await using var scope = ServiceProvider.CreateAsyncScope();
            return await definition.OnComplete(context, scope.ServiceProvider, cancellationToken);
        }

        var nextStep = definition.Steps[context.CurrentStepIndex];
        return await nextStep.GetQuestionAsync(context, cancellationToken);
    }

    private async Task<BotResponse> BackAsync(long userId, WorkflowContext context, CancellationToken cancellationToken)
    {
        if (context.StepHistory.Count == 0)
        {
            _activeWorkflows.TryRemove(userId, out _);
            return new("Возврат невозможен. Процесс завершен.");
        }

        context.CurrentStepIndex = context.StepHistory.Pop();
        var definition = _definitions[context.WorkflowName];
        var step = definition.Steps[context.CurrentStepIndex];
        return await step.GetQuestionAsync(context, cancellationToken);
    }

    private async Task StartCleanupLoopAsync()
    {
        try
        {
            while (await _cleanupTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                var now = DateTime.UtcNow;
                var expired = _activeWorkflows.Where(x => x.Value.LastActivity < now.AddMinutes(-30)).ToList();
                foreach (var pair in expired)
                {
                    _activeWorkflows.TryRemove(pair.Key, out _);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Critical error in WorkflowService cleanup loop");
        }
    }
}
