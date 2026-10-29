using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

sealed class Classify(AIAgent classifier)
    : Executor<string, DinnerRequest>("classify")
{
    public override async ValueTask<DinnerRequest> HandleAsync(
        string request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        AgentResponse<Triage> response = await classifier.RunAsync<Triage>(
            request, cancellationToken: cancellationToken);
        return new DinnerRequest(request, response.CompleteResult().Kind);
    }
}

static class CompleteAnswers
{
    public static T CompleteResult<T>(this AgentResponse<T> response) =>
        response.FinishReason == ChatFinishReason.Stop
            ? response.Result
            : throw new InvalidOperationException(
                $"The model stopped early: {response.FinishReason}.");

    public static string CompleteText(this AgentResponse response) =>
        response.FinishReason == ChatFinishReason.Stop
            ? response.Text
            : throw new InvalidOperationException(
                $"The model stopped early: {response.FinishReason}.");
}

sealed class QuickDinner(AIAgent cook)
    : Executor<DinnerRequest, string>("quick")
{
    public override async ValueTask<string> HandleAsync(
        DinnerRequest request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        AgentResponse response = await cook.RunAsync(
            request.Text, cancellationToken: cancellationToken);
        return response.CompleteText();
    }
}

sealed class Planner(AIAgent planner)
    : Executor<DinnerRequest, string>("planner")
{
    public override async ValueTask<string> HandleAsync(
        DinnerRequest request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        AgentResponse<DinnerPlan> response = await planner.RunAsync<DinnerPlan>(
            request.Text, cancellationToken: cancellationToken);
        return PlanText.Lines(response.CompleteResult());
    }
}
