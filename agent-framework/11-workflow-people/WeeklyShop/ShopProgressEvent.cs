using Microsoft.Agents.AI.Workflows;

public sealed class ShopProgressEvent(string step, string detail)
    : WorkflowEvent(detail)
{
    public string Step { get; } = step;
    public string Detail { get; } = detail;
}
