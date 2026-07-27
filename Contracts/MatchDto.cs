namespace ResonanceServerOrchestrator.Contracts;

public sealed record MatchDto(
    Guid Id,
    MatchStatus Status,
    IPlatformInformationDto PlatformInformation,
    string MatchKey,
    DateTime CreatedAt
);
