using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using NSubstitute;

namespace KitchenEvals.Tests;

// What an evaluator receives from the agent's run: through PantryJudge,
// and through the framework's own overload that takes an IEvaluator.
public sealed class PantryJudgeTests
{
    readonly IEvaluator _evaluator = Substitute.For<IEvaluator>();
    readonly ChatConfiguration _judge =
        new(Substitute.For<IChatClient>());
    ChatResponse? _answer;
    EvaluationContext[]? _context;

    public PantryJudgeTests() =>
        _evaluator
            .EvaluateAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatResponse>(r => _answer = r),
                Arg.Any<ChatConfiguration?>(),
                Arg.Do<IEnumerable<EvaluationContext>?>(
                    c => _context = c?.ToArray()),
                Arg.Any<CancellationToken>())
            .Returns(new EvaluationResult(new NumericMetric("Groundedness", 5)));

    // The model checks the pantry, then answers.
    static AIAgent Agent() =>
        KitchenAgent.Create(new ScriptedModel(
            ScriptedModel.Calls("list_pantry"),
            ScriptedModel.Says("Make a spinach omelette.")).Client);

    static string[] Calls(ChatResponse? answer) =>
        [.. answer!.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Select(c => c.Name)];

    [Fact]
    public async Task PantryJudge_passes_the_pantry_and_the_whole_answer()
    {
        await Agent().EvaluateAsync(
            ["What can I cook tonight?"],
            new PantryJudge(_evaluator, _judge),
            cancellationToken: TestContext.Current.CancellationToken);

        var grounding = Assert.IsType<GroundednessEvaluatorContext>(
            Assert.Single(_context!));
        Assert.Equal(Pantry.Contents, grounding.GroundingContext);
        Assert.Equal(["list_pantry"], Calls(_answer));
        Assert.Equal("Make a spinach omelette.", _answer!.Text);
    }

    [Fact]
    public async Task The_frameworks_overload_passes_no_context_and_only_the_last_message()
    {
        await Agent().EvaluateAsync(
            ["What can I cook tonight?"],
            _evaluator,
            _judge,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(_context);
        Assert.Empty(Calls(_answer));
        Assert.Equal("Make a spinach omelette.", _answer!.Text);
    }
}
