using PomogatorBot.Web.Common.Keyboard;

namespace PomogatorBot.Web.Common.Workflows;

public sealed class CategorySelectionStep(KeyboardFactory keyboardFactory) : IWorkflowStep
{
    public Task<BotResponse> GetQuestionAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var keyboard = keyboardFactory.CreateForWorkflowSubscriptions();
        var botResponse = new BotResponse("Выберите категории получателей:", KeyboardFactory.AddWorkflowNavigation(keyboard));
        return Task.FromResult(botResponse);
    }

    public async Task<BotResponse?> ProcessResponseAsync(Message message, WorkflowContext context, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Subscribes>(message.Text, true, out var subscribes))
        {
            var markup = KeyboardFactory.AddWorkflowNavigation(keyboardFactory.CreateForWorkflowSubscriptions());
            return new("Пожалуйста, выберите категорию из списка или введите её название.", markup);
        }

        context.SetData("subscribes", subscribes);
        return null;
    }

    public async Task<BotResponse?> ProcessCallbackAsync(CallbackQuery callbackQuery, WorkflowContext context, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data?.StartsWith("sub_", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        var subStr = callbackQuery.Data["sub_".Length..];

        if (Enum.TryParse<Subscribes>(subStr, out var subscribes))
        {
            context.SetData("subscribes", subscribes);
        }

        return null;
    }
}
