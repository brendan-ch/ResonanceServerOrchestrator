using System.Net;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class JoinMatchValidationTests : IDisposable
{
    private const string Player = "76561198000000001";

    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public JoinMatchValidationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Join_EmptyRoster_ReturnsBadRequest()
    {
        var response = await _client.PostJoinAsync(
            MatchRequests.JoinBody(Player, "lobby", []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_JoinerAbsentFromOwnRoster_ReturnsBadRequest()
    {
        var response = await _client.PostJoinAsync(
            MatchRequests.JoinBody(Player, "lobby", ["76561198000000009"]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_DuplicateRosterEntries_ReturnsBadRequest()
    {
        var response = await _client.PostJoinAsync(
            MatchRequests.JoinBody(Player, "lobby", [Player, Player]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"platformUserInformation":null,"expectedLobbyPlayers":[]}""")]
    [InlineData("""{"expectedLobbyPlayers":[]}""")]
    [InlineData("""{"platformUserInformation":{"platformUserId":"1","platformLobbyId":"l"},"expectedLobbyPlayers":[]}""")]
    [InlineData("""{"platformUserInformation":{"platform":"Xbox","platformUserId":"1","platformLobbyId":"l"},"expectedLobbyPlayers":[]}""")]
    [InlineData("""{"platformUserInformation":[],"expectedLobbyPlayers":[]}""")]
    [InlineData("""{"platformUserInformation":"steam","expectedLobbyPlayers":[]}""")]
    [InlineData("[]")]
    [InlineData("not json at all")]
    public async Task Join_UnbindableBody_ReturnsBadRequestNeverServerError(string json)
    {
        var response = await _client.PostRawJoinAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Join_PlatformDiscriminatorLast_StillBinds()
    {
        var json = $$"""
        {
          "platformUserInformation": {
            "platformUserId": "{{Player}}",
            "platformLobbyId": "lobby",
            "platform": "Steam"
          },
          "expectedLobbyPlayers": [
            { "username": "p", "platformUserId": "{{Player}}", "platform": "Steam" }
          ]
        }
        """;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var join = _client.PostRawJoinAsync(json, cancellation.Token);

        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, "lobby"), 1)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(join.IsFaulted);
    }

    [Fact]
    public async Task UnversionedRoute_IsNotMapped()
    {
        var response = await _client.PostAsync(
            "/matches/join",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
