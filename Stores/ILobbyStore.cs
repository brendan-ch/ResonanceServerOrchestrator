using ResonanceServerOrchestrator.Models;

namespace ResonanceServerOrchestrator.Stores;

public interface ILobbyStore
{
    void Set(string lobbyCode, Lobby lobby);
    Lobby? Get(string lobbyCode);
}
