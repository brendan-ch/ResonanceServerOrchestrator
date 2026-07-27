namespace ResonanceServerOrchestrator.Services;

public sealed class DisabledSteamTicketValidator : ISteamTicketValidator
{
    private static readonly Task<SteamTicketValidationResult> AcceptedWithoutAnAssertedIdentity =
        Task.FromResult(SteamTicketValidationResult.IdentityNotAsserted());

    public Task<SteamTicketValidationResult> ValidateAsync(
        string ticketHex, CancellationToken cancellationToken) =>
        AcceptedWithoutAnAssertedIdentity;
}
