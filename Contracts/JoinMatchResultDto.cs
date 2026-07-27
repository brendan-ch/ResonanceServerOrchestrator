namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchResultDto(
    string MatchId,
    Uri DedicatedServerBaseUrl,
    string ServerAuthToken
);