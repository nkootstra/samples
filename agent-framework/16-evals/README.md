# Part 16: Agent Framework LLM-as-a-judge evals in C# with Claude

A kitchen assistant suggests dinners from a small pantry. The app evaluates it three ways: checks in code through Agent Framework's `LocalEvaluator`, a judge model scoring answers written by hand (to find out what the judge can tell apart), and the same judge scoring the agent's own answers for relevance and for groundedness in the pantry. The judge is Claude Haiku 5.5, through the quality evaluators of `Microsoft.Extensions.AI.Evaluation`. This is the code from [Agent Framework LLM-as-a-judge evals in C# with Claude](https://kootstra.io/writing/agent-framework-llm-as-a-judge-evals-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **Aspire CLI**
- **An Anthropic API key**, stored in user secrets as `Parameters:anthropic-api-key` under the ID `kootstra-samples`

## Run it

From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-evals`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead:

```sh
aspire start
aspire wait kitchen-evals --status down
aspire logs kitchen-evals
aspire stop
```

One run, from an empty cache:

```text
Checks in code: 9/9 passed

Answers written by hand    relevance  groundedness
  from the pantry          4 4 4      5 5 4
  salmon                   4 4 4      3 3 2
  only the pantry          3 3 3      4 4 4
  20.2 s, judge tokens: 30579 in, 5244 out

The agent's answers        relevance  groundedness
What can I cook tonight?
                           4 4 4      5 5 5
I have 20 minutes. What's a quick vegetarian dinner?
                           4 5 4      4 5 5
Can I make lasagne tonight?
                           5 5 3      5 5 4
...
Passed: 8/9
Agent: 8823 in, 3182 out, $0.0025
Judge: 32497 in, 6633 out, $0.0066
```

The scores of the agent's answers change from run to run: they measure what two models decide. The judge's verdicts on the answers written by hand are cached on disk, under `kitchen-evals` in your temp folder, so from the second run on that part takes a fraction of a second, costs nothing and repeats the first run's scores. Delete that folder to judge them again. Cached entries expire after 14 days.

### Without Aspire

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenEvals
```

### An HTML report

Every judged answer written by hand is also stored as a result next to the cache. The `aieval` tool from `Microsoft.Extensions.AI.Evaluation.Console` turns the results into a report:

```sh
dotnet tool install --global Microsoft.Extensions.AI.Evaluation.Console --version 10.10.0
aieval report --path "${TMPDIR:-/tmp}/kitchen-evals" --output report.html
```

That is the path on macOS and Linux; on Windows it is `%TEMP%\kitchen-evals`. The tool collects anonymous usage data unless `DOTNET_AIEVAL_TELEMETRY_OPTOUT` is `1`.

## Run the tests

```sh
dotnet test
```

Eleven tests, no API calls. The model and the evaluator are NSubstitute stubs.

- `ChecksTests`: the checks in code pass a salmon dinner that is served with rice, fail an answer without a pantry item, and fail an answer without a call to `list_pantry`.
- `JudgeTests`: the judge's chat client drops `Temperature` and `TopP` and keeps the other options; `AgentEvaluationResults` counts a metric that the judge couldn't score as passed; `Judge.FailWhenErrored` turns it into a failure and leaves scored metrics alone.
- `PantryJudgeTests`: `PantryJudge` hands the evaluator the pantry as `GroundednessEvaluatorContext` and the whole answer, tool call included. The framework's own `EvaluateAsync` overload that takes an `IEvaluator` passes no context and only the answer's last message.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenEvals/Program.cs` | The three evaluations and the token costs |
| `KitchenEvals/Pantry.cs` | The pantry and its `list_pantry` tool |
| `KitchenEvals/KitchenAgent.cs` | The kitchen assistant |
| `KitchenEvals/Checks.cs` | The checks in code, as a `LocalEvaluator` |
| `KitchenEvals/Judge.cs` | The judge's chat configuration and the rule that fails an unscored metric |
| `KitchenEvals/PantryJudge.cs` | An `IAgentEvaluator` that gives the judge the pantry |
| `KitchenEvals/TokenCounter.cs` | Counts the tokens of the calls that reach Claude |
| `KitchenEvals/Report.cs` | Formats the scores |
| `KitchenEvals.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenEvals.Tests/` | Tests with stubs |

## Versions

Microsoft Agent Framework 1.24.0, `Microsoft.Extensions.AI.Evaluation.Quality` and `Microsoft.Extensions.AI.Evaluation.Reporting` 10.10.0 (the version `Microsoft.Agents.AI` 1.24.0 depends on), .NET 10.

## Try it

- Add a fourth answer written by hand that serves the salmon with rice, and compare its groundedness with the salmon row.
- Remove "and name anything the household would have to buy" from the instructions in `KitchenAgent.cs`, run the app a few times and compare the lasagne rows.
