namespace ResonanceServerOrchestrator.Contracts;

/// <summary>
/// The basic platform information required to join a match.
/// Player identification specifics is handled by the implementation.
/// </summary>
public interface IPlatformInformationDto
{
    Platform Platform { get; }
    string PlatformUserId { get; }
    string PlatformLobbyId { get; }
}
