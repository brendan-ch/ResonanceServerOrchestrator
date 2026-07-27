using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Services;

internal sealed class MatchCleanupService(
    IMatchStore store,
    IOptions<OrchestratorOptions> options,
    TimeProvider timeProvider) : IHostedService, IDisposable
{
    private ITimer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.CleanupIntervalSeconds);
        _timer = timeProvider.CreateTimer(_ => store.ReapExpired(), null, interval, interval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
