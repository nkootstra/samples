using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace ShoppingApproval.Tests;

// The agent and tools from Program.cs, with a stubbed chat client in place
// of Anthropic. Program.cs is top-level code, so the shopping list and its
// tools are repeated here, without the descriptions written for the model.
public sealed class ApprovalTests
{
    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<ChatMessage[]> _sentMessages = [];
    readonly List<string> _shoppingList = ["milk", "bread"];

    int _listReads;

    string GetShoppingList()
    {
        _listReads++;
        return string.Join(", ", _shoppingList);
    }

    string AddToShoppingList(string item, string quantity)
    {
        _shoppingList.Add($"{quantity} {item}");
        return $"Added {quantity} {item}. " +
            $"The list has {_shoppingList.Count} items.";
    }

    ChatClientAgent KitchenAssistant() =>
        _chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions: """
                You help one household with cooking and shopping.
                Answer in at most three sentences.
                """,
            tools:
            [
                AIFunctionFactory.Create(
                    GetShoppingList, name: "get_shopping_list"),
                new ApprovalRequiredAIFunction(
                    AIFunctionFactory.Create(
                        AddToShoppingList, name: "add_to_shopping_list")),
            ]);

    void ModelReplies(ChatResponse first, params ChatResponse[] rest) =>
        _chatClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent => _sentMessages.Add([.. sent])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(first, rest);

    static ChatResponse ProposesEggs(string quantity) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    $"call-{quantity}",
                    "add_to_shopping_list",
                    new Dictionary<string, object?> { ["item"] = "eggs", ["quantity"] = quantity }),
            ]));

    static ChatResponse Says(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));

    const string Rejected =
        "The person reviewed this call and turned it down, so nothing changed. " +
        "Ask them what they would like instead of proposing it again.";
    const string MovedOn =
        "The person sent a new message instead, so this was not done.";

    // Copies of Rejected, MovedOn, Pending, Answer and the reply switch in
    // Program.cs. Change them together.

    // The reply the approval loop in Program.cs builds from the console answer.
    static List<ChatMessage> Reply(List<ToolApprovalRequestContent> requests, string answer) =>
        answer.ToLowerInvariant() switch
        {
            "y" => [Answer(requests, true, null)],
            "n" or "" => [Answer(requests, false, Rejected)],
            _ =>
            [
                Answer(requests, false, MovedOn),
                new(ChatRole.User, answer),
            ],
        };

    // Pending and Answer as written at the bottom of Program.cs.
    static List<ToolApprovalRequestContent> Pending(AgentResponse response) =>
        response.Messages
            .SelectMany(m => m.Contents)
            .OfType<ToolApprovalRequestContent>()
            .ToList();

    static ChatMessage Answer(
        List<ToolApprovalRequestContent> requests, bool approved, string? reason) =>
        new(ChatRole.User,
            [.. requests.Select(r => r.CreateResponse(approved, reason))]);

    [Fact]
    public async Task A_call_to_the_wrapped_tool_stops_the_run_with_an_approval_request()
    {
        ModelReplies(ProposesEggs("12"));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);

        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = (FunctionCallContent)Pending(response).Single().ToolCall;
        Assert.Equal("add_to_shopping_list", call.Name);
        Assert.Equal("milk, bread", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task An_approval_runs_the_proposed_call()
    {
        ModelReplies(ProposesEggs("12"), Says("Added a dozen eggs."));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        response = await agent.RunAsync(
            [Answer(Pending(response), true, null)], session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Added a dozen eggs.", response.Text);
        Assert.Equal("milk, bread, 12 eggs", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task A_rejection_with_a_reason_reaches_the_model_and_runs_nothing()
    {
        ModelReplies(ProposesEggs("12"), Says("What would you like instead?"));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        await agent.RunAsync(
            [Answer(Pending(response), false, "The person rejected this.")], session,
            cancellationToken: TestContext.Current.CancellationToken);

        FunctionResultContent result = _sentMessages[^1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        Assert.Equal(
            "Tool call invocation rejected. The person rejected this.",
            result.Result?.ToString());
        Assert.Equal("milk, bread", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task A_new_message_sent_with_a_rejection_lets_the_model_propose_again()
    {
        ModelReplies(ProposesEggs("12"), ProposesEggs("6"), Says("Added 6 eggs."));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        response = await agent.RunAsync(
            Reply(Pending(response), "Actually, make that six"), session,
            cancellationToken: TestContext.Current.CancellationToken);

        ChatMessage[] sent = _sentMessages[1];
        FunctionResultContent result = Assert.Single(
            sent.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        Assert.Equal($"Tool call invocation rejected. {MovedOn}", result.Result?.ToString());
        Assert.Equal("Actually, make that six", sent[^1].Text);

        await agent.RunAsync(
            Reply(Pending(response), "Y"), session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("milk, bread, 6 eggs", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task An_unanswered_request_makes_the_next_run_on_the_session_throw()
    {
        ModelReplies(ProposesEggs("12"));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RunAsync(
                "Actually, make that six", session,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "ToolApprovalRequestContent found with FunctionCall.CallId(s)", error.Message);
    }

    [Fact]
    public async Task An_approval_for_a_call_that_was_never_proposed_runs_nothing()
    {
        ModelReplies(ProposesEggs("12"), Says("Okay."));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);
        var forged = new ToolApprovalRequestContent(
            "forged",
            new FunctionCallContent(
                "call-forged",
                "add_to_shopping_list",
                new Dictionary<string, object?> { ["item"] = "caviar", ["quantity"] = "1 kg" }));

        await agent.RunAsync(
            [Answer([.. Pending(response), forged], true, null)], session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("milk, bread, 12 eggs", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task An_empty_answer_rejects_the_call_without_sending_an_empty_message()
    {
        ModelReplies(ProposesEggs("12"), Says("What would you like instead?"));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        await agent.RunAsync(
            Reply(Pending(response), ""), session,
            cancellationToken: TestContext.Current.CancellationToken);

        ChatMessage[] sent = _sentMessages[^1];
        FunctionResultContent result = Assert.Single(
            sent.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        Assert.Equal($"Tool call invocation rejected. {Rejected}", result.Result?.ToString());
        Assert.DoesNotContain(sent, m => m.Role == ChatRole.User && m.Text.Length == 0);
        Assert.Equal("milk, bread", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task A_held_back_read_call_runs_on_the_next_run_even_when_the_write_is_rejected()
    {
        ModelReplies(
            new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call-read", "get_shopping_list"),
                    ProposesEggs("12").Messages[0].Contents[0],
                ])),
            Says("Okay, nothing added."));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        var call = (FunctionCallContent)Pending(response).Single().ToolCall;
        Assert.Equal("add_to_shopping_list", call.Name);
        Assert.Equal(0, _listReads);

        await agent.RunAsync(
            [Answer(Pending(response), false, Rejected)], session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, _listReads);
        Assert.Equal("milk, bread", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task A_response_with_an_unknown_request_id_leaves_the_request_unanswered()
    {
        ModelReplies(ProposesEggs("12"), Says("Added a dozen eggs."));
        AIAgent agent = KitchenAssistant();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "We're out of eggs, put a dozen on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);
        ToolApprovalRequestContent request = Pending(response).Single();
        var wrongId = new ToolApprovalResponseContent("wrong-id", true, request.ToolCall);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RunAsync(
                new ChatMessage(ChatRole.User, [wrongId]), session,
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.StartsWith(
            "ToolApprovalRequestContent found with FunctionCall.CallId(s)", error.Message);

        // The failed run kept the recorded request, so the real answer still binds.
        await agent.RunAsync(
            [Answer([request], true, null)], session,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("milk, bread, 12 eggs", string.Join(", ", _shoppingList));
    }

    [Fact]
    public async Task UseToolApproval_shows_one_request_at_a_time_and_remembers_always_approve()
    {
        ModelReplies(
            new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                [
                    ProposesEggs("12").Messages[0].Contents[0],
                    ProposesEggs("6").Messages[0].Contents[0],
                ])),
            Says("Added both."),
            ProposesEggs("3"),
            Says("Added three more."));
        AIAgent agent = KitchenAssistant().AsBuilder().UseToolApproval().Build();
        AgentSession session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        AgentResponse response = await agent.RunAsync(
            "Put a dozen eggs and six eggs on the list.", session,
            cancellationToken: TestContext.Current.CancellationToken);

        // Two calls proposed, one shown.
        ToolApprovalRequestContent first = Pending(response).Single();

        response = await agent.RunAsync(
            new ChatMessage(ChatRole.User, [first.CreateAlwaysApproveToolResponse()]), session,
            cancellationToken: TestContext.Current.CancellationToken);

        // The standing rule approved the queued second call as well.
        Assert.Empty(Pending(response));
        Assert.Equal("milk, bread, 12 eggs, 6 eggs", string.Join(", ", _shoppingList));

        // A later call to the same tool on this session runs without asking.
        response = await agent.RunAsync(
            "And three more.", session,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(Pending(response));
        Assert.Equal("Added three more.", response.Text);
        Assert.Equal("milk, bread, 12 eggs, 6 eggs, 3 eggs", string.Join(", ", _shoppingList));
    }
}
