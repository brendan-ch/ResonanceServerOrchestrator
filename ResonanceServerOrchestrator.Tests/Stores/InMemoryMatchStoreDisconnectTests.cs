using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreDisconnectTests
{
    private const string SampleNextSceneName = "TestScene";

    private static readonly OrchestratorOptions FortyFiveSecondAssemblyBudget = new()
    {
        RosterAssemblyTimeoutSeconds = 45,
        ServerReadyTimeoutSeconds = 600
    };

    [Fact]
    public void APendingDisconnectDeregistersTheMemberWithoutDiscardingTheMatch()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol");
        var alice = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);
        context.Join(MatchStoreTestContext.FirstLobby, "bob", roster, SampleNextSceneName);

        context.AbortRequestOf(alice, "alice");

        var match = context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby);
        Assert.NotNull(match);
        Assert.Equal(1, match.JoinedCount);
        Assert.DoesNotContain(MatchStoreTestContext.Player("alice"), match.Members.Keys);
        Assert.True(MatchStoreTestContext.CompletionOf(alice).IsCompleted);
    }


    [Fact]
    public void APendingDisconnectByTheSoleMemberDestroysTheMatch()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName);

        context.AbortRequestOf(alice, "alice");

        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
        Assert.Equal(0, context.Store.LiveMatchCount);
    }

    [Fact]
    public void ARetryAfterASoleMemberDisconnectGetsAFullAssemblyDeadlineRatherThanTheZombiesRemainder()
    {
        var context = new MatchStoreTestContext(FortyFiveSecondAssemblyBudget);
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        var abandoned = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);

        context.Clock.Advance(TimeSpan.FromSeconds(10));
        context.AbortRequestOf(abandoned, "alice");
        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);

        context.Clock.Advance(TimeSpan.FromSeconds(40));
        Assert.NotNull(context.Store.FindMatch(MatchStoreTestContext.MatchIdOf(retry)));

        context.Clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Null(context.Store.FindMatch(MatchStoreTestContext.MatchIdOf(retry)));
    }

    [Fact]
    public void ALaunchingDisconnectCompletesTheWaiterButKeepsTheMemberAndItsToken()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", "alice", "bob");

        context.AbortRequestOf(assembled.OutcomeAt(0), "alice");

        Assert.True(MatchStoreTestContext.CompletionOf(assembled.OutcomeAt(0)).IsCompleted);
        var lookup = context.Store.LookUpSnapshotForGameServer(
            assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey);
        Assert.Equal(MatchSnapshotLookupOutcome.Granted, lookup.Outcome);
        Assert.Contains(lookup.Snapshot!.Members, member => member.PlatformUserId == "alice");
    }

    [Fact]
    public void AStartedDisconnectCompletesTheWaiterButKeepsTheMemberAndItsToken()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", "alice", "bob");

        context.AbortRequestOf(assembled.OutcomeAt(0), "alice");

        var match = context.Store.FindMatch(assembled.Snapshot.MatchId);
        Assert.NotNull(match);
        Assert.Equal(2, match.JoinedCount);
    }

    [Fact]
    public void ALateAbortForASupersededMemberDoesNotDeregisterItsReplacement()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        var supersededAttempt = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);
        var replacementAttempt = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);

        context.AbortRequestOf(supersededAttempt, "alice");

        var match = context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby);
        Assert.NotNull(match);
        Assert.Contains(MatchStoreTestContext.Player("alice"), match.Members.Keys);
        Assert.False(MatchStoreTestContext.CompletionOf(replacementAttempt).IsCompleted);
    }

    [Fact]
    public void AnAbortForAMatchThatNoLongerExistsIsANoOp()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName);
        context.AbortRequestOf(alice, "alice");

        context.AbortRequestOf(alice, "alice");

        Assert.Equal(0, context.Store.LiveMatchCount);
    }
}