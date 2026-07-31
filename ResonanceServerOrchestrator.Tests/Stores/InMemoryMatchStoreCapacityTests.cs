using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreCapacityTests
{
    private const string SampleNextSceneName = "TestScene";

    private static readonly OrchestratorOptions SingleMatchCapacity = new()
    {
        MaxMatches = 1,
        RosterAssemblyTimeoutSeconds = 3600,
        ServerReadyTimeoutSeconds = 3600
    };

    [Fact]
    public void EveryJoinerOfTheSameLobbyIsAcceptedEvenWhenOnlyOneMatchFitsInTheStore()
    {
        var context = new MatchStoreTestContext(SingleMatchCapacity);
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol", "dave", "erin");

        var outcomes = new[] { "alice", "bob", "carol", "dave", "erin" }
            .Select(platformUserId =>
                context.Join(MatchStoreTestContext.FirstLobby, platformUserId, roster, SampleNextSceneName))
            .ToArray();

        Assert.DoesNotContain(outcomes, outcome => outcome is Rejected);
        Assert.Equal(4, outcomes.OfType<MemberAdded>().Count());
        Assert.Single(outcomes.OfType<RosterComplete>());
    }


    [Fact]
    public void ASecondLobbyIsRejectedWithCapacityReachedOnceTheStoreIsFull()
    {
        var context = new MatchStoreTestContext(SingleMatchCapacity);
        context.Join(MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName);

        var secondLobbyAttempt = context.Join(
            MatchStoreTestContext.SecondLobby, "carol", MatchStoreTestContext.Roster("carol", "dave"),
            SampleNextSceneName);

        var rejected = Assert.IsType<Rejected>(secondLobbyAttempt);
        Assert.Equal(JoinFailureReason.CapacityReached, rejected.Reason);
        Assert.Equal(0, rejected.JoinedCount);
        Assert.Equal(0, rejected.ExpectedCount);
    }

    [Fact]
    public void CapacityFreedByADestroyedMatchIsImmediatelyReusable()
    {
        var context = new MatchStoreTestContext(SingleMatchCapacity);
        context.Join(MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName);
        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));

        var secondLobbyAttempt = context.Join(
            MatchStoreTestContext.SecondLobby, "carol", MatchStoreTestContext.Roster("carol", "dave"),
            SampleNextSceneName);

        Assert.IsType<MemberAdded>(secondLobbyAttempt);
    }

    [Fact]
    public void ConcurrentJoinsForTwoLobbiesNeverBothCreateAMatch()
    {
        var context = new MatchStoreTestContext(SingleMatchCapacity);
        var lobbies = new[] { MatchStoreTestContext.FirstLobby, MatchStoreTestContext.SecondLobby };
        var platformUserIds = new[] { "alice", "carol" };
        var outcomes = new JoinOutcome[2];

        RunSimultaneously(2, index => outcomes[index] = context.Join(
            lobbies[index], platformUserIds[index],
            MatchStoreTestContext.Roster(platformUserIds[index], "peer"), SampleNextSceneName));

        Assert.Equal(1, context.Store.LiveMatchCount);
        var rejected = Assert.Single(outcomes.OfType<Rejected>());
        Assert.Equal(JoinFailureReason.CapacityReached, rejected.Reason);
    }

    [Fact]
    public void OneHundredConcurrentJoinsForOneLobbyYieldExactlyOneMatchAndOneRosterComplete()
    {
        var context = new MatchStoreTestContext(SingleMatchCapacity);
        var platformUserIds = Enumerable.Range(0, 100).Select(index => $"player-{index:D3}").ToArray();
        var roster = MatchStoreTestContext.Roster(platformUserIds);
        var outcomes = new JoinOutcome[platformUserIds.Length];

        RunSimultaneously(platformUserIds.Length, index => outcomes[index] = context.Join(
            MatchStoreTestContext.FirstLobby, platformUserIds[index], roster, SampleNextSceneName));

        Assert.DoesNotContain(outcomes, outcome => outcome is Rejected);
        Assert.Single(outcomes.OfType<RosterComplete>());
        Assert.Single(outcomes.Select(MatchStoreTestContext.MatchIdOf).Distinct());
        Assert.Equal(1, context.Store.LiveMatchCount);
    }

    private static void RunSimultaneously(int workerCount, Action<int> work)
    {
        using var startLine = new Barrier(workerCount);
        var workers = Enumerable.Range(0, workerCount)
            .Select(index => new Thread(() =>
            {
                startLine.SignalAndWait();
                work(index);
            }))
            .ToArray();

        foreach (var worker in workers)
            worker.Start();

        foreach (var worker in workers)
            Assert.True(worker.Join(TimeSpan.FromSeconds(30)));
    }
}