using Microsoft.Agents.AI.Workflows;

namespace KitchenRouting.Tests;

// What the article says about switches, edges and reusing a workflow, shown on
// small graphs of plain functions.
public sealed class EdgeBehaviourTests
{
    static ExecutorBinding Start() =>
        ((Func<int, int>)(n => n)).BindAsExecutor("start");

    static ExecutorBinding Says(string id) =>
        ((Func<int, string>)(n => $"{id} got {n}")).BindAsExecutor(id);

    static async Task<List<WorkflowEvent>> RunAsync(Workflow workflow, int input)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, input, cancellationToken: cancellationToken);

        List<WorkflowEvent> events = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            events.Add(evt);
        }
        return events;
    }

    static string[] Outputs(List<WorkflowEvent> events) =>
        [.. events.OfType<WorkflowOutputEvent>()
            .Select(o => $"{o.Data}")
            .Order(StringComparer.Ordinal)];

    [Fact]
    public async Task Every_conditional_edge_whose_condition_holds_gets_the_message()
    {
        ExecutorBinding start = Start(), small = Says("small"), even = Says("even");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge<int>(start, small, condition: n => n < 10)
            .AddEdge<int>(start, even, condition: n => n % 2 == 0)
            .WithOutputFrom(small, even)
            .Build();

        string[] expected = ["even got 4", "small got 4"];
        Assert.Equal(expected, Outputs(await RunAsync(workflow, 4)));
    }

    [Fact]
    public async Task A_switch_takes_only_the_first_case_that_matches()
    {
        ExecutorBinding start = Start(), small = Says("small"), even = Says("even");
        Workflow workflow = new WorkflowBuilder(start)
            .AddSwitch(start, route => route
                .AddCase<int>(n => n < 10, small)
                .AddCase<int>(n => n % 2 == 0, even))
            .WithOutputFrom(small, even)
            .Build();

        string[] expected = ["small got 4"];
        Assert.Equal(expected, Outputs(await RunAsync(workflow, 4)));
    }

    [Fact]
    public async Task A_message_no_edge_or_case_accepts_ends_the_run_with_no_output_and_no_error()
    {
        ExecutorBinding start = Start(), small = Says("small");
        Workflow workflow = new WorkflowBuilder(start)
            .AddSwitch(start, route => route.AddCase<int>(n => n < 10, small))
            .WithOutputFrom(small)
            .Build();

        List<WorkflowEvent> events = await RunAsync(workflow, 40);

        Assert.Empty(Outputs(events));
        Assert.Empty(events.OfType<ExecutorFailedEvent>());
        Assert.Empty(events.OfType<WorkflowErrorEvent>());
    }

    [Fact]
    public void An_output_executor_that_no_edge_reaches_stops_the_build()
    {
        ExecutorBinding start = Start(), small = Says("small"), other = Says("other");
        WorkflowBuilder builder = new WorkflowBuilder(start)
            .AddSwitch(start, route => route.AddCase<int>(n => n < 10, small))
            .WithOutputFrom(small, other);

        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal(
            "Workflow cannot be built because there are unreachable executors: other.",
            ex.Message);
    }

    [Fact]
    public async Task A_target_without_a_handler_for_the_message_type_is_skipped_without_an_error()
    {
        ExecutorBinding start = Start(), small = Says("small");
        ExecutorBinding text = ((Func<string, string>)(s => $"text got {s}")).BindAsExecutor("text");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, small)
            .AddEdge(start, text)
            .WithOutputFrom(small, text)
            .Build();

        List<WorkflowEvent> events = await RunAsync(workflow, 4);

        string[] expected = ["small got 4"];
        Assert.Equal(expected, Outputs(events));
        Assert.DoesNotContain(events, e => e is ExecutorInvokedEvent { ExecutorId: "text" });
        Assert.Empty(events.OfType<ExecutorFailedEvent>());
        Assert.Empty(events.OfType<WorkflowErrorEvent>());
    }

    [Fact]
    public async Task A_second_run_of_a_workflow_that_is_still_running_is_refused()
    {
        ExecutorBinding start = Start(), small = Says("small");
        Workflow workflow = new WorkflowBuilder(start).AddEdge(start, small).Build();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using StreamingRun first = await InProcessExecution.RunStreamingAsync(
            workflow, 1, cancellationToken: cancellationToken);
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await InProcessExecution.RunStreamingAsync(
                workflow, 2, cancellationToken: cancellationToken));

        Assert.Equal(
            "Cannot use a Workflow that is already owned by another runner or parent workflow.",
            ex.Message);
    }
}
