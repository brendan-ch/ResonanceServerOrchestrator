using System.Net;
using System.Net.Http.Json;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Endpoints;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class JoinMatchEndpointTests : IDisposable
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    private const string LobbyId = "steam-lobby-1";
    private const string FirstPlayer = "76561198000000001";
    private const string SecondPlayer = "76561198000000002";

    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public JoinMatchEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static readonly string[] BothPlayers = [FirstPlayer, SecondPlayer];

    private Task<HttpResponseMessage> JoinAsync(string platformUserId,
        CancellationToken token,
        string? intendedServerVersion = null
    ) =>
        _client.PostJoinAsync(
            MatchRequests.JoinBody(
                platformUserId,
                LobbyId,
                BothPlayers,
                intendedServerVersion: intendedServerVersion
            ),
            token);

    private Task<Guid> BothPlayersParkedAsync() =>
        _factory.Store.WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 2);

    private async Task ReportServerReadyAsync(Guid matchId)
    {
        var spec = await _factory.LaunchObserved.WaitAsync(TestBudget);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/v1/server/matches/{matchId:D}/ready");
        request.Headers.Add(
            ServerEndpoints.MatchKeyHeader,
            spec.Environment[LocalGameServerLaunchSpec.MatchKeyVariable]);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Join_ReleasesEveryWaiterOnceTheServerReportsReady()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        var second = JoinAsync(SecondPlayer, cancellation.Token);

        var matchId = await BothPlayersParkedAsync().WaitAsync(TestBudget);
        await ReportServerReadyAsync(matchId);

        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var results = await Task.WhenAll(
            responses.Select(r => r.Content.ReadFromJsonAsync<JoinMatchResultDto>(MatchRequests.SerializerOptions)));

        Assert.All(results, result =>
        {
            Assert.Equal(matchId, result!.MatchId);
            Assert.Equal("test-host", result.DedicatedServerHost);
            Assert.Equal(7777, result.DedicatedServerPort);
            Assert.False(string.IsNullOrWhiteSpace(result.ServerAuthToken));
        });

        Assert.NotEqual(results[0]!.ServerAuthToken, results[1]!.ServerAuthToken);
    }

    [Fact]
    public async Task Join_LaunchesTheServerWithTheMatchEnvironment()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        var second = JoinAsync(SecondPlayer, cancellation.Token);

        var matchId = await BothPlayersParkedAsync().WaitAsync(TestBudget);
        var spec = await _factory.LaunchObserved.WaitAsync(TestBudget);

        Assert.Equal(
            matchId.ToString("D"),
            spec.Environment[LocalGameServerLaunchSpec.MatchIdVariable]);
        Assert.Equal("7777", spec.Environment[LocalGameServerLaunchSpec.GameServerPortVariable]);
        Assert.Equal(
            "http://orchestrator.test",
            spec.Environment[LocalGameServerLaunchSpec.OrchestratorUrlVariable]);
        Assert.False(string.IsNullOrWhiteSpace(
            spec.Environment[LocalGameServerLaunchSpec.MatchKeyVariable]));

        await ReportServerReadyAsync(matchId);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Join_DoesNotLaunchUntilTheRosterIsComplete()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        Assert.False(_factory.HasLaunched);

        var second = JoinAsync(SecondPlayer, cancellation.Token);
        var matchId = await BothPlayersParkedAsync().WaitAsync(TestBudget);

        await ReportServerReadyAsync(matchId);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Join_RosterAssemblyDeadlineExpires_ReturnsConflictWithTheReason()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        _factory.Clock.Advance(TimeSpan.FromSeconds(45));

        var response = await first;

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var failure = await response.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);
        Assert.Equal(JoinFailureReason.RosterAssemblyTimedOut, failure!.Reason);
        Assert.Equal(1, failure.JoinedCount);
        Assert.Equal(2, failure.ExpectedCount);
    }

    [Fact]
    public async Task Join_ServerNeverReportsReady_ReturnsServerReadyTimedOut()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        var second = JoinAsync(SecondPlayer, cancellation.Token);

        await BothPlayersParkedAsync().WaitAsync(TestBudget);

        _factory.Clock.Advance(TimeSpan.FromSeconds(30));

        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));

        var failure = await responses[0].Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);
        Assert.Equal(JoinFailureReason.ServerReadyTimedOut, failure!.Reason);
    }

    [Fact]
    public async Task Join_AssemblyDeadlineDoesNotFireOnceTheRosterIsComplete()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        var second = JoinAsync(SecondPlayer, cancellation.Token);

        var matchId = await BothPlayersParkedAsync().WaitAsync(TestBudget);

        _factory.Clock.Advance(TimeSpan.FromSeconds(29));

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        await ReportServerReadyAsync(matchId);

        var responses = await Task.WhenAll(first, second);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task Join_MismatchedRoster_DiscardsTheMatchAndReleasesEveryone()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        var mismatched = await _client.PostJoinAsync(
            MatchRequests.JoinBody(
                SecondPlayer, LobbyId, [SecondPlayer, "76561198000000009"]),
            cancellation.Token);

        Assert.Equal(HttpStatusCode.Conflict, mismatched.StatusCode);

        var mismatchedFailure =
            await mismatched.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);
        Assert.Equal(JoinFailureReason.RosterMismatch, mismatchedFailure!.Reason);

        var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);

        var firstFailure =
            await firstResponse.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);
        Assert.Equal(JoinFailureReason.RosterMismatch, firstFailure!.Reason);
    }

    [Fact]
    public async Task Join_MismatchedIntendedServerVersion_DiscardsTheMatchAndReleasesEveryone()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token, "server-1");
        var second = JoinAsync(SecondPlayer, cancellation.Token, "server-2");

        var secondResponse = await second;
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var mismatchedFailure =
            await secondResponse.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions,
                cancellationToken: cancellation.Token);
        Assert.Equal(JoinFailureReason.OtherDataMismatch, mismatchedFailure!.Reason);

        var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);

        var firstFailure =
            await firstResponse.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions,
                cancellationToken: cancellation.Token);
        Assert.Equal(JoinFailureReason.OtherDataMismatch, firstFailure!.Reason);
    }

    [Fact]
    public async Task Join_SecondLobbyWhileAtCapacity_ReturnsServiceUnavailable()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        var otherLobby = await _client.PostJoinAsync(
            MatchRequests.JoinBody("76561198000000003", "steam-lobby-2", ["76561198000000003"]),
            cancellation.Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, otherLobby.StatusCode);
        Assert.NotNull(otherLobby.Headers.RetryAfter);

        var failure = await otherLobby.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);
        Assert.Equal(JoinFailureReason.CapacityReached, failure!.Reason);
    }

    [Fact]
    public async Task Join_SecondMemberOfTheSameLobbyIsNotRejectedByCapacity()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, cancellation.Token);
        var second = JoinAsync(SecondPlayer, cancellation.Token);

        var matchId = await BothPlayersParkedAsync().WaitAsync(TestBudget);
        await ReportServerReadyAsync(matchId);

        var responses = await Task.WhenAll(first, second);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public async Task Join_JoiningPlayerAlreadyInAnotherLobby_ReturnsPlayerInMultipleLobbies()
    {
        using var factory = new OrchestratorWebApplicationFactory(
            new Dictionary<string, string?> { ["Orchestrator:MaxMatches"] = "1" });
        using var client = factory.CreateClient();
        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = client.PostJoinAsync(
            MatchRequests.JoinBody(FirstPlayer, LobbyId, BothPlayers), cancellation.Token);

        await factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        var otherLobby = await client.PostJoinAsync(
            MatchRequests.JoinBody(FirstPlayer, "steam-lobby-other", [FirstPlayer]),
            cancellation.Token);

        Assert.Equal(HttpStatusCode.Conflict, otherLobby.StatusCode);

        var failure = await otherLobby.Content.ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);
        Assert.Equal(JoinFailureReason.PlayerInMultipleLobbies, failure!.Reason);

        var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);
    }
}