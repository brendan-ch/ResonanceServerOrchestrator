using System.ComponentModel;
using System.Diagnostics;

namespace ResonanceServerOrchestrator.Services;

public sealed class UnityProcessLauncher : IProcessLauncher
{
    public void Launch(string path, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = false,
        };

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"Failed to start Unity server process at path: {path}");

            _ = process;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start Unity server process at path: {path}", ex);
        }
    }
}
