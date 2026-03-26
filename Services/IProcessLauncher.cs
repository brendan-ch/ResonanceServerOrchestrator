namespace ResonanceServerOrchestrator.Services;

public interface IProcessLauncher
{
    IGameInstance Launch(string path, string arguments);
}
