namespace ResonanceServerOrchestrator.Contracts;

public sealed record ExpectedLobbyPlayerDto
{
    public string Username { get; init; }
    public Platform Platform { get; init; }
    public string PlatformId { get; init; }
}