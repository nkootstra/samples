using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenDate.Tests;

public sealed class AlternativesTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<string?> _sentInstructions = [];

    public AlternativesTests() =>
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatOptions?>(options => _sentInstructions.Add(options?.Instructions)),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Pasta.")));

    // Alternatives reads DateTime.Now, as in the article. The tests read it
    // before and after the call, so a run across midnight accepts either day.
    static string Today() =>
        DateTime.Now.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Date_in_instructions_puts_the_date_in_the_agents_instructions()
    {
        string before = Today();
        AIAgent agent = Alternatives.DateInInstructions(_chatClient, "Keep answers short.");
        string after = Today();

        await agent.RunAsync(
            "Dinner tonight?",
            cancellationToken: TestContext.Current.CancellationToken);

        string[] expected =
            [$"Keep answers short. Today is {before}.", $"Keep answers short. Today is {after}."];
        Assert.Contains(Assert.Single(_sentInstructions), expected);
    }

    [Fact]
    public async Task Date_in_message_stores_the_date_in_the_session_history()
    {
        AIAgent agent = _chatClient.AsAIAgent(name: "kitchen-assistant");
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);

        string before = Today();
        await Alternatives.DateInMessage(agent, session, "Dinner tonight?");
        string after = Today();

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        string[] expected =
            [$"Today is {before}. Dinner tonight?", $"Today is {after}. Dinner tonight?"];
        Assert.Contains(history[0].Text, expected);
    }
}
