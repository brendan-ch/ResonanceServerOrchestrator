namespace ResonanceServerOrchestrator.Contracts;

public sealed record DummyPlatformUserInformationDto : IPlatformUserInformationDto
{
    public Platform Platform => Platform.Dummy;
    public required string PlatformUserId { get; init; }
    public required string PlatformLobbyId { get; init; }
    public string? AuthenticationTicketHex { get; init; }
}