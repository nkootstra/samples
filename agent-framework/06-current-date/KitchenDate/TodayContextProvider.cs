using System.Globalization;
using Microsoft.Agents.AI;

public sealed class TodayContextProvider(TimeProvider clock)
    : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        string today = clock.GetLocalNow().ToString(
            "dddd d MMMM yyyy", CultureInfo.InvariantCulture);

        return ValueTask.FromResult(
            new AIContext { Instructions = $"Today is {today}." });
    }
}
