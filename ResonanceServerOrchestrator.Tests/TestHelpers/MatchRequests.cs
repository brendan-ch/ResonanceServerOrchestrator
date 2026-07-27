using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResonanceServerOrchestrator.Serialization;

namespace ResonanceServerOrchestrator.Tests.TestHelpers;

internal static class MatchRequests
{
    public const string JoinPath = "/v1/matches/join";
    public const string LeavePath = "/v1/matches/leave";

    public static readonly JsonSerializerOptions SerializerOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        }.ApplyOrchestratorConventions();

    public static object Player(string platformUserId, string username = "player") => new
    {
        username,
        platform = "Steam",
        platformUserId,
    };

    public static object JoinBody(
        string platformUserId,
        string lobbyId,
        IEnumerable<string> rosterPlatformUserIds,
        string? authenticationTicketHex = null) => new
    {
        platformUserInformation = new
        {
            platform = "Steam",
            platformUserId,
            platformLobbyId = lobbyId,
            authenticationTicketHex,
        },
        expectedLobbyPlayers = rosterPlatformUserIds
            .Select(id => Player(id, $"player-{id}"))
            .ToArray(),
    };

    public static object LeaveBody(string platformUserId, string lobbyId) => new
    {
        platformUserInformation = new
        {
            platform = "Steam",
            platformUserId,
            platformLobbyId = lobbyId,
            authenticationTicketHex = (string?)null,
        },
    };

    public static Task<HttpResponseMessage> PostJoinAsync(
        this HttpClient client, object body, CancellationToken cancellationToken = default) =>
        client.PostAsJsonAsync(JoinPath, body, SerializerOptions, cancellationToken);

    public static Task<HttpResponseMessage> PostLeaveAsync(
        this HttpClient client, object body, CancellationToken cancellationToken = default) =>
        client.PostAsJsonAsync(LeavePath, body, SerializerOptions, cancellationToken);

    public static Task<HttpResponseMessage> PostRawJoinAsync(
        this HttpClient client, string json, CancellationToken cancellationToken = default) =>
        client.PostAsync(
            JoinPath,
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            cancellationToken);
}
