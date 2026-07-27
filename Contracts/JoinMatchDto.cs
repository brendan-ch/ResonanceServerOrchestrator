namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinMatchDto(
    IPlatformUserInformationDto PlatformUserInformation,
    ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers
);