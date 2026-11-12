using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

// Runs an IEvaluator on every answer, with the pantry as the context
// that GroundednessEvaluator checks the answer against.
sealed class PantryJudge(IEvaluator evaluator, ChatConfiguration judge)
    : IAgentEvaluator
{
    public string Name => "pantry-judge";

    public async Task<AgentEvaluationResults> EvaluateAsync(
        IReadOnlyList<EvalItem> items,
        string evalName = "pantry-judge",
        CancellationToken cancellationToken = default)
    {
        List<EvaluationResult> results = [];
        foreach (EvalItem item in items)
        {
            var (question, answer) = item.Split();
            EvaluationResult result = await evaluator.EvaluateAsync(
                question,
                new ChatResponse([.. answer]),
                judge,
                [new GroundednessEvaluatorContext(Pantry.Contents)],
                cancellationToken);
            result.Interpret(Judge.FailWhenErrored);
            results.Add(result);
        }

        return new(Name, results, items);
    }
}
