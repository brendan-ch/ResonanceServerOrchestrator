namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchDto
{
    public required IPlatformUserInformationDto PlatformUserInformation { get; init; }
    public required ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers { get; init; }
}
