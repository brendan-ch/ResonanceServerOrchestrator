namespace ResonanceServerOrchestrator.Contracts;

public sealed record PlatformUserInformationDto(
    Platform Platform,
    string PlatformUserId,
    string PlatformLobbyId,
    string? AuthenticationTicketHex = null)
{
    /// <remarks>
    /// Deliberately a method rather than a property: a public getter would be serialized onto
    /// the wire as a redundant copy of the two fields it derives from.
    /// </remarks>
    public PlayerIdentity GetIdentity() => new(Platform, PlatformUserId);
}
