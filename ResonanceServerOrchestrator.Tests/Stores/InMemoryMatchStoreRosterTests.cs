using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreRosterTests
{
    [Fact]
    public void RosterComparisonIgnoresTheOrderOfTheExpectedPlayers()
    {
        var context = new MatchStoreTestContext();

        context.Join(MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"));
        var bob = context.Join(
            MatchStoreTestContext.FirstLobby, "bob", MatchStoreTestContext.Roster("bob", "alice"));

        Assert.IsType<RosterComplete>(bob);
    }

    [Fact]
    public async Task ARosterMismatchDiscardsTheMatchAndReleasesEveryWaiterWithRosterMismatch()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"));

        var mallory = context.Join(
            MatchStoreTestContext.FirstLobby, "mallory", MatchStoreTestContext.Roster("mallory", "alice", "bob"));

        var rejected = Assert.IsType<Rejected>(mallory);
        Assert.Equal(JoinFailureReason.RosterMismatch, rejected.Reason);
        Assert.Equal(1, rejected.JoinedCount);
        Assert.Equal(2, rejected.ExpectedCount);

        var aliceFailure = await MatchStoreTestContext.FailureOf(alice);
        Assert.Equal(JoinFailureReason.RosterMismatch, aliceFailure.Reason);
        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public void TheSameLobbyCanFormAFreshMatchImmediatelyAfterARosterMismatch()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);
        context.Join(
            MatchStoreTestContext.FirstLobby, "mallory", MatchStoreTestContext.Roster("mallory", "alice", "bob"));

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);

        Assert.IsType<MemberAdded>(retry);
        Assert.NotNull(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public async Task ARepeatJoinInAPendingMatchReplacesTheMemberAndSupersedesTheOrphanedWaiter()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol");
        var firstAttempt = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);

        var orphaned = await MatchStoreTestContext.FailureOf(firstAttempt);
        Assert.Equal(JoinFailureReason.SupersededByReconnect, orphaned.Reason);

        var replacement = Assert.IsType<MemberAdded>(retry);
        Assert.False(replacement.Completion.IsCompleted);
        Assert.Equal(
            MatchStoreTestContext.MemberGenerationOf(firstAttempt) + 1, replacement.MemberGeneration);

        var match = context.Store.FindMatch(replacement.MatchId)!;
        Assert.Equal(1, match.JoinedCount);
        Assert.Equal(MatchStatus.Pending, match.Status);
    }

    [Fact]
    public void ARepeatJoinInAPendingMatchIssuesAFreshServerAuthToken()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        var firstAttempt = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);
        var originalToken = context.Store
            .FindMatch(MatchStoreTestContext.MatchIdOf(firstAttempt))!
            .Members[MatchStoreTestContext.Player("alice")].ServerAuthToken;

        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);

        var replacedToken = context.Store
            .FindMatch(MatchStoreTestContext.MatchIdOf(firstAttempt))!
            .Members[MatchStoreTestContext.Player("alice")].ServerAuthToken;
        Assert.NotEqual(originalToken, replacedToken);
    }

    [Fact]
    public void ARepeatJoinInAStartedMatchIsRejectedWithMatchAlreadyStarted()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        context.StartMatch(MatchStoreTestContext.FirstLobby, "alice", "bob");

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster);

        var rejected = Assert.IsType<Rejected>(retry);
        Assert.Equal(JoinFailureReason.MatchAlreadyStarted, rejected.Reason);
        Assert.Equal(2, rejected.JoinedCount);
        Assert.Equal(2, rejected.ExpectedCount);
        Assert.NotNull(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }
}
