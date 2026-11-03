using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

public sealed partial class WriteBasketExecutor(AIAgent shopper)
    : Executor("write-basket")
{
    [MessageHandler]
    ValueTask<Basket> WriteAsync(
        string request,
        IWorkflowContext context,
        CancellationToken cancellationToken) =>
        RunShopperAsync(request, round: 1, context, cancellationToken);

    [MessageHandler]
    async ValueTask<Basket> ReviseAsync(
        BasketDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        Basket current = await context.ReadStateAsync<Basket>(
            "basket", scopeName: "shop", cancellationToken)
            ?? throw new InvalidOperationException("No basket in state.");

        string prompt = $"""
            Your last basket:
            {string.Join("\n", current.Items)}

            The household asks for this change:
            {decision.Change}

            Return the whole basket.
            """;
        return await RunShopperAsync(
            prompt, current.Round + 1, context, cancellationToken);
    }

    async ValueTask<Basket> RunShopperAsync(
        string prompt,
        int round,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        AgentResponse response = await shopper.RunAsync(
            prompt, cancellationToken: cancellationToken);
        if (response.FinishReason != ChatFinishReason.Stop)
        {
            throw new InvalidOperationException(
                $"The shopper stopped with {response.FinishReason}.");
        }

        Basket basket = Basket.Parse(response.Text, round);

        await context.QueueStateUpdateAsync(
            "basket", basket, scopeName: "shop", cancellationToken);
        await context.AddEventAsync(
            new ShopProgressEvent(
                round == 1 ? "basket written" : "basket revised",
                $"{basket.Items.Count} items, "
                    + $"{response.Usage?.OutputTokenCount} output tokens"),
            cancellationToken);
        return basket;
    }
}
