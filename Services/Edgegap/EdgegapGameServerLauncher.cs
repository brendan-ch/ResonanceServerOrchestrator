namespace ResonanceServerOrchestrator.Services.Edgegap;

public sealed class EdgegapGameServerLauncher(IEdgegapClient client) : IGameServerLauncher
{
    private IEdgegapClient _client = client;

    public bool ReportsReadiness => true;
    public IGameInstance Launch(GameServerLaunchSpec spec)
    {
        throw new NotImplementedException();
    }
}