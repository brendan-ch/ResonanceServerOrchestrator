namespace ResonanceServerOrchestrator.Services;

public sealed class EdgegapGameServerLauncher : IGameServerLauncher
{
    public bool ReportsReadiness => true;
    public IGameInstance Launch(GameServerLaunchSpec spec)
    {
        throw new NotImplementedException();
    }
}