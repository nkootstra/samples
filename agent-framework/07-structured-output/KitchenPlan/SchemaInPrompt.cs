using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

static class SchemaInPrompt
{
    public static async Task<AgentResponse<T>> RunAsync<T>(
        AIAgent agent, string message, AgentSession? session = null)
    {
        ChatResponseFormatJson format = ChatResponseFormat.ForJsonSchema<T>(
            AgentAbstractionsJsonUtilities.DefaultOptions);

        AgentResponse response = await agent.RunAsync(
            $"""
            {message}

            Respond with a JSON value conforming to the following schema:
            {format.Schema}
            Reply with the JSON only, without a code fence.
            """,
            session,
            new AgentRunOptions { ResponseFormat = ChatResponseFormat.Json });

        return new AgentResponse<T>(
            response, AgentAbstractionsJsonUtilities.DefaultOptions);
    }
}
