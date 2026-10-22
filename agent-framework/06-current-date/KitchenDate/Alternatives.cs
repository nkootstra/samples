using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class Alternatives
{
    public static AIAgent DateInInstructions(
        IChatClient chatClient, string instructions)
    {
        string today = DateTime.Now.ToString(
            "dddd d MMMM yyyy", CultureInfo.InvariantCulture);

        AIAgent agent = chatClient.AsAIAgent(
            name: "kitchen-assistant",
            instructions: $"{instructions} Today is {today}.");
        return agent;
    }

    public static async Task<AgentResponse> DateInMessage(
        AIAgent agent, AgentSession session, string question)
    {
        string today = DateTime.Now.ToString(
            "dddd d MMMM yyyy", CultureInfo.InvariantCulture);

        return await agent.RunAsync(
            $"Today is {today}. {question}", session);
    }
}
