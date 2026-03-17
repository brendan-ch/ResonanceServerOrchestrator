using ResonanceServerOrchestrator.Models;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

public sealed class InMemoryLobbyStoreTests
{
    private readonly InMemoryLobbyStore _store = new();

    private static Lobby CreateLobby(string code = "TEST01") =>
        new("Test Lobby", true, "id-1", code, 4, true,
            Array.Empty<LobbyUser>(),
            new Dictionary<string, string>());

    [Fact]
    public void Set_ThenGet_ReturnsSameLobby()
    {
        var lobby = CreateLobby();
        _store.Set(lobby.LobbyCode, lobby);

        var result = _store.Get(lobby.LobbyCode);

        Assert.Equal(lobby, result);
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
        var original = CreateLobby();
        var updated = original with { Name = "Updated Lobby" };

        _store.Set(original.LobbyCode, original);
        _store.Set(updated.LobbyCode, updated);

        var result = _store.Get(original.LobbyCode);
        Assert.Equal(updated, result);
    }
}
