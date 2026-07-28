using System.Net;
using System.Net.Http.Json;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Endpoints;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class ServerEndpointTests : IDisposable
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    private const string LobbyId = "steam-lobby-1";
    private const string FirstPlayer = "76561198000000001";
    private const string SecondPlayer = "76561198000000002";
    private static readonly string[] BothPlayers = [FirstPlayer, SecondPlayer];

    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ServerEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private sealed record LaunchedMatch(Guid MatchId, string MatchKey, Task<HttpResponseMessage>[] Joins);

    private async Task<LaunchedMatch> AssembleMatchAsync(CancellationToken token)
    {
        var joins = new[]
        {
            _client.PostJoinAsync(MatchRequests.JoinBody(FirstPlayer, LobbyId, BothPlayers), token),
            _client.PostJoinAsync(MatchRequests.JoinBody(SecondPlayer, LobbyId, BothPlayers), token),
        };

        var matchId = await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 2)
            .WaitAsync(TestBudget);

        var spec = await _factory.LaunchObserved.WaitAsync(TestBudget);

        return new LaunchedMatch(
            matchId, spec.Environment[GameServerLaunchSpec.MatchKeyVariable], joins);
    }

    private Task<HttpResponseMessage> SendServerRequestAsync(
        HttpMethod method, string path, string? matchKey)
    {
        var request = new HttpRequestMessage(method, path);
        if (matchKey is not null)
            request.Headers.Add(ServerEndpoints.MatchKeyHeader, matchKey);

        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostReadyAsync(Guid matchId, string? matchKey) =>
        SendServerRequestAsync(HttpMethod.Post, $"/v1/server/matches/{matchId:D}/ready", matchKey);

    private Task<HttpResponseMessage> GetMembersAsync(Guid matchId, string? matchKey) =>
        SendServerRequestAsync(HttpMethod.Get, $"/v1/server/matches/{matchId:D}/members", matchKey);

    [Fact]
    public async Task Ready_WithTheCorrectMatchKey_ReturnsNoContent()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);

        var response = await PostReadyAsync(match.MatchId, match.MatchKey);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await Task.WhenAll(match.Joins);
    }

    [Fact]
    public async Task Ready_CalledTwice_IsIdempotent()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);

        await PostReadyAsync(match.MatchId, match.MatchKey);
        var second = await PostReadyAsync(match.MatchId, match.MatchKey);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        await Task.WhenAll(match.Joins);
    }

    [Fact]
    public async Task Ready_WithTheWrongMatchKey_ReturnsUnauthorized()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);

        var response = await PostReadyAsync(match.MatchId, "not-the-match-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await PostReadyAsync(match.MatchId, match.MatchKey);
        await Task.WhenAll(match.Joins);
    }

    [Fact]
    public async Task Ready_WithNoMatchKeyHeader_ReturnsUnauthorized()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);

        var response = await PostReadyAsync(match.MatchId, matchKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await PostReadyAsync(match.MatchId, match.MatchKey);
        await Task.WhenAll(match.Joins);
    }

    [Fact]
    public async Task Ready_ForAnUnknownMatch_ReturnsNotFound()
    {
        var response = await PostReadyAsync(Guid.NewGuid(), "any-key");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ready_AfterTheMatchWasTornDown_ReturnsGone()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);

        _factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await Task.WhenAll(match.Joins);

        var response = await PostReadyAsync(match.MatchId, match.MatchKey);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Members_ReturnsEveryMemberWithADistinctToken()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);
        await PostReadyAsync(match.MatchId, match.MatchKey);

        var response = await GetMembersAsync(match.MatchId, match.MatchKey);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var members = await response.Content
            .ReadFromJsonAsync<MatchMemberDto[]>(MatchRequests.SerializerOptions);

        Assert.Equal(2, members!.Length);
        Assert.Equal(
            [FirstPlayer, SecondPlayer],
            members.Select(member => member.PlatformUserId).OrderBy(id => id));
        Assert.Equal(2, members.Select(member => member.ServerAuthToken).Distinct().Count());
        Assert.All(members, member => Assert.Equal(Platform.Steam, member.Platform));

        await Task.WhenAll(match.Joins);
    }

    [Fact]
    public async Task Members_TokensMatchWhatTheJoiningClientsReceived()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);
        await PostReadyAsync(match.MatchId, match.MatchKey);

        var joinResponses = await Task.WhenAll(match.Joins);
        var clientTokens = await Task.WhenAll(joinResponses.Select(response =>
            response.Content.ReadFromJsonAsync<JoinMatchResultDto>(MatchRequests.SerializerOptions)));

        var membersResponse = await GetMembersAsync(match.MatchId, match.MatchKey);
        var members = await membersResponse.Content
            .ReadFromJsonAsync<MatchMemberDto[]>(MatchRequests.SerializerOptions);

        Assert.Equal(
            clientTokens.Select(result => result!.ServerAuthToken).OrderBy(token => token),
            members!.Select(member => member.ServerAuthToken).OrderBy(token => token));
    }

    [Fact]
    public async Task Members_WithTheWrongMatchKey_ReturnsUnauthorized()
    {
        using var cancellation = new CancellationTokenSource(TestBudget);
        var match = await AssembleMatchAsync(cancellation.Token);
        await PostReadyAsync(match.MatchId, match.MatchKey);

        var response = await GetMembersAsync(match.MatchId, "not-the-match-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await Task.WhenAll(match.Joins);
    }

    [Fact]
    public async Task Members_ForAnUnknownMatch_ReturnsNotFound()
    {
        var response = await GetMembersAsync(Guid.NewGuid(), "any-key");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
