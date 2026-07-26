namespace ResonanceServerOrchestrator.Contracts;

public sealed record SteamPlatformInformationDto : IPlatformInformationDto
{
    public Platform Platform { get; init; }
    public string PlatformUserId { get; init; }
    public string PlatformLobbyId { get; init; }

    /// <summary>
    /// A user authentication token checked against the Steam web API.
    /// Required if Steam validation is enabled in the environment.
    /// </summary>
    public string? AuthenticationTicketHex { get; init; }
}