using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenPlan.Tests;

// RunAsync<DinnerPlan> on an agent like the one in Program.cs, with a stubbed
// chat client in place of Anthropic that returns a scripted reply.
public sealed class StructuredOutputTests
{
    const string Plan = """
        {"dinners":[{"day":"Monday","dish":"Salmon fried rice","minutes":20,
        "mainIngredients":["salmon","rice"]}]}
        """;

    readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    readonly List<ChatOptions?> _sentOptions = [];

    void Reply(string text, ChatFinishReason finishReason) =>
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatOptions?>(_sentOptions.Add),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                FinishReason = finishReason,
            });

    Task<AgentResponse<DinnerPlan>> AskForPlan() =>
        _chatClient
            .AsAIAgent(name: "planner", instructions: "You plan dinners.")
            .RunAsync<DinnerPlan>(
                "Dinner for Monday.",
                cancellationToken: TestContext.Current.CancellationToken);

    JsonElement SentSchema()
    {
        var format = Assert.IsType<ChatResponseFormatJson>(
            Assert.Single(_sentOptions)?.ResponseFormat);
        return Assert.NotNull(format.Schema);
    }

    static JsonElement MinutesSchema(JsonElement schema) =>
        schema.GetProperty("properties").GetProperty("dinners")
            .GetProperty("items").GetProperty("properties").GetProperty("minutes");

    [Fact]
    public async Task The_reply_is_read_into_the_record()
    {
        Reply(Plan, ChatFinishReason.Stop);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Dinner dinner = Assert.Single(response.Result.Dinners);
        Assert.Equal(
            (DayOfWeek.Monday, "Salmon fried rice", 20, "salmon, rice"),
            (dinner.Day, dinner.Dish, dinner.Minutes, string.Join(", ", dinner.MainIngredients)));
    }

    [Fact]
    public async Task The_request_carries_a_json_schema_named_after_the_record()
    {
        Reply(Plan, ChatFinishReason.Stop);

        await AskForPlan();

        var format = Assert.IsType<ChatResponseFormatJson>(
            Assert.Single(_sentOptions)?.ResponseFormat);
        Assert.Equal(
            ("DinnerPlan", "Dinners for the days the household asked about."),
            (format.SchemaName, format.SchemaDescription));
    }

    [Fact]
    public async Task The_schema_Program_prints_is_the_one_RunAsync_sends()
    {
        Reply(Plan, ChatFinishReason.Stop);

        await AskForPlan();

        ChatResponseFormatJson printed = ChatResponseFormat.ForJsonSchema<DinnerPlan>(
            AgentAbstractionsJsonUtilities.DefaultOptions);
        Assert.True(JsonElement.DeepEquals(Assert.NotNull(printed.Schema), SentSchema()));
    }

    [Fact]
    public async Task Property_names_in_the_schema_are_camel_case()
    {
        Reply(Plan, ChatFinishReason.Stop);

        await AskForPlan();

        JsonElement dinner = SentSchema().GetProperty("properties").GetProperty("dinners")
            .GetProperty("items");
        string[] names = ["day", "dish", "minutes", "mainIngredients"];
        Assert.Equal(names, dinner.GetProperty("properties").EnumerateObject().Select(p => p.Name));
        Assert.Equal(names, dinner.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task The_schema_requires_dinners_at_the_top_level()
    {
        Reply(Plan, ChatFinishReason.Stop);

        await AskForPlan();

        Assert.Equal(
            ["dinners"],
            SentSchema().GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    // The class style from the article: Anthropic's client drops a format
    // whose schema has no top-level required list.
    sealed class ClassStylePlan
    {
        public List<Dinner> Dinners { get; init; } = [];
    }

    [Fact]
    public void A_class_with_a_settable_list_has_no_required_list_in_its_schema()
    {
        ChatResponseFormatJson format = ChatResponseFormat.ForJsonSchema<ClassStylePlan>(
            AgentAbstractionsJsonUtilities.DefaultOptions);

        JsonElement schema = Assert.NotNull(format.Schema);
        Assert.True(schema.GetProperty("properties").TryGetProperty("dinners", out _));
        Assert.False(schema.TryGetProperty("required", out _));
    }

    [Fact]
    public async Task Descriptions_on_the_properties_reach_the_schema()
    {
        Reply(Plan, ChatFinishReason.Stop);

        await AskForPlan();

        Assert.Equal(
            "Minutes from start to plate.",
            MinutesSchema(SentSchema()).GetProperty("description").GetString());
    }

    [Fact]
    public async Task The_range_attribute_becomes_minimum_and_maximum_in_the_schema()
    {
        Reply(Plan, ChatFinishReason.Stop);

        await AskForPlan();

        JsonElement minutes = MinutesSchema(SentSchema());
        Assert.Equal(
            (1, 240),
            (minutes.GetProperty("minimum").GetInt32(), minutes.GetProperty("maximum").GetInt32()));
    }

    [Fact]
    public async Task Minutes_outside_the_range_are_read_without_complaint()
    {
        Reply(Plan.Replace("\"minutes\":20", "\"minutes\":500", StringComparison.Ordinal),
            ChatFinishReason.Stop);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Assert.Equal(500, Assert.Single(response.Result.Dinners).Minutes);
    }

    [Fact]
    public async Task A_reply_cut_off_at_the_token_limit_throws_a_json_exception_on_result()
    {
        Reply(Plan[..60], ChatFinishReason.Length);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Assert.Equal(ChatFinishReason.Length, response.FinishReason);
        Assert.Throws<JsonException>(() => response.Result);
    }

    [Fact]
    public async Task An_empty_reply_throws_an_invalid_operation_exception_on_result()
    {
        Reply("", ChatFinishReason.Length);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        var ex = Assert.Throws<InvalidOperationException>(() => response.Result);
        Assert.Equal("The response did not contain JSON to be deserialized.", ex.Message);
    }

    [Fact]
    public async Task A_reply_of_null_throws_an_invalid_operation_exception_on_result()
    {
        Reply("null", ChatFinishReason.Stop);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        var ex = Assert.Throws<InvalidOperationException>(() => response.Result);
        Assert.Equal("The deserialized response is null.", ex.Message);
    }

    [Fact]
    public async Task Day_names_are_read_in_any_capitalization()
    {
        Reply(Plan.Replace("\"Monday\"", "\"monday\"", StringComparison.Ordinal),
            ChatFinishReason.Stop);

        AgentResponse<DinnerPlan> response = await AskForPlan();

        Assert.Equal(DayOfWeek.Monday, Assert.Single(response.Result.Dinners).Day);
    }
}
