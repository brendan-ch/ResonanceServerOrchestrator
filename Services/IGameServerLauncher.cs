namespace ResonanceServerOrchestrator.Services;

public interface IGameServerLauncher
{
    bool ReportsReadiness { get; }
    Task<IGameInstance> Launch(GameServerLaunchSpec spec);
}
