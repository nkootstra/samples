using Microsoft.Agents.AI;

namespace KitchenEvals.Tests;

// The checks in code see words, not meaning.
public sealed class ChecksTests
{
    static async Task<bool> PassesAsync(EvalItem item, string check)
    {
        AgentEvaluationResults results = await Checks.Create().EvaluateAsync(
            [item], cancellationToken: TestContext.Current.CancellationToken);
        return results.Items[0].Metrics[check].Interpretation?.Failed == false;
    }

    [Fact]
    public async Task A_salmon_dinner_served_with_rice_names_a_pantry_item()
    {
        var item = new EvalItem(
            "What can I cook tonight?",
            "Make salmon teriyaki and serve it with rice.");

        Assert.True(await PassesAsync(item, "names a pantry item"));
    }

    [Fact]
    public async Task An_answer_without_a_pantry_item_fails()
    {
        var item = new EvalItem(
            "What can I cook tonight?", "Order a pizza.");

        Assert.False(await PassesAsync(item, "names a pantry item"));
    }

    [Fact]
    public async Task An_answer_without_a_call_to_list_pantry_fails()
    {
        var item = new EvalItem(
            "What can I cook tonight?", "Make a spinach omelette.");

        Assert.False(await PassesAsync(item, "tool_called_check"));
    }
}
