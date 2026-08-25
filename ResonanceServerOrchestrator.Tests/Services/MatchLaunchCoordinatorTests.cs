using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class MatchLaunchCoordinatorTests
{
    private const string MatchKey = "the-match-key";

    private readonly IMatchStore _store = Substitute.For<IMatchStore>();
    private readonly IGameServerLauncher _launcher = Substitute.For<IGameServerLauncher>();

    private static readonly MatchSnapshot Snapshot = new(
        Guid.NewGuid(),
        MatchKey,
        7777,
        [new MatchMemberDto(Platform.Steam, "76561198000000001", "alice", "token")],
        "TestScene",
        "Arena",
        "IntendedServerVersion");

    private MatchLaunchCoordinator CreateCoordinator() =>
        new(_store, _launcher,
            Options.Create(new OrchestratorOptions
            {
                UnityServerPath = "/opt/game/server",
                UnityServerBaseArgs = "-batchmode",
                OrchestratorUrl = "http://orchestrator:9000",
            }),
            NullLogger<MatchLaunchCoordinator>.Instance);

    [Fact]
    public void LaunchGameServerFor_InjectsTheMatchEnvironment()
    {
        _launcher.ReportsReadiness.Returns(true);
        _launcher.Launch(Arg.Any<LocalGameServerLaunchSpec>()).Returns(new NullGameInstance());
        _store.TrySetInstance(Snapshot.MatchId, Arg.Any<IGameInstance>()).Returns(true);

        CreateCoordinator().LaunchGameServerFor(Snapshot);

        var spec = (LocalGameServerLaunchSpec)_launcher.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IGameServerLauncher.Launch))
            .GetArguments()[0]!;

        Assert.Equal("/opt/game/server", spec.ExecutablePath);
        Assert.Equal("-batchmode", spec.Arguments);
        Assert.Equal(
            Snapshot.MatchId.ToString("D"),
            spec.Environment[LocalGameServerLaunchSpec.MatchIdVariable]);
        Assert.Equal(MatchKey, spec.Environment[LocalGameServerLaunchSpec.MatchKeyVariable]);
        Assert.Equal("7777", spec.Environment[LocalGameServerLaunchSpec.GameServerPortVariable]);
        Assert.Equal(
            "http://orchestrator:9000",
            spec.Environment[LocalGameServerLaunchSpec.OrchestratorUrlVariable]);
    }

    [Fact]
    public void LaunchGameServerFor_WhenTheLauncherNeverReportsReadiness_MarksTheMatchReadyItself()
    {
        _launcher.ReportsReadiness.Returns(false);
        _launcher.Launch(Arg.Any<LocalGameServerLaunchSpec>()).Returns(new NullGameInstance());
        _store.TrySetInstance(Snapshot.MatchId, Arg.Any<IGameInstance>()).Returns(true);

        CreateCoordinator().LaunchGameServerFor(Snapshot);

        _store.Received(1).MarkReady(Snapshot.MatchId, MatchKey);
    }

    [Fact]
    public void LaunchGameServerFor_WhenTheLauncherReportsReadiness_WaitsForTheCallback()
    {
        _launcher.ReportsReadiness.Returns(true);
        _launcher.Launch(Arg.Any<LocalGameServerLaunchSpec>()).Returns(new NullGameInstance());
        _store.TrySetInstance(Snapshot.MatchId, Arg.Any<IGameInstance>()).Returns(true);

        CreateCoordinator().LaunchGameServerFor(Snapshot);

        _store.DidNotReceive().MarkReady(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public void LaunchGameServerFor_WhenLaunchThrows_FailsTheMatchInsteadOfLeavingItToTimeOut()
    {
        _launcher.ReportsReadiness.Returns(true);
        _launcher.Launch(Arg.Any<LocalGameServerLaunchSpec>())
            .Throws(new GameServerLaunchException("the binary is missing"));

        CreateCoordinator().LaunchGameServerFor(Snapshot);

        _store.Received(1).OnInstanceExited(Snapshot.MatchId);
        _store.DidNotReceive().TrySetInstance(Arg.Any<Guid>(), Arg.Any<IGameInstance>());
    }

    [Fact]
    public void LaunchGameServerFor_WhenTheMatchIsAlreadyGone_StopsTheOrphanedProcess()
    {
        var instance = Substitute.For<IGameInstance>();
        _launcher.ReportsReadiness.Returns(true);
        _launcher.Launch(Arg.Any<LocalGameServerLaunchSpec>()).Returns(instance);
        _store.TrySetInstance(Snapshot.MatchId, instance).Returns(false);

        CreateCoordinator().LaunchGameServerFor(Snapshot);

        instance.Received(1).Stop();
        _store.DidNotReceive().MarkReady(Arg.Any<Guid>(), Arg.Any<string>());
    }
}