namespace ResonanceServerOrchestrator.Stores;

public interface ILobbyStore
{
    void Set(string lobbyCode, string body);
    string? Get(string lobbyCode);
}
