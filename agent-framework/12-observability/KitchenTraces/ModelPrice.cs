using Microsoft.Extensions.AI;

public sealed record ModelPrice(
    decimal InputPerMillion,
    decimal CacheWritePerMillion,
    decimal CacheReadPerMillion,
    decimal OutputPerMillion)
{
    public decimal CostOf(UsageDetails? usage)
    {
        if (usage is null)
        {
            return 0m;
        }

        long input = usage.InputTokenCount ?? 0;
        long cacheRead = usage.CachedInputTokenCount ?? 0;
        long cacheWrite = usage.AdditionalCounts?
            .GetValueOrDefault("CacheCreationInputTokens") ?? 0;
        long output = usage.OutputTokenCount ?? 0;

        decimal dollars =
            (input - cacheRead - cacheWrite) * InputPerMillion
            + cacheRead * CacheReadPerMillion
            + cacheWrite * CacheWritePerMillion
            + output * OutputPerMillion;
        return dollars / 1_000_000m;
    }
}
