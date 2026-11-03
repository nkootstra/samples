using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

public static class ShopWorkflow
{
    public const int MaxRounds = 3;

    public static Workflow Build(AIAgent shopper)
    {
        var writeBasket = new WriteBasketExecutor(shopper);
        RequestPort<Basket, BasketDecision> approveBasket =
            RequestPort.Create<Basket, BasketDecision>("approve-basket");
        var placeOrder = new PlaceOrderExecutor();

        return new WorkflowBuilder(writeBasket)
            .AddEdge(writeBasket, approveBasket)
            .AddEdge<BasketDecision>(approveBasket, writeBasket,
                condition: d => d is { Change: not null, Round: < MaxRounds })
            .AddEdge<BasketDecision>(approveBasket, placeOrder,
                condition: d => d is { Change: null } or { Round: >= MaxRounds })
            .WithOutputFrom(placeOrder)
            .Build();
    }
}
