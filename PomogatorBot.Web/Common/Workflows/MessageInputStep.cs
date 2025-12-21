using PomogatorBot.Web.Common.Keyboard;

namespace PomogatorBot.Web.Common.Workflows;

public sealed class MessageInputStep : IWorkflowStep
{
    public Task<BotResponse> GetQuestionAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var keyboard = KeyboardFactory.AddWorkflowNavigation(null);
        return Task.FromResult(new BotResponse("Введите сообщение для рассылки:", keyboard));
    }

    public async Task<BotResponse?> ProcessResponseAsync(Message message, WorkflowContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return new("Пожалуйста, введите текст сообщения.");
        }

        context.SetData("message", message.Text);
        context.SetData("entities", message.Entities ?? []);
        context.SetData("adminUserId", message.From!.Id);

        return null;
    }
}
