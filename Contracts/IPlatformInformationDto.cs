namespace ResonanceServerOrchestrator.Contracts;

public interface IPlatformInformationDto
{
    Platform Platform { get; }
    string PlatformId { get; }
    string LobbyId { get; }
}
