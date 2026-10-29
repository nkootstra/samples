# Part 9: Agent Framework workflow routing in C# with Claude

A dinner assistant that sorts its requests. A classifier agent labels each request with a `RequestKind` enum, and a switch sends it to one of three branches: a cook agent for a quick dinner, a planner agent for dinners on several days, or a fixed reply for anything else (the default). Each branch's answer is the workflow's output. This is the code from [Agent Framework workflow routing in C# with Claude](https://kootstra.io/writing/agent-framework-workflow-routing-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenRouting
```

Each line starting with `[step N]` is a superstep and the executors that got a message in it. The answers differ from run to run:

```text
> Something quick for tonight. We have eggs and spinach.

[step 0] classify
[step 1] quick
Spinach and cheese omelette: wilt the spinach, then fold it into beaten eggs with a little cheese if you have it.

> Dinners for Monday, Wednesday and Saturday. We have salmon.

[step 0] classify
[step 1] planner
Monday: Pan-seared salmon with lemon and green beans, 25 min (salmon, green bean, lemon, garlic)
Wednesday: Salmon rice bowls with cucumber and sesame, 30 min (salmon, rice, cucumber, sesame seed, soy sauce)
Saturday: Baked salmon with roasted potatoes and dill, 45 min (salmon, potato, dill, olive oil)

> Which wine goes with lasagne?

[step 0] classify
[step 1] decline
I only plan dinners. Ask me for tonight's dinner or for a few days.
```

A run makes 5 calls to Claude Haiku 5.5: one classification per request, one for the quick dinner and one for the plan. The wine question never reaches a model after the classifier. Every executor that calls the model checks `FinishReason` and fails the run if an answer was cut off, so a complete run is also a run without truncated answers.

The plan has salmon on all three days. Nothing here checks it against the household's rules.

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-routing`. The app runs three requests through the workflow and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-routing --status down
aspire logs kitchen-routing
aspire stop
```

## Run the tests

```sh
dotnet test
```

Fifteen tests, no API calls:

- `DinnerWorkflowTests` build the workflow from `DinnerWorkflow.Build` with each agent on its own stubbed chat client: a quick request is answered by the cook, a week plan request by the planner, anything else goes to the default branch without a model call, a branch runs in the superstep after the classifier, a classifier or planner cut off at the token limit fails the run without output, and the same workflow runs again once the first run is disposed.
- `EdgeBehaviourTests` show the edge behaviour the article describes on small graphs of plain functions: every conditional edge whose condition holds gets the message, a switch takes only the first matching case, a message no case accepts ends the run with no output and no error, an output executor that no edge reaches stops `Build`, a target without a handler for the message's type is skipped without an error, and a second run of a workflow that is still running is refused.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenRouting/Program.cs` | The three agents, three requests and the event loop |
| `KitchenRouting/DinnerWorkflow.cs` | The graph: a switch with three cases and a default |
| `KitchenRouting/Executors.cs` | The classifier, cook and planner executors, and the `FinishReason` check |
| `KitchenRouting/Messages.cs` | `RequestKind`, `Triage`, `DinnerRequest` and the plan formatting |
| `KitchenRouting/DinnerPlan.cs` | The `DinnerPlan` and `Dinner` records from part 7 |
| `KitchenRouting.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenRouting.Tests/` | Tests with stubbed chat clients |

## Try it

- Remove `.WithDefault(decline)` and `decline` from `WithOutputFrom`, and run again. The wine question is classified and the run ends with no output and no error.
- Leave `decline` in `WithOutputFrom` and remove only `.WithDefault(decline)`. `Build` now throws `Workflow cannot be built because there are unreachable executors: decline.`
- Give both `AddCase` calls the same predicate. Only the first case added gets the message.
- Ask "Something quick for Friday?" and see which branch it takes. The classifier's instructions are where that boundary is drawn: one named day counts as a `WeekPlan`, so the planner can answer it.
- Add a value such as `Shopping` to `RequestKind` and describe it in the classifier's instructions, without adding a case. A shopping question then goes to the default.
