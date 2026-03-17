using System.Collections.Concurrent;
using ResonanceServerOrchestrator.Models;

namespace ResonanceServerOrchestrator.Stores;

public sealed class InMemoryLobbyStore : ILobbyStore
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

    public void Set(string lobbyCode, Lobby lobby) =>
        _lobbies[lobbyCode] = lobby;

    public Lobby? Get(string lobbyCode) =>
        _lobbies.TryGetValue(lobbyCode, out var lobby) ? lobby : null;
}
