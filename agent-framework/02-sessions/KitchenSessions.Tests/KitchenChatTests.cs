using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenSessions.Tests;

public sealed class KitchenChatTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<string?[]> _sentTexts = [];
    readonly InMemorySessionStore _store = new();

    ChatClientAgent KitchenAssistant() =>
        _chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions: "You help one household decide what to cook.");

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
    public async Task A_started_conversation_belongs_to_the_user_who_started_it()
    {
        ModelAnswers("Fried rice.");
        AIAgent agent = KitchenAssistant();

        string sessionId = await KitchenChat.StartAsync(agent, _store, "alice");

        Assert.Equal("Fried rice.", await KitchenChat.ReplyAsync(agent, _store, "alice", sessionId, "Dinner?"));
        Assert.Null(await KitchenChat.ReplyAsync(agent, _store, "mallory", sessionId, "Dinner?"));
    }

    [Fact]
    public async Task Each_start_gets_a_new_id()
    {
        AIAgent agent = KitchenAssistant();

        string first = await KitchenChat.StartAsync(agent, _store, "alice");
        string second = await KitchenChat.StartAsync(agent, _store, "alice");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task The_owner_gets_the_reply()
    {
        ModelAnswers("Fried rice.");
        AIAgent agent = KitchenAssistant();
        await _store.AddEmptySessionAsync(agent, "s1", ownerId: "alice");

        string? reply = await KitchenChat.ReplyAsync(agent, _store, "alice", "s1", "Dinner?");

        Assert.Equal("Fried rice.", reply);
    }

    [Fact]
    public async Task Another_users_session_is_not_found_and_never_reaches_the_model()
    {
        AIAgent agent = KitchenAssistant();
        await _store.AddEmptySessionAsync(agent, "s1", ownerId: "alice");

        string? reply = await KitchenChat.ReplyAsync(agent, _store, "mallory", "s1", "Dinner?");

        Assert.Null(reply);
        _ = _chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_session_is_not_found()
    {
        string? reply = await KitchenChat.ReplyAsync(
            KitchenAssistant(), _store, "alice", "s1", "Dinner?");

        Assert.Null(reply);
    }

    [Fact]
    public async Task The_next_reply_continues_the_stored_conversation()
    {
        ModelAnswers("Fried rice.", "A tortilla.");
        AIAgent agent = KitchenAssistant();
        await _store.AddEmptySessionAsync(agent, "s1", ownerId: "alice");

        await KitchenChat.ReplyAsync(agent, _store, "alice", "s1", "Dinner tonight?");
        await KitchenChat.ReplyAsync(agent, _store, "alice", "s1", "And tomorrow?");

        string?[] expected =
        [
            "Dinner tonight?",
            "Fried rice.",
            "And tomorrow?",
        ];
        Assert.Equal(expected, _sentTexts[1]);
    }

    [Fact]
    public async Task The_saved_session_keeps_its_owner()
    {
        ModelAnswers("Fried rice.");
        AIAgent agent = KitchenAssistant();
        await _store.AddEmptySessionAsync(agent, "s1", ownerId: "alice");

        await KitchenChat.ReplyAsync(agent, _store, "alice", "s1", "Dinner?");

        Assert.Equal("alice", (await _store.FindAsync("s1"))?.OwnerId);
    }
}
