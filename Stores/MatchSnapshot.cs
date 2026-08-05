using Resonance.Contracts;

namespace ResonanceServerOrchestrator.Stores;

internal sealed record MatchSnapshot(
    Guid MatchId,
    string MatchKey,
    int GameServerPort,
    IReadOnlyList<MatchMemberDto> Members,
    string NextSceneName
);
