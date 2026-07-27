namespace ResonanceServerOrchestrator.Contracts;

public sealed record SteamPlatformInformationDto(
    string LobbyId
) : IPlatformInformationDto
{
    public Platform Platform => Platform.Steam;
}
