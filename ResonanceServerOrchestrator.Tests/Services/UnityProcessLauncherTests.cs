using ResonanceServerOrchestrator.Services;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class UnityProcessLauncherTests
{
    private readonly UnityProcessLauncher _launcher = new();

    [Fact]
    public void Launch_NonExistentPath_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _launcher.Launch("/nonexistent/path/to/server", "-batchmode --lobbyCode TEST"));
    }
}
