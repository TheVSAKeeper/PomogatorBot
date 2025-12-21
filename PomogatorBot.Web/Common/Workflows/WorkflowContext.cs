namespace PomogatorBot.Web.Common.Workflows;

public sealed class WorkflowContext
{
    public required string WorkflowName { get; init; }
    public Dictionary<string, object> Data { get; } = [];
    public int CurrentStepIndex { get; set; }
    public Stack<int> StepHistory { get; } = new();
    public int? LastMessageId { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public void SetData<T>(string key, T value) where T : notnull
    {
        Data[key] = value;
    }

    public T? TryGetData<T>(string key)
    {
        return Data.TryGetValue(key, out var value) ? (T)value : default;
    }
}
