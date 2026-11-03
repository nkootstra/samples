using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.AI;
using NSubstitute;
using NSubstitute.Core;
using NSubstitute.ExceptionExtensions;

namespace WeeklyShop.Tests;

// The workflow from Program.cs, with the shopper on a stubbed chat client in
// place of Anthropic and the checkpoints in a temporary folder. Each "process"
// builds its own workflow and opens its own store, as the app does on every run.
public sealed class ShopWorkflowTests : IDisposable
{
    const string SessionId = "weekly-shop";
    const string Answer = "- 500 g chicken thighs\n- 1 red pepper\n- 250 g red lentils";
    const string Revised = "- 500 g chicken thighs\n- 1 red pepper";

    readonly DirectoryInfo _folder = new(
        Path.Combine(Path.GetTempPath(), $"weekly-shop-tests-{Guid.NewGuid():N}"));
    readonly IChatClient _model = Substitute.For<IChatClient>();

    public ShopWorkflowTests() => Answers(Answer, Revised);

    public void Dispose()
    {
        if (_folder.Exists)
        {
            _folder.Delete(recursive: true);
        }
    }

    // The shopper answers with these baskets in turn, the last one repeating.
    void Answers(params string[] texts) =>
        _model
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Response(texts[0], ChatFinishReason.Stop),
                [.. texts.Skip(1).Select(text => Response(text, ChatFinishReason.Stop))]);

    void AnswersCutOff(string text) =>
        _model
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Response(text, ChatFinishReason.Length));

    static Func<CallInfo, Task<ChatResponse>> Response(string text, ChatFinishReason finishReason) =>
        _ => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            FinishReason = finishReason,
            Usage = new() { OutputTokenCount = 120 },
        });

    Workflow ShopWorkflow() => global::ShopWorkflow.Build(
        _model.AsAIAgent(name: "shopper", instructions: "You write the weekly shopping basket."));

    FileSystemJsonCheckpointStore OpenStore() => new(_folder);

    static CheckpointManager Checkpoints(FileSystemJsonCheckpointStore store) =>
        CheckpointManager.CreateJson(store);

    // The first process: runs until the approval request and stops watching.
    async Task<List<WorkflowEvent>> StartAsync() => (await StartWithStatusAsync()).Events;

    // The status is what Program.cs checks before it says the run is waiting.
    async Task<(List<WorkflowEvent> Events, RunStatus Status)> StartWithStatusAsync()
    {
        using FileSystemJsonCheckpointStore store = OpenStore();
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            ShopWorkflow(), "Dinners this week.", Checkpoints(store), SessionId,
            TestContext.Current.CancellationToken);

        List<WorkflowEvent> events =
            await CollectAsync(run, blockOnPendingRequest: false, answer: null);
        return (events, await run.GetStatusAsync(TestContext.Current.CancellationToken));
    }

    // A later process: resumes from the latest checkpoint and types the answers
    // in turn, as Program.cs does. When it runs out of answers while a request
    // is open, the process stops, and the next one resumes from there.
    async Task<List<WorkflowEvent>> ResumeAsync(params string?[] answers) =>
        (await ResumeWithStatusAsync(answers)).Events;

    async Task<(List<WorkflowEvent> Events, RunStatus Status)> ResumeWithStatusAsync(
        params string?[] answers)
    {
        using FileSystemJsonCheckpointStore store = OpenStore();
        CheckpointManager checkpoints = Checkpoints(store);
        CheckpointInfo latest = await checkpoints.GetLatestCheckpointAsync(
            SessionId, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("No checkpoint.");

        await using StreamingRun run = await InProcessExecution.ResumeStreamingAsync(
            ShopWorkflow(), latest, checkpoints, TestContext.Current.CancellationToken);

        Queue<string?> typed = new(answers);
        List<WorkflowEvent> events = [];
        while (true)
        {
            List<WorkflowEvent> step =
                await CollectAsync(run, blockOnPendingRequest: false, answer: null);
            events.AddRange(step);
            RunStatus status = await run.GetStatusAsync(TestContext.Current.CancellationToken);
            if (status != RunStatus.PendingRequests || typed.Count == 0)
            {
                return (events, status);
            }

            RequestInfoEvent request = step.OfType<RequestInfoEvent>().Last();
            Assert.True(request.Request.TryGetDataAs(out Basket? basket));
            await run.SendResponseAsync(request.Request.CreateResponse(
                BasketDecision.FromAnswer(typed.Dequeue(), basket.Round)));
        }
    }

    static async Task<List<WorkflowEvent>> CollectAsync(
        StreamingRun run, bool blockOnPendingRequest, BasketDecision? answer)
    {
        List<WorkflowEvent> events = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(
            blockOnPendingRequest, TestContext.Current.CancellationToken))
        {
            events.Add(evt);
            if (answer is not null && evt is RequestInfoEvent request)
            {
                await run.SendResponseAsync(request.Request.CreateResponse(answer));
            }
        }
        return events;
    }

    static List<Basket> RequestedBaskets(IEnumerable<WorkflowEvent> events) =>
        [.. events.OfType<RequestInfoEvent>().Select(request =>
        {
            Assert.True(request.Request.TryGetDataAs(out Basket? basket));
            return basket;
        })];

    static Basket RequestedBasket(IEnumerable<WorkflowEvent> events)
    {
        RequestInfoEvent request = Assert.Single(events.OfType<RequestInfoEvent>());
        Assert.True(request.Request.TryGetDataAs(out Basket? basket));
        return basket;
    }

    [Fact]
    public async Task The_first_run_stops_at_the_approval_request_with_the_shoppers_basket()
    {
        List<WorkflowEvent> events = await StartAsync();

        Assert.Equal(
            ["500 g chicken thighs", "1 red pepper", "250 g red lentils"],
            RequestedBasket(events).Items);
        Assert.Empty(events.OfType<WorkflowOutputEvent>());
    }

    [Fact]
    public async Task The_first_run_leaves_a_checkpoint_on_disk()
    {
        await StartAsync();

        using FileSystemJsonCheckpointStore store = OpenStore();
        CheckpointInfo? latest = await Checkpoints(store).GetLatestCheckpointAsync(
            SessionId, TestContext.Current.CancellationToken);

        Assert.NotNull(latest);
    }

    [Fact]
    public async Task A_resumed_run_raises_the_same_request_again()
    {
        await StartAsync();

        List<WorkflowEvent> events = await ResumeAsync("y");

        Assert.Equal(
            ["500 g chicken thighs", "1 red pepper", "250 g red lentils"],
            RequestedBasket(events).Items);
    }

    [Fact]
    public async Task An_approval_after_resuming_places_the_order_from_the_basket_in_shared_state()
    {
        await StartAsync();

        List<WorkflowEvent> events = await ResumeAsync("y");

        WorkflowOutputEvent output = Assert.Single(events.OfType<WorkflowOutputEvent>());
        Assert.Equal("Ordered 3 items.", output.Data);
    }

    [Fact]
    public async Task A_resumed_run_does_not_call_the_model_again()
    {
        await StartAsync();
        _model.ClearReceivedCalls();

        await ResumeAsync("y");

        await _model.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_n_places_no_order_and_writes_no_new_basket()
    {
        await StartAsync();

        List<WorkflowEvent> events = await ResumeAsync("n");

        WorkflowOutputEvent output = Assert.Single(events.OfType<WorkflowOutputEvent>());
        Assert.Equal("No order placed: rejected", output.Data);
    }

    [Fact]
    public async Task Each_step_reports_its_progress_as_a_custom_event()
    {
        List<WorkflowEvent> first = await StartAsync();
        List<WorkflowEvent> second = await ResumeAsync("y");

        Assert.Equal(
            ["basket written: 3 items, 120 output tokens", "order placed: 3 items"],
            first.Concat(second).OfType<ShopProgressEvent>().Select(e => $"{e.Step}: {e.Detail}"));
    }

    [Fact]
    public async Task A_shopper_answer_cut_off_at_the_token_limit_fails_the_step()
    {
        AnswersCutOff("- 500 g chicken");

        List<WorkflowEvent> events = await StartAsync();

        Assert.Single(events.OfType<ExecutorFailedEvent>());
        Assert.Empty(events.OfType<RequestInfoEvent>());
    }

    [Fact]
    public async Task A_first_run_that_reaches_the_request_is_pending()
    {
        (_, RunStatus status) = await StartWithStatusAsync();

        Assert.Equal(RunStatus.PendingRequests, status);
    }

    [Fact]
    public async Task A_first_run_cut_off_at_the_token_limit_is_not_pending()
    {
        AnswersCutOff("- 500 g chicken");

        (_, RunStatus status) = await StartWithStatusAsync();

        Assert.NotEqual(RunStatus.PendingRequests, status);
    }

    [Fact]
    public async Task A_first_run_whose_model_call_fails_is_not_pending()
    {
        _model
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Unauthorized"));

        (List<WorkflowEvent> events, RunStatus status) = await StartWithStatusAsync();

        Assert.Single(events.OfType<ExecutorFailedEvent>());
        Assert.NotEqual(RunStatus.PendingRequests, status);
    }

    // Stands in for PlaceOrderExecutor under a new ID.
    sealed class RenamedPlaceOrderExecutor() : Executor<BasketDecision>("order")
    {
        public override ValueTask HandleAsync(
            BasketDecision message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
            default;
    }

    [Fact]
    public async Task Resuming_after_renaming_a_step_throws()
    {
        await StartAsync();
        using FileSystemJsonCheckpointStore store = OpenStore();
        CheckpointManager checkpoints = Checkpoints(store);
        CheckpointInfo latest = (await checkpoints.GetLatestCheckpointAsync(
            SessionId, TestContext.Current.CancellationToken))!;
        var writeBasket = new WriteBasketExecutor(_model.AsAIAgent(name: "shopper"));
        RequestPort<Basket, BasketDecision> approveBasket =
            RequestPort.Create<Basket, BasketDecision>("approve-basket");
        var order = new RenamedPlaceOrderExecutor();
        Workflow renamed = new WorkflowBuilder(writeBasket)
            .AddEdge(writeBasket, approveBasket)
            .AddEdge<BasketDecision>(approveBasket, writeBasket,
                condition: d => d is { Change: not null, Round: < global::ShopWorkflow.MaxRounds })
            .AddEdge<BasketDecision>(approveBasket, order,
                condition: d => d is { Change: null } or { Round: >= global::ShopWorkflow.MaxRounds })
            .WithOutputFrom(order)
            .Build();

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await InProcessExecution.ResumeStreamingAsync(
                renamed, latest, checkpoints, TestContext.Current.CancellationToken));

        Assert.Equal(
            "The specified checkpoint is not compatible with the workflow associated with this runner.",
            exception.Message);
    }

    [Fact]
    public async Task A_change_sends_the_basket_back_to_the_shopper_and_asks_again()
    {
        await StartAsync();

        List<WorkflowEvent> events = await ResumeAsync("We're away on Saturday, no pizza", "y");

        Assert.Equal(
            [
                ["500 g chicken thighs", "1 red pepper", "250 g red lentils"],
                ["500 g chicken thighs", "1 red pepper"],
            ],
            RequestedBaskets(events).Select(b => b.Items));
        Assert.Equal([1, 2], RequestedBaskets(events).Select(b => b.Round));
        WorkflowOutputEvent output = Assert.Single(events.OfType<WorkflowOutputEvent>());
        Assert.Equal("Ordered 2 items.", output.Data);
    }

    [Fact]
    public async Task The_revision_prompt_holds_the_basket_from_state_and_the_change()
    {
        await StartAsync();
        _model.ClearReceivedCalls();

        await ResumeAsync("We're away on Saturday, no pizza", "y");

        IEnumerable<ChatMessage> messages = (IEnumerable<ChatMessage>)Assert.Single(
            _model.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IChatClient.GetResponseAsync))
            .GetArguments()[0]!;
        string prompt = messages.Last(m => m.Role == ChatRole.User).Text;
        Assert.Contains("250 g red lentils", prompt, StringComparison.Ordinal);
        Assert.Contains("We're away on Saturday, no pizza", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revised_basket_reports_its_own_progress_event()
    {
        await StartAsync();

        List<WorkflowEvent> events = await ResumeAsync("No pizza", "y");

        Assert.Equal(
            ["basket revised: 2 items, 120 output tokens", "order placed: 2 items"],
            events.OfType<ShopProgressEvent>().Select(e => $"{e.Step}: {e.Detail}"));
    }

    [Fact]
    public async Task A_change_to_the_third_basket_places_no_order()
    {
        await StartAsync();
        _model.ClearReceivedCalls();

        List<WorkflowEvent> events = await ResumeAsync("No pizza", "No lentils", "No chicken");

        Assert.Equal([1, 2, 3], RequestedBaskets(events).Select(b => b.Round));
        WorkflowOutputEvent output = Assert.Single(events.OfType<WorkflowOutputEvent>());
        Assert.Equal("No order placed: still a change after basket 3: No chicken", output.Data);
        await _model.ReceivedWithAnyArgs(2).GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_process_stopped_after_a_change_leaves_the_revised_basket_for_the_next_one()
    {
        await StartAsync();
        (List<WorkflowEvent> second, RunStatus status) =
            await ResumeWithStatusAsync("We're away on Saturday, no pizza");
        _model.ClearReceivedCalls();

        List<WorkflowEvent> third = await ResumeAsync("y");

        Assert.Equal(RunStatus.PendingRequests, status);
        Assert.Equal(2, RequestedBaskets(second).Count);
        Basket asked = RequestedBasket(third);
        Assert.Equal(["500 g chicken thighs", "1 red pepper"], asked.Items);
        Assert.Equal(2, asked.Round);
        WorkflowOutputEvent output = Assert.Single(third.OfType<WorkflowOutputEvent>());
        Assert.Equal("Ordered 2 items.", output.Data);
        await _model.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default, TestContext.Current.CancellationToken);
    }
}
