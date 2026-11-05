# Part 12: Agent Framework OpenTelemetry tracing in C# with Claude

The kitchen assistant from part 3 and the planner-to-reviewer workflow from part 5, traced with OpenTelemetry. Every agent run, model call, tool call and workflow step becomes a span with its token counts: in the terminal with a plain `dotnet run`, or in the Aspire dashboard under `aspire run`. The app then turns the token counts into dollars with Claude Haiku 5.5's prices. This is the code from [Agent Framework OpenTelemetry tracing in C# with Claude](https://kootstra.io/writing/agent-framework-opentelemetry-tracing-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, only for the dashboard, with the key stored in user secrets as `Parameters:anthropic-api-key` under the ID `kootstra-samples`

## Run it in the terminal

From this folder:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenTraces
```

With no `OTEL_EXPORTER_OTLP_ENDPOINT` set, `Program.cs` adds the OpenTelemetry console exporter instead of the OTLP exporter, so every span is printed to the terminal as it ends, and the token metric when the meter provider exports (every 10 seconds and once more at exit). That is a lot of output: one run printed 28 spans and about 900 lines, 1,100 with `DOTNET_ENVIRONMENT=Development`. The app's own lines sit between the spans.

The first `chat` span of one run, without `DOTNET_ENVIRONMENT` set:

```text
Activity.TraceId:            c2bb31d567c3b1dc002405709252d3df
Activity.SpanId:             adf6d6fe538ffc24
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       5c2c875c0230768e
Activity.DisplayName:        chat claude-haiku-5-5
Activity.Kind:               Client
Activity.StartTime:          2026-10-15T10:09:02.6574430Z
Activity.Duration:           00:00:00.9885410
Activity.Tags:
    gen_ai.operation.name: chat
    gen_ai.request.model: claude-haiku-5-5
    gen_ai.provider.name: anthropic
    server.address: api.anthropic.com
    server.port: 443
    gen_ai.tool.definitions: [
  {
    "type": "function",
    "name": "list_pantry"
  }
]
    gen_ai.response.finish_reasons: ["tool_calls"]
    gen_ai.response.id: msg_011Cg3qehCvcSHFgNQvzMU9M
    gen_ai.response.model: claude-haiku-5-5
    gen_ai.usage.input_tokens: 410
    gen_ai.usage.output_tokens: 28
    gen_ai.usage.cache_read.input_tokens: 0
Instrumentation scope (ActivitySource):
    Name: Kitchen
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 1.19.1
    service.name: unknown_service:KitchenTraces
    Schema URL: https://opentelemetry.io/schemas/1.44.0
```

A span is printed when it ends, so the `invoke_agent` span comes after its `chat` and `execute_tool` spans. To see only the span names, the token counts and the app's cost lines, filter the output:

```sh
dotnet run --project KitchenTraces | grep -E 'DisplayName|usage\.(in|out)put_tokens|^\[|^Total'
```

With `DOTNET_ENVIRONMENT=Development` the spans also carry the messages, the instructions and the tool calls' arguments and results:

```sh
DOTNET_ENVIRONMENT=Development dotnet run --project KitchenTraces
```

## Run it with the Aspire dashboard

From this folder:

```sh
aspire run
```

The AppHost sets `OTEL_EXPORTER_OTLP_ENDPOINT` on the app, so the same spans go to the dashboard over OTLP instead of to the console. Open the dashboard link that `aspire run` prints. The app's output is on **Console logs**, resource `kitchen-traces`; the spans are on **Traces** and the token counts on **Metrics**, meter `Kitchen`. The app runs once and exits; stop the AppHost with `Ctrl+C`.

To read the output and the spans in the terminal instead, start the AppHost in the background, wait for the app to exit, and read its logs and spans before you stop the AppHost:

```sh
aspire start
aspire wait kitchen-traces --status down
aspire logs kitchen-traces
aspire otel traces kitchen-traces
aspire otel spans kitchen-traces
aspire stop
```

Reading the logs before the app has exited can return only part of its output. The spans live in the dashboard, so `aspire otel` has nothing to show after `aspire stop`.

The output of one run (the answer differs every time):

```text
You could make a spinach and feta frittata: sauté the onions, add the spinach, then pour in beaten eggs and crumble feta on top before cooking until set. For a heartier meal, serve it with rice on the side. Or make egg fried rice with the onions, spinach, and a little feta stirred in at the end.
[kitchen-assistant: 871 in, 206 out, finish: stop, $0.000190]
[planner: 85 in, 143 out, finish: stop, $0.000080]
[reviewer: 266 in, 1056 out, finish: stop, $0.000555]
Total: $0.000825
```

and the first spans of `aspire otel spans kitchen-traces`:

```text
20:20:49.923 OK     3.38s kitchen-traces: invoke_agent kitchen-assistant(fa177f6f66bf41fe8fb7504ac428e67e) 97bb2c6
20:20:49.949 OK     1.28s kitchen-traces: chat claude-haiku-5-5 ccec3a7
20:20:51.241 OK    3.12ms kitchen-traces: execute_tool list_pantry dae2ece
20:20:51.245 OK     2.05s kitchen-traces: chat claude-haiku-5-5 acf9502
[...]
```

The AppHost passes its own environment name to the app as `DOTNET_ENVIRONMENT`, so under `aspire run` and `aspire start` the app runs as `Development` and the spans include the messages, the instructions and the tool results. In any other environment the code sets `EnableSensitiveData` to `false`, which also overrides `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` wherever that variable is set. Aspire sets it for the app when the AppHost runs in Development.

## Notes

The prices in `Program.cs` are Claude Haiku 5.5's list prices for prompts up to 100,000 tokens, from [platform.claude.com/docs/en/about-claude/pricing](https://platform.claude.com/docs/en/about-claude/pricing) on 14 October 2026. Check that page before you rely on the dollar amounts.

`Program.cs` holds the article's code in the article's order, except for the two static local functions `ListPantry` and `Dollars`: the article shows each next to where it's used, the file keeps both at the end. Nothing from the article is left out.

## Run the tests

```sh
dotnet test
```

25 tests, no API calls:

- `ModelPriceTests` check the cost calculation with hand-worked amounts: a million tokens in and out, a short run, cache hits at the cache-hit price, cache writes at the cache-write price, and a response without usage costs nothing.
- `AgentTelemetryTests` wrap the kitchen agent with `UseOpenTelemetry` as `Program.cs` does, on a stubbed chat client, and record the spans with an `ActivityListener`: model calls and the tool call sit under the `invoke_agent` span, the agent span carries the summed token counts and each `chat` span its own, no message content is recorded unless sensitive data is on, without a source name the spans go to `Experimental.Microsoft.Agents.AI`, an explicit `false` overrides the capture variable, and the token counts reach the `gen_ai.client.token.usage` histogram on a meter named after the source.
- `WorkflowTelemetryTests` build the planner-to-reviewer workflow with traced agents: without `WithOpenTelemetry` there are no workflow spans, each agent span sits under its executor span, the run sits under one `workflow.session` span, `workflow.build` is a trace of its own, executor input is recorded only with sensitive data, and each agent's streamed updates add up to its own token counts.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY` and its environment as `DOTNET_ENVIRONMENT`.

## Files

| File | What it is |
| --- | --- |
| `KitchenTraces/Program.cs` | The tracer and meter providers (console or OTLP exporter), the traced agent and workflow, and the cost lines |
| `KitchenTraces/ModelPrice.cs` | Turns a `UsageDetails` into dollars |
| `KitchenTraces.AppHost/AppHost.cs` | Starts the app with the API key and the environment name |
| `KitchenTraces.Tests/` | Price, telemetry and AppHost tests with stubbed chat clients |

## Try it

- Remove the `configure` argument from `UseOpenTelemetry`, set `isDevelopment` to `false` and run again. The agent spans still contain the messages, because Aspire sets `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true` for the app; the workflow spans don't, because the workflow doesn't read that variable.
- Change the prices to another model's from the pricing page and compare the totals.
