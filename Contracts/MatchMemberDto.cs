namespace ResonanceServerOrchestrator.Contracts;

public sealed record MatchMemberDto(
    Platform Platform,
    string PlatformUserId,
    string Username,
    string ServerAuthToken
);
