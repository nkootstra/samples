using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenChecks.Tests;

// What the article says about conditional edges and shared workflows, shown on
// small graphs of plain functions and on the dinner workflow.
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

    static Workflow SmallOrEven()
    {
        ExecutorBinding start = Start(), small = Says("small"), even = Says("even");
        return new WorkflowBuilder(start)
            .AddEdge<int>(start, small, condition: n => n < 10)
            .AddEdge<int>(start, even, condition: n => n % 2 == 0)
            .WithOutputFrom(small, even)
            .Build();
    }

    [Fact]
    public async Task Every_conditional_edge_whose_condition_holds_gets_the_message()
    {
        string[] expected = ["even got 4", "small got 4"];
        Assert.Equal(expected, Outputs(await RunAsync(SmallOrEven(), 4)));
    }

    [Fact]
    public async Task A_message_no_condition_accepts_ends_the_run_with_no_output_and_no_error()
    {
        List<WorkflowEvent> events = await RunAsync(SmallOrEven(), 41);

        Assert.Empty(Outputs(events));
        Assert.Empty(events.OfType<ExecutorFailedEvent>());
        Assert.Empty(events.OfType<WorkflowErrorEvent>());
    }

    [Fact]
    public async Task The_concurrent_environment_refuses_a_workflow_with_shared_executor_instances()
    {
        Workflow workflow = DinnerWorkflow.Build(Agent(), Agent());

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await InProcessExecution.Concurrent.RunStreamingAsync(
                workflow, "Dinners for Monday.", cancellationToken: TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "Workflow must only consist of cross-run share-capable or factory-created executors.",
            ex.Message);

        static AIAgent Agent() => Substitute.For<IChatClient>().AsAIAgent();
    }
}
