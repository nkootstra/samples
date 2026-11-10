using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenScenarios.Tests;

// Layer 2: the agent wiring, with a scripted model in place of Claude.
public sealed class KitchenAgentTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<ChatOptions?> _sentOptions = [];
    readonly List<ChatMessage[]> _sentMessages = [];

    // The model asks for one tool call, then answers.
    void ModelCalls(string tool, string parameter, string argument) =>
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent => _sentMessages.Add([.. sent])),
                Arg.Do<ChatOptions?>(_sentOptions.Add),
                Arg.Any<CancellationToken>())
            .Returns(
                new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            "call-1",
                            tool,
                            new Dictionary<string, object?> { [parameter] = argument }),
                    ])),
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")));

    Task<AgentResponse> RunAsync(Kitchen kitchen, string message) =>
        KitchenAgent.Create(_chatClient, kitchen).RunAsync(
            message,
            cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task An_add_call_from_the_model_lands_on_the_kitchens_list()
    {
        ModelCalls("add_to_shopping_list", "item", "milk");
        var kitchen = new Kitchen();

        await RunAsync(kitchen, "We're out of milk.");

        Assert.Equal(["milk"], kitchen.ShoppingList);
    }

    [Fact]
    public async Task The_model_gets_both_tools_by_name()
    {
        ModelCalls("find_in_pantry", "ingredient", "eggs");

        await RunAsync(new Kitchen(), "Do we have eggs?");

        Assert.Equal(
            ["find_in_pantry", "add_to_shopping_list"],
            _sentOptions[0]!.Tools!.Select(t => t.Name));
    }

    [Fact]
    public async Task The_model_gets_the_instructions()
    {
        ModelCalls("find_in_pantry", "ingredient", "eggs");

        await RunAsync(new Kitchen(), "Do we have eggs?");

        Assert.Contains(
            "Only add items to the shopping list when the household asks for it.",
            _sentOptions[0]!.Instructions,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_tool_result_goes_back_to_the_model()
    {
        ModelCalls("find_in_pantry", "ingredient", "eggs");

        await RunAsync(new Kitchen(), "Do we have eggs?");

        FunctionResultContent result = _sentMessages[1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        Assert.Equal("eggs: 6.", result.Result?.ToString());
    }

    [Fact]
    public async Task A_refused_peanut_item_leaves_the_list_empty_whatever_the_model_asks()
    {
        ModelCalls("add_to_shopping_list", "item", "peanut butter");
        var kitchen = new Kitchen();

        await RunAsync(kitchen, "Put peanut butter on the list.");

        Assert.Empty(kitchen.ShoppingList);
    }
}
