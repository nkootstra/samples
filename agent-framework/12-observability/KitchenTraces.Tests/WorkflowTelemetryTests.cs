using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenTraces.Tests;

// The planner-to-reviewer workflow from Program.cs, with WithOpenTelemetry
// and traced agents as there, each agent on a stubbed chat client.
[Collection(nameof(RecordsSpans))]
public sealed class WorkflowTelemetryTests : IDisposable
{
    const string SourceName = "Kitchen";
    const string WorkflowSource = "Microsoft.Agents.AI.Workflows";
    const string Request =
        "Dinners for Monday, Wednesday and Saturday. We have salmon.";

    readonly IChatClient _plannerModel = Substitute.For<IChatClient>();
    readonly IChatClient _reviewerModel = Substitute.For<IChatClient>();
    readonly RecordedSpans _spans = new(SourceName, WorkflowSource);

    public WorkflowTelemetryTests()
    {
        Streams(_plannerModel, "Monday: salmon", inputTokens: 85, outputTokens: 150);
        Streams(_reviewerModel, "Monday: chicken", inputTokens: 270, outputTokens: 900);
    }

    static void Streams(IChatClient model, string answer, int inputTokens, int outputTokens) =>
        model
            .GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new ChatResponseUpdate[]
                {
                    new(ChatRole.Assistant, answer),
                    new(ChatRole.Assistant, [new UsageContent(new UsageDetails
                    {
                        InputTokenCount = inputTokens,
                        OutputTokenCount = outputTokens,
                    })])
                    {
                        FinishReason = ChatFinishReason.Stop,
                    },
                }
                .ToAsyncEnumerable());

    static AIAgent Traced(AIAgent agent) => agent
        .AsBuilder()
        .UseOpenTelemetry(SourceName)
        .Build();

    Workflow PlannerThenReviewer(bool withOpenTelemetry, bool sensitiveData = false)
    {
        AIAgent planner = Traced(_plannerModel.AsAIAgent(
            name: "planner", instructions: "You plan dinners for one household."));
        AIAgent reviewer = Traced(_reviewerModel.AsAIAgent(
            name: "reviewer", instructions: "You check a dinner plan."));

        WorkflowBuilder builder = new WorkflowBuilder(planner).AddEdge(planner, reviewer);
        if (withOpenTelemetry)
        {
            builder = builder.WithOpenTelemetry(otel => otel.EnableSensitiveData = sensitiveData);
        }
        return builder.Build();
    }

    static async Task<Dictionary<string, List<AgentResponseUpdate>>> RunAsync(Workflow workflow)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, Request, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        Dictionary<string, List<AgentResponseUpdate>> updates = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is AgentResponseUpdateEvent update)
            {
                updates.TryAdd(update.ExecutorId, []);
                updates[update.ExecutorId].Add(update.Update);
            }
        }
        return updates;
    }

    public void Dispose() => _spans.Dispose();

    [Fact]
    public async Task Without_WithOpenTelemetry_only_the_agent_spans_are_recorded()
    {
        await RunAsync(PlannerThenReviewer(withOpenTelemetry: false));

        Assert.DoesNotContain(_spans.All, span => span.Source.Name == WorkflowSource);
        Assert.Equal(2, _spans.All.Count(span => span.DisplayName.StartsWith("invoke_agent", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Each_agent_span_sits_under_the_executor_span_that_ran_it()
    {
        await RunAsync(PlannerThenReviewer(withOpenTelemetry: true));

        Activity planner = _spans.Named("invoke_agent planner(");
        Activity reviewer = _spans.Named("invoke_agent reviewer(");
        Assert.StartsWith("executor.process planner_", planner.Parent?.DisplayName);
        Assert.StartsWith("executor.process reviewer_", reviewer.Parent?.DisplayName);
    }

    [Fact]
    public async Task The_whole_run_sits_under_one_workflow_session_span()
    {
        await RunAsync(PlannerThenReviewer(withOpenTelemetry: true));

        Activity session = _spans.Named("workflow.session");
        Assert.All(
            _spans.All.Where(span => span.DisplayName.StartsWith("invoke_agent", StringComparison.Ordinal)),
            span => Assert.Equal(session.TraceId, span.TraceId));
    }

    [Fact]
    public void Building_the_workflow_is_a_trace_of_its_own()
    {
        PlannerThenReviewer(withOpenTelemetry: true);

        Activity build = _spans.Named("workflow.build");
        Assert.Null(build.Parent);
    }

    [Fact]
    public async Task Without_sensitive_data_no_executor_input_is_recorded()
    {
        await RunAsync(PlannerThenReviewer(withOpenTelemetry: true, sensitiveData: false));

        Assert.DoesNotContain(
            _spans.All.SelectMany(span => span.TagObjects),
            tag => tag.Key is "executor.input" or "message.content");
    }

    [Fact]
    public async Task With_sensitive_data_the_executor_input_holds_the_request()
    {
        await RunAsync(PlannerThenReviewer(withOpenTelemetry: true, sensitiveData: true));

        Assert.Contains(
            _spans.All.Select(span => span.GetTagItem("executor.input") as string),
            input => input?.Contains(Request, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Each_agents_updates_add_up_to_its_own_token_counts()
    {
        Dictionary<string, List<AgentResponseUpdate>> updates =
            await RunAsync(PlannerThenReviewer(withOpenTelemetry: true));

        UsageDetails? reviewer = updates
            .Single(pair => pair.Key.StartsWith("reviewer_", StringComparison.Ordinal))
            .Value.ToAgentResponse().Usage;
        Assert.Equal(270, reviewer?.InputTokenCount);
        Assert.Equal(900, reviewer?.OutputTokenCount);
    }
}
