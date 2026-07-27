namespace ResonanceServerOrchestrator.Services;

public interface ISteamTicketValidator
{
    Task<SteamTicketValidationResult> ValidateAsync(string ticketHex, CancellationToken cancellationToken);
}
