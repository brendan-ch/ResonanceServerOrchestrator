using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class MatchCleanupServiceTests
{
    private const double CleanupIntervalSeconds = 60;

    private readonly IMatchStore _store = Substitute.For<IMatchStore>();
    private readonly FakeTimeProvider _clock = new();

    private MatchCleanupService CreateService() =>
        new(_store,
            Options.Create(new OrchestratorOptions
            {
                CleanupIntervalSeconds = CleanupIntervalSeconds,
            }),
            _clock);

    [Fact]
    public async Task ReapsOnceEveryConfiguredInterval()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(CleanupIntervalSeconds));
        _store.Received(1).ReapExpired();

        _clock.Advance(TimeSpan.FromSeconds(CleanupIntervalSeconds));
        _store.Received(2).ReapExpired();

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DoesNotReapBeforeTheIntervalElapses()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(CleanupIntervalSeconds - 1));

        _store.DidNotReceive().ReapExpired();

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopsReapingOnceStopped()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(CleanupIntervalSeconds * 3));

        _store.DidNotReceive().ReapExpired();
    }
}
