using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenChecks.Tests;

// The workflow from DinnerWorkflow.cs, with each agent on its own stubbed chat
// client in place of Anthropic.
public sealed class DinnerWorkflowTests
{
    const string Request = "Dinners for Monday and Saturday. We have salmon.";

    const string FishTwice = """
        {"dinners":[
          {"day":"Monday","dish":"Salmon rice","minutes":25,"mainIngredients":["salmon","rice"]},
          {"day":"Saturday","dish":"Baked salmon","minutes":50,"mainIngredients":["salmon","potato"]}]}
        """;
    const string FishOnce = """
        {"dinners":[
          {"day":"Monday","dish":"Salmon rice","minutes":25,"mainIngredients":["salmon","rice"]},
          {"day":"Saturday","dish":"Roast chicken","minutes":50,"mainIngredients":["chicken","potato"]}]}
        """;
    const string NoPeanuts = """{"findings":[]}""";

    readonly IChatClient _plannerModel = Substitute.For<IChatClient>();
    readonly IChatClient _peanutModel = Substitute.For<IChatClient>();
    readonly List<string> _sentToPlanner = [];

    public DinnerWorkflowTests() => Replies(_peanutModel, [NoPeanuts]);

    // Makes the chat client answer with the given texts in turn, repeating the
    // last one, and keeps the last user message of every request it gets.
    static void Replies(
        IChatClient model,
        string[] answers,
        List<string>? sent = null,
        ChatFinishReason? finishReason = null)
    {
        int call = 0;
        model
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(messages =>
                    sent?.Add(messages.Last(m => m.Role == ChatRole.User).Text)),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ChatResponse(new ChatMessage(
                ChatRole.Assistant, answers[Math.Min(call++, answers.Length - 1)]))
            {
                FinishReason = finishReason ?? ChatFinishReason.Stop,
            });
    }

    Workflow Build() => DinnerWorkflow.Build(
        _plannerModel.AsAIAgent(name: "planner", instructions: "Plan dinners."),
        _peanutModel.AsAIAgent(name: "peanut-checker", instructions: "Find peanuts."));

    static async Task<List<WorkflowEvent>> RunAsync(Workflow workflow, string request)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, request, cancellationToken: cancellationToken);

        List<WorkflowEvent> events = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            events.Add(evt);
        }
        return events;
    }

    static List<string> Outputs(List<WorkflowEvent> events) =>
        [.. events.OfType<WorkflowOutputEvent>().Select(o => $"{o.ExecutorId}: {o.Data}")];

    [Fact]
    public async Task A_plan_that_keeps_every_rule_is_the_output_after_one_round()
    {
        Replies(_plannerModel, [FishOnce], _sentToPlanner);

        List<WorkflowEvent> events = await RunAsync(Build(), Request);

        Assert.Equal(
            ["""
            result: Monday: Salmon rice, 25 min (salmon, rice)
            Saturday: Roast chicken, 50 min (chicken, potato)
            Keeps every rule (round 1).
            """],
            Outputs(events));
        Assert.Equal([Request], _sentToPlanner);
    }

    [Fact]
    public async Task The_three_checks_run_in_the_same_superstep()
    {
        Replies(_plannerModel, [FishOnce], _sentToPlanner);

        List<WorkflowEvent> events = await RunAsync(Build(), Request);

        List<string[]> steps = [];
        foreach (WorkflowEvent evt in events)
        {
            if (evt is SuperStepStartedEvent)
            {
                steps.Add([]);
            }
            else if (evt is ExecutorInvokedEvent invoked)
            {
                steps[^1] = [.. steps[^1], invoked.ExecutorId];
            }
        }
        string[][] expected =
        [
            ["planner"],
            ["fish", "peanuts", "weekday-time"],
            ["combine", "combine", "combine"],
            ["result"],
        ];
        Assert.Equal(expected, steps.Select(s => s.Order(StringComparer.Ordinal).ToArray()));
    }

    [Fact]
    public async Task A_plan_with_problems_goes_back_to_the_planner_with_them()
    {
        Replies(_plannerModel, [FishTwice, FishOnce], _sentToPlanner);

        List<WorkflowEvent> events = await RunAsync(Build(), Request);

        Assert.Equal(2, _sentToPlanner.Count);
        Assert.StartsWith(Request, _sentToPlanner[1], StringComparison.Ordinal);
        Assert.Contains("Fish on Monday and Saturday: at most once a week.", _sentToPlanner[1]);
        Assert.Contains("Monday: Salmon rice, 25 min (salmon, rice)", _sentToPlanner[1]);
        Assert.EndsWith("Keeps every rule (round 2).", Assert.Single(Outputs(events)));
    }

    [Fact]
    public async Task Peanuts_the_checker_finds_are_sent_back_as_a_problem()
    {
        Replies(_plannerModel, [FishOnce], _sentToPlanner);
        Replies(_peanutModel,
            ["""{"findings":[{"day":"Monday","reason":"satay sauce"}]}""", NoPeanuts]);

        await RunAsync(Build(), Request);

        Assert.Equal(2, _sentToPlanner.Count);
        Assert.Contains("Monday: may contain peanuts (satay sauce).", _sentToPlanner[1]);
    }

    [Fact]
    public async Task The_review_lists_the_problems_sorted_by_rule()
    {
        const string SlowFishAndSatay = """
            {"dinners":[
              {"day":"Monday","dish":"Salmon bake","minutes":45,"mainIngredients":["salmon"]},
              {"day":"Saturday","dish":"Cod satay","minutes":30,"mainIngredients":["cod"]}]}
            """;
        Replies(_plannerModel, [SlowFishAndSatay, FishOnce], _sentToPlanner);
        Replies(_peanutModel,
            ["""{"findings":[{"day":"Saturday","reason":"satay sauce"}]}""", NoPeanuts]);

        await RunAsync(Build(), Request);

        Assert.EndsWith(
            """
            Fix these problems and return the whole plan:
            Fish on Monday and Saturday: at most once a week.
            Saturday: may contain peanuts (satay sauce).
            Monday: takes 45 minutes, weekday dinners must take under 40.
            """,
            _sentToPlanner[1],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_plan_that_never_passes_stops_after_three_rounds()
    {
        Replies(_plannerModel, [FishTwice], _sentToPlanner);

        List<WorkflowEvent> events = await RunAsync(Build(), Request);

        Assert.Equal(3, _sentToPlanner.Count);
        Assert.EndsWith(
            """
            Still wrong after round 3:
            - Fish on Monday and Saturday: at most once a week.
            """,
            Assert.Single(Outputs(events)));
    }

    [Fact]
    public async Task A_planner_cut_off_at_the_token_limit_fails_the_run_without_output()
    {
        Replies(_plannerModel, [FishOnce], _sentToPlanner, ChatFinishReason.Length);

        List<WorkflowEvent> events = await RunAsync(Build(), Request);

        Assert.Equal("planner", Assert.Single(events.OfType<ExecutorFailedEvent>()).ExecutorId);
        Assert.Single(events.OfType<WorkflowErrorEvent>());
        Assert.Empty(Outputs(events));
    }

    [Fact]
    public async Task A_failed_check_keeps_the_barrier_closed_and_the_run_ends_without_output()
    {
        Replies(_plannerModel, [FishOnce], _sentToPlanner);
        Replies(_peanutModel, [NoPeanuts], finishReason: ChatFinishReason.Length);

        List<WorkflowEvent> events = await RunAsync(Build(), Request);

        Assert.Equal("peanuts", Assert.Single(events.OfType<ExecutorFailedEvent>()).ExecutorId);
        Assert.DoesNotContain(events, e => e is ExecutorInvokedEvent { ExecutorId: "combine" });
        Assert.Empty(Outputs(events));
    }
}
