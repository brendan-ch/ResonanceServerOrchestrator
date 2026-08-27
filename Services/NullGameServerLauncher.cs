namespace ResonanceServerOrchestrator.Services;

public sealed class NullGameServerLauncher : IGameServerLauncher
{
    public bool ReportsReadiness => false;

    public Task<IGameInstance> Launch(GameServerLaunchSpec spec, CancellationToken token = default)
    {
        Console.WriteLine($"Match ID: {spec.Environment[GameServerLaunchSpec.MatchIdVariable]}");
        Console.WriteLine($"Match key: {spec.Environment[GameServerLaunchSpec.MatchKeyVariable]}");

        return Task.FromResult<IGameInstance>(new NullGameInstance());
    }
}
