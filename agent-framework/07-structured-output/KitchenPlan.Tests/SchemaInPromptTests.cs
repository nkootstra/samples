using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenPlan.Tests;

// SchemaInPrompt.RunAsync on a stubbed chat client that, like a client
// without native structured output, only sees what is in the messages.
public sealed class SchemaInPromptTests
{
    const string Plan = """
        {"dinners":[{"day":"Monday","dish":"Salmon fried rice","minutes":20,
        "mainIngredients":["salmon","rice"]}]}
        """;

    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<ChatMessage> _sentMessages = [];
    readonly List<ChatOptions?> _sentOptions = [];

    void Reply(string text) =>
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(_sentMessages.AddRange),
                Arg.Do<ChatOptions?>(_sentOptions.Add),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    Task<AgentResponse<DinnerPlan>> AskForPlan() =>
        SchemaInPrompt.RunAsync<DinnerPlan>(
            _chatClient.AsAIAgent(name: "planner", instructions: "You plan dinners."),
            "Dinner for Monday.");

    [Fact]
    public async Task The_schema_is_in_the_message()
    {
        Reply(Plan);

        await AskForPlan();

        string sent = Assert.Single(_sentMessages).Text;
        Assert.StartsWith("Dinner for Monday.", sent, StringComparison.Ordinal);
        Assert.Contains(
            "Respond with a JSON value conforming to the following schema:",
            sent,
            StringComparison.Ordinal);
        Assert.Contains("\"mainIngredients\"", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_response_format_asks_for_json_without_a_schema()
    {
        Reply(Plan);

        await AskForPlan();

        var format = Assert.IsType<ChatResponseFormatJson>(
            Assert.Single(_sentOptions)?.ResponseFormat);
        Assert.Null(format.Schema);
    }

    [Fact]
    public async Task A_json_answer_is_read_into_the_record()
    {
        Reply(Plan);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Assert.Equal("Salmon fried rice", Assert.Single(response.Result.Dinners).Dish);
    }

    [Fact]
    public async Task An_empty_object_reads_as_a_plan_without_dinners()
    {
        Reply("{}");

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Assert.Null(response.Result.Dinners);
    }

    [Fact]
    public async Task An_answer_in_prose_throws_a_json_exception_on_result()
    {
        Reply("**Monday: Salmon fried rice**");

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Assert.Throws<JsonException>(() => response.Result);
    }
}
