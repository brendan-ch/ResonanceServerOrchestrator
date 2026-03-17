namespace ResonanceServerOrchestrator.Services;

public interface IProcessLauncher
{
    void Launch(string path, string arguments);
}
