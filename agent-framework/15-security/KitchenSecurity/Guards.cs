using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

static partial class Guards
{
    public const string Removed = "[address removed]";

    // Function middleware: runs before every tool call, with the call's
    // arguments and the conversation so far.
    public static async ValueTask<object?> RecipientsFromThePerson(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken,
            ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        if (context.Function.Name == "share_shopping_list"
            && context.Arguments.TryGetValue("email", out object? email))
        {
            // Compare whole addresses, so typing sam@household.example
            // doesn't count as asking for m@household.example.
            bool typed = context.Messages
                .Where(m => m.Role == ChatRole.User)
                .SelectMany(m => EmailAddress().Matches(m.Text))
                .Any(match => string.Equals(
                    match.Value, $"{email}", StringComparison.OrdinalIgnoreCase));
            if (!typed)
            {
                return "Not shared: the person did not ask "
                    + $"to share with {email}.";
            }
        }

        return await next(context, cancellationToken);
    }

    // Agent run middleware: wraps the whole run, so it sees the caller's
    // messages on the way in and the response on the way out.
    public static async Task<AgentResponse> RedactUnknownAddresses(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        AgentResponse response = await innerAgent.RunAsync(
            messages, session, options, cancellationToken);

        foreach (TextContent text in response.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<TextContent>()))
        {
            text.Text = EmailAddress().Replace(text.Text, match =>
                ShoppingList.Household.Contains(
                    match.Value, StringComparer.OrdinalIgnoreCase)
                    ? match.Value
                    : Removed);
        }

        return response;
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex EmailAddress();
}
