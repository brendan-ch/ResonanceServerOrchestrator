namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchResultDto(
    string MatchId,
    string ServerFqdn,
    string ServerAuthToken
);