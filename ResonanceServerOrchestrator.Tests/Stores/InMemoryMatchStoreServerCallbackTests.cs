using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreServerCallbackTests
{
    [Fact]
    public void ReadyOnAPendingMatchReportsThatTheRosterIsNotYetComplete()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"));
        var match = context.Store.FindMatch(MatchStoreTestContext.MatchIdOf(alice))!;

        Assert.Equal(
            MarkReadyOutcome.RosterNotYetComplete, context.Store.MarkReady(match.MatchId, match.MatchKey));
    }

    [Fact]
    public void ReadyTwiceIsIdempotent()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "alice", "bob");

        Assert.Equal(
            MarkReadyOutcome.MatchWasAlreadyStarted,
            context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));
    }

    [Fact]
    public void ReadyWithTheWrongMatchKeyIsRejected()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "alice", "bob");

        Assert.Equal(
            MarkReadyOutcome.MatchKeyRejected,
            context.Store.MarkReady(assembled.Snapshot.MatchId, "not-the-match-key"));
    }

    [Fact]
    public void ReadyForAnIdThatNeverExistedReportsThatNoMatchWasFound()
    {
        var context = new MatchStoreTestContext();

        Assert.Equal(
            MarkReadyOutcome.MatchNotFound, context.Store.MarkReady(Guid.NewGuid(), "any-match-key"));
    }

    [Fact]
    public void ATombstoneDistinguishesADestroyedMatchFromOneThatNeverExisted()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "alice", "bob");
        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));

        Assert.Equal(
            MarkReadyOutcome.MatchAlreadyDestroyed,
            context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));
        Assert.Equal(
            MarkReadyOutcome.MatchNotFound, context.Store.MarkReady(Guid.NewGuid(), assembled.Snapshot.MatchKey));
    }

    [Fact]
    public void ATombstoneStillAuthenticatesTheMatchKey()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "alice", "bob");
        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));

        Assert.Equal(
            MarkReadyOutcome.MatchKeyRejected,
            context.Store.MarkReady(assembled.Snapshot.MatchId, "not-the-match-key"));
    }

    [Fact]
    public void TheGameServerRosterLookupRequiresTheMatchKey()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "alice", "bob");

        var granted = context.Store.LookUpSnapshotForGameServer(
            assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey);
        var rejected = context.Store.LookUpSnapshotForGameServer(
            assembled.Snapshot.MatchId, "not-the-match-key");

        Assert.Equal(MatchSnapshotLookupOutcome.Granted, granted.Outcome);
        Assert.Equal(["alice", "bob"], granted.Snapshot!.Members.Select(member => member.PlatformUserId));
        Assert.Equal(MatchSnapshotLookupOutcome.MatchKeyRejected, rejected.Outcome);
        Assert.Null(rejected.Snapshot);
    }

    [Fact]
    public void ReapingDestroysStartedMatchesOnceTheMatchTimeoutHasElapsedSinceTheReadyCallback()
    {
        var context = new MatchStoreTestContext(new OrchestratorOptions
        {
            MatchTimeoutMinutes = 30,
            RosterAssemblyTimeoutSeconds = 3600,
            ServerReadyTimeoutSeconds = 3600
        });
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);
        context.Clock.Advance(TimeSpan.FromMinutes(20));
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "alice", "bob");

        context.Clock.Advance(TimeSpan.FromMinutes(25));
        context.Store.ReapExpired();
        Assert.NotNull(context.Store.FindMatch(assembled.Snapshot.MatchId));

        context.Clock.Advance(TimeSpan.FromMinutes(6));
        context.Store.ReapExpired();
        Assert.Null(context.Store.FindMatch(assembled.Snapshot.MatchId));
    }

    [Fact]
    public void ReapingRemovesTombstonesOnlyAfterTheRetentionWindow()
    {
        var context = new MatchStoreTestContext(new OrchestratorOptions { TombstoneRetentionMinutes = 10 });
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "alice", "bob");
        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));

        context.Clock.Advance(TimeSpan.FromMinutes(9));
        context.Store.ReapExpired();
        Assert.Equal(
            MarkReadyOutcome.MatchAlreadyDestroyed,
            context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));

        context.Clock.Advance(TimeSpan.FromMinutes(2));
        context.Store.ReapExpired();
        Assert.Equal(
            MarkReadyOutcome.MatchNotFound,
            context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));
    }
}
