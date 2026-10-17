using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenTools.Tests;

// The tools and the agent from Program.cs, with a stubbed chat client in
// place of Anthropic.
public sealed class ToolTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<ChatMessage[]> _sentMessages = [];

    static AIFunction[] ToolsFor(PantryTools pantry) =>
    [
        AIFunctionFactory.Create(pantry.FindInPantry, name: "find_in_pantry"),
        AIFunctionFactory.Create(pantry.ListExpiringSoon, name: "list_expiring_soon"),
    ];

    // The model first asks for a tool call, then answers.
    void ModelCallsThenAnswers(string tool, string parameter, object argument, string answer) =>
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent => _sentMessages.Add([.. sent])),
                Arg.Any<ChatOptions?>(),
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
                new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));

    static FunctionResultContent ToolResult(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();

    [Fact]
    public void The_model_sees_the_tool_name_and_the_method_description()
    {
        AIFunction tool = ToolsFor(new())[0];

        Assert.Equal(
            "find_in_pantry: Looks up one ingredient in the household's pantry.",
            $"{tool.Name}: {tool.Description}");
    }

    [Fact]
    public void The_schema_comes_from_the_parameter_type_and_description()
    {
        AIFunction tool = ToolsFor(new())[1];

        JsonElement days = tool.JsonSchema.GetProperty("properties").GetProperty("days");
        Assert.Equal("integer", days.GetProperty("type").GetString());
        Assert.Equal(
            "How many days ahead to look, from 1 to 14.",
            days.GetProperty("description").GetString());
        Assert.Equal("days", tool.JsonSchema.GetProperty("required")[0].GetString());
    }

    [Fact]
    public async Task The_agent_runs_the_requested_tool_and_sends_the_result_back()
    {
        ModelCallsThenAnswers("list_expiring_soon", "days", 3, "Make a spinach and feta omelette.");
        AIAgent agent = _chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions: "You help one household decide what to cook.",
            tools: ToolsFor(new()));

        await agent.RunAsync(
            "What should I cook tonight so nothing goes to waste?",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("Today is ", ToolResult(_sentMessages[1]).Result?.ToString());
    }

    [Fact]
    public async Task A_thrown_exception_reaches_the_model_as_a_bare_error()
    {
        ModelCallsThenAnswers("list_expiring_soon", "days", 30, "Sorry.");
        AIAgent agent = _chatClient.AsAIAgent(
            name: "kitchen-assistant",
            tools:
            [
                AIFunctionFactory.Create(
                    (int days) =>
                    {
                        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, 14);
                        return "";
                    },
                    name: "list_expiring_soon"),
            ]);

        AgentResponse response = await agent.RunAsync(
            "What expires this month?",
            cancellationToken: TestContext.Current.CancellationToken);

        FunctionResultContent result = ToolResult(response.Messages);
        Assert.Equal("Error: Function failed.", result.Result);
        Assert.IsType<ArgumentOutOfRangeException>(result.Exception);
    }
}
