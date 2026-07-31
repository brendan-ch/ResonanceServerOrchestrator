using NSubstitute;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreStartedMatchProtectionTests
{
    private static readonly LobbyKey Lobby = MatchStoreTestContext.FirstLobby;
    private const string SampleNextSceneName = "TestScene";

    private static MatchStoreTestContext StartedMatchWithInstance(out IGameInstance instance)
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(Lobby, "TestScene", "alice", "bob");

        instance = Substitute.For<IGameInstance>();
        instance.HasExited.Returns(false);
        Assert.True(context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance));

        Assert.Equal(
            MarkReadyOutcome.MatchStarted,
            context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));

        return context;
    }

    [Fact]
    public void AStrangerSubmittingAMismatchedRosterCannotDestroyARunningMatch()
    {
        var context = StartedMatchWithInstance(out var instance);

        var outcome = context.Join(Lobby, "attacker", MatchStoreTestContext.Roster("attacker"), SampleNextSceneName);

        var rejected = Assert.IsType<Rejected>(outcome);
        Assert.Equal(JoinFailureReason.MatchAlreadyStarted, rejected.Reason);

        instance.DidNotReceive().Stop();
        Assert.Equal(1, context.Store.LiveMatchCount);
    }

    [Fact]
    public void AMismatchedRosterAgainstARunningMatchLeavesTheGameServerReachable()
    {
        var context = StartedMatchWithInstance(out _);
        var match = Assert.Single(
            new[] { context.Store.FindMatchInLobby(Lobby) }.OfType<MatchState>());

        context.Join(Lobby, "attacker", MatchStoreTestContext.Roster("attacker", "accomplice"), SampleNextSceneName);

        var lookup = context.Store.LookUpSnapshotForGameServer(match.MatchId, match.MatchKey);

        Assert.Equal(MatchSnapshotLookupOutcome.Granted, lookup.Outcome);
        Assert.Equal(2, lookup.Snapshot!.Members.Count);
    }

    [Fact]
    public void AMismatchedRosterStillDiscardsAMatchThatHasNotStarted()
    {
        var context = new MatchStoreTestContext();
        context.Join(Lobby, "alice", MatchStoreTestContext.Roster("alice", "bob"), SampleNextSceneName);

        var outcome = context.Join(Lobby, "bob", MatchStoreTestContext.Roster("bob", "carol"), SampleNextSceneName);

        Assert.Equal(JoinFailureReason.RosterMismatch, Assert.IsType<Rejected>(outcome).Reason);
        Assert.Equal(0, context.Store.LiveMatchCount);
    }
}
