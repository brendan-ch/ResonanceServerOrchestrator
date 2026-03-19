using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryLobbyStoreTests
{
    private readonly InMemoryLobbyStore _store = new();

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
}
