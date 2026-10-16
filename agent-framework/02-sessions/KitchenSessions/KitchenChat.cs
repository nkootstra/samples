using System.Text.Json;
using Microsoft.Agents.AI;

public sealed record StoredSession(string OwnerId, JsonElement State);

public interface ISessionStore
{
    Task<StoredSession?> FindAsync(string sessionId);
    Task SaveAsync(string sessionId, StoredSession session);
}

public static class KitchenChat
{
    // Starts a conversation for the signed-in user. The server picks the ID;
    // the client sends it back with every later message.
    public static async Task<string> StartAsync(
        AIAgent agent, ISessionStore store, string userId)
    {
        AgentSession session = await agent.CreateSessionAsync();
        JsonElement state = await agent.SerializeSessionAsync(session);
        string sessionId = Guid.NewGuid().ToString("n");
        await store.SaveAsync(sessionId, new StoredSession(userId, state));
        return sessionId;
    }

    // userId comes from the sign-in,
    // sessionId and message from the request.
    public static async Task<string?> ReplyAsync(
        AIAgent agent,
        ISessionStore store,
        string userId,
        string sessionId,
        string message)
    {
        StoredSession? stored = await store.FindAsync(sessionId);
        if (stored is null || stored.OwnerId != userId)
        {
            return null; // the endpoint answers 404 Not Found
        }

        AgentSession session =
            await agent.DeserializeSessionAsync(stored.State);
        AgentResponse response = await agent.RunAsync(message, session);

        JsonElement state = await agent.SerializeSessionAsync(session);
        await store.SaveAsync(sessionId, stored with { State = state });
        return response.Text;
    }
}
