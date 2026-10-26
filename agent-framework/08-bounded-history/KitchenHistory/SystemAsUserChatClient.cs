using Microsoft.Extensions.AI;

// Sends system messages as user messages at the end of the request.
// Wraps the summarizers' client only.
public sealed class SystemAsUserChatClient(IChatClient innerClient)
    : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(AsUser(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(
            AsUser(messages), options, cancellationToken);

    static List<ChatMessage> AsUser(IEnumerable<ChatMessage> messages) =>
    [
        .. messages.Where(message => message.Role != ChatRole.System),
        .. messages
            .Where(message => message.Role == ChatRole.System)
            .Select(message => new ChatMessage(ChatRole.User, message.Contents)),
    ];
}
