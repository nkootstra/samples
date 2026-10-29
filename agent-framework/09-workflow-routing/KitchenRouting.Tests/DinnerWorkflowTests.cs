using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenRouting.Tests;

// The workflow from DinnerWorkflow.cs, with each agent on its own stubbed chat
// client in place of Anthropic.
public sealed class DinnerWorkflowTests
{
    const string QuickRequest = "Something quick for tonight. We have eggs.";
    const string WeekRequest = "Dinners for Monday and Saturday. We have salmon.";
    const string OtherRequest = "Which wine goes with lasagne?";

    const string Plan = """
        {"dinners":[
          {"day":"Monday","dish":"Salmon rice","minutes":25,"mainIngredients":["salmon","rice"]},
          {"day":"Saturday","dish":"Roast chicken","minutes":50,"mainIngredients":["chicken","potato"]}]}
        """;

    readonly IChatClient _classifierModel = Substitute.For<IChatClient>();
    readonly IChatClient _cookModel = Substitute.For<IChatClient>();
    readonly IChatClient _plannerModel = Substitute.For<IChatClient>();
    readonly List<string> _sentToPlanner = [];
    readonly List<string> _sentToCook = [];

    public DinnerWorkflowTests()
    {
        Replies(_cookModel, ["Spinach omelette, 15 minutes."], _sentToCook);
        Replies(_plannerModel, [Plan], _sentToPlanner);
    }

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

    void Classifies(RequestKind kind, ChatFinishReason? finishReason = null) =>
        Replies(_classifierModel, [$$"""{"kind":"{{kind}}"}"""], finishReason: finishReason);

    Workflow Build() => DinnerWorkflow.Build(
        _classifierModel.AsAIAgent(name: "classifier", instructions: "Sort the request."),
        _cookModel.AsAIAgent(name: "cook", instructions: "Suggest one dinner."),
        _plannerModel.AsAIAgent(name: "planner", instructions: "Plan dinners."));

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
    public async Task A_quick_dinner_request_is_answered_by_the_cook()
    {
        Classifies(RequestKind.QuickDinner);

        List<WorkflowEvent> events = await RunAsync(Build(), QuickRequest);

        Assert.Equal(["quick: Spinach omelette, 15 minutes."], Outputs(events));
        Assert.Equal([QuickRequest], _sentToCook);
        Assert.Empty(_sentToPlanner);
    }

    [Fact]
    public async Task A_week_plan_request_is_answered_by_the_planner()
    {
        Classifies(RequestKind.WeekPlan);

        List<WorkflowEvent> events = await RunAsync(Build(), WeekRequest);

        Assert.Equal(
            ["""
            planner: Monday: Salmon rice, 25 min (salmon, rice)
            Saturday: Roast chicken, 50 min (chicken, potato)
            """],
            Outputs(events));
        Assert.Equal([WeekRequest], _sentToPlanner);
        Assert.Empty(_sentToCook);
    }

    [Fact]
    public async Task Anything_else_goes_to_the_default_branch_and_no_model_answers()
    {
        Classifies(RequestKind.SomethingElse);

        List<WorkflowEvent> events = await RunAsync(Build(), OtherRequest);

        Assert.Equal(
            ["decline: I only plan dinners. Ask me for tonight's dinner or for a few days."],
            Outputs(events));
        Assert.Empty(_sentToCook);
        Assert.Empty(_sentToPlanner);
    }

    [Fact]
    public async Task Each_branch_runs_one_superstep_after_the_classifier()
    {
        Classifies(RequestKind.WeekPlan);

        List<WorkflowEvent> events = await RunAsync(Build(), WeekRequest);

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
        string[][] expected = [["classify"], ["planner"]];
        Assert.Equal(expected, steps);
    }

    [Fact]
    public async Task A_classifier_cut_off_at_the_token_limit_fails_the_run_without_output()
    {
        Classifies(RequestKind.WeekPlan, ChatFinishReason.Length);

        List<WorkflowEvent> events = await RunAsync(Build(), WeekRequest);

        ExecutorFailedEvent failed = Assert.Single(events.OfType<ExecutorFailedEvent>());
        Assert.Equal("classify", failed.ExecutorId);
        Assert.Single(events.OfType<WorkflowErrorEvent>());
        Assert.Empty(Outputs(events));
    }

    [Fact]
    public async Task A_plan_cut_off_at_the_token_limit_fails_the_run_without_output()
    {
        Classifies(RequestKind.WeekPlan);
        Replies(_plannerModel, [Plan], _sentToPlanner, ChatFinishReason.Length);

        List<WorkflowEvent> events = await RunAsync(Build(), WeekRequest);

        Assert.Equal("planner", Assert.Single(events.OfType<ExecutorFailedEvent>()).ExecutorId);
        Assert.Single(events.OfType<WorkflowErrorEvent>());
        Assert.Empty(Outputs(events));
    }

    [Fact]
    public async Task The_same_workflow_runs_again_after_the_first_run_is_disposed()
    {
        Classifies(RequestKind.QuickDinner);
        Workflow workflow = Build();

        await RunAsync(workflow, QuickRequest);
        List<WorkflowEvent> events = await RunAsync(workflow, QuickRequest);

        Assert.Single(Outputs(events));
    }
}
