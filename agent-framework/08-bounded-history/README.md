# Part 8: Agent Framework chat history compaction in C# with Claude

A kitchen assistant runs the same six-turn conversation six times: with no compaction; with the framework's `CompactionProvider` and three strategies (a sliding window of three turns, `ContextWindowCompactionStrategy` with a small token budget, and `SummarizationCompactionStrategy`); and with the two chat reducers from `Microsoft.Extensions.AI` (`MessageCountingChatReducer` keeping the last four messages, and `SummarizingChatReducer`). Each run prints the input tokens of every turn, the last answer and what the session has stored. The first message says "Our son doesn't eat mushrooms" and the last question asks for it back, so the runs that cut the history show the model forgetting it. The compaction strategies only trim the request: their sessions still store all twelve messages. The counting reducer rewrites the stored history. This is the code from [Agent Framework chat history compaction in C# with Claude](https://kootstra.io/writing/chat-history-compaction-ai-agent-csharp/).

## What you need

Install these first (once for all samples), as described in [docs/prerequisites.md](../../docs/prerequisites.md):

- **.NET 10 SDK**
- **An Anthropic API key**
- **Aspire CLI**, optional: only to run the sample through its AppHost (see [With Aspire](#with-aspire-optional))

## Run it

From this folder, set the key for this terminal and run the app. `read -rs` asks for the key without echoing it, so it stays out of your shell history:

```sh
read -rs ANTHROPIC_API_KEY && export ANTHROPIC_API_KEY
dotnet run --project KitchenHistory
```

A run makes 36 calls to Claude Haiku 5.5 for the conversations, plus one per summary (five in the run below). One run, with the stored summary cut short:

```text
  66 input tokens, stop: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
 166 input tokens, stop: And tomorrow?
 277 input tokens, stop: I also have a leek. Does that change anything?
 382 input tokens, stop: Which of those keeps best for lunch the next day?
 474 input tokens, stop: Something quick for Friday?
 579 input tokens, stop: Remind me: what doesn't our son eat?
Your son doesn't eat mushrooms.
12 messages stored, the first: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
  66 input tokens, stop: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
 150 input tokens, stop: And tomorrow?
 240 input tokens, stop: I also have a leek. Does that change anything?
 232 input tokens, stop: Which of those keeps best for lunch the next day?
 367 input tokens, stop: Something quick for Friday?
 463 input tokens, stop: Remind me: what doesn't our son eat?
I don't have any record of your son's food preferences or dislikes from our conversation, so I can't tell you what he avoids. If you tell me, I'll keep it in mind for future meal suggestions.
12 messages stored, the first: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
  66 input tokens, stop: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
 165 input tokens, stop: And tomorrow?
 264 input tokens, stop: I also have a leek. Does that change anything?
 380 input tokens, stop: Which of those keeps best for lunch the next day?
 259 input tokens, stop: Something quick for Friday?
 230 input tokens, stop: Remind me: what doesn't our son eat?
I don't have any record of your son's food preferences or dislikes, since this conversation hasn't mentioned them. If you tell me what he avoids, I'll keep it in mind for the rest of our planning.
12 messages stored, the first: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
  66 input tokens, stop: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
 167 input tokens, stop: And tomorrow?
 276 input tokens, stop: I also have a leek. Does that change anything?
     summary: 233 input, 238 output tokens, stop
 388 input tokens, stop: Which of those keeps best for lunch the next day?
     summary: 369 input, 205 output tokens, stop
 424 input tokens, stop: Something quick for Friday?
     summary: 412 input, 272 output tokens, stop
 538 input tokens, stop: Remind me: what doesn't our son eat?
Your son doesn't eat mushrooms.
12 messages stored, the first: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
  66 input tokens, stop: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
 152 input tokens, stop: And tomorrow?
 251 input tokens, stop: I also have a leek. Does that change anything?
 224 input tokens, stop: Which of those keeps best for lunch the next day?
 306 input tokens, stop: Something quick for Friday?
 411 input tokens, stop: Remind me: what doesn't our son eat?
I don't have any record of your son's food dislikes in our conversation, so I can't tell you what he avoids. If you tell me, I'll keep it in mind for the rest of our meal planning.
6 messages stored, the first: Which of those keeps best for lunch the next day?
  66 input tokens, stop: Our son doesn't eat mushrooms. What can I cook tonight with rice, eggs and an onion?
 156 input tokens, stop: And tomorrow?
 271 input tokens, stop: I also have a leek. Does that change anything?
 460 input tokens, stop: Which of those keeps best for lunch the next day?
     summary: 422 input, 269 output tokens, stop
 623 input tokens, stop: Something quick for Friday?
     summary: 584 input, 201 output tokens, stop
 715 input tokens, stop: Remind me: what doesn't our son eat?
Your son doesn't eat mushrooms.
7 messages stored, the first: The user asked what to cook tonight for their son, who doesn't eat mushrooms, using rice, eggs, and an onion. [...]
```

The answers and token counts differ from run to run. Claude Haiku 5.5 sometimes returns an answer with a reasoning block, which `ContextWindowCompactionStrategy` counts toward its estimate: in this run the fourth and fifth answers had one, so the last two requests held only the previous answer and the new question.

### With Aspire (optional)

This sample has an AppHost that starts the app and passes it the key from user secrets, stored as described in [docs/prerequisites.md](../../docs/prerequisites.md). From this folder:

```sh
aspire run
```

Open the dashboard link that `aspire run` prints and go to **Console logs**, resource `kitchen-history`. The app runs once and exits; stop the AppHost with `Ctrl+C`. To read the output in the terminal instead, start the AppHost in the background and wait for the app to exit before reading its logs, or you may get only part of them:

```sh
aspire start
aspire wait kitchen-history --status down
aspire logs kitchen-history
aspire stop
```

## Run the tests

```sh
dotnet test
```

Twenty-seven tests, no API calls:

- `CompactionTests` rebuild the compaction agents from `Program.cs` on a stubbed chat client and run the same script: the sliding window first cuts on the fourth turn, then sends the last two turns plus the new question, always keeps the latest turn, and leaves the stored history whole; a saved session holds every message twice, in the history provider's and the compaction provider's state; a token budget drops whole messages and can start a request with an answer; `ToolResultCompactionStrategy` collapses an old tool call into one message; the summarization strategy sends its prompt first and the conversation last (the request Claude rejects), sends its summary in place of the oldest turns, summarizes again from the last summary, keeps the summary out of the stored history, and sends the whole history without an error when the summarizer fails.
- `ReducerTests` do the same for the chat reducers: without a reducer the last turn resends the whole conversation; the counting reducer first cuts on the fourth turn and then sends the last four messages plus the new question; by default the session keeps the limit plus the last turn, and with `AfterMessageAdded` only the limit; the counting reducer drops tool calls and results, even the latest; the summarizing reducer sends its prompt last as a system message (the request Claude rejects), stores the summary as the first message, and keeps a tool call with its result.
- `SystemAsUserChatClientTests` check that `SystemAsUserChatClient` sends a system message as a user message with the same text, moves a leading system message to the end, passes other messages through unchanged, and does the same for streaming.
- `AppHostTests` check that the AppHost passes the secret `anthropic-api-key` parameter to the app as `ANTHROPIC_API_KEY`.

## Files

| File | What it is |
| --- | --- |
| `KitchenHistory/Program.cs` | Runs the script without compaction, with `CompactionProvider` and three strategies, and with the counting and summarizing reducers |
| `KitchenHistory/SystemAsUserChatClient.cs` | Wraps the summarizers' chat client so Claude accepts the summary requests |
| `KitchenHistory.AppHost/AppHost.cs` | Starts the app with the API key |
| `KitchenHistory.Tests/` | Tests with a stubbed chat client; `Kitchen.cs` holds the script and the stub |

The chat reducers and the compaction API are experimental, so `KitchenHistory.csproj` and the test project suppress `MEAI001` and `MAAI001`. The article also shows `SummarizationCompactionStrategy` and `SummarizingChatReducer` on a summarizer without `SystemAsUserChatClient`. Claude rejects those summary requests with a 400 error: the strategy catches it and silently skips the summary, the reducer throws. `Program.cs` leaves both versions out.

## Try it

- Remove `new SystemAsUserChatClient(...)` around `summarizer` in the `SummarizationCompactionStrategy` run and run again. That run then looks like one without compaction, and no summary line appears.
- Set `ReducerTriggerEvent` to `InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent.AfterMessageAdded` on the counting reducer in `Program.cs` and run again. The session now holds four messages instead of six.
- Raise the counting reducer to 10 and run again. In six turns it drops nothing, and the last answer names the mushrooms again.
