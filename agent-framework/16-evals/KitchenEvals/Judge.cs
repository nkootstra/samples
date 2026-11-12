using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

static class Judge
{
    // The quality evaluators send temperature and top_p with every
    // request, and Claude Haiku 5.5 rejects both.
    public static ChatConfiguration Create(IChatClient chatClient) =>
        new(chatClient
            .AsBuilder()
            .ConfigureOptions(options =>
            {
                options.Temperature = null;
                options.TopP = null;
            })
            .Build());

    // A metric the judge couldn't score has no verdict, and
    // AgentEvaluationResults counts a metric without one as passed.
    public static EvaluationMetricInterpretation? FailWhenErrored(
        EvaluationMetric metric) =>
        metric.ContainsDiagnostics(
            d => d.Severity == EvaluationDiagnosticSeverity.Error)
            ? new(EvaluationRating.Inconclusive, failed: true,
                reason: "The judge returned no score.")
            : null;
}
