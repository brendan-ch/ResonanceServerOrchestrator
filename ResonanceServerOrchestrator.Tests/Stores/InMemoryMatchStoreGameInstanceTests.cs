using NSubstitute;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryMatchStoreGameInstanceTests
{
    private const string SampleGameMode = "Arena";
    private const string NextSceneName = "TestScene";
    private const string SampleIntendedServerVersion = "test-server-version";

    [Fact]
    public void RegisteringAnInstanceOnAMatchAlreadyFlippedToStartedSucceedsAndDoesNotStopTheProcess()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        var assembled =
            context.StartMatch(MatchStoreTestContext.FirstLobby, NextSceneName, SampleGameMode, SampleIntendedServerVersion, "alice", "bob");

        Assert.True(context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance));

        instance.DidNotReceive().Stop();
        Assert.Same(instance, context.Store.FindMatch(assembled.Snapshot.MatchId)!.Instance);
    }


    [Fact]
    public void RegisteringAnInstanceOnADestroyedMatchFails()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, NextSceneName, SampleGameMode, SampleIntendedServerVersion, "alice",
            "bob");
        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));

        Assert.False(context.Store.TrySetInstance(
            assembled.Snapshot.MatchId, Substitute.For<IGameInstance>()));
    }

    [Fact]
    public async Task AnInstanceThatHasAlreadyExitedAtRegistrationIsTreatedAsAnExit()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        instance.HasExited.Returns(true);
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, NextSceneName, SampleGameMode, SampleIntendedServerVersion, "alice",
            "bob");

        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);

        Assert.Equal(
            JoinFailureReason.ServerLaunchFailed,
            (await MatchStoreTestContext.FailureOf(assembled.OutcomeAt(0))).Reason);
        Assert.Null(context.Store.FindMatch(assembled.Snapshot.MatchId));
    }

    [Fact]
    public async Task AnExitWhileLaunchingDestroysTheMatchAndReleasesWaitersWithServerLaunchFailed()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, NextSceneName, SampleGameMode, SampleIntendedServerVersion, "alice",
            "bob");
        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);

        instance.Exited += Raise.Event<EventHandler>(instance, EventArgs.Empty);

        Assert.Equal(
            JoinFailureReason.ServerLaunchFailed,
            (await MatchStoreTestContext.FailureOf(assembled.OutcomeAt(1))).Reason);
        Assert.Null(context.Store.FindMatch(assembled.Snapshot.MatchId));
    }

    [Fact]
    public async Task AnExitAfterTheMatchStartedDeletesTheMatchAndFreesTheCapacitySlot()
    {
        var context = new MatchStoreTestContext();
        var instance = Substitute.For<IGameInstance>();
        var assembled =
            context.StartMatch(MatchStoreTestContext.FirstLobby, NextSceneName, SampleGameMode, SampleIntendedServerVersion, "alice", "bob");
        context.Store.TrySetInstance(assembled.Snapshot.MatchId, instance);
        var alreadySucceeded = await MatchStoreTestContext.SuccessOf(assembled.OutcomeAt(0));

        instance.Exited += Raise.Event<EventHandler>(instance, EventArgs.Empty);

        Assert.NotNull(alreadySucceeded);
        Assert.Equal(0, context.Store.LiveMatchCount);
        Assert.IsType<MemberAdded>(context.Join(
            MatchStoreTestContext.SecondLobby, "carol", MatchStoreTestContext.Roster("carol", "dave"), NextSceneName,
            SampleGameMode, SampleIntendedServerVersion));
    }

    [Fact]
    public void AnExitForAMatchThatIsAlreadyGoneIsANoOp()
    {
        var context = new MatchStoreTestContext();
        var assembled = context.AssembleRoster(MatchStoreTestContext.FirstLobby, NextSceneName, SampleGameMode, SampleIntendedServerVersion, "alice",
            "bob");
        context.Store.TryLeave(MatchStoreTestContext.Player("alice"));

        context.Store.OnInstanceExited(assembled.Snapshot.MatchId);

        Assert.Equal(0, context.Store.LiveMatchCount);
    }
}