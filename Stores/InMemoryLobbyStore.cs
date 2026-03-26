using System.Collections.Concurrent;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Stores;

public sealed class InMemoryLobbyStore : ILobbyStore
{
    private sealed record LobbyRecord(string Body, IGameInstance? Instance, DateTimeOffset CreatedAt);

    private readonly ConcurrentDictionary<string, LobbyRecord> _lobbies = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryLobbyStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public int ActiveCount => _lobbies.Count;

    public bool TrySet(string lobbyCode, string body) =>
        _lobbies.TryAdd(lobbyCode, new LobbyRecord(body, null, _timeProvider.GetUtcNow()));

    public void SetInstance(string lobbyCode, IGameInstance instance)
    {
        _lobbies.AddOrUpdate(
            lobbyCode,
            _ => new LobbyRecord(string.Empty, instance, _timeProvider.GetUtcNow()),
            (_, existing) => existing with { Instance = instance });

        instance.Exited += (_, _) => _lobbies.TryRemove(lobbyCode, out _);
    }

    public string? Get(string lobbyCode) =>
        _lobbies.TryGetValue(lobbyCode, out var record) ? record.Body : null;

    public IReadOnlyList<IGameInstance> RemoveExpired(TimeSpan timeout)
    {
        var now = _timeProvider.GetUtcNow();
        var expired = new List<IGameInstance>();

        foreach (var (code, record) in _lobbies)
        {
            if (now - record.CreatedAt >= timeout &&
                _lobbies.TryRemove(code, out var removed) &&
                removed.Instance is not null)
            {
                expired.Add(removed.Instance);
            }
        }

        return expired;
    }
}
