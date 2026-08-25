using NSubstitute;
using ResonanceServerOrchestrator.Services.Edgegap;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class EdgegapGameServerLauncherTests : IDisposable
{
    private readonly IEdgegapClient _edgegapClient = Substitute.For<IEdgegapClient>();
    private EdgegapGameServerLauncher? _launcher;

    private void SetupEdgegapLauncherWithMockEdgegapClient()
    {
        _launcher = new EdgegapGameServerLauncher(_edgegapClient);
    }

    public void Dispose()
    {
        // called after each test executes
        _launcher = null;
    }

    [Fact]
    public void Launch_CallsEdgegapPostDeploymentWithIntendedServerVersion()
    {
        SetupEdgegapLauncherWithMockEdgegapClient();
        var response = new EdgegapDeploymentResponse(
            RequestId: Guid.NewGuid().ToString(),
            Message: "Hello world"
        );

        _edgegapClient.DeployAsync(Arg.Any<EdgegapDeploymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);
    }

    [Fact]
    public void Launch_PollsEdgegapGetEndpointUntilSuccessfulDeploy()
    {
    }

    [Fact]
    public void Launch_GivesUpPollingEdgegapGetEndpointAfterConfiguredRetries()
    {
    }

    [Fact]
    public void Launch_PassesEnvironmentVariablesInPostRequest()
    {
    }

    [Fact]
    public void Launch_ThrowsIfUnableToConnectToEdgegap()
    {
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)] // not in Edgegap docs, but just in case
    [InlineData(422)]
    [InlineData(500)]
    public void Launch_ThrowsOnStatusCodesAndIncludesCodeInMessage(int statusCode)
    {
    }

    [Fact]
    public void Stop_CallsEdgegapStopEndpoint()
    {
    }

    [Fact]
    public void Stop_PollsEdgegapGetEndpointUntilProcessExited()
    {
    }

    [Fact]
    public void Stop_GivesUpPollingEdgegapGetEndpointAfterConfiguredRetries()
    {
    }

    [Fact]
    public void Stop_ThrowsIfUnableToConnect()
    {
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public void Stop_ThrowsOnNon2xxStatusCodesExcept410(int statusCode)
    {
    }

    public void Stop_DoesNotThrowOn410StatusCode()
    {
    }
}

internal class StubEdgegapClient : IEdgegapClient
{
    public Task<EdgegapDeploymentResponse> DeployAsync(EdgegapDeploymentRequest request, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<EdgegapStopResponse> StopAsync(EdgegapStopRequest request, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}