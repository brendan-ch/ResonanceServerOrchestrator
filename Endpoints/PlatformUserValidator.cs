using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Contracts;

namespace ResonanceServerOrchestrator.Endpoints;

internal static class PlatformUserValidator
{
    public static string? DescribeFirstProblem(
        IPlatformUserInformationDto user, OrchestratorOptions limits)
    {
        if (string.IsNullOrWhiteSpace(user.PlatformUserId))
            return "platformUserInformation.platformUserId must not be empty.";

        if (user.PlatformUserId.Length > limits.MaxPlatformIdentifierLength)
            return $"platformUserInformation.platformUserId must be at most {limits.MaxPlatformIdentifierLength} characters.";

        if (string.IsNullOrWhiteSpace(user.PlatformLobbyId))
            return "platformUserInformation.platformLobbyId must not be empty.";

        if (user.PlatformLobbyId.Length > limits.MaxPlatformIdentifierLength)
            return $"platformUserInformation.platformLobbyId must be at most {limits.MaxPlatformIdentifierLength} characters.";

        if (user.AuthenticationTicketHex?.Length > limits.MaxAuthenticationTicketHexLength)
            return $"platformUserInformation.authenticationTicketHex must be at most {limits.MaxAuthenticationTicketHexLength} characters.";

        return null;
    }
}
