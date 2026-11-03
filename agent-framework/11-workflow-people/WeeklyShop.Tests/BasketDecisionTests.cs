namespace WeeklyShop.Tests;

public sealed class BasketDecisionTests
{
    [Theory]
    [InlineData("y")]
    [InlineData(" Y ")]
    public void A_y_approves_the_basket(string answer)
    {
        BasketDecision decision = BasketDecision.FromAnswer(answer, round: 1);

        Assert.Equal(new BasketDecision(true, null, 1), decision);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("")]
    [InlineData(null)]
    public void An_n_or_no_answer_rejects_the_basket_without_a_change(string? answer)
    {
        BasketDecision decision = BasketDecision.FromAnswer(answer, round: 1);

        Assert.Equal(new BasketDecision(false, null, 1), decision);
    }

    [Fact]
    public void Any_other_answer_is_a_change_to_the_basket()
    {
        BasketDecision decision = BasketDecision.FromAnswer(" We're away on Saturday ", round: 1);

        Assert.Equal(new BasketDecision(false, "We're away on Saturday", 1), decision);
    }

    [Fact]
    public void The_decision_carries_the_round_of_the_basket_it_answers()
    {
        BasketDecision decision = BasketDecision.FromAnswer("y", round: 3);

        Assert.Equal(3, decision.Round);
    }
}
