namespace ResonanceServerOrchestrator.Services;

public sealed class NullGameServerLauncher : IGameServerLauncher
{
    public bool ReportsReadiness => false;

    public IGameInstance Launch(GameServerLaunchSpec spec) => NullGameInstance.Instance;
}
