# Microsoft Agent Framework in C#

Code for the series [Microsoft Agent Framework in C#](https://kootstra.io/writing/microsoft-agent-framework-first-csharp-agent/) on kootstra.io. Each part is one app with a kitchen assistant on Anthropic's Claude Haiku 5.5, built with Microsoft Agent Framework 1.24.0 on .NET 10.

| Part | Sample | Article | Run with |
| --- | --- | --- | --- |
| 1 | [01-first-agent](01-first-agent/) | [Microsoft Agent Framework: your first C# agent](https://kootstra.io/writing/microsoft-agent-framework-first-csharp-agent/) | `dotnet run` |
| 2 | [02-sessions](02-sessions/) | [Remembering the conversation with agent sessions](https://kootstra.io/writing/agent-framework-sessions-csharp/) | `dotnet run` |
| 3 | [03-tools](03-tools/) | [Tool calling for AI agents in C#](https://kootstra.io/writing/tool-calling-ai-agents-csharp/) | `dotnet run` |
| 4 | [04-tool-approval](04-tool-approval/) | [Human-in-the-loop tool approval in C#](https://kootstra.io/writing/human-in-the-loop-tool-approval-csharp/) | `dotnet run` (asks for input) |
| 5 | [05-first-workflow](05-first-workflow/) | [Planner, then reviewer: two agents in one workflow](https://kootstra.io/writing/agent-framework-workflow-planner-reviewer/) | `dotnet run` |
| 6 | [06-current-date](06-current-date/) | [Telling an agent what day it is](https://kootstra.io/writing/ai-agent-current-date-csharp/) | `dotnet run` |
| 7 | [07-structured-output](07-structured-output/) | [Agent Framework structured output in C# with Claude](https://kootstra.io/writing/structured-output-ai-agent-csharp/) | `dotnet run` |
| 8 | [08-bounded-history](08-bounded-history/) | [Agent Framework chat history compaction in C# with Claude](https://kootstra.io/writing/chat-history-compaction-ai-agent-csharp/) | `dotnet run` |
| 9 | [09-workflow-routing](09-workflow-routing/) | [Agent Framework workflow routing in C# with Claude](https://kootstra.io/writing/agent-framework-workflow-routing-csharp/) | `dotnet run` |

Every part runs with `dotnet run` and the key in `ANTHROPIC_API_KEY`. Parts 1 to 3 and 5 to 9 also have an Aspire AppHost, which their READMEs describe as an optional way to run them. Part 4 has none: it reads your answer from the console, and Aspire doesn't connect a terminal to the apps it runs.

## Before you start

Install the .NET 10 SDK and get an Anthropic API key, as described in [docs/prerequisites.md](../docs/prerequisites.md). For the optional Aspire runs, also install the Aspire CLI and store the key in user secrets as described there. Every run calls Claude Haiku 5.5 on your account; the tests don't call it at all.

## Versions

| Package | Version | Parts |
| --- | --- | --- |
| `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Anthropic` | 1.24.0 | 1 to 9 |
| `Microsoft.Agents.AI.Workflows` | 1.24.0 | 5, 9 |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 | 6 (tests) |
| Aspire (`Aspire.AppHost.Sdk`, `Aspire.Hosting.Testing`) | 13.6.1 | 1, 2, 3, 5, 6, 7, 8, 9 |
| xUnit (`xunit.v3.mtp-v2`) | 4.0.1 | 1 to 9 |
| `NSubstitute`, `NSubstitute.Analyzers.CSharp` | 6.2.0, 1.0.17 | 1 to 9 |
| .NET SDK | 10.0.100 or later | 1 to 9 |

Each sample pins its own versions in its `Directory.Packages.props`.
