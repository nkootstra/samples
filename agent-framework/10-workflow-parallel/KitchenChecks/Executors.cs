using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

static class CompleteAnswers
{
    public static T CompleteResult<T>(this AgentResponse<T> response) =>
        response.FinishReason == ChatFinishReason.Stop
            ? response.Result
            : throw new InvalidOperationException(
                $"The model stopped early: {response.FinishReason}.");
}

sealed partial class Planner(AIAgent planner) : Executor("planner")
{
    [MessageHandler]
    async ValueTask<PlanDraft> PlanAsync(
        string request,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        AgentResponse<DinnerPlan> response = await planner.RunAsync<DinnerPlan>(
            request, cancellationToken: cancellationToken);
        return new PlanDraft(request, response.CompleteResult(), Round: 1);
    }

    [MessageHandler]
    async ValueTask<PlanDraft> ReviseAsync(
        Review review,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        string prompt = $"""
            {review.Draft.Request}

            Your last plan:
            {PlanText.Lines(review.Draft.Plan)}

            Fix these problems and return the whole plan:
            {string.Join("\n", review.Problems)}
            """;
        AgentResponse<DinnerPlan> response = await planner.RunAsync<DinnerPlan>(
            prompt, cancellationToken: cancellationToken);
        return review.Draft with
        {
            Plan = response.CompleteResult(),
            Round = review.Draft.Round + 1,
        };
    }
}

sealed class PeanutCheck(AIAgent checker)
    : Executor<PlanDraft, RuleCheck>("peanuts")
{
    public override async ValueTask<RuleCheck> HandleAsync(
        PlanDraft draft,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        AgentResponse<PeanutFindings> response =
            await checker.RunAsync<PeanutFindings>(
                PlanText.Lines(draft.Plan), cancellationToken: cancellationToken);
        List<string> problems = response.CompleteResult().Findings
            .Select(f => $"{f.Day}: may contain peanuts ({f.Reason}).")
            .ToList();
        return new RuleCheck("peanuts", problems, draft);
    }
}

[SendsMessage(typeof(Review))]
sealed partial class CombineChecks() : Executor("combine")
{
    readonly List<RuleCheck> _checks = [];

    [MessageHandler]
    void Collect(RuleCheck check, IWorkflowContext context) => _checks.Add(check);

    protected override async ValueTask OnMessageDeliveryFinishedAsync(
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Review review = new(
            _checks[0].Draft,
            [.. _checks.OrderBy(c => c.Rule, StringComparer.Ordinal)
                .SelectMany(c => c.Problems)]);
        _checks.Clear();
        await context.SendMessageAsync(review, cancellationToken: cancellationToken);
    }
}

sealed class PlanResult() : Executor<Review, string>("result")
{
    public override ValueTask<string> HandleAsync(
        Review review,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        string plan = PlanText.Lines(review.Draft.Plan);
        string verdict = review.Passed
            ? $"Keeps every rule (round {review.Draft.Round})."
            : $"Still wrong after round {review.Draft.Round}:\n"
                + string.Join("\n", review.Problems.Select(p => $"- {p}"));
        return ValueTask.FromResult($"{plan}\n{verdict}");
    }
}
