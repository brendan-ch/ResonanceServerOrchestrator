using ResonanceServerOrchestrator.Contracts;

namespace ResonanceServerOrchestrator.Endpoints;

internal static class JoinMatchRequestValidator
{
    public static string? DescribeFirstProblem(JoinMatchDto request)
    {
        var user = request.PlatformUserInformation;

        if (user is null)
            return "platformUserInformation is required.";

        if (string.IsNullOrWhiteSpace(user.PlatformUserId))
            return "platformUserInformation.platformUserId must not be empty.";

        if (string.IsNullOrWhiteSpace(user.PlatformLobbyId))
            return "platformUserInformation.platformLobbyId must not be empty.";

        var roster = request.ExpectedLobbyPlayers;

        if (roster is null || roster.Length == 0)
            return "expectedLobbyPlayers must contain at least one player.";

        if (roster.Any(player => player is null))
            return "expectedLobbyPlayers must not contain null entries.";

        if (roster.Any(player => string.IsNullOrWhiteSpace(player.PlatformUserId)))
            return "every expectedLobbyPlayers entry requires a platformUserId.";

        if (roster.Any(player => string.IsNullOrWhiteSpace(player.Username)))
            return "every expectedLobbyPlayers entry requires a username.";

        var identities = roster.Select(player => player.Identity).ToList();

        if (identities.Distinct().Count() != identities.Count)
            return "expectedLobbyPlayers must not contain duplicate players.";

        if (!identities.Contains(user.Identity))
            return "the joining player must appear in their own expectedLobbyPlayers.";

        return null;
    }
}
