namespace ResonanceServerOrchestrator.Services;

public interface IGameInstance
{
    void Stop();
    event EventHandler? Exited;
}
