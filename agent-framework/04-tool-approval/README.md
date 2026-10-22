# Part 4: Human-in-the-loop tool approval in C#

A kitchen assistant with a shopping list and two tools: one reads the list, the other adds to it and is wrapped in `ApprovalRequiredAIFunction`. When the model wants to add something, the run stops and the console shows the exact call. You answer `y`, `n` or type a new message, and the conversation continues on the same session. This is the code from [Human-in-the-loop tool approval in C#](https://kootstra.io/writing/human-in-the-loop-tool-approval-csharp/), with the final approval loop that also handles a new message.

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**

This sample has no Aspire AppHost: the app reads your answers from the console, and Aspire doesn't connect a terminal to the apps it runs.

## Run it

This app reads your answer from the console, so run it directly in your terminal. Set the key for this terminal first (`read -rs` keeps it out of your shell history), then run:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project ShoppingApproval
```

Answer `y` to approve, or `n` (or just Enter) to reject:

```text
Waiting for approval: 1
Shopping list: milk, bread
add_to_shopping_list {"item":"eggs","quantity":"12"}
Approve? (y/n, or type a new message) y
I've added a dozen eggs (12) to the shopping list, which now has 3 items.
Shopping list: milk, bread, 12 eggs
```

Or type a new message instead. The pending call is rejected with a reason, your message goes to the model in the same turn, and the corrected call comes back for approval:

```text
add_to_shopping_list {"item":"eggs","quantity":"12"}
Approve? (y/n, or type a new message) Actually, make that six
add_to_shopping_list {"item":"eggs","quantity":"6"}
Approve? (y/n, or type a new message) y
I've added 6 eggs to the shopping list, so the list now has 3 items.
Shopping list: milk, bread, 6 eggs
```

## Run the tests

```sh
dotnet test
```

Ten tests, no API calls. `Program.cs` is top-level code, so `ApprovalTests` rebuild the same agent, tools and reply switch on a stubbed chat client and check the approval flow the article describes:

- a call to the wrapped tool stops the run with a `ToolApprovalRequestContent`, and the list is unchanged;
- an approval runs the proposed call;
- a rejection with a reason reaches the model as `Tool call invocation rejected. <reason>` and runs nothing;
- an empty answer counts as `n`: the call is rejected with the `Rejected` reason and no empty message goes to the model;
- rejecting the pending call and sending a new message in the same run carries the conversation on;
- an unanswered request makes the next run on that session throw `InvalidOperationException`;
- an approval for a call that was never proposed runs nothing;
- a call to `get_shopping_list` in the same response is held back and runs on the next run, even when the write is rejected;
- a response with an unknown request ID leaves the request unanswered, so the run throws, and the real answer still binds afterward;
- with `UseToolApproval`, two proposed calls are shown one at a time, and an answer from `CreateAlwaysApproveToolResponse` approves the queued call and later calls to the same tool.

## Files

| File | What it is |
| --- | --- |
| `ShoppingApproval/Program.cs` | The shopping list, its tools, the agent and the approval loop |
| `ShoppingApproval.Tests/` | Tests of the approval flow with a stubbed chat client |

The article first shows a simpler loop that only asks y or n; `Program.cs` holds the loop that replaces it.

## Try it

- Answer `n` and read what the model does next with the rejection reason (`Rejected` in `Program.cs`).
- Replace the `_ =>` branch of the `switch` with `_ => [new(ChatRole.User, answer)]`, so a new message no longer rejects the pending request. Type a new message while an approval is waiting and read the exception the next run throws.
