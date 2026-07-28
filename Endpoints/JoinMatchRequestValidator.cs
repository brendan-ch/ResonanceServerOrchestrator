using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Contracts;

namespace ResonanceServerOrchestrator.Endpoints;

internal static class JoinMatchRequestValidator
{
    public static bool TryValidate(
        JoinMatchDto? request,
        OrchestratorOptions limits,
        out ExpectedLobbyPlayerDto joiningPlayer,
        out string? problem)
    {
        joiningPlayer = null!;
        problem = DescribeFirstProblem(request, limits);

        if (problem is not null)
            return false;

        joiningPlayer = request!.ExpectedLobbyPlayers
            .First(player => player.GetIdentity() == request.PlatformUserInformation.GetIdentity());

        return true;
    }

    private static string? DescribeFirstProblem(JoinMatchDto? request, OrchestratorOptions limits)
    {
        // `required` only asserts that a property was present in the payload, so an explicit
        // null still binds. Every reference below has to be guarded here.
        if (request is null)
            return "the request body is required.";

        var userProblem = PlatformUserValidator.DescribeFirstProblem(
            request.PlatformUserInformation, limits);

        if (userProblem is not null)
            return userProblem;

        var roster = request.ExpectedLobbyPlayers;

        if (roster is null)
            return "expectedLobbyPlayers is required.";

        if (roster.Length == 0)
            return "expectedLobbyPlayers must contain at least one player.";

        if (roster.Length > limits.MaxExpectedLobbyPlayers)
            return $"expectedLobbyPlayers must contain at most {limits.MaxExpectedLobbyPlayers} players.";

        if (roster.Any(player => player is null))
            return "expectedLobbyPlayers must not contain null entries.";

        if (roster.Any(player => !Enum.IsDefined(player.Platform)))
            return "every expectedLobbyPlayers entry requires a supported platform.";

        if (roster.Any(player => string.IsNullOrWhiteSpace(player.PlatformUserId)))
            return "every expectedLobbyPlayers entry requires a platformUserId.";

        if (roster.Any(player => player.PlatformUserId.Length > limits.MaxPlatformIdentifierLength))
            return $"every expectedLobbyPlayers platformUserId must be at most {limits.MaxPlatformIdentifierLength} characters.";

        if (roster.Any(player => string.IsNullOrWhiteSpace(player.Username)))
            return "every expectedLobbyPlayers entry requires a username.";

        if (roster.Any(player => player.Username.Length > limits.MaxUsernameLength))
            return $"every expectedLobbyPlayers username must be at most {limits.MaxUsernameLength} characters.";

        var identities = roster.Select(player => player.GetIdentity()).ToList();

        if (identities.Distinct().Count() != identities.Count)
            return "expectedLobbyPlayers must not contain duplicate players.";

        if (!identities.Contains(request.PlatformUserInformation.GetIdentity()))
            return "the joining player must appear in their own expectedLobbyPlayers.";

        return null;
    }
}
