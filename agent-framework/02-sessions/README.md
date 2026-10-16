# Part 2: Remembering the conversation with agent sessions

A kitchen assistant that keeps a conversation in an `AgentSession`. It asks a question and a follow-up on the same session, prints the stored history, shows the input token count growing with each turn, saves the session as JSON and restores it to ask one more question. `KitchenChat.cs` holds the article's web example: it starts a conversation with a server-chosen ID and loads a stored session only for the user who owns it. This is the code from [Remembering the conversation with agent sessions](https://kootstra.io/writing/maf-sessions/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenSessions
```

The shape of the output (answers shortened here):

```text
Make fried rice: sauté the diced onion in oil, add cooked rice [...]
Tomorrow, try an onion and egg frittata with leftover rice folded in [...]
user: What can I cook tonight with rice, eggs and an onion?
assistant: Make fried rice: [...]
user: And tomorrow?
assistant: Tomorrow, try an onion and egg frittata [...]
302 input tokens: I also have a leek. Does that change anything?
519 input tokens: Which of those keeps best for lunch the next day?
From our conversation, you mentioned rice, eggs, an onion, and a leek. [...]
```

The saved session is written to `KitchenSessions/bin/Debug/net10.0/kitchen-session.json`. Open it to see the conversation in plain JSON.

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-sessions`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-sessions --status down
aspire logs kitchen-sessions
aspire stop
```

## Run the tests

```sh
dotnet test
```

Fourteen tests, no API calls:

- `KitchenChatTests` call `KitchenChat.StartAsync` and `KitchenChat.ReplyAsync` with an in-memory `ISessionStore` (a fake) and a stubbed chat client: a started conversation belongs to the user who started it and gets a new ID each time, the owner gets a reply, another user's session and an unknown session both return `null` without reaching the model, the next reply continues the stored conversation, and the stored session keeps its owner.
- `SessionTests` check what `Program.cs` relies on: without a session the follow-up arrives alone, with one every turn resends the earlier ones, the history holds the messages but not the instructions, the saved JSON holds the conversation in plain text, and a session saved as JSON and restored carries the conversation into the next run.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenSessions/Program.cs` | The console app: one session, the history, growing token counts, save and restore |
| `KitchenSessions/KitchenChat.cs` | `StoredSession`, `ISessionStore` and the owner check from the article's web example |
| `KitchenSessions.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenSessions.Tests/` | Tests with a stubbed chat client and an in-memory session store |

The article first runs the two questions without a session to show the follow-up getting lost, then adds the session. `Program.cs` holds the final version with the session; the test `Without_a_session_the_follow_up_arrives_alone` shows the first step.

## Try it

- Run five turns in one session and print `InputTokenCount` after each. The number only goes up.
- Save the session, open `kitchen-session.json` and find your own messages in it. Then delete the file and ask a follow-up question: the agent has lost the thread.
- Remove `session` from the two `RunAsync` calls at the top of `Program.cs` and read the answer to "And tomorrow?".
