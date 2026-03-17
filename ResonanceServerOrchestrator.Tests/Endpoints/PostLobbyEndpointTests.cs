using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ResonanceServerOrchestrator.Models;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Endpoints;

public sealed class PostLobbyEndpointTests : IDisposable
{
    private readonly OrchestratorWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public PostLobbyEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static Lobby CreateLobby(string code = "ABC123") =>
        new("Test Lobby", true, "lobby-id-1", code, 4, true,
            new List<LobbyUser> { new("user-1", "Player1", true) },
            new Dictionary<string, string> { ["gameMode"] = "Deathmatch" });

    [Fact]
    public async Task Post_ValidLobby_Returns202Accepted()
    {
        var lobby = CreateLobby();

        var response = await _client.PostAsJsonAsync("/lobbies", lobby);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidLobby_StoresLobbyInStore()
    {
        var lobby = CreateLobby("STORE1");

        await _client.PostAsJsonAsync("/lobbies", lobby);

        var store = _factory.Services.GetRequiredService<ILobbyStore>();
        var stored = store.Get("STORE1");
        Assert.NotNull(stored);
        Assert.Equal(lobby.LobbyCode, stored.LobbyCode);
        Assert.Equal(lobby.Name, stored.Name);
        Assert.Equal(lobby.LobbyId, stored.LobbyId);
        Assert.Equal(lobby.MaxPlayers, stored.MaxPlayers);
        Assert.Equal(lobby.IsOwner, stored.IsOwner);
        Assert.Equal(lobby.Members.Count, stored.Members.Count);
    }

    [Fact]
    public async Task Post_ValidLobby_LaunchesProcessWithLobbyCode()
    {
        var lobby = CreateLobby("LAUNCH1");

        await _client.PostAsJsonAsync("/lobbies", lobby);

        _factory.LauncherSubstitute.Received(1).Launch(
            Arg.Any<string>(),
            Arg.Is<string>(args => args.Contains("--lobbyCode LAUNCH1")));
    }

    [Fact]
    public async Task Post_EmptyLobbyCode_Returns400BadRequest()
    {
        var lobby = CreateLobby(string.Empty);

        var response = await _client.PostAsJsonAsync("/lobbies", lobby);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_MalformedJson_Returns400BadRequest()
    {
        var content = new StringContent("{ not valid json }", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/lobbies", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
