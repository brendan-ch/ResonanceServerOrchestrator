namespace ResonanceServerOrchestrator.Contracts;

public sealed record SteamPlatformUserInformationDto : IPlatformUserInformationDto
{
    public Platform Platform => Platform.Steam;
    public required string PlatformUserId { get; init; }
    public required string PlatformLobbyId { get; init; }
    public string? AuthenticationTicketHex { get; init; }
}
