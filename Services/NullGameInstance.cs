namespace ResonanceServerOrchestrator.Services;

public sealed class NullGameInstance : IGameInstance
{
    public static readonly NullGameInstance Instance = new();

    private NullGameInstance() { }

    public bool HasExited => false;

    public void Stop() { }

#pragma warning disable CS0067
    public event EventHandler? Exited;
#pragma warning restore CS0067
}
