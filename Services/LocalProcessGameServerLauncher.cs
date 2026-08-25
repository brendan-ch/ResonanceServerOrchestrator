using System.ComponentModel;
using System.Diagnostics;

namespace ResonanceServerOrchestrator.Services;

public sealed class LocalProcessGameServerLauncher : IGameServerLauncher
{
    public bool ReportsReadiness => true;

    public IGameInstance Launch(GameServerLaunchSpec genericSpec)
    {
        if (genericSpec is not LocalGameServerLaunchSpec spec)
        {
            throw new GameServerLaunchException("Invalid launch spec");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.ExecutablePath,
            Arguments = spec.Arguments,
            UseShellExecute = false,
        };

        foreach (var (name, value) in spec.Environment)
            startInfo.Environment[name] = value;

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new GameServerLaunchException(
                    $"Failed to start game server process at path: {spec.ExecutablePath}");

            return new ProcessGameInstance(process);
        }
        catch (Win32Exception exception)
        {
            throw new GameServerLaunchException(
                $"Failed to start game server process at path: {spec.ExecutablePath}", exception);
        }
    }
}
