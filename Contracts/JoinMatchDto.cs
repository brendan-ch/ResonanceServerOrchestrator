namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchDto
{
    public required PlatformUserInformationDto PlatformUserInformation { get; init; }
    public required ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers { get; init; }
}
