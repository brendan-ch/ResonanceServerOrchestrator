using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Services;

public sealed class LobbyCleanupService(
    ILobbyStore store,
    IOptions<OrchestratorOptions> options,
    ILogger<LobbyCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            Cleanup();
        }
    }

    internal void Cleanup()
    {
        var timeout = TimeSpan.FromMinutes(options.Value.LobbyTimeoutMinutes);
        var expired = store.RemoveExpired(timeout);
        foreach (var instance in expired)
        {
            instance.Stop();
            logger.LogInformation("Stopped expired lobby instance.");
        }
    }
}
