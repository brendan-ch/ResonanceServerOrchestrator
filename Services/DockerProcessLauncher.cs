using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;

namespace ResonanceServerOrchestrator.Services;

public sealed class DockerProcessLauncher : IProcessLauncher
{
    private const string ImageName = "resonance-server-game";

    private readonly string _dockerContextPath;
    private readonly Lazy<bool> _imageBuilt;

    public DockerProcessLauncher(IOptions<OrchestratorOptions> options)
    {
        _dockerContextPath = options.Value.UnityServerDockerContextPath;
        _imageBuilt = new Lazy<bool>(BuildImage);
    }

    public void Launch(string path, string arguments)
    {
        _ = _imageBuilt.Value;

        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --rm {ImageName} {arguments}",
            UseShellExecute = false,
        };

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"Failed to start Docker container for image: {ImageName}");

            _ = process;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start Docker container for image: {ImageName}", ex);
        }
    }

    private bool BuildImage()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"build -t {ImageName} \"{_dockerContextPath}\"",
            UseShellExecute = false,
        };

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"Failed to start docker build process for context: {_dockerContextPath}");

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"docker build failed with exit code {process.ExitCode} for context: {_dockerContextPath}");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start docker build process for context: {_dockerContextPath}", ex);
        }

        return true;
    }
}
