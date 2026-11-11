using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenSecurity.Tests;

// A stubbed chat client in place of Anthropic. It answers with the scripted
// responses in order and records every request it was sent.
sealed class ScriptedModel
{
    public IChatClient Client { get; } = Substitute.For<IChatClient>();

    public List<ChatMessage[]> Requests { get; } = [];

    public ScriptedModel(params ChatResponse[] responses) =>
        Client
            .GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(sent => Requests.Add([.. sent])),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(responses[0], responses[1..]);

    public static ChatResponse Calls(string tool, params (string Name, object? Value)[] arguments) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    $"call-{tool}",
                    tool,
                    arguments.ToDictionary(a => a.Name, a => a.Value)),
            ]));

    public static ChatResponse Says(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));
}
