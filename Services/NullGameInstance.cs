namespace ResonanceServerOrchestrator.Services;

public sealed class NullGameInstance : IGameInstance
{
    public static readonly NullGameInstance Instance = new();

    private NullGameInstance() { }

    public void Stop() { }

#pragma warning disable CS0067 // Exited intentionally never raised on the null object
    public event EventHandler? Exited;
#pragma warning restore CS0067
}
