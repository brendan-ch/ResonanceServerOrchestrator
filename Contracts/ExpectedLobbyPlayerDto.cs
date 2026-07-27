namespace ResonanceServerOrchestrator.Contracts;

public sealed record ExpectedLobbyPlayerDto
{
    public required string Username { get; init; }
    public required Platform Platform { get; init; }
    public required string PlatformUserId { get; init; }

    public PlayerIdentity Identity => new(Platform, PlatformUserId);
}
