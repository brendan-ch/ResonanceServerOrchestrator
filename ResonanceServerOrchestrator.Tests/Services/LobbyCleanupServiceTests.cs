using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class LobbyCleanupServiceTests
{
    private static LobbyCleanupService CreateService(
        ILobbyStore store,
        double lobbyTimeoutMinutes = 30)
    {
        var options = Options.Create(new OrchestratorOptions
        {
            LobbyTimeoutMinutes = lobbyTimeoutMinutes
        });
        return new LobbyCleanupService(store, options, NullLogger<LobbyCleanupService>.Instance);
    }

    [Fact]
    public void Cleanup_CallsRemoveExpiredWithConfiguredTimeout()
    {
        var store = Substitute.For<ILobbyStore>();
        store.RemoveExpired(Arg.Any<TimeSpan>()).Returns([]);
        var service = CreateService(store, lobbyTimeoutMinutes: 45);

        service.Cleanup();

        store.Received(1).RemoveExpired(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public void Cleanup_StopsEachExpiredInstance()
    {
        var instance1 = Substitute.For<IGameInstance>();
        var instance2 = Substitute.For<IGameInstance>();
        var store = Substitute.For<ILobbyStore>();
        store.RemoveExpired(Arg.Any<TimeSpan>()).Returns([instance1, instance2]);
        var service = CreateService(store);

        service.Cleanup();

        instance1.Received(1).Stop();
        instance2.Received(1).Stop();
    }

    [Fact]
    public void Cleanup_NoExpiredLobbies_DoesNotCallStop()
    {
        var store = Substitute.For<ILobbyStore>();
        store.RemoveExpired(Arg.Any<TimeSpan>()).Returns([]);
        var service = CreateService(store);

        service.Cleanup();

        // No exceptions, no Stop() calls needed — just verify no crash
    }
}
