using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreRosterTests
{
    private const string SampleNextSceneName = "TestScene";
    private const string SampleGameMode = "Arena";
    private const string SampleIntendedServerVersion = "test-server-version";

    [Fact]
    public void RosterComparisonIgnoresTheOrderOfTheExpectedPlayers()
    {
        var context = new MatchStoreTestContext();

        context.Join(MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);
        var bob = context.Join(
            MatchStoreTestContext.FirstLobby, "bob", MatchStoreTestContext.Roster("bob", "alice"), SampleNextSceneName,
            SampleGameMode, SampleIntendedServerVersion);

        Assert.IsType<RosterComplete>(bob);
    }


    [Fact]
    public async Task ARosterMismatchDiscardsTheMatchAndReleasesEveryWaiterWithRosterMismatch()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);

        var mallory = context.Join(
            MatchStoreTestContext.FirstLobby, "mallory", MatchStoreTestContext.Roster("mallory", "alice", "bob"),
            SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);

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
        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode,
            SampleIntendedServerVersion);
        context.Join(
            MatchStoreTestContext.FirstLobby, "mallory", MatchStoreTestContext.Roster("mallory", "alice", "bob"),
            SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode,
            SampleIntendedServerVersion);

        Assert.IsType<MemberAdded>(retry);
        Assert.NotNull(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public async Task ARepeatJoinInAPendingMatchReplacesTheMemberAndSupersedesTheOrphanedWaiter()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol");
        var firstAttempt = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName,
            SampleGameMode, SampleIntendedServerVersion);

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode,
            SampleIntendedServerVersion);

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
        var firstAttempt = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName,
            SampleGameMode, SampleIntendedServerVersion);
        var originalToken = context.Store
            .FindMatch(MatchStoreTestContext.MatchIdOf(firstAttempt))!
            .Members[MatchStoreTestContext.Player("alice")].ServerAuthToken;

        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode,
            SampleIntendedServerVersion);

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
        context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, SampleIntendedServerVersion,
            "alice", "bob");

        var retry = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode,
            SampleIntendedServerVersion);

        var rejected = Assert.IsType<Rejected>(retry);
        Assert.Equal(JoinFailureReason.MatchAlreadyStarted, rejected.Reason);
        Assert.Equal(2, rejected.JoinedCount);
        Assert.Equal(2, rejected.ExpectedCount);
        Assert.NotNull(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }
}