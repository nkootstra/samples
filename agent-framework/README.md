# Microsoft Agent Framework in C#

Code for the series [Microsoft Agent Framework in C#](https://kootstra.io/writing/maf-first-agent/) on kootstra.io. Each part is one app with a kitchen assistant on Anthropic's Claude Haiku 5.5, built with Microsoft Agent Framework 1.24.0 on .NET 10.

| Part | Sample | Article | Run with |
| --- | --- | --- | --- |
| 1 | [01-first-agent](01-first-agent/) | [Microsoft Agent Framework: your first C# agent](https://kootstra.io/writing/maf-first-agent/) | `dotnet run` |

Every part runs with `dotnet run` and the key in `ANTHROPIC_API_KEY`. Part 1 also has an Aspire AppHost, which its README describes as an optional way to run it.

## Before you start

Install the .NET 10 SDK and get an Anthropic API key, as described in [docs/prerequisites.md](../docs/prerequisites.md). For the optional Aspire runs, also install the Aspire CLI and store the key in user secrets as described there. Every run calls Claude Haiku 5.5 on your account; the tests don't call it at all.

## Versions

| Package | Version | Parts |
| --- | --- | --- |
| `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Anthropic` | 1.24.0 | 1 |
| Aspire (`Aspire.AppHost.Sdk`, `Aspire.Hosting.Testing`) | 13.6.1 | 1 |
| xUnit (`xunit.v3.mtp-v2`) | 4.0.1 | 1 |
| `NSubstitute`, `NSubstitute.Analyzers.CSharp` | 6.2.0, 1.0.17 | 1 |
| .NET SDK | 10.0.100 or later | 1 |

Each sample pins its own versions in its `Directory.Packages.props`.
