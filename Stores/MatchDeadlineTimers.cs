namespace ResonanceServerOrchestrator.Stores;

internal sealed class MatchDeadlineTimers
{
    private ITimer? _rosterAssemblyTimer;
    private ITimer? _serverReadyTimer;

    public void ArmRosterAssemblyTimerOnce(TimeProvider timeProvider, TimeSpan budget, Action onExpiry) =>
        _rosterAssemblyTimer ??= CreateOneShotTimer(timeProvider, budget, onExpiry);

    public void ArmServerReadyTimer(TimeProvider timeProvider, TimeSpan budget, Action onExpiry) =>
        _serverReadyTimer = CreateOneShotTimer(timeProvider, budget, onExpiry);

    public void DisposeRosterAssemblyTimer()
    {
        _rosterAssemblyTimer?.Dispose();
        _rosterAssemblyTimer = null;
    }

    public void DisposeServerReadyTimer()
    {
        _serverReadyTimer?.Dispose();
        _serverReadyTimer = null;
    }

    public void DisposeAll()
    {
        DisposeRosterAssemblyTimer();
        DisposeServerReadyTimer();
    }

    private static ITimer CreateOneShotTimer(TimeProvider timeProvider, TimeSpan budget, Action onExpiry) =>
        timeProvider.CreateTimer(_ => onExpiry(), null, budget, Timeout.InfiniteTimeSpan);
}
