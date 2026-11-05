using Microsoft.Extensions.AI;

namespace KitchenTraces.Tests;

// Claude Haiku 5.5 for prompts up to 100,000 tokens, in dollars per million
// tokens: input 0.10, 5-minute cache writes 0.125, cache hits 0.01, output 0.50.
public sealed class ModelPriceTests
{
    readonly ModelPrice _haiku = new(
        InputPerMillion: 0.10m,
        CacheWritePerMillion: 0.125m,
        CacheReadPerMillion: 0.01m,
        OutputPerMillion: 0.50m);

    [Fact]
    public void A_million_tokens_in_and_out_cost_the_two_list_prices()
    {
        var usage = new UsageDetails { InputTokenCount = 1_000_000, OutputTokenCount = 1_000_000 };

        Assert.Equal(0.60m, _haiku.CostOf(usage));
    }

    [Fact]
    public void A_short_run_costs_a_fraction_of_a_cent()
    {
        var usage = new UsageDetails { InputTokenCount = 600, OutputTokenCount = 300 };

        Assert.Equal(0.00021m, _haiku.CostOf(usage));
    }

    [Fact]
    public void Cached_input_tokens_cost_the_cache_hit_price()
    {
        var usage = new UsageDetails { InputTokenCount = 10_000, CachedInputTokenCount = 8_000 };

        Assert.Equal(0.00028m, _haiku.CostOf(usage));
    }

    [Fact]
    public void Input_tokens_written_to_the_cache_cost_the_cache_write_price()
    {
        var usage = new UsageDetails
        {
            InputTokenCount = 10_000,
            AdditionalCounts = new() { ["CacheCreationInputTokens"] = 10_000 },
        };

        Assert.Equal(0.00125m, _haiku.CostOf(usage));
    }

    [Fact]
    public void A_response_without_usage_costs_nothing()
    {
        Assert.Equal(0m, _haiku.CostOf(null));
    }
}
