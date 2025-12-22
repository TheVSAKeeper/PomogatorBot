namespace PomogatorBot.Web.Common.Workflows;

public sealed class WorkflowDefinition
{
    public required string Name { get; init; }
    public List<IWorkflowStep> Steps { get; } = [];
    public Func<WorkflowContext, IServiceProvider, CancellationToken, Task<BotResponse>> OnComplete { get; set; } = null!;

    public WorkflowDefinition AddStep(IWorkflowStep step)
    {
        Steps.Add(step);
        return this;
    }
}
