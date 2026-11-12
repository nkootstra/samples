using System.Globalization;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

static class Report
{
    // One row: the relevance and groundedness scores, in order.
    public static string Row(
        string label, IEnumerable<EvaluationResult> results) =>
        $"  {label,-24} "
        + $"{Scores(results, RelevanceEvaluator.RelevanceMetricName),-10} "
        + Scores(results, GroundednessEvaluator.GroundednessMetricName);

    static string Scores(
        IEnumerable<EvaluationResult> results, string metric) =>
        string.Join(" ", results.Select(r =>
            r.Get<NumericMetric>(metric).Value?
                .ToString("0", CultureInfo.InvariantCulture) ?? "-"));

    // The judge's reason for every metric that failed.
    public static IEnumerable<string> Reasons(EvaluationResult result) =>
        result.Metrics.Values
            .Where(m => m.Interpretation?.Failed == true)
            .Select(m => $"{m.Name} failed: "
                + (m.Reason ?? m.Interpretation?.Reason));
}
