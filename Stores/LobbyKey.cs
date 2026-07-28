using Resonance.Contracts;

namespace ResonanceServerOrchestrator.Stores;

internal readonly record struct LobbyKey(Platform Platform, string LobbyId);
