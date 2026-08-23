using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreDeadlineTests
{
    private const string SampleNextSceneName = "TestScene";
    private const string SampleGameMode = "Arena";
    private const string SampleIntendedServerVersion = "test-server-version";

    private static readonly OrchestratorOptions ShortAssemblyLongReadyBudgets = new()
    {
        RosterAssemblyTimeoutSeconds = 45,
        ServerReadyTimeoutSeconds = 600
    };

    private static readonly OrchestratorOptions LongAssemblyShortReadyBudgets = new()
    {
        RosterAssemblyTimeoutSeconds = 600,
        ServerReadyTimeoutSeconds = 30
    };

    [Fact]
    public async Task TheRosterAssemblyDeadlineDestroysThePendingMatchAndReleasesItsWaiters()
    {
        var context = new MatchStoreTestContext(ShortAssemblyLongReadyBudgets);
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);

        context.Clock.Advance(TimeSpan.FromSeconds(46));

        var failure = await MatchStoreTestContext.FailureOf(alice);
        Assert.Equal(JoinFailureReason.RosterAssemblyTimedOut, failure.Reason);
        Assert.Equal(1, failure.JoinedCount);
        Assert.Equal(2, failure.ExpectedCount);
        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }


    [Fact]
    public void TheRosterAssemblyDeadlineIsSharedByTheMatchRatherThanRestartedPerJoin()
    {
        var context = new MatchStoreTestContext(ShortAssemblyLongReadyBudgets);
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol");
        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);

        context.Clock.Advance(TimeSpan.FromSeconds(30));
        context.Join(MatchStoreTestContext.FirstLobby, "bob", roster, SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);
        context.Clock.Advance(TimeSpan.FromSeconds(16));

        Assert.Null(context.Store.FindMatchInLobby(MatchStoreTestContext.FirstLobby));
    }

    [Fact]
    public void TheRosterAssemblyTimerDoesNotFireAfterTheMatchTransitionsToLaunching()
    {
        var context = new MatchStoreTestContext(ShortAssemblyLongReadyBudgets);

        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, SampleIntendedServerVersion, "alice", "bob");
        context.Clock.Advance(TimeSpan.FromSeconds(120));

        var match = context.Store.FindMatch(assembled.Snapshot.MatchId);
        Assert.NotNull(match);
        Assert.Equal(MatchStatus.Launching, match.Status);
        Assert.All(assembled.Outcomes, outcome =>
            Assert.False(MatchStoreTestContext.CompletionOf(outcome).IsCompleted));
    }

    [Fact]
    public async Task TheServerReadyDeadlineDestroysTheLaunchingMatchAndReleasesItsWaiters()
    {
        var context = new MatchStoreTestContext(LongAssemblyShortReadyBudgets);
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, SampleIntendedServerVersion, "alice", "bob");

        context.Clock.Advance(TimeSpan.FromSeconds(31));

        var failure = await MatchStoreTestContext.FailureOf(assembled.OutcomeAt(0));
        Assert.Equal(JoinFailureReason.ServerReadyTimedOut, failure.Reason);
        Assert.Null(context.Store.FindMatch(assembled.Snapshot.MatchId));
    }

    [Fact]
    public void TheServerReadyDeadlineIsMeasuredFromTheTransitionToLaunchingNotFromMatchCreation()
    {
        var context = new MatchStoreTestContext(LongAssemblyShortReadyBudgets);
        var roster = MatchStoreTestContext.Roster("alice", "bob");
        context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);

        context.Clock.Advance(TimeSpan.FromSeconds(40));
        var bob = context.Join(MatchStoreTestContext.FirstLobby, "bob", roster, SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion);
        context.Clock.Advance(TimeSpan.FromSeconds(29));

        Assert.NotNull(context.Store.FindMatch(MatchStoreTestContext.MatchIdOf(bob)));
    }

    [Fact]
    public void TheServerReadyTimerDoesNotFireAfterTheGameServerReportsReady()
    {
        var context = new MatchStoreTestContext(LongAssemblyShortReadyBudgets);

        var assembled = context.StartMatch(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, SampleIntendedServerVersion, "alice", "bob");
        context.Clock.Advance(TimeSpan.FromSeconds(120));

        var match = context.Store.FindMatch(assembled.Snapshot.MatchId);
        Assert.NotNull(match);
        Assert.Equal(MatchStatus.Started, match.Status);
    }

    [Fact]
    public void ADeadlineExpiryLeavesATombstoneSoALateReadyCallbackIsToldTheMatchWasDestroyed()
    {
        var context = new MatchStoreTestContext(LongAssemblyShortReadyBudgets);
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, SampleIntendedServerVersion, "alice", "bob");

        context.Clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(
            MarkReadyOutcome.MatchAlreadyDestroyed,
            context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));
    }
}