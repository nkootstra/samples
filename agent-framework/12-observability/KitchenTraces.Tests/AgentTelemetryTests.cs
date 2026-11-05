using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace KitchenTraces.Tests;

// The kitchen agent from Program.cs, wrapped with UseOpenTelemetry as there,
// on a stubbed chat client in place of Anthropic.
[Collection(nameof(RecordsSpans))]
public sealed class AgentTelemetryTests : IDisposable
{
    const string SourceName = "Kitchen";
    const string Question = "What can I cook tonight?";

    readonly IChatClient _model = Substitute.For<IChatClient>();
    readonly RecordedSpans _spans = new(SourceName, "Experimental.Microsoft.Agents.AI");

    public AgentTelemetryTests() =>
        _model
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Answer(new FunctionCallContent("call-1", "list_pantry")),
                Answer(new TextContent("Make a spinach and feta frittata.")));

    static ChatResponse Answer(AIContent content) =>
        new(new ChatMessage(ChatRole.Assistant, [content]))
        {
            Usage = new UsageDetails { InputTokenCount = 400, OutputTokenCount = 30 },
        };

    [Description("Lists what is in the household's pantry.")]
    static string ListPantry() => "rice, eggs, onions, spinach, feta";

    ChatClientAgent KitchenAssistant() => _model.AsAIAgent(
        name: "kitchen-assistant",
        instructions: "You help one household decide what to cook.",
        tools: [AIFunctionFactory.Create(ListPantry, name: "list_pantry")]);

    static AIAgent Traced(AIAgent agent, bool sensitiveData = false) => agent
        .AsBuilder()
        .UseOpenTelemetry(
            SourceName,
            configure: otel => otel.EnableSensitiveData = sensitiveData)
        .Build();

    static async Task RunAsync(AIAgent agent) =>
        await agent.RunAsync(Question, cancellationToken: TestContext.Current.CancellationToken);

    public void Dispose() => _spans.Dispose();

    [Fact]
    public async Task Model_calls_and_tool_calls_are_spans_under_the_agent_span()
    {
        await RunAsync(Traced(KitchenAssistant()));

        Activity agentSpan = _spans.Named("invoke_agent kitchen-assistant(");
        string[] expected = ["chat", "execute_tool list_pantry", "chat"];
        Assert.Equal(
            expected,
            _spans.All
                .Where(span => span.ParentSpanId == agentSpan.SpanId)
                .OrderBy(span => span.StartTimeUtc)
                .Select(span => span.DisplayName));
    }

    [Fact]
    public async Task The_agent_span_carries_the_token_counts_of_all_its_model_calls()
    {
        await RunAsync(Traced(KitchenAssistant()));

        Activity agentSpan = _spans.Named("invoke_agent kitchen-assistant(");
        Assert.Equal(800, agentSpan.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.Equal(60, agentSpan.GetTagItem("gen_ai.usage.output_tokens"));
    }

    [Fact]
    public async Task Each_model_call_span_carries_its_own_token_counts()
    {
        await RunAsync(Traced(KitchenAssistant()));

        Assert.All(
            _spans.All.Where(span => span.DisplayName == "chat"),
            span => Assert.Equal(400, span.GetTagItem("gen_ai.usage.input_tokens")));
    }

    [Fact]
    public async Task Without_sensitive_data_no_message_or_tool_result_is_recorded()
    {
        await RunAsync(Traced(KitchenAssistant(), sensitiveData: false));

        Assert.DoesNotContain(
            _spans.All.SelectMany(span => span.TagObjects),
            tag => tag.Key is "gen_ai.input.messages" or "gen_ai.output.messages"
                or "gen_ai.system_instructions" or "gen_ai.tool.call.result");
    }

    [Fact]
    public async Task With_sensitive_data_the_question_and_the_tool_result_are_recorded()
    {
        await RunAsync(Traced(KitchenAssistant(), sensitiveData: true));

        Assert.Contains(Question, (string?)_spans.Named("invoke_agent").GetTagItem("gen_ai.input.messages"));
        Assert.Contains(
            "spinach",
            (string?)_spans.Named("execute_tool").GetTagItem("gen_ai.tool.call.result"));
    }

    [Fact]
    public async Task Without_a_source_name_the_spans_go_to_Experimental_Microsoft_Agents_AI()
    {
        await RunAsync(KitchenAssistant().AsBuilder().UseOpenTelemetry().Build());

        Assert.Equal(
            "Experimental.Microsoft.Agents.AI",
            _spans.Named("invoke_agent").Source.Name);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void With_the_capture_variable_set_only_an_explicit_false_keeps_content_out(
        bool? configured, bool expected)
    {
        Assert.Equal("true", Environment.GetEnvironmentVariable(CaptureVariable.Name));

        AIAgent agent = KitchenAssistant()
            .AsBuilder()
            .UseOpenTelemetry(
                SourceName,
                configure: otel =>
                {
                    if (configured is { } value)
                    {
                        otel.EnableSensitiveData = value;
                    }
                })
            .Build();

        Assert.Equal(expected, agent.GetService<OpenTelemetryAgent>()?.EnableSensitiveData);
    }

    [Fact]
    public async Task The_token_counts_reach_a_meter_named_after_the_source()
    {
        List<(int Tokens, string? Type)> recorded = [];
        using MeterListener listener = new()
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == SourceName
                    && instrument.Name == "gen_ai.client.token.usage")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, tags, _) =>
        {
            string? type = null;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "gen_ai.token.type")
                {
                    type = tag.Value as string;
                }
            }
            lock (recorded)
            {
                recorded.Add((value, type));
            }
        });
        listener.Start();

        await RunAsync(Traced(KitchenAssistant()));

        Assert.Contains((400, "input"), recorded);
        Assert.Contains((30, "output"), recorded);
    }
}
