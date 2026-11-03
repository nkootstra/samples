public sealed record Basket(IReadOnlyList<string> Items, int Round)
{
    public static Basket Parse(string text, int round) =>
        new([.. text
            .Split('\n')
            .Select(line => line.Trim().TrimStart('-', '*', '•').Trim())
            .Where(line => line.Length > 0)], round);
}

public sealed record BasketDecision(bool Approved, string? Change, int Round)
{
    public static BasketDecision FromAnswer(string? answer, int round) =>
        answer?.Trim() switch
        {
            "y" or "Y" => new(true, null, round),
            null or "" or "n" or "N" => new(false, null, round),
            string change => new(false, change, round),
        };
}
