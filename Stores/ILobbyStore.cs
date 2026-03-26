using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Stores;

public interface ILobbyStore
{
    bool TrySet(string lobbyCode, string body);
    void SetInstance(string lobbyCode, IGameInstance instance);
    string? Get(string lobbyCode);
    int ActiveCount { get; }
    IReadOnlyList<IGameInstance> RemoveExpired(TimeSpan timeout);
}
