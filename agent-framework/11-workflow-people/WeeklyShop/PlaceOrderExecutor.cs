using Microsoft.Agents.AI.Workflows;

[YieldsOutput(typeof(string))]
public sealed partial class PlaceOrderExecutor()
    : Executor<BasketDecision>("place-order")
{
    public override async ValueTask HandleAsync(
        BasketDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Basket basket = await context.ReadStateAsync<Basket>(
            "basket", scopeName: "shop", cancellationToken)
            ?? throw new InvalidOperationException("No basket in state.");

        if (!decision.Approved)
        {
            string reason = decision.Change is null
                ? "rejected"
                : $"still a change after basket {decision.Round}: {decision.Change}";
            await context.AddEventAsync(
                new ShopProgressEvent("order skipped", reason),
                cancellationToken);
            await context.YieldOutputAsync(
                $"No order placed: {reason}", cancellationToken);
            return;
        }

        await context.AddEventAsync(
            new ShopProgressEvent(
                "order placed", $"{basket.Items.Count} items"),
            cancellationToken);
        await context.YieldOutputAsync(
            $"Ordered {basket.Items.Count} items.", cancellationToken);
    }
}
