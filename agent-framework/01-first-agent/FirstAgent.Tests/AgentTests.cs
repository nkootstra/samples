using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace FirstAgent.Tests;

// The agent from Program.cs, with a stubbed chat client in place of Anthropic.
public sealed class AgentTests
{
    const string Instructions = """
        You help one household decide what to cook.
        Answer in at most three sentences.
        """;

    readonly IChatClient _chatClient = Substitute.For<IChatClient>();

    ChatClientAgent KitchenAssistant() =>
        _chatClient.AsAIAgent(name: "kitchen-assistant", instructions: Instructions);

    void ModelAnswers(ChatResponse response) =>
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

    [Fact]
    public async Task Instructions_go_out_with_every_request()
    {
        ModelAnswers(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fried rice.")));
        AIAgent agent = KitchenAssistant();

        await agent.RunAsync(
            "What can I cook tonight with rice, eggs and an onion?",
            cancellationToken: TestContext.Current.CancellationToken);
        await agent.RunAsync(
            "And tomorrow?",
            cancellationToken: TestContext.Current.CancellationToken);

        _ = _chatClient.Received(2).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Is<ChatOptions?>(options => options != null && options.Instructions == Instructions),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_answer_cut_off_at_the_token_limit_keeps_its_text_and_reports_length()
    {
        ModelAnswers(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Sauté the onion, then"))
        {
            FinishReason = ChatFinishReason.Length,
        });

        AgentResponse response = await KitchenAssistant().RunAsync(
            "Give me a full recipe.", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ChatFinishReason.Length, response.FinishReason);
        Assert.Equal("Sauté the onion, then", response.Text);
    }

    [Fact]
    public async Task Streamed_fragments_combine_into_one_response_with_its_usage()
    {
        _chatClient
            .GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponseUpdate[]
            {
                new(ChatRole.Assistant, "Try a classic cr"),
                new(ChatRole.Assistant, "ème brûlée."),
                new() { Contents = [new UsageContent(new UsageDetails { OutputTokenCount = 77 })] },
            }.ToAsyncEnumerable());

        List<AgentResponseUpdate> updates = [];
        await foreach (AgentResponseUpdate update in KitchenAssistant().RunStreamingAsync(
            "Suggest one dessert that uses eggs.", cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }
        AgentResponse streamed = updates.ToAgentResponse();

        Assert.Equal("Try a classic crème brûlée.", streamed.Text);
        Assert.Equal(77, streamed.Usage?.OutputTokenCount);
    }
}
