using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ResonanceServerOrchestrator.Models;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class GetLobbyEndpointTests : IDisposable
{
    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public GetLobbyEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static Lobby CreateLobby(string code = "GET001") =>
        new("Get Test Lobby", true, "get-lobby-id", code, 6, false,
            new List<LobbyUser>
            {
                new("u1", "Alice", true),
                new("u2", "Bob", false),
            },
            new Dictionary<string, string>
            {
                ["gameMode"] = "Capture",
                ["sceneName"] = "Arena01",
            });

    private void SeedStore(Lobby lobby)
    {
        var store = _factory.Services.GetRequiredService<ILobbyStore>();
        store.Set(lobby.LobbyCode, lobby);
    }

    [Fact]
    public async Task Get_ExistingLobbyCode_Returns200WithLobbyJson()
    {
        var lobby = CreateLobby("GET200");
        SeedStore(lobby);

        var response = await _client.GetAsync($"/lobbies/{lobby.LobbyCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownLobbyCode_Returns404()
    {
        var response = await _client.GetAsync("/lobbies/NOTFOUND");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_LobbyJson_IncludesMembers()
    {
        var lobby = CreateLobby("GETMEM");
        SeedStore(lobby);

        var result = await _client.GetFromJsonAsync<Lobby>($"/lobbies/{lobby.LobbyCode}");

        Assert.NotNull(result);
        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.DisplayName == "Alice");
        Assert.Contains(result.Members, m => m.DisplayName == "Bob");
    }

    [Fact]
    public async Task Get_LobbyJson_IncludesUnderlyingProviderProperties()
    {
        var lobby = CreateLobby("GETPROP");
        SeedStore(lobby);

        var result = await _client.GetFromJsonAsync<Lobby>($"/lobbies/{lobby.LobbyCode}");

        Assert.NotNull(result);
        Assert.Equal("Capture", result.UnderlyingProviderProperties["gameMode"]);
        Assert.Equal("Arena01", result.UnderlyingProviderProperties["sceneName"]);
    }
}
