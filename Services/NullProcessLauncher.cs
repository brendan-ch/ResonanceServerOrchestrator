namespace ResonanceServerOrchestrator.Services;

public sealed class NullProcessLauncher : IProcessLauncher
{
    public IGameInstance Launch(string path, string arguments) => NullGameInstance.Instance;
}
