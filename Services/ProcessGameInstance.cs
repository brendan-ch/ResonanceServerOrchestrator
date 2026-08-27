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

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    public Task Stop()
    {
        try
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e) => Exited?.Invoke(this, e);
}
