using System.Collections.Concurrent;

namespace ResonanceServerOrchestrator.Stores;

public sealed class InMemoryLobbyStore : ILobbyStore
{
    private readonly ConcurrentDictionary<string, string> _lobbies = new();

    public void Set(string lobbyCode, string body) =>
        _lobbies[lobbyCode] = body;

    public string? Get(string lobbyCode) =>
        _lobbies.TryGetValue(lobbyCode, out var body) ? body : null;
}
