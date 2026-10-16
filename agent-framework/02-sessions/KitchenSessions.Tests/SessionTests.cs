using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenSessions.Tests;

// What Program.cs relies on, with a stubbed chat client in place of Anthropic.
public sealed class SessionTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<string?[]> _sentTexts = [];

    ChatClientAgent KitchenAssistant() =>
        _chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions:
                "You help one household decide what to cook. "
                + "Answer in at most three sentences.");

    void ModelAnswers(string first, params string[] rest) =>
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent =>
                    _sentTexts.Add([.. sent.Select(m => m.Text)])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Answer(first),
                [.. rest.Select(Answer)]);

    static ChatResponse Answer(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));

    [Fact]
    public async Task Without_a_session_the_follow_up_arrives_alone()
    {
        ModelAnswers("Fried rice.", "Tell me more.");
        AIAgent agent = KitchenAssistant();

        await agent.RunAsync(
            "What can I cook tonight with rice, eggs and an onion?",
            cancellationToken: TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "And tomorrow?",
            cancellationToken: TestContext.Current.CancellationToken);

        string?[] expected = ["And tomorrow?"];
        Assert.Equal(expected, _sentTexts[1]);
    }

    [Fact]
    public async Task With_a_session_every_turn_resends_the_earlier_ones()
    {
        ModelAnswers("Fried rice.", "A tortilla.");
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);

        await agent.RunAsync(
            "What can I cook tonight with rice, eggs and an onion?", session,
            cancellationToken: TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "And tomorrow?", session,
            cancellationToken: TestContext.Current.CancellationToken);

        string?[] expected =
        [
            "What can I cook tonight with rice, eggs and an onion?",
            "Fried rice.",
            "And tomorrow?",
        ];
        Assert.Equal(expected, _sentTexts[1]);
    }

    [Fact]
    public async Task The_history_holds_the_messages_but_not_the_instructions()
    {
        ModelAnswers("Fried rice.");
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);

        await agent.RunAsync(
            "What can I cook tonight?", session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        string[] expected = ["user: What can I cook tonight?", "assistant: Fried rice."];
        Assert.Equal(expected, history.Select(m => $"{m.Role}: {m.Text}").ToArray());
    }

    [Fact]
    public async Task A_session_saved_as_json_and_restored_carries_the_conversation_on()
    {
        ModelAnswers("Fried rice.", "Rice, eggs and an onion.");
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "I have rice, eggs and an onion.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        JsonElement saved = await agent.SerializeSessionAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);
        AgentSession restored = await agent.DeserializeSessionAsync(
            JsonElement.Parse(saved.GetRawText()),
            cancellationToken: TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "Remind me what I had in the cupboard.", restored,
            cancellationToken: TestContext.Current.CancellationToken);

        string?[] expected =
        [
            "I have rice, eggs and an onion.",
            "Fried rice.",
            "Remind me what I had in the cupboard.",
        ];
        Assert.Equal(expected, _sentTexts[1]);
    }

    [Fact]
    public async Task The_saved_session_holds_the_conversation_in_plain_json()
    {
        ModelAnswers("Fried rice.");
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "I have rice, eggs and an onion.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        JsonElement saved = await agent.SerializeSessionAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("I have rice, eggs and an onion.", saved.GetRawText());
    }
}
