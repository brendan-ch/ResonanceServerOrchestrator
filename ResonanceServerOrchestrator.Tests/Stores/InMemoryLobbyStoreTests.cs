using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryLobbyStoreTests
{
    private readonly InMemoryLobbyStore _store = new(TimeProvider.System);

    [Fact]
    public void TrySet_ThenGet_ReturnsSameString()
    {
        const string body = """{"name":"Test","players":4}""";
        _store.TrySet("TEST01", body);

        var result = _store.Get("TEST01");

        Assert.Equal(body, result);
    }

    [Fact]
    public void Get_UnknownCode_ReturnsNull()
    {
        var result = _store.Get("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public void TrySet_NewCode_ReturnsTrue()
    {
        var result = _store.TrySet("NEW01", """{"name":"New"}""");

        Assert.True(result);
    }

    [Fact]
    public void TrySet_ExistingCode_ReturnsFalse()
    {
        _store.TrySet("DUP01", """{"name":"Original"}""");

        var result = _store.TrySet("DUP01", """{"name":"Duplicate"}""");

        Assert.False(result);
    }

    [Fact]
    public void TrySet_ExistingCode_DoesNotOverwrite()
    {
        _store.TrySet("DUP02", """{"name":"Original"}""");
        _store.TrySet("DUP02", """{"name":"Updated"}""");

        var result = _store.Get("DUP02");

        Assert.Equal("""{"name":"Original"}""", result);
    }

    [Fact]
    public void TrySet_IncrementsActiveCount()
    {
        Assert.Equal(0, _store.ActiveCount);

        _store.TrySet("A1", "{}");
        Assert.Equal(1, _store.ActiveCount);

        _store.TrySet("A2", "{}");
        Assert.Equal(2, _store.ActiveCount);
    }

    [Fact]
    public void SelfExit_RemovesLobbyFromStore()
    {
        _store.TrySet("EXIT01", "{}");
        var instance = Substitute.For<IGameInstance>();
        _store.SetInstance("EXIT01", instance);

        Assert.Equal(1, _store.ActiveCount);

        instance.Exited += Raise.Event();

        Assert.Equal(0, _store.ActiveCount);
    }

    [Fact]
    public void RemoveExpired_ZeroTimeout_ReturnsAllInstances()
    {
        var timeProvider = new FakeTimeProvider();
        var store = new InMemoryLobbyStore(timeProvider);
        var instance = Substitute.For<IGameInstance>();

        store.TrySet("EXP01", "{}");
        store.SetInstance("EXP01", instance);

        var expired = store.RemoveExpired(TimeSpan.Zero);

        Assert.Single(expired);
        Assert.Equal(0, store.ActiveCount);
    }

    [Fact]
    public void RemoveExpired_TimeoutNotYetElapsed_ReturnsEmpty()
    {
        var timeProvider = new FakeTimeProvider();
        var store = new InMemoryLobbyStore(timeProvider);
        var instance = Substitute.For<IGameInstance>();

        store.TrySet("NOTYET", "{}");
        store.SetInstance("NOTYET", instance);

        // Advance only 15 minutes — timeout is 30
        timeProvider.Advance(TimeSpan.FromMinutes(15));

        var expired = store.RemoveExpired(TimeSpan.FromMinutes(30));

        Assert.Empty(expired);
        Assert.Equal(1, store.ActiveCount);
    }

    [Fact]
    public void RemoveExpired_AfterTimeoutElapses_ReturnsInstance()
    {
        var timeProvider = new FakeTimeProvider();
        var store = new InMemoryLobbyStore(timeProvider);
        var instance = Substitute.For<IGameInstance>();

        store.TrySet("ELAPSED", "{}");
        store.SetInstance("ELAPSED", instance);

        timeProvider.Advance(TimeSpan.FromMinutes(31));

        var expired = store.RemoveExpired(TimeSpan.FromMinutes(30));

        Assert.Single(expired);
        Assert.Equal(0, store.ActiveCount);
    }
}
