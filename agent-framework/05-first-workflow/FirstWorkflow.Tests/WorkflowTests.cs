using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace FirstWorkflow.Tests;

// The workflow from Program.cs, with each agent on a stubbed chat client in
// place of Anthropic.
public sealed class WorkflowTests
{
    const string Request =
        "Dinners for Monday, Wednesday and Saturday. We have salmon.";
    const string Plan = "Monday: salmon\nWednesday: salmon\nSaturday: salmon";
    const string Review = "Monday: chicken\nWednesday: chicken\nSaturday: salmon";

    // Concurrent, because the concurrent test runs the workflow twice at once.
    readonly ConcurrentQueue<string[]> _sentToPlanner = new();
    readonly ConcurrentQueue<string[]> _sentToReviewer = new();
    readonly IChatClient _plannerModel = Substitute.For<IChatClient>();
    readonly IChatClient _reviewerModel = Substitute.For<IChatClient>();

    public WorkflowTests()
    {
        Streams(_plannerModel, Plan, _sentToPlanner);
        Streams(_reviewerModel, Review, _sentToReviewer);
    }

    // Makes the chat client stream one fixed answer and keep a copy of the
    // messages it was sent.
    static void Streams(
        IChatClient model,
        string answer,
        ConcurrentQueue<string[]> sent,
        ChatFinishReason? finishReason = null) =>
        model
            .GetStreamingResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(messages =>
                    sent.Enqueue([.. messages.Select(m => $"{m.Role}: {m.Text}")])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ChatResponseUpdate[]
                {
                    new(ChatRole.Assistant, answer),
                    // Anthropic sends the finish reason in a last update of its own.
                    new() { FinishReason = finishReason ?? ChatFinishReason.Stop },
                }
                .ToAsyncEnumerable());

    Workflow PlannerThenReviewer(bool plannerOutput = true, bool reviewerOutput = true)
    {
        AIAgent planner = _plannerModel.AsAIAgent(
            name: "planner", instructions: "You plan dinners for one household.");
        AIAgent reviewer = _reviewerModel.AsAIAgent(
            name: "reviewer", instructions: "You check a dinner plan.");

        WorkflowBuilder builder = new WorkflowBuilder(planner)
            .AddEdge(planner, reviewer);
        builder = (plannerOutput, reviewerOutput) switch
        {
            (true, true) => builder.WithOutputFrom(planner, reviewer),
            (true, false) => builder.WithOutputFrom(planner),
            (false, true) => builder.WithOutputFrom(reviewer),
            (false, false) => builder,
        };
        return builder.Build();
    }

    static async Task<List<AgentResponseUpdateEvent>> RunAsync(
        Workflow workflow,
        bool sendTurnToken,
        IWorkflowExecutionEnvironment? environment = null) =>
        [.. (await EventsAsync(workflow, sendTurnToken, environment))
            .OfType<AgentResponseUpdateEvent>()];

    static async Task<List<WorkflowEvent>> EventsAsync(
        Workflow workflow,
        bool sendTurnToken,
        IWorkflowExecutionEnvironment? environment = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using StreamingRun run = await (environment ?? InProcessExecution.Default)
            .RunStreamingAsync(workflow, Request, cancellationToken: cancellationToken);
        if (sendTurnToken)
        {
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        }

        List<WorkflowEvent> events = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            events.Add(evt);
        }
        return events;
    }

    static string TextFrom(List<AgentResponseUpdateEvent> updates, string agentName) =>
        string.Concat(updates
            .Where(u => u.ExecutorId.StartsWith($"{agentName}_", StringComparison.Ordinal))
            .Select(u => u.Update.Text));

    [Fact]
    public async Task The_planner_answers_first_and_the_reviewer_second()
    {
        List<AgentResponseUpdateEvent> updates =
            await RunAsync(PlannerThenReviewer(), sendTurnToken: true);

        string[] expected = ["planner", "reviewer"];
        Assert.Equal(
            expected,
            updates.Select(u => u.ExecutorId.Split('_')[0]).Distinct().ToArray());
    }

    [Fact]
    public async Task Each_agents_text_arrives_under_its_own_executor_id()
    {
        List<AgentResponseUpdateEvent> updates =
            await RunAsync(PlannerThenReviewer(), sendTurnToken: true);

        Assert.Equal(Plan, TextFrom(updates, "planner"));
        Assert.Equal(Review, TextFrom(updates, "reviewer"));
    }

    [Fact]
    public async Task The_reviewer_reads_the_request_and_the_plan_as_user_messages()
    {
        await RunAsync(PlannerThenReviewer(), sendTurnToken: true);

        string[] expected = [$"user: {Request}", $"user: {Plan}"];
        Assert.Equal(expected, Assert.Single(_sentToReviewer));
    }

    [Fact]
    public async Task Without_a_turn_token_no_agent_calls_its_model()
    {
        List<AgentResponseUpdateEvent> updates =
            await RunAsync(PlannerThenReviewer(), sendTurnToken: false);

        Assert.Empty(updates);
        Assert.Empty(_sentToPlanner);
    }

    [Fact]
    public async Task A_reviewer_cut_off_at_the_token_limit_reports_length_in_its_update()
    {
        Streams(
            _reviewerModel,
            "Monday: chicken\n- Monday: Swapped",
            _sentToReviewer,
            ChatFinishReason.Length);

        List<AgentResponseUpdateEvent> updates =
            await RunAsync(PlannerThenReviewer(), sendTurnToken: true);

        AgentResponseUpdateEvent last = updates.Last();
        Assert.StartsWith("reviewer_", last.ExecutorId);
        Assert.Equal(ChatFinishReason.Length, last.Update.FinishReason);
    }

    [Fact]
    public async Task A_failing_agent_raises_an_executor_failure_and_a_workflow_error()
    {
        _reviewerModel
            .GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("The model is unavailable."));

        List<WorkflowEvent> events =
            await EventsAsync(PlannerThenReviewer(), sendTurnToken: true);

        ExecutorFailedEvent failed = Assert.Single(events.OfType<ExecutorFailedEvent>());
        Assert.StartsWith("reviewer_", failed.ExecutorId);
        Assert.Single(events.OfType<WorkflowErrorEvent>());
    }

    [Fact]
    public async Task WithOutputFrom_changes_nothing_today()
    {
        List<AgentResponseUpdateEvent> with =
            await RunAsync(PlannerThenReviewer(), sendTurnToken: true);
        List<AgentResponseUpdateEvent> without =
            await RunAsync(
                PlannerThenReviewer(plannerOutput: false, reviewerOutput: false),
                sendTurnToken: true);

        Assert.Equal(
            without.Select(u => (u.ExecutorId.Split('_')[0], u.Update.Text)),
            with.Select(u => (u.ExecutorId.Split('_')[0], u.Update.Text)));
    }

    // The behavior that Futures says becomes the default in 2.0.0. The flag is
    // process-wide; the tests in this class run one at a time.
    [Fact]
    public async Task With_output_filtering_on_only_designated_agents_stream()
    {
        Futures.EnableAgentResponseOutputTaggingAndFiltering = true;
        try
        {
            List<AgentResponseUpdateEvent> both =
                await RunAsync(PlannerThenReviewer(), sendTurnToken: true);
            List<AgentResponseUpdateEvent> reviewerOnly =
                await RunAsync(PlannerThenReviewer(plannerOutput: false), sendTurnToken: true);
            List<AgentResponseUpdateEvent> none =
                await RunAsync(
                    PlannerThenReviewer(plannerOutput: false, reviewerOutput: false),
                    sendTurnToken: true);

            Assert.Equal(Plan, TextFrom(both, "planner"));
            Assert.Equal(Review, TextFrom(both, "reviewer"));
            Assert.Empty(TextFrom(reviewerOnly, "planner"));
            Assert.Equal(Review, TextFrom(reviewerOnly, "reviewer"));
            Assert.Empty(none);
        }
        finally
        {
            Futures.EnableAgentResponseOutputTaggingAndFiltering = false;
        }
    }

    [Fact]
    public async Task A_second_run_of_the_same_workflow_needs_the_concurrent_environment()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Workflow workflow = PlannerThenReviewer();

        await using (StreamingRun first = await InProcessExecution.RunStreamingAsync(
            workflow, Request, cancellationToken: cancellationToken))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await InProcessExecution.RunStreamingAsync(
                    workflow, Request, cancellationToken: cancellationToken));
            Assert.Equal(
                "Cannot use a Workflow that is already owned by another runner or parent workflow.",
                error.Message);
        }

        Task<List<AgentResponseUpdateEvent>>[] runs =
        [
            RunAsync(workflow, sendTurnToken: true, InProcessExecution.Concurrent),
            RunAsync(workflow, sendTurnToken: true, InProcessExecution.Concurrent),
        ];
        foreach (List<AgentResponseUpdateEvent> updates in await Task.WhenAll(runs))
        {
            Assert.Equal(Review, TextFrom(updates, "reviewer"));
        }
    }
}
