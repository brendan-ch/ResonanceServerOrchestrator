namespace ResonanceServerOrchestrator.Services;

public sealed class NullGameInstance : IGameInstance
{
    public bool HasExited => false;

    public Task Stop()
    {
        return Task.CompletedTask;
    }

#pragma warning disable CS0067
    public event EventHandler? Exited;
#pragma warning restore CS0067
}
