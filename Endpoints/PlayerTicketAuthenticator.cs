using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Endpoints;

internal sealed class PlayerTicketAuthenticator(
    ISteamTicketValidator ticketValidator,
    IOptions<OrchestratorOptions> options,
    ILogger<PlayerTicketAuthenticator> logger)
{
    private const string ClientFacingRejection = "The authentication ticket was rejected.";

    public async Task<string?> DescribeAuthenticationFailureAsync(
        IPlatformUserInformationDto user, CancellationToken cancellationToken)
    {
        if (options.Value.SteamCredentialCheckDisabled)
            return null;

        if (string.IsNullOrWhiteSpace(user.AuthenticationTicketHex))
            return Reject(user, "No authentication ticket was supplied.");

        var validation = await ticketValidator.ValidateAsync(
            user.AuthenticationTicketHex, cancellationToken);

        if (!validation.IsValid)
            return Reject(user, validation.FailureDetail ?? "The validator rejected the ticket.");

        if (validation.IsBanned)
            return Reject(user, "The account is banned.");

        if (validation.SteamId is null)
            return Reject(user, "The validator asserted no identity.");

        if (!string.Equals(validation.SteamId, user.PlatformUserId, StringComparison.Ordinal))
            return Reject(user,
                $"The ticket belongs to {validation.SteamId} but claimed {user.PlatformUserId}.");

        return null;
    }

    private string Reject(IPlatformUserInformationDto user, string serverSideDetail)
    {
        logger.LogWarning(
            "Rejected a join by {Platform} player {PlatformUserId}: {Detail}",
            user.Platform, user.PlatformUserId, serverSideDetail);

        return ClientFacingRejection;
    }
}
