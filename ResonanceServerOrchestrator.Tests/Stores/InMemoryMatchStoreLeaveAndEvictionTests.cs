using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreLeaveAndEvictionTests
{
    private const string SampleNextSceneName = "TestScene";
    private static readonly OrchestratorOptions TwoConcurrentMatches = new() { MaxMatches = 2 };

    [Fact]
    public async Task LeavingAPendingMatchReleasesEveryWaiterWithPeerLeftAndDiscardsTheMatch()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol");
        var alice = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);
        var bob = context.Join(MatchStoreTestContext.FirstLobby, "bob", roster, SampleNextSceneName);

        Assert.True(context.Store.TryLeave(MatchStoreTestContext.Player("bob")));

        Assert.Equal(JoinFailureReason.PeerLeft, (await MatchStoreTestContext.FailureOf(alice)).Reason);
        Assert.Equal(JoinFailureReason.PeerLeft, (await MatchStoreTestContext.FailureOf(bob)).Reason);
        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public void LeavingAStartedMatchRemovesOnlyThatMembership()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", "alice", "bob");
        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);

        Assert.True(context.Store.TryLeave(MatchStoreTestContext.Player("alice")));

        var match = context.Store.FindMatch(assembled.Snapshot.MatchId);
        Assert.NotNull(match);
        Assert.Equal(1, match.JoinedCount);
        instance.DidNotReceive().Stop();
    }

    [Fact]
    public void TheLastLeaverOfAStartedMatchStopsTheGameInstance()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", "alice", "bob");
        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);

        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));
        context.Store.TryLeave(MatchStoreTestContext.Player("bob"));

        instance.Received(1).Stop();
        Assert.Null(context.Store.FindMatch(assembled.Snapshot.MatchId));
    }

    [Fact]
    public void LeavingWithoutAMembershipReportsThatNothingWasRemoved()
    {
        var context = new MatchStoreTestContext();

        Assert.False(context.Store.TryLeave(MatchStoreTestContext.Player("stranger")));
    }

    [Fact]
    public async Task AFailedAuthDiscardsAPendingMatchWhoseCanonicalRosterNamesTheClaimedIdentity()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName);

        Assert.True(context.Store.TryTearDownForFailedAuth(
            MatchStoreTestContext.FirstLobby, MatchStoreTestContext.Player("bob")));

        Assert.Equal(
            JoinFailureReason.PeerAuthenticationFailed,
            (await MatchStoreTestContext.FailureOf(alice)).Reason);
        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public void AFailedAuthLeavesTheMatchAloneWhenTheClaimedIdentityIsNotOnTheCanonicalRoster()
    {
        var context = new MatchStoreTestContext();
        context.Join(MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName);

        Assert.False(context.Store.TryTearDownForFailedAuth(
            MatchStoreTestContext.FirstLobby, MatchStoreTestContext.Player("mallory")));

        Assert.NotNull(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public void AFailedAuthLeavesAStartedMatchRunning()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", "alice", "bob");
        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);

        Assert.False(context.Store.TryTearDownForFailedAuth(
            MatchStoreTestContext.FirstLobby, MatchStoreTestContext.Player("alice")));

        Assert.NotNull(context.Store.FindMatch(assembled.Snapshot.MatchId));
        instance.DidNotReceive().Stop();
    }

    [Fact]
    public void AFailedAuthForALobbyWithNoMatchReportsThatNothingWasTornDown()
    {
        var context = new MatchStoreTestContext();

        Assert.False(context.Store.TryTearDownForFailedAuth(
            MatchStoreTestContext.FirstLobby, MatchStoreTestContext.Player("alice")));
    }

    [Fact]
    public async Task JoiningASecondLobbyDiscardsThePendingMatchInTheFirstAndRejectsTheJoiner()
    {
        var context = new MatchStoreTestContext(TwoConcurrentMatches);
        var firstLobbyRoster = MatchStoreTestContext.Roster("alice", "bob");
        var alice = context.Join(MatchStoreTestContext.FirstLobby, "alice", firstLobbyRoster, SampleNextSceneName);
        var bob = context.Join(MatchStoreTestContext.FirstLobby, "bob", firstLobbyRoster, SampleNextSceneName);

        var secondLobbyAttempt = context.Join(
            MatchStoreTestContext.SecondLobby, "alice", MatchStoreTestContext.Roster("alice", "carol"),
            SampleNextSceneName);

        var rejected = Assert.IsType<Rejected>(secondLobbyAttempt);
        Assert.Equal(JoinFailureReason.PlayerInMultipleLobbies, rejected.Reason);
        Assert.Equal(JoinFailureReason.PeerLeft, (await MatchStoreTestContext.FailureOf(bob)).Reason);
        Assert.Equal(JoinFailureReason.PeerLeft, (await MatchStoreTestContext.FailureOf(alice)).Reason);
        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.SecondLobby));
    }

    [Fact]
    public void JoiningASecondLobbyLeavesARunningGameAliveAndRemovesOnlyTheMembership()
    {
        var context = new MatchStoreTestContext(TwoConcurrentMatches);
        var instance = Substitute.For<IGameInstance>();
        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", "alice", "bob");
        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);

        var secondLobbyAttempt = context.Join(
            MatchStoreTestContext.SecondLobby, "alice", MatchStoreTestContext.Roster("alice", "carol"),
            SampleNextSceneName);

        Assert.Equal(
            JoinFailureReason.PlayerInMultipleLobbies, Assert.IsType<Rejected>(secondLobbyAttempt).Reason);
        var match = context.Store.FindMatch(assembled.Snapshot.MatchId);
        Assert.NotNull(match);
        Assert.Equal(1, match.JoinedCount);
        Assert.DoesNotContain(MatchStoreTestContext.Player("alice"), match.Members.Keys);
        instance.DidNotReceive().Stop();
    }

    [Fact]
    public void ARepeatJoinInTheSameLobbyIsNeverTreatedAsAMultiLobbyEviction()
    {
        var context = new MatchStoreTestContext(TwoConcurrentMatches);
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName);

        Assert.IsType<MemberAdded>(retry);
        Assert.NotNull(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }
}