using System.Net;
using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

/// <summary>
/// The dummy platform asserts an identity without proving it. Before the platform user
/// information was flattened these payloads could not deserialize at all, so nothing enforced
/// the restriction the feature was introduced with.
/// </summary>
public sealed class DummyPlatformEndpointTests
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    private const string LobbyId = "dummy-lobby-1";
    private const string Player = "dummy-player-1";

    /// <remarks>
    /// A ticket is supplied deliberately. Without it these payloads would be rejected by the
    /// "no authentication ticket was supplied" branch, and the tests would pass whether or not
    /// the dummy-platform guard exists.
    /// </remarks>
    private const string AcceptedTicket = "accepted-ticket";

    private static object DummyJoinBody(string lobbyId = LobbyId) => new
    {
        platformUserInformation = new
        {
            platform = "Dummy",
            platformUserId = Player,
            platformLobbyId = lobbyId,
            authenticationTicketHex = AcceptedTicket,
        },
        expectedLobbyPlayers = new[]
        {
            new { username = "dummy", platform = "Dummy", platformUserId = Player },
        },
    };

    private static object DummyLeaveBody() => new
    {
        platformUserInformation = new
        {
            platform = "Dummy",
            platformUserId = Player,
            platformLobbyId = LobbyId,
            authenticationTicketHex = AcceptedTicket,
        },
    };

    private static void TicketIsAccepted(OrchestratorWebApplicationFactory factory) =>
        factory.TicketValidatorSubstitute
            .ValidateAsync(AcceptedTicket, Arg.Any<CancellationToken>())
            .Returns(new SteamTicketValidationResult(true, Player, false, null));

    private static OrchestratorWebApplicationFactory WithCredentialChecking() => new(
        new Dictionary<string, string?>
        {
            [$"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.SteamCredentialCheckDisabled)}"] = "false",
            [$"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.SteamPublisherWebApiKey)}"] = "test-key",
            [$"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.SteamAppId)}"] = "480",
        });

    [Fact]
    public async Task Join_AsDummy_WhileCredentialCheckingIsOn_ReturnsUnauthorized()
    {
        using var factory = WithCredentialChecking();
        TicketIsAccepted(factory);
        using var client = factory.CreateClient();

        var response = await client.PostJoinAsync(DummyJoinBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Leave_AsDummy_WhileCredentialCheckingIsOn_ReturnsUnauthorized()
    {
        using var factory = WithCredentialChecking();
        TicketIsAccepted(factory);
        using var client = factory.CreateClient();

        var response = await client.PostLeaveAsync(DummyLeaveBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Join_AsDummy_WhileCredentialCheckingIsOff_IsAccepted()
    {
        using var factory = new OrchestratorWebApplicationFactory();
        using var client = factory.CreateClient();

        using var cancellation = new CancellationTokenSource(TestBudget);
        var join = client.PostJoinAsync(DummyJoinBody(), cancellation.Token);

        // A single-player roster completes immediately, so reaching the store at all is the
        // signal that authentication let the request through.
        await factory.Store
            .WhenMemberCountReaches(new LobbyKey(Platform.Dummy, LobbyId), 1)
            .WaitAsync(TestBudget);

        Assert.False(join.IsFaulted);
    }
}
