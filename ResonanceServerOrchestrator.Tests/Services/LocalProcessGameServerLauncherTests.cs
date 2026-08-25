using ResonanceServerOrchestrator.Services;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class LocalProcessGameServerLauncherTests : IDisposable
{
    private readonly LocalProcessGameServerLauncher _launcher = new();
    private readonly List<string> _temporaryPaths = [];

    public void Dispose()
    {
        foreach (var path in _temporaryPaths)
            File.Delete(path);
    }

    private string CreateTemporaryPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"resonance-launcher-{Guid.NewGuid():N}");
        _temporaryPaths.Add(path);
        return path;
    }

    private static LocalGameServerLaunchSpec ShellSpec(
        string script, IReadOnlyDictionary<string, string> environment) =>
        new("/bin/sh", $"-c \"{script}\"", environment);

    [Fact]
    public void Launch_MakesEveryEnvironmentEntryVisibleToTheProcess()
    {
        var outputPath = CreateTemporaryPath();
        var environment = new Dictionary<string, string>
        {
            [LocalGameServerLaunchSpec.MatchIdVariable] = "11111111-2222-3333-4444-555555555555",
            [LocalGameServerLaunchSpec.MatchKeyVariable] = "a-match-key",
            [LocalGameServerLaunchSpec.OrchestratorUrlVariable] = "http://orchestrator:9000",
            [LocalGameServerLaunchSpec.GameServerPortVariable] = "7777",
            [LocalGameServerLaunchSpec.NextSceneNameVariable] = "TestScene"
        };

        var script =
            $"printf '%s\\n%s\\n%s\\n%s\\n%s' " +
            $"\\\"${LocalGameServerLaunchSpec.MatchIdVariable}\\\" " +
            $"\\\"${LocalGameServerLaunchSpec.MatchKeyVariable}\\\" " +
            $"\\\"${LocalGameServerLaunchSpec.OrchestratorUrlVariable}\\\" " +
            $"\\\"${LocalGameServerLaunchSpec.GameServerPortVariable}\\\" " +
            $"\\\"${LocalGameServerLaunchSpec.NextSceneNameVariable}\\\" > {outputPath}";

        var instance = _launcher.Launch(ShellSpec(script, environment));

        WaitForExit(instance);

        Assert.Equal(
            [
                "11111111-2222-3333-4444-555555555555",
                "a-match-key",
                "http://orchestrator:9000",
                "7777",
                "TestScene"
            ],
            File.ReadAllLines(outputPath));
    }

    [Fact]
    public void Launch_PassesArgumentsToTheProcess()
    {
        var outputPath = CreateTemporaryPath();

        var instance = _launcher.Launch(
            ShellSpec($"printf 'ran' > {outputPath}", new Dictionary<string, string>()));

        WaitForExit(instance);

        Assert.Equal("ran", File.ReadAllText(outputPath));
    }

    [Fact]
    public void Launch_MissingExecutable_ThrowsGameServerLaunchException()
    {
        var spec = new LocalGameServerLaunchSpec(
            "/nonexistent/resonance-server-binary", string.Empty, new Dictionary<string, string>());

        Assert.Throws<GameServerLaunchException>(() => _launcher.Launch(spec));
    }

    [Fact]
    public void ReportsReadiness_IsTrue()
    {
        Assert.True(_launcher.ReportsReadiness);
    }

    [Fact]
    public void Stop_TerminatesALongRunningProcess()
    {
        var instance = _launcher.Launch(
            ShellSpec("sleep 60", new Dictionary<string, string>()));

        Assert.False(instance.HasExited);

        instance.Stop();

        WaitForExit(instance);
        Assert.True(instance.HasExited);
    }

    [Fact]
    public void HasExited_BecomesTrueAfterTheProcessEnds()
    {
        var instance = _launcher.Launch(ShellSpec("exit 0", new Dictionary<string, string>()));

        WaitForExit(instance);

        Assert.True(instance.HasExited);
    }

    [Fact]
    public async Task Exited_IsRaisedWhenTheProcessEnds()
    {
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var instance = _launcher.Launch(ShellSpec("exit 0", new Dictionary<string, string>()));
        instance.Exited += (_, _) => exited.TrySetResult();

        await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static void WaitForExit(IGameInstance instance)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!instance.HasExited && DateTime.UtcNow < deadline)
            Thread.Sleep(20);

        Assert.True(instance.HasExited, "The launched process did not exit within 10 seconds.");
    }
}