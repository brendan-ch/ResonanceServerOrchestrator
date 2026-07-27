namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchResultDto(
    Guid MatchId,
    string DedicatedServerHost,
    int DedicatedServerPort,
    string ServerAuthToken
);
