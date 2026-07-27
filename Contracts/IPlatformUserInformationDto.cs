namespace ResonanceServerOrchestrator.Contracts;

/// <summary>
/// The basic platform user information required to join a match.
/// The implementation handles player identification specifics.
/// </summary>
public interface IPlatformUserInformationDto
{
    Platform Platform { get; }
    string PlatformUserId { get; }
    string PlatformLobbyId { get; }
}
