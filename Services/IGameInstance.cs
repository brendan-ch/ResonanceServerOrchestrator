namespace ResonanceServerOrchestrator.Services;

public interface IGameInstance
{
    bool HasExited { get; }
    Task Stop();
    event EventHandler? Exited;
}
