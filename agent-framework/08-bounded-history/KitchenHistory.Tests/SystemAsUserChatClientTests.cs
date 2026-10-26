using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenHistory.Tests;

public sealed class SystemAsUserChatClientTests
{
    readonly IChatClient _innerClient = Substitute.For<IChatClient>();
    readonly List<ChatMessage[]> _sent = [];

    public SystemAsUserChatClientTests() =>
        _innerClient
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(messages => _sent.Add([.. messages])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "A summary.")));

    // The order SummarizingChatReducer sends: the conversation, then its prompt.
    static ChatMessage[] SummaryRequest() =>
    [
        new(ChatRole.User, "Our son doesn't eat mushrooms."),
        new(ChatRole.Assistant, "Make egg fried rice."),
        new(ChatRole.System, "Summarize the conversation."),
    ];

    [Fact]
    public async Task A_system_message_is_sent_as_a_user_message_with_the_same_text()
    {
        using var client = new SystemAsUserChatClient(_innerClient);

        await client.GetResponseAsync(
            SummaryRequest(), cancellationToken: TestContext.Current.CancellationToken);

        ChatMessage last = Assert.Single(_sent)[^1];
        Assert.Equal((ChatRole.User, "Summarize the conversation."), (last.Role, last.Text));
    }

    [Fact]
    public async Task A_leading_system_message_is_sent_last_as_a_user_message()
    {
        // The order SummarizationCompactionStrategy sends: its prompt, then the conversation.
        ChatMessage[] request =
        [
            new(ChatRole.System, "Summarize the conversation."),
            new(ChatRole.User, "Our son doesn't eat mushrooms."),
            new(ChatRole.Assistant, "Make egg fried rice."),
        ];
        using var client = new SystemAsUserChatClient(_innerClient);

        await client.GetResponseAsync(
            request, cancellationToken: TestContext.Current.CancellationToken);

        ChatMessage[] sent = Assert.Single(_sent);
        Assert.Equal(request[1..], sent[..2]);
        Assert.Equal((ChatRole.User, "Summarize the conversation."), (sent[^1].Role, sent[^1].Text));
    }

    [Fact]
    public async Task User_and_assistant_messages_are_sent_unchanged()
    {
        ChatMessage[] request = SummaryRequest();
        using var client = new SystemAsUserChatClient(_innerClient);

        await client.GetResponseAsync(
            request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(request[..2], Assert.Single(_sent)[..2]);
    }

    [Fact]
    public async Task A_streamed_request_gets_the_same_change()
    {
        List<ChatMessage[]> streamed = [];
        _innerClient
            .GetStreamingResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(messages => streamed.Add([.. messages])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<ChatResponseUpdate>());
        using var client = new SystemAsUserChatClient(_innerClient);

        await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync(
            SummaryRequest(), cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        Assert.Equal(ChatRole.User, Assert.Single(streamed)[^1].Role);
    }
}
