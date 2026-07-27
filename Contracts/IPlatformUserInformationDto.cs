namespace ResonanceServerOrchestrator.Contracts;

public interface IPlatformUserInformationDto
{
    Platform Platform { get; }
    string PlatformUserId { get; }
    string PlatformLobbyId { get; }
    string? AuthenticationTicketHex { get; }

    PlayerIdentity Identity => new(Platform, PlatformUserId);
}
