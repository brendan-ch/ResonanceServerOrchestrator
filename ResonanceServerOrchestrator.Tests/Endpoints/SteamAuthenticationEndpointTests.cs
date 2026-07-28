using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class SteamAuthenticationEndpointTests : IDisposable
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    private const string LobbyId = "steam-lobby-1";
    private const string FirstPlayer = "76561198000000001";
    private const string SecondPlayer = "76561198000000002";
    private static readonly string[] BothPlayers = [FirstPlayer, SecondPlayer];

    private readonly OrchestratorWebApplicationFactory _factory = new(
        new Dictionary<string, string?>
        {
            [$"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.SteamCredentialCheckDisabled)}"] = "false",
            [$"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.SteamPublisherWebApiKey)}"] = "test-key",
            [$"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.SteamAppId)}"] = "480",
        });

    private readonly HttpClient _client;

    public SteamAuthenticationEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private void TicketResolvesTo(string ticketHex, string steamId) =>
        _factory.TicketValidatorSubstitute
            .ValidateAsync(ticketHex, Arg.Any<CancellationToken>())
            .Returns(new SteamTicketValidationResult(true, steamId, false, null));

    private void TicketIsRejected(string ticketHex) =>
        _factory.TicketValidatorSubstitute
            .ValidateAsync(ticketHex, Arg.Any<CancellationToken>())
            .Returns(new SteamTicketValidationResult(false, null, false, "Steam rejected the ticket."));

    private Task<HttpResponseMessage> JoinAsync(
        string platformUserId, string ticketHex, CancellationToken token) =>
        _client.PostJoinAsync(
            MatchRequests.JoinBody(platformUserId, LobbyId, BothPlayers, ticketHex), token);

    [Fact]
    public async Task Join_WithNoTicket_ReturnsUnauthorized()
    {
        var response = await _client.PostJoinAsync(
            MatchRequests.JoinBody(FirstPlayer, LobbyId, BothPlayers));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Join_WithARejectedTicket_ReturnsUnauthorized()
    {
        TicketIsRejected("bad-ticket");

        var response = await JoinAsync(FirstPlayer, "bad-ticket", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Join_WhenTheTicketBelongsToSomeoneElse_ReturnsUnauthorized()
    {
        TicketResolvesTo("borrowed-ticket", SecondPlayer);

        var response = await JoinAsync(FirstPlayer, "borrowed-ticket", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Join_WithABannedAccount_ReturnsUnauthorized()
    {
        _factory.TicketValidatorSubstitute
            .ValidateAsync("banned-ticket", Arg.Any<CancellationToken>())
            .Returns(new SteamTicketValidationResult(true, FirstPlayer, true, null));

        var response = await JoinAsync(FirstPlayer, "banned-ticket", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Join_WithAValidTicket_Proceeds()
    {
        TicketResolvesTo("good-ticket", FirstPlayer);

        using var cancellation = new CancellationTokenSource(TestBudget);
        var join = JoinAsync(FirstPlayer, "good-ticket", cancellation.Token);

        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        Assert.False(join.IsCompleted);
    }

    [Fact]
    public async Task Join_WhenARosterMemberFailsAuth_TearsDownTheMatchForEveryone()
    {
        TicketResolvesTo("good-ticket", FirstPlayer);
        TicketIsRejected("bad-ticket");

        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, "good-ticket", cancellation.Token);

        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        var rejected = await JoinAsync(SecondPlayer, "bad-ticket", cancellation.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        var firstResponse = await first;
        Assert.Equal(HttpStatusCode.Conflict, firstResponse.StatusCode);

        var failure = await firstResponse.Content
            .ReadFromJsonAsync<JoinFailureDto>(MatchRequests.SerializerOptions);

        Assert.Equal(JoinFailureReason.PeerAuthenticationFailed, failure!.Reason);
    }

    [Fact]
    public async Task Join_WhenAStrangerFailsAuth_LeavesTheMatchAlone()
    {
        TicketResolvesTo("good-ticket", FirstPlayer);
        TicketIsRejected("stranger-ticket");

        using var cancellation = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, "good-ticket", cancellation.Token);

        await _factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Steam, LobbyId), 1)
            .WaitAsync(TestBudget);

        var stranger = await _client.PostJoinAsync(
            MatchRequests.JoinBody(
                "76561198000000099", LobbyId, ["76561198000000099"], "stranger-ticket"),
            cancellation.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, stranger.StatusCode);
        Assert.False(first.IsCompleted);
    }

    [Fact]
    public async Task Leave_WithARejectedTicket_ReturnsUnauthorized()
    {
        TicketIsRejected("bad-ticket");

        var body = new
        {
            platformUserInformation = new
            {
                platform = "Steam",
                platformUserId = FirstPlayer,
                platformLobbyId = LobbyId,
                authenticationTicketHex = "bad-ticket",
            },
        };

        var response = await _client.PostLeaveAsync(body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
