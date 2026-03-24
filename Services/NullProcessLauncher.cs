namespace ResonanceServerOrchestrator.Services;

public sealed class NullProcessLauncher : IProcessLauncher
{
    public void Launch(string path, string arguments) { }
}
