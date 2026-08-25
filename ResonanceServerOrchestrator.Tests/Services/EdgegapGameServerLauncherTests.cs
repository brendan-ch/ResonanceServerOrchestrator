using ResonanceServerOrchestrator.Services;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class EdgegapGameServerLauncherTests
{
    private readonly EdgegapGameServerLauncher _launcher = new();

    [Fact]
    public void Launch_CallsEdgegapPostDeploymentWithIntendedServerVersion()
    {

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
    [InlineData(403)]  // not in Edgegap docs, but just in case
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
