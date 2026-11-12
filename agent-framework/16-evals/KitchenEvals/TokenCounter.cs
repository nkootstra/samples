using Microsoft.Extensions.AI;

// Adds up the tokens of every model call that reaches it.
sealed class TokenCounter(IChatClient inner) : DelegatingChatClient(inner)
{
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ChatResponse response = await base.GetResponseAsync(
            messages, options, cancellationToken);
        InputTokens += response.Usage?.InputTokenCount ?? 0;
        OutputTokens += response.Usage?.OutputTokenCount ?? 0;
        return response;
    }
}
