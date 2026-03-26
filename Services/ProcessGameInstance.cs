using System.Diagnostics;

namespace ResonanceServerOrchestrator.Services;

public sealed class ProcessGameInstance : IGameInstance
{
    private readonly Process _process;

    public event EventHandler? Exited;

    public ProcessGameInstance(Process process)
    {
        _process = process;
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;
    }

    public void Stop()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have already exited — safe to ignore.
        }
    }

    private void OnProcessExited(object? sender, EventArgs e) => Exited?.Invoke(this, e);
}
