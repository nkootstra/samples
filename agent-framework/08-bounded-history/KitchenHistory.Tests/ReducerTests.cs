using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenHistory.Tests;

// The chat reducers from Program.cs, with a stubbed chat client in place of Anthropic.
public sealed class ReducerTests : Kitchen
{
    ChatClientAgent WithReducer(
        IChatReducer reducer,
        InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent trigger =
            InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent.BeforeMessagesRetrieval) =>
        ChatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "kitchen-assistant",
            ChatOptions = new ChatOptions { Instructions = Instructions },
            ChatHistoryProvider = new InMemoryChatHistoryProvider(new()
            {
                ChatReducer = reducer,
                ReducerTriggerEvent = trigger,
            }),
        });

    [Fact]
    public async Task Without_a_reducer_the_last_turn_resends_the_whole_conversation()
    {
        AIAgent agent = ChatClient.AsAIAgent(
            name: "kitchen-assistant", instructions: Instructions);

        await RunScriptAsync(agent);

        Assert.Equal(11, SentTexts[^1].Length);
    }

    [Fact]
    public async Task A_counting_reducer_sends_the_last_four_messages_and_the_new_question()
    {
        await RunScriptAsync(WithReducer(new MessageCountingChatReducer(4)));

        string[] expected =
        [
            Script[3], "Answer 4.", Script[4], "Answer 5.", Script[5],
        ];
        Assert.Equal(expected, SentTexts[^1]);
    }

    [Fact]
    public async Task A_counting_reducer_first_cuts_on_the_fourth_turn()
    {
        await RunScriptAsync(WithReducer(new MessageCountingChatReducer(4)), turns: 4);

        Assert.Equal([5, 5], SentTexts[2..].Select(texts => texts.Length));
    }

    [Fact]
    public async Task By_default_the_session_keeps_the_limit_plus_the_last_turn()
    {
        AgentSession session = await RunScriptAsync(
            WithReducer(new MessageCountingChatReducer(4)));

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        Assert.Equal(
            [Script[3], "Answer 4.", Script[4], "Answer 5.", Script[5], "Answer 6."],
            history.Select(m => m.Text));
    }

    [Fact]
    public async Task After_message_added_the_session_keeps_only_the_limit()
    {
        AgentSession session = await RunScriptAsync(WithReducer(
            new MessageCountingChatReducer(4),
            InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent.AfterMessageAdded));

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        Assert.Equal(
            [Script[4], "Answer 5.", Script[5], "Answer 6."],
            history.Select(m => m.Text));
    }

    [Fact]
    public async Task A_counting_reducer_drops_tool_calls_and_results_even_the_latest()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.User, "What's in the pantry?"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_pantry")]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "rice, eggs")]),
            new(ChatRole.Assistant, "Rice and eggs."),
        ];

        IEnumerable<ChatMessage> reduced = await new MessageCountingChatReducer(4)
            .ReduceAsync(messages, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["What's in the pantry?", "Rice and eggs."],
            reduced.Select(m => m.Text));
    }

    [Fact]
    public async Task A_summarizing_reducer_sends_its_prompt_last_as_a_system_message()
    {
        List<ChatMessage[]> summaryRequests = [];
        IChatClient summarizer = Summarizer(summaryRequests);

        await RunScriptAsync(WithReducer(new SummarizingChatReducer(summarizer, 4, 2)), turns: 5);

        ChatMessage[] request = Assert.Single(summaryRequests);
        Assert.Equal(
            [ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.Assistant, ChatRole.System],
            request.Select(m => m.Role));
    }

    [Fact]
    public async Task A_summarizing_reducer_stores_the_summary_as_the_first_message()
    {
        IChatClient summarizer = Summarizer();

        AgentSession session = await RunScriptAsync(
            WithReducer(new SummarizingChatReducer(summarizer, 4, 2)), turns: 5);

        Assert.True(session.TryGetInMemoryChatHistory(out List<ChatMessage>? history));
        Assert.Equal((ChatRole.Assistant, "Summary."), (history[0].Role, history[0].Text));
    }

    [Fact]
    public async Task A_summarizing_reducer_keeps_a_tool_call_with_its_result()
    {
        IChatClient summarizer = Summarizer();
        ChatMessage[] messages =
        [
            new(ChatRole.User, "Our son doesn't eat mushrooms."),
            new(ChatRole.Assistant, "Noted."),
            new(ChatRole.User, "What's in the pantry?"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_pantry")]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "rice, eggs")]),
            new(ChatRole.Assistant, "Rice and eggs."),
        ];

        // A target of 2 would cut between the call and its result. The cut
        // moves back to keep the pair, and the question goes into the summary.
        ChatMessage[] reduced =
        [
            .. await new SummarizingChatReducer(summarizer, 2, 0)
                .ReduceAsync(messages, TestContext.Current.CancellationToken),
        ];

        Assert.Equal("Summary.", reduced[0].Text);
        Assert.Equal(messages[3..], reduced[1..]);
    }
}
