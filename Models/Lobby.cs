namespace ResonanceServerOrchestrator.Models;

public sealed record Lobby(
    string Name,
    bool IsValid,
    string LobbyId,
    string LobbyCode,
    int MaxPlayers,
    bool IsOwner,
    IReadOnlyList<LobbyUser> Members,
    IReadOnlyDictionary<string, string> UnderlyingProviderProperties
);
