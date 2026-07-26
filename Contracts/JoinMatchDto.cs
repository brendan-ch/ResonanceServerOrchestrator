namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchDto(
    IPlatformInformationDto PlatformInformation,
    ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers
);