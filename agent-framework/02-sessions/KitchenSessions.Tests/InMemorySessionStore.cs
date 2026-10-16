using System.Text.Json;
using Microsoft.Agents.AI;

namespace KitchenSessions.Tests;

// A fake session store: a working ISessionStore kept in a dictionary.
sealed class InMemorySessionStore : ISessionStore
{
    readonly Dictionary<string, StoredSession> _sessions = [];

    public Task<StoredSession?> FindAsync(string sessionId) =>
        Task.FromResult(_sessions.GetValueOrDefault(sessionId));

    public Task SaveAsync(string sessionId, StoredSession session)
    {
        _sessions[sessionId] = session;
        return Task.CompletedTask;
    }

    public async Task AddEmptySessionAsync(AIAgent agent, string sessionId, string ownerId)
    {
        AgentSession session = await agent.CreateSessionAsync();
        JsonElement state = await agent.SerializeSessionAsync(session);
        _sessions[sessionId] = new StoredSession(ownerId, state);
    }
}
