using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Services.Edgegap;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class EdgegapGameServerLauncherTests : IDisposable
{
    private const string ServerVersion = "ServerVersion";
    private readonly IEdgegapClient _edgegapClient = Substitute.For<IEdgegapClient>();
    private EdgegapGameServerLauncher? _launcher;

    private void SetupEdgegapLauncherWithMockEdgegapClient(
        int pollingDelayMs = 0,
        int maxPollingAttempts = 5
    )
    {
        _launcher = new EdgegapGameServerLauncher(
            _edgegapClient,
            pollingDelayMs,
            maxPollingAttempts,
            new NullLogger<EdgegapGameServerLauncher>()
        );
    }

    public void Dispose()
    {
        // called after each test executes
        _launcher = null;
    }

    [Fact]
    public async Task Launch_CallsEdgegapPostDeploymentWithIntendedServerVersion()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();
        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
                Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );
        var readyResponse = notReadyYetResponse with
        {
            CurrentStatus = EdgegapGetResponse.StatusReady,
            LastStatus = EdgegapGetResponse.StatusSeeking
        };

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == edgegapDeploymentResponse.RequestId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, notReadyYetResponse, notReadyYetResponse, readyResponse);

        await _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        ));

        await _edgegapClient.Received(1).DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Launch_PollsEdgegapGetEndpointUntilSuccessfulDeploy()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
                Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var serverStatusRequest = new EdgegapGetRequest(
            DeploymentId: edgegapDeploymentResponse.RequestId!
        );
        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );
        var readyResponse = notReadyYetResponse with
        {
            CurrentStatus = EdgegapGetResponse.StatusReady,
            LastStatus = EdgegapGetResponse.StatusSeeking
        };

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == serverStatusRequest.DeploymentId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, notReadyYetResponse, notReadyYetResponse, readyResponse);

        await _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        ));

        await _edgegapClient.Received(4).GetAsync(
            Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == serverStatusRequest.DeploymentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Launch_GivesUpPollingEdgegapGetEndpointAfterConfiguredPollingAttempts()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
                Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var serverStatusRequest = new EdgegapGetRequest(
            DeploymentId: edgegapDeploymentResponse.RequestId!
        );
        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == serverStatusRequest.DeploymentId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, notReadyYetResponse, notReadyYetResponse, notReadyYetResponse,
                notReadyYetResponse, notReadyYetResponse);

        await Assert.ThrowsAsync<GameServerLaunchException>(async () =>
        {
            await _launcher!.Launch(new EdgegapGameServerLaunchSpec(
                ServerVersion,
                new Dictionary<string, string>(),
                new List<string>()
            ));
        });

        await _edgegapClient.Received(5).GetAsync(
            Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == serverStatusRequest.DeploymentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Launch_PassesEnvironmentVariablesInPostRequest()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
                Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );
        var readyResponse = notReadyYetResponse with
        {
            CurrentStatus = EdgegapGetResponse.StatusReady,
            LastStatus = EdgegapGetResponse.StatusSeeking
        };

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == edgegapDeploymentResponse.RequestId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, readyResponse);

        var environment = new Dictionary<string, string>
        {
            { "MatchId", Guid.NewGuid().ToString() },
            { "MatchKey", Guid.NewGuid().ToString() }
        };

        await _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            environment,
            new List<string>()
        ));

        await _edgegapClient.Received(1).DeployAsync(
            Arg.Is<EdgegapDeploymentRequest>(r =>
                r.EnvironmentVariables != null &&
                environment.All(kv => r.EnvironmentVariables.Any(ev => ev.Key == kv.Key && ev.Value == kv.Value))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Launch_ThrowsIfEdgegapClientThrowsOnDeploy()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        _edgegapClient.DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Unable to connect to Edgegap"));

        await Assert.ThrowsAsync<GameServerLaunchException>(() => _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        )));
    }

    [Fact]
    public async Task Launch_ThrowsIfEdgegapClientThrowsOnGet()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Is<EdgegapDeploymentRequest>(r => r.Version == ServerVersion),
                Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        _edgegapClient.GetAsync(Arg.Any<EdgegapGetRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Unable to connect to Edgegap"));

        await Assert.ThrowsAsync<GameServerLaunchException>(() => _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        )));
    }

    [Fact]
    public async Task Stop_CallsEdgegapStopEndpoint()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        var stopResponse = new EdgegapStopResponse(
            Message: "Requested",
            DeploymentSummary: null
        );
        _edgegapClient.StopAsync(Arg.Any<EdgegapStopRequest>(), Arg.Any<CancellationToken>())
            .Returns(stopResponse);

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Any<EdgegapDeploymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );
        var readyResponse = notReadyYetResponse with
        {
            CurrentStatus = EdgegapGetResponse.StatusReady,
            LastStatus = EdgegapGetResponse.StatusSeeking
        };

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == edgegapDeploymentResponse.RequestId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, readyResponse);

        // first run, then stop
        var instance = await _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        ));
        Assert.NotNull(instance);

        await instance.Stop();

        await _edgegapClient.Received(1)
            .StopAsync(Arg.Is<EdgegapStopRequest>(r => r.DeploymentId == edgegapDeploymentResponse.RequestId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_ImmediatelyMarksInstanceAsExited()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        var stopResponse = new EdgegapStopResponse(
            Message: "Requested",
            DeploymentSummary: null
        );
        _edgegapClient.StopAsync(Arg.Any<EdgegapStopRequest>(), Arg.Any<CancellationToken>())
            .Returns(stopResponse);

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Any<EdgegapDeploymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );
        var readyResponse = notReadyYetResponse with
        {
            CurrentStatus = EdgegapGetResponse.StatusReady,
            LastStatus = EdgegapGetResponse.StatusSeeking
        };

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == edgegapDeploymentResponse.RequestId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, readyResponse, readyResponse);

        // first run, then stop
        var instance = await _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        ));
        Assert.NotNull(instance);

        await instance.Stop();

        Assert.True(instance.HasExited);
        // does not poll the GET endpoint again after the instance has started
        await _edgegapClient.Received(2).GetAsync(Arg.Any<EdgegapGetRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_ThrowsIfEdgegapClientThrows()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();

        _edgegapClient.StopAsync(Arg.Any<EdgegapStopRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Unable to connect to Edgegap"));

        var edgegapDeploymentResponse = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );
        _edgegapClient.DeployAsync(Arg.Any<EdgegapDeploymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(edgegapDeploymentResponse);

        var notReadyYetResponse = new EdgegapGetResponse(
            RequestId: edgegapDeploymentResponse.RequestId!,
            Fqdn: "c0653765de3b.pr.edgegap.net",
            PublicIp: "192.53.120.48",
            AppName: "test",
            AppVersion: ServerVersion,
            CurrentStatus: EdgegapGetResponse.StatusSeeking,
            Running: true,
            StartTime: "2026-04-22 12:00:46.444265",
            ElapsedTime: 1,
            MaxDuration: 1440
        );
        var readyResponse = notReadyYetResponse with
        {
            CurrentStatus = EdgegapGetResponse.StatusReady,
            LastStatus = EdgegapGetResponse.StatusSeeking
        };

        _edgegapClient.GetAsync(Arg.Is<EdgegapGetRequest>(r => r.DeploymentId == edgegapDeploymentResponse.RequestId),
                Arg.Any<CancellationToken>())
            .Returns(notReadyYetResponse, readyResponse, readyResponse);

        await Assert.ThrowsAsync<GameServerLaunchException>(() => _launcher!.Launch(new EdgegapGameServerLaunchSpec(
            ServerVersion,
            new Dictionary<string, string>(),
            new List<string>()
        )));
    }
}