using System.Buffers.Text;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreJoinTests
{
    private const string SampleNextSceneName = "TestScene";
    private const string SampleGameMode = "Arena";

    [Fact]
    public void TheFirstJoinerOfAMultiPlayerRosterIsParkedAsAMember()
    {
        var context = new MatchStoreTestContext();

        var outcome = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName, SampleGameMode);

        var added = Assert.IsType<MemberAdded>(outcome);
        Assert.False(added.Completion.IsCompleted);
        Assert.Equal(MatchStatus.Pending, context.Store.FindMatch(added.MatchId)!.Status);
    }


    [Fact]
    public void TheFinalJoinerReceivesRosterCompleteAndTheMatchTransitionsToLaunching()
    {
        var context = new MatchStoreTestContext();

        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, "alice",
            "bob");

        Assert.IsType<MemberAdded>(assembled.OutcomeAt(0));
        Assert.IsType<RosterComplete>(assembled.OutcomeAt(1));
        Assert.Equal(MatchStatus.Launching, context.Store.FindMatch(assembled.Snapshot.MatchId)!.Status);
    }

    [Fact]
    public void ASoloRosterCompletesOnTheJoinThatCreatesTheMatch()
    {
        var context = new MatchStoreTestContext();

        var outcome = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice"), SampleNextSceneName,
            SampleGameMode);

        var rosterComplete = Assert.IsType<RosterComplete>(outcome);
        Assert.Equal(MatchStatus.Launching, context.Store.FindMatch(rosterComplete.MatchId)!.Status);
    }

    [Fact]
    public void NoWaiterCompletesBeforeTheGameServerReportsReady()
    {
        var context = new MatchStoreTestContext();

        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, "alice",
            "bob");

        Assert.All(assembled.Outcomes, outcome =>
            Assert.False(MatchStoreTestContext.CompletionOf(outcome).IsCompleted));
    }

    [Fact]
    public async Task MarkingReadyReleasesEveryWaiterWithItsOwnServerAuthToken()
    {
        var context = new MatchStoreTestContext(
            new OrchestratorOptions { GameServerHost = "game.example", GameServerPort = 7801 });

        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, "alice",
            "bob");
        context.Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey);

        var alice = await MatchStoreTestContext.SuccessOf(assembled.OutcomeAt(0));
        var bob = await MatchStoreTestContext.SuccessOf(assembled.OutcomeAt(1));

        Assert.Equal(assembled.Snapshot.MatchId, alice.MatchId);
        Assert.Equal("game.example", alice.DedicatedServerHost);
        Assert.Equal(7801, alice.DedicatedServerPort);
        Assert.NotEqual(alice.ServerAuthToken, bob.ServerAuthToken);
    }

    [Fact]
    public void TheSnapshotCarriesTheMatchKeyThePortAndEveryMemberInCanonicalRosterOrder()
    {
        var context = new MatchStoreTestContext(
            new OrchestratorOptions { GameServerHost = "game.example", GameServerPort = 7801 });

        var snapshot = context
            .AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, "alice", "bob").Snapshot;

        Assert.Equal(7801, snapshot.GameServerPort);
        Assert.NotEmpty(snapshot.MatchKey);
        Assert.Equal(["alice", "bob"], snapshot.Members.Select(member => member.PlatformUserId));
        Assert.Equal(
            [MatchStoreTestContext.UsernameOf("alice"), MatchStoreTestContext.UsernameOf("bob")],
            snapshot.Members.Select(member => member.Username));
    }

    [Fact]
    public void TheMatchKeyAndEveryServerAuthTokenAreThirtyTwoRandomBase64UrlBytes()
    {
        var context = new MatchStoreTestContext();

        var first = context
            .AssembleRoster(MatchStoreTestContext.FirstLobby, "TestScene", SampleGameMode, "alice", "bob").Snapshot;
        context.Store.MarkReady(first.MatchId, first.MatchKey);
        context.Store.OnInstanceExited(first.MatchId);
        var second = context
            .AssembleRoster(MatchStoreTestContext.SecondLobby, "TestScene", SampleGameMode, "carol", "dave").Snapshot;

        var secrets = new[] { first.MatchKey, second.MatchKey }
            .Concat(first.Members.Select(member => member.ServerAuthToken))
            .Concat(second.Members.Select(member => member.ServerAuthToken))
            .ToArray();

        Assert.All(secrets, secret => Assert.Equal(32, Base64Url.DecodeFromChars(secret).Length));
        Assert.Equal(secrets.Length, secrets.Distinct().Count());
    }

    [Fact]
    public async Task TheMemberCountSignalCompletesWithTheMatchIdOnceEnoughPlayersHaveJoined()
    {
        var context = new MatchStoreTestContext();
        var roster = MatchStoreTestContext.Roster("alice", "bob", "carol");

        var signal = context.Store.WhenMemberCountReaches(MatchStoreTestContext.FirstLobby, 2);
        var alice = context.Join(MatchStoreTestContext.FirstLobby, "alice", roster, SampleNextSceneName,
            SampleGameMode);
        Assert.False(signal.IsCompleted);

        context.Join(MatchStoreTestContext.FirstLobby, "bob", roster, SampleNextSceneName, SampleGameMode);

        Assert.Equal(MatchStoreTestContext.MatchIdOf(alice), await signal.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task TheMemberCountSignalCompletesImmediatelyWhenTheCountIsAlreadyReached()
    {
        var context = new MatchStoreTestContext();
        var alice = context.Join(
            MatchStoreTestContext.FirstLobby, "alice", MatchStoreTestContext.Roster("alice", "bob"),
            SampleNextSceneName, SampleGameMode);

        var signal = context.Store.WhenMemberCountReaches(MatchStoreTestContext.FirstLobby, 1);

        Assert.True(signal.IsCompletedSuccessfully);
        Assert.Equal(MatchStoreTestContext.MatchIdOf(alice), await signal);
    }
}