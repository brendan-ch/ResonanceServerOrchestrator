namespace ResonanceServerOrchestrator.Services;

public interface IGameServerLauncher
{
    bool ReportsReadiness { get; }
    IGameInstance Launch(GameServerLaunchSpec spec);
}
