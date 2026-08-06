namespace ResonanceServerOrchestrator.Services;

public sealed class NullGameServerLauncher : IGameServerLauncher
{
    public bool ReportsReadiness => false;

    public IGameInstance Launch(GameServerLaunchSpec spec)
    {
        Console.WriteLine($"Match ID: {spec.Environment[GameServerLaunchSpec.MatchIdVariable]}");
        Console.WriteLine($"Match key: {spec.Environment[GameServerLaunchSpec.MatchKeyVariable]}");

        return new NullGameInstance();
    }
}
