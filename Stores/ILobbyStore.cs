namespace ResonanceServerOrchestrator.Stores;

public interface ILobbyStore
{
    bool TrySet(string lobbyCode, string body);
    string? Get(string lobbyCode);
}
