namespace ResonanceServerOrchestrator.Contracts;

public sealed record SteamPlatformInformationDto : IPlatformInformationDto
{
    public Platform Platform { get; init; }
    public string PlatformId { get; init; }
    public string LobbyId { get; init; }

    /// <summary>
    /// A user authentication token checked against the Steam web API.
    /// Required if Steam validation is enabled in the environment.
    /// </summary>
    public string? AuthenticationTicketHex { get; init; }
}