# Part 5: Planner, then reviewer: two agents in one workflow

Two agents in a row: a planner writes three dinners, a reviewer checks them against the household's rules (no peanuts, fish at most once a week, weekday dinners under 40 minutes). A `WorkflowBuilder` connects them with one edge, so every plan goes through the review. The app streams both agents' text as workflow events, with a header line for each agent. This is the code from [Planner, then reviewer: two agents in one workflow](https://kootstra.io/writing/maf-first-workflow/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project FirstWorkflow
```

The executor IDs end in a new GUID on every run. The shape of the output (shortened here):

```text
--- planner_13ef12db4c4849a099bab9bdb3873cb2
Monday: Lemon-herb baked salmon with garlic, olive oil, lemon, dill, and roasted asparagus
Wednesday: Salmon teriyaki bowls with rice, soy sauce, mirin, ginger, scallions, and steamed broccoli
Saturday: Salmon tacos with corn tortillas, cabbage slaw, avocado, lime, and chipotle crema
--- reviewer_d0b4008df6dd446eb03a20578c191dfd
Monday: Lemon-herb sheet-pan chicken breasts with garlic, olive oil, lemon, dill, and roasted asparagus (about 25 minutes)
Wednesday: Teriyaki chicken bowls with rice, soy sauce, mirin, ginger, scallions, and steamed broccoli (about 30 minutes)
Saturday: Salmon tacos with corn tortillas, cabbage slaw, avocado, lime, and chipotle crema

- Monday: Swapped salmon for chicken so the plan has only one fish dinner, which meets the fish-once-a-week limit.
- Wednesday: Swapped salmon for chicken so the plan has only one fish dinner, which meets the fish-once-a-week limit.
[...]
Workflow finished.
```

The reviewer reasons before it answers, and Claude Haiku 5.5 counts those reasoning tokens as output tokens. With a limit of 1,024 output tokens the reviewer often ran out: its list of changes stopped mid-sentence, and once it printed nothing at all. Each time, the workflow still finished normally. This sample therefore sets the limit to 4,096 and prints a warning to standard error, once per agent, when an agent stops at the limit:

```text
reviewer_<id> stopped at the output token limit.
```

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `first-workflow`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait first-workflow --status down
aspire logs first-workflow
aspire stop
```

## Run the tests

```sh
dotnet test
```

Eleven tests, no API calls. `Program.cs` is top-level code, so `WorkflowTests` build the same planner-to-reviewer workflow with each agent on its own stubbed chat client and check what the article describes:

- the planner answers first and the reviewer second;
- each agent's text arrives under its own executor ID;
- the reviewer receives the request and the plan as user messages;
- without a turn token no agent runs;
- an agent cut off at the token limit reports `ChatFinishReason.Length` on its last update, which is what the warning checks;
- a failing agent raises both an `ExecutorFailedEvent` and a `WorkflowErrorEvent`;
- `WithOutputFrom` changes nothing about the events today;
- with `Futures.EnableAgentResponseOutputTaggingAndFiltering` on (the default from 2.0.0), only the steps named in `WithOutputFrom` stream their text, so naming only the reviewer hides the planner's text;
- a second run of the same `Workflow` throws on the default environment, and two runs at once work on `InProcessExecution.Concurrent`.

`AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `FirstWorkflow/Program.cs` | The two agents, the workflow and the event loop |
| `FirstWorkflow.AppHost/AppHost.cs` | Starts the app with the API key |
| `FirstWorkflow.Tests/` | Workflow tests with stubbed chat clients |

## Try it

- Tell the reviewer to reject any plan with fish, ask the planner for fish, and watch the review come back in the output.
- Add a third agent, for example one that writes the shopping list, connect it with one more `AddEdge`, and add it to `WithOutputFrom`.
- Delete the `TrySendMessageAsync` line and run again: the program prints only `Workflow finished.`
- Replace the `WorkflowBuilder` code with `AgentWorkflowBuilder.BuildSequential(planner, reviewer)`, the builder for a straight line of agents. It also yields the whole conversation as a `WorkflowOutputEvent`, which the loop doesn't print; add a `case WorkflowOutputEvent` to see it.
