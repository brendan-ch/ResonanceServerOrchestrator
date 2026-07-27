using ResonanceServerOrchestrator.Contracts;

namespace ResonanceServerOrchestrator.Stores;

internal readonly record struct LobbyKey(Platform Platform, string LobbyId);
