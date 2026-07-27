using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

internal sealed class PlayerTicketAuthenticator(
    ISteamTicketValidator ticketValidator,
    IOptions<OrchestratorOptions> options,
    ILogger<PlayerTicketAuthenticator> logger)
{
    public async Task<string?> DescribeAuthenticationFailureAsync(
        IPlatformUserInformationDto user, CancellationToken cancellationToken)
    {
        if (options.Value.SteamCredentialCheckDisabled)
            return null;

        if (string.IsNullOrWhiteSpace(user.AuthenticationTicketHex))
            return "An authentication ticket is required.";

        var validation = await ticketValidator.ValidateAsync(
            user.AuthenticationTicketHex, cancellationToken);

        if (!validation.IsValid)
            return validation.FailureDetail ?? "The authentication ticket was rejected.";

        if (validation.IsBanned)
            return "The account is banned.";

        if (validation.SteamId is null)
            return null;

        if (!string.Equals(validation.SteamId, user.PlatformUserId, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "A ticket belonging to {ActualPlatformUserId} was presented while claiming {ClaimedPlatformUserId}.",
                validation.SteamId, user.PlatformUserId);

            return "The authentication ticket does not belong to the claimed player.";
        }

        return null;
    }
}
