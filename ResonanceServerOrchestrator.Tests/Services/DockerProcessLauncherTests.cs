using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class DockerProcessLauncherTests
{
    private static DockerProcessLauncher CreateLauncher(string dockerContextPath)
    {
        var options = Options.Create(new OrchestratorOptions
        {
            UnityServerDockerContextPath = dockerContextPath,
        });
        return new DockerProcessLauncher(options);
    }

    [Fact]
    public void Launch_NonExistentContextPath_ThrowsInvalidOperationException()
    {
        var launcher = CreateLauncher("/nonexistent/context");

        Assert.Throws<InvalidOperationException>(() =>
            launcher.Launch(string.Empty, "-batchmode --lobbyCode TEST"));
    }
}
