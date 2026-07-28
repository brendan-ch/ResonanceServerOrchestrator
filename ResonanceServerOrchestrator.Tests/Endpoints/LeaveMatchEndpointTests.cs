using System.Net;
using System.Net.Http.Json;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class LeaveMatchEndpointTests : IDisposable
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    private const string LobbyId = "steam-lobby-1";
    private const string FirstPlayer = "76561198000000001";
    private const string SecondPlayer = "76561198000000002";
    private static readonly string[] BothPlayers = [FirstPlayer, SecondPlayer];

    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public LeaveMatchEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Leave_WithNoMembership_ReturnsNotFound()
    {
        var response = await _client.PostLeaveAsync(
            MatchRequests.LeaveBody(FirstPlayer, LobbyId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Leave_WhilePending_ReleasesEveryWaiterWithPeerLeft()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = _client.PostJoinAsync(
            MatchRequests.JoinBody(FirstPlayer, LobbyId, BothPlayers), cancellation.Token);

        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        var leave = await _client.PostLeaveAsync(
            MatchRequests.LeaveBody(FirstPlayer, LobbyId), cancellation.Token);

        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);

        var failure = await firstResponse.Content
            .ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);

        Assert.Equal(JoinFailureReason.PeerLeft, failure!.Reason);
    }

    [Fact]
    public async Task Leave_ByOnePlayerDropsTheWholePendingMatch()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = _client.PostJoinAsync(
            MatchRequests.JoinBody(FirstPlayer, LobbyId, BothPlayers), cancellation.Token);
        var second = _client.PostJoinAsync(
            MatchRequests.JoinBody(SecondPlayer, LobbyId, BothPlayers), cancellation.Token);

        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 2)
            .WaitAsync(TestBudget);

        await _factory.LaunchObserved.WaitAsync(TestBudget);

        await _client.PostLeaveAsync(
            MatchRequests.LeaveBody(FirstPlayer, LobbyId), cancellation.Token);

        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
    }

    [Fact]
    public async Task Leave_MissingPlatformUserId_ReturnsBadRequest()
    {
        var response = await _client.PostRawLeaveAsync(
            """{"platformUserInformation":{"platform":"Steam","platformUserId":"","platformLobbyId":"l"}}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <remarks>
    /// The join path has had this theory since the converter existed; leave never did, so the
    /// null guard added when `required` stopped protecting it had no direct coverage.
    /// </remarks>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"platformUserInformation":null}""")]
    [InlineData("""{"platformUserInformation":[]}""")]
    [InlineData("""{"platformUserInformation":"steam"}""")]
    [InlineData("""{"platformUserInformation":{"platformUserId":"1","platformLobbyId":"l"}}""")]
    [InlineData("""{"platformUserInformation":{"platform":"Steam","platformLobbyId":"l"}}""")]
    [InlineData("""{"platformUserInformation":{"platform":"Steam","platformUserId":"1"}}""")]
    [InlineData("""{"platformUserInformation":{"platform":99,"platformUserId":"1","platformLobbyId":"l"}}""")]
    [InlineData("""{"platformUserInformation":{"platform":"Xbox","platformUserId":"1","platformLobbyId":"l"}}""")]
    [InlineData("[]")]
    [InlineData("not json at all")]
    public async Task Leave_UnbindableBody_ReturnsBadRequestNeverServerError(string json)
    {
        var response = await _client.PostRawLeaveAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
