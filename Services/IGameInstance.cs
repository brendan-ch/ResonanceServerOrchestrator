namespace ResonanceServerOrchestrator.Services;

public interface IGameInstance
{
    bool HasExited { get; }
    void Stop();
    event EventHandler? Exited;
}
