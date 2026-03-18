using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryLobbyStoreTests
{
    private readonly InMemoryLobbyStore _store = new();

    [Fact]
    public void Set_ThenGet_ReturnsSameString()
    {
        const string body = """{"name":"Test","players":4}""";
        _store.Set("TEST01", body);

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
    public void Set_OverwritesExistingEntry()
    {
        _store.Set("TEST01", """{"name":"Original"}""");
        _store.Set("TEST01", """{"name":"Updated"}""");

        var result = _store.Get("TEST01");

        Assert.Equal("""{"name":"Updated"}""", result);
    }
}
