using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class JoinMatchDisconnectTests : IDisposable
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(20);

    private const string LobbyId = "steam-lobby-1";
    private const string FirstPlayer = "76561198000000001";
    private const string SecondPlayer = "76561198000000002";
    private static readonly string[] BothPlayers = [FirstPlayer, SecondPlayer];
    private static readonly LobbyKey Lobby = new(Platform.Steam, LobbyId);

    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public JoinMatchDisconnectTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> JoinAsync(string platformUserId, CancellationToken token) =>
        _client.PostJoinAsync(MatchRequests.JoinBody(platformUserId, LobbyId, BothPlayers), token);

    private Task<Guid> ParkedAsync(int memberCount) =>
        _factory.Store.WhenMemberCountReaches(Lobby, memberCount);

    [Fact]
    public async Task ASoleWaiterDisconnecting_DestroysTheMatchItWasAssembling()
    {
        using var cancellation = new CancellationTokenSource();

        var join = JoinAsync(FirstPlayer, cancellation.Token);
        await ParkedAsync(1).WaitAsync(TestBudget);

        Assert.Equal(1, _factory.Store.LiveMatchCount);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => join);

        await WaitUntilAsync(() => _factory.Store.LiveMatchCount == 0);
    }

    [Fact]
    public async Task ARetryAfterDisconnecting_StartsAFreshMatchRatherThanInheritingTheOldDeadline()
    {
        using var firstAttempt = new CancellationTokenSource();

        var abandoned = JoinAsync(FirstPlayer, firstAttempt.Token);
        await ParkedAsync(1).WaitAsync(TestBudget);

        await firstAttempt.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => abandoned);
        await WaitUntilAsync(() => _factory.Store.LiveMatchCount == 0);

        using var retry = new CancellationTokenSource(TestBudget);
        var rejoin = JoinAsync(FirstPlayer, retry.Token);
        await ParkedAsync(1).WaitAsync(TestBudget);

        _factory.Clock.Advance(TimeSpan.FromSeconds(44));
        Assert.False(rejoin.IsCompleted);

        _factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var response = await rejoin;

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OnePeerDisconnecting_LeavesTheOtherWaiting()
    {
        using var abandoning = new CancellationTokenSource();
        using var surviving = new CancellationTokenSource(TestBudget);

        var first = JoinAsync(FirstPlayer, surviving.Token);
        await ParkedAsync(1).WaitAsync(TestBudget);

        var second = JoinAsync(SecondPlayer, abandoning.Token);
        await ParkedAsync(2).WaitAsync(TestBudget);

        await abandoning.CancelAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => second);

        Assert.False(first.IsCompleted);
        Assert.Equal(1, _factory.Store.LiveMatchCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(TestBudget);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(condition(), "The expected store state was never reached.");
    }
}
