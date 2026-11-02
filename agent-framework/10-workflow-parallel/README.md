# Part 10: Agent Framework fan-out, fan-in and loops in C# with Claude

A dinner planner whose plans are checked against the household's rules. A planner agent writes a plan, three checks run on it at once (peanuts by a model, fish and weekday cooking time in C#), a fan-in barrier combines them into one review, and a plan that breaks a rule goes back to the planner with its problems, at most three rounds. This is the code from [Agent Framework fan-out, fan-in and loops in C# with Claude](https://kootstra.io/writing/agent-framework-fan-out-fan-in-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenChecks
```

Each line starting with `[step N]` is a superstep and the executors that got a message in it. The lines under a `planner` are the problems in the review it was sent back. The plans differ from run to run:

```text
> Dinners for Monday, Wednesday and Saturday. We have salmon.

[step 0] planner
[step 1] fish weekday-time peanuts
[step 2] combine combine combine
[step 3] planner
  - Fish on Monday, Wednesday and Saturday: at most once a week.
[step 4] fish weekday-time peanuts
[step 5] combine combine combine
[step 6] result
Monday: Pan-seared salmon with lemon green beans and rice, 30 min (salmon, green beans, rice, lemon)
Wednesday: Chicken teriyaki bowls with cucumber and sesame, 25 min (chicken, rice, cucumber, sesame seeds, soy sauce)
Saturday: Chickpea and spinach curry with rice, 40 min (chickpeas, spinach, tomatoes, coconut milk, rice)
Keeps every rule (round 2).
```

A run makes 2 to 6 calls to Claude Haiku 5.5: one plan and one peanut check per round. In three runs of this request, the first plan had salmon on all three days every time, and the second plan kept every rule. Every executor that calls the model checks `FinishReason` and fails the run if an answer was cut off, so a complete run is also a run without truncated answers.

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-checks`. The app runs one request through the workflow and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-checks --status down
aspire logs kitchen-checks
aspire stop
```

## Run the tests

```sh
dotnet test
```

Nineteen tests, no API calls:

- `DinnerWorkflowTests` build the workflow from `DinnerWorkflow.Build` with each agent on its own stubbed chat client: a plan that keeps every rule is the output after one round, the three checks run in the same superstep, a plan with problems goes back to the planner with those problems (from the fish check or the peanut check), the review lists the problems sorted by rule, a plan that never passes stops after three rounds, a planner cut off at the token limit fails the run without output, and a failed check keeps the barrier closed so the combine step never runs.
- `EdgeBehaviourTests` show the conditional edge behaviour the article describes on a small graph of plain functions (every edge whose condition holds gets the message; a message no condition accepts ends the run with no output and no error), and that `InProcessExecution.Concurrent` refuses this workflow.
- `HouseholdRulesTests` feed the two code checks known plans.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenChecks/Program.cs` | The two agents, one request and the event loop |
| `KitchenChecks/DinnerWorkflow.cs` | The graph: fan-out, fan-in barrier and the loop's conditional edges |
| `KitchenChecks/Executors.cs` | The planner with two handlers, the peanut check, the combine step and the result |
| `KitchenChecks/HouseholdRules.cs` | The fish and weekday time checks in plain C# |
| `KitchenChecks/Messages.cs` | The records the executors send each other |
| `KitchenChecks/DinnerPlan.cs` | The `DinnerPlan` and `Dinner` records from part 7 |
| `KitchenChecks.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenChecks.Tests/` | Tests with stubbed chat clients |

The `[MessageHandler]` executors need the `Microsoft.Agents.AI.Workflows.Generators` package, a source generator that `Microsoft.Agents.AI.Workflows` doesn't bring along. The compiler errors the article quotes (a `[MessageHandler]` class without the generator package, and executor classes that aren't `partial` with it) and the error for a `SendMessageAsync` without `[SendsMessage]` came from copies of this sample with that one change; they aren't part of it.

## Try it

- Set `MaxRounds` in `DinnerWorkflow.cs` to 1 and run again. The plan now goes to the result after its first round, and the fish problem is listed under "Still wrong" instead of going back to the planner.
- Ask for "Dinners for Monday to Friday. We love satay." and see whether the peanut check catches what the code check in [the structured output post](https://kootstra.io/writing/structured-output-ai-agent-csharp/) missed.
- Remove `[SendsMessage(typeof(Review))]` from `CombineChecks` and run again. The run ends with a `WorkflowErrorEvent` after the first round of checks.
