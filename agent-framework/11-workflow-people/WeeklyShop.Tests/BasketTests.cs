namespace WeeklyShop.Tests;

public sealed class BasketTests
{
    [Fact]
    public void Each_line_of_the_answer_is_one_item()
    {
        Basket basket = Basket.Parse("500 g chicken thighs\n1 red pepper\n", round: 1);

        Assert.Equal(["500 g chicken thighs", "1 red pepper"], basket.Items);
    }

    [Fact]
    public void List_markers_and_blank_lines_are_left_out()
    {
        Basket basket = Basket.Parse("- 500 g chicken thighs\n\n* 1 red pepper\r\n  • 250 g red lentils  ", round: 1);

        Assert.Equal(["500 g chicken thighs", "1 red pepper", "250 g red lentils"], basket.Items);
    }

    [Fact]
    public void The_basket_keeps_its_round()
    {
        Basket basket = Basket.Parse("1 red pepper", round: 2);

        Assert.Equal(2, basket.Round);
    }
}
