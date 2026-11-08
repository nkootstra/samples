using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenBench.Tests;

public sealed class BenchTests
{
    static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");

    [Fact]
    public async Task In_development_the_bench_serves_DevUI_to_a_local_browser()
    {
        await using var bench = new BenchFactory("Development", IPAddress.Loopback);
        using var client = bench.CreateClient();

        using var response = await client.GetAsync("/devui/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DevUI_lists_the_kitchen_assistant_with_its_two_tools()
    {
        await using var bench = new BenchFactory("Development", IPAddress.Loopback);
        using var client = bench.CreateClient();

        var entity = await GetEntityAsync(client, "kitchen-assistant");

        Assert.Equal("agent", entity.GetProperty("type").GetString());
        Assert.Equal(
            ["find_in_pantry", "list_expiring_soon"],
            entity.GetProperty("tools").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task DevUI_shows_the_agent_instructions_to_whoever_can_open_it()
    {
        await using var bench = new BenchFactory("Development", IPAddress.Loopback);
        using var client = bench.CreateClient();

        var entity = await GetEntityAsync(client, "kitchen-assistant");

        Assert.StartsWith(
            "You help one household decide what to cook.",
            entity.GetProperty("instructions").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DevUI_lists_the_dinner_review_workflow()
    {
        await using var bench = new BenchFactory("Development", IPAddress.Loopback);
        using var client = bench.CreateClient();

        var entity = await GetEntityAsync(client, "dinner-review");

        Assert.Equal("workflow", entity.GetProperty("type").GetString());
    }

    [Fact]
    public async Task DevUI_turns_away_a_caller_on_another_machine()
    {
        await using var bench = new BenchFactory("Development", Lan);
        using var client = bench.CreateClient();

        using var response = await client.GetAsync("/v1/entities", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DevUI_treats_a_request_without_a_remote_address_as_remote()
    {
        await using var bench = new BenchFactory("Development", remoteAddress: null);
        using var client = bench.CreateClient();

        using var response = await client.GetAsync("/v1/entities", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_responses_endpoint_runs_an_agent_for_a_caller_on_another_machine()
    {
        await using var bench = new BenchFactory("Development", Lan);
        StubAnswer(bench.ChatClient, "Spinach and feta frittata.");
        using var client = bench.CreateClient();

        var output = await CreateResponseAsync(client, "kitchen-assistant", "What should I cook tonight?");

        Assert.Equal("Spinach and feta frittata.", output);
    }

    [Fact]
    public async Task The_workflow_runs_through_the_responses_endpoint_without_AddAsAIAgent()
    {
        await using var bench = new BenchFactory("Development", IPAddress.Loopback);
        StubAnswer(bench.ChatClient, "Monday: salmon.");
        using var client = bench.CreateClient();

        var output = await CreateResponseAsync(client, "dinner-review", "Dinners for Monday.");

        Assert.Contains("Monday: salmon.", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/devui/")]
    [InlineData("/v1/entities")]
    [InlineData("/v1/conversations")]
    public async Task Outside_development_the_bench_maps_none_of_the_DevUI_routes(string path)
    {
        await using var bench = new BenchFactory("Production", IPAddress.Loopback);
        using var client = bench.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Outside_development_nobody_can_run_an_agent_over_HTTP()
    {
        await using var bench = new BenchFactory("Production", IPAddress.Loopback);
        using var client = bench.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/responses",
            new { input = "What should I cook tonight?", metadata = new { entity_id = "kitchen-assistant" } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    static void StubAnswer(IChatClient chatClient, string text)
    {
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        chatClient
            .GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ => new[] { new ChatResponseUpdate(ChatRole.Assistant, text) }.ToAsyncEnumerable());
    }

    static async Task<JsonElement> GetEntityAsync(HttpClient client, string id)
    {
        var listing = await client.GetFromJsonAsync<JsonElement>("/v1/entities", TestContext.Current.CancellationToken);
        return listing.GetProperty("entities").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == id);
    }

    static async Task<string> CreateResponseAsync(HttpClient client, string entityId, string input)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/responses",
            new { input, metadata = new { entity_id = entityId } },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return string.Concat(body.GetProperty("output").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "message")
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Select(part => part.GetProperty("text").GetString()));
    }
}
