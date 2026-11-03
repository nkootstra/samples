using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace WeeklyShop.Tests;

// What another executor sees of the basket that WriteBasketExecutor stores in
// the "shop" scope.
public sealed partial class SharedStateTests
{
    [YieldsOutput(typeof(string))]
    sealed partial class ReadBasketExecutor(string? scopeName) : Executor<Basket>("read-basket")
    {
        public override async ValueTask HandleAsync(
            Basket message, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            Basket? stored = await context.ReadStateAsync<Basket>(
                "basket", scopeName, cancellationToken);
            await context.YieldOutputAsync(
                stored is null ? "nothing" : $"{stored.Items.Count} items", cancellationToken);
        }
    }

    static async Task<string?> ReadAfterWriteBasketAsync(string? scopeName)
    {
        IChatClient model = Substitute.For<IChatClient>();
        model
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "- 1 red pepper"))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        var writeBasket = new WriteBasketExecutor(model.AsAIAgent(name: "shopper"));
        var readBasket = new ReadBasketExecutor(scopeName);
        Workflow workflow = new WorkflowBuilder(writeBasket)
            .AddEdge(writeBasket, readBasket)
            .WithOutputFrom(readBasket)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            workflow, "Dinners this week.", cancellationToken: TestContext.Current.CancellationToken);
        string? output = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(TestContext.Current.CancellationToken))
        {
            if (evt is WorkflowOutputEvent result)
            {
                output = (string?)result.Data;
            }
        }
        return output;
    }

    [Fact]
    public async Task The_next_step_reads_the_basket_from_the_shop_scope()
    {
        Assert.Equal("1 items", await ReadAfterWriteBasketAsync("shop"));
    }

    [Fact]
    public async Task A_read_without_the_scope_name_finds_nothing()
    {
        Assert.Equal("nothing", await ReadAfterWriteBasketAsync(scopeName: null));
    }
}
