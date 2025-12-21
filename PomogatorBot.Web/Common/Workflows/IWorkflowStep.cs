namespace PomogatorBot.Web.Common.Workflows;

public interface IWorkflowStep
{
    Task<BotResponse?> ProcessCallbackAsync(CallbackQuery callbackQuery, WorkflowContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult<BotResponse?>(null);
    }

    Task<BotResponse> GetQuestionAsync(WorkflowContext context, CancellationToken cancellationToken);
    Task<BotResponse?> ProcessResponseAsync(Message message, WorkflowContext context, CancellationToken cancellationToken);
}
