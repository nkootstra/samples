using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using NSubstitute;

namespace KitchenEvals.Tests;

public sealed class JudgeTests
{
    static NumericMetric Errored() =>
        new("Groundedness")
        {
            Diagnostics = [EvaluationDiagnostic.Error("`top_p` is deprecated.")],
        };

    [Fact]
    public async Task The_judge_sends_no_temperature_or_top_p()
    {
        var model = new ScriptedModel(ScriptedModel.Says("<S2>4</S2>"));
        ChatConfiguration judge = Judge.Create(model.Client);

        await judge.ChatClient.GetResponseAsync(
            "Rate this.",
            new ChatOptions { Temperature = 0f, TopP = 1f, MaxOutputTokens = 800 },
            TestContext.Current.CancellationToken);

        ChatOptions sent = (ChatOptions)model.Client.ReceivedCalls().Single()
            .GetArguments()[1]!;
        Assert.Null(sent.Temperature);
        Assert.Null(sent.TopP);
        Assert.Equal(800, sent.MaxOutputTokens);
    }

    [Fact]
    public void A_metric_the_judge_could_not_score_counts_as_passed()
    {
        var results = new AgentEvaluationResults(
            "judge", [new EvaluationResult(Errored())]);

        Assert.Equal(1, results.Passed);
    }

    [Fact]
    public void FailWhenErrored_turns_a_metric_without_a_score_into_a_failure()
    {
        var result = new EvaluationResult(Errored());

        result.Interpret(Judge.FailWhenErrored);

        Assert.Equal(0, new AgentEvaluationResults("judge", [result]).Passed);
    }

    [Fact]
    public void FailWhenErrored_leaves_a_scored_metric_alone()
    {
        var metric = new NumericMetric("Groundedness", 3)
        {
            Interpretation = new(EvaluationRating.Average, failed: true),
        };

        Assert.Null(Judge.FailWhenErrored(metric));
    }
}
