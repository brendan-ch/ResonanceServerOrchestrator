using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Post_Returns202Accepted()
    {
        var response = await _client.PostAsync("/lobbies/ABC123", JsonContent("""{"whatever":"you","want":true}"""));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_StoresBodyInStore()
    {
        const string body = """{"mode":"Deathmatch","players":4}""";

        await _client.PostAsync("/lobbies/STORE1", JsonContent(body));

        var store = _factory.Services.GetRequiredService<ILobbyStore>();
        var stored = store.Get("STORE1");
        Assert.Equal(body, stored);
    }

    [Fact]
    public async Task Post_LaunchesProcessWithLobbyCode()
    {
        await _client.PostAsync("/lobbies/LAUNCH1", JsonContent("{}"));

        _factory.LauncherSubstitute.Received(1).Launch(
            Arg.Any<string>(),
            Arg.Is<string>(args => args.Contains("-lobbyCode LAUNCH1")));
    }

    [Fact]
    public async Task Post_EmptyBody_Returns202Accepted()
    {
        var response = await _client.PostAsync("/lobbies/XYZ", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns403_WhenMaxLobbiesReached()
    {
        using var factory = new OrchestratorWebApplicationFactory(maxLobbies: 1);
        using var client = factory.CreateClient();

        var first = await client.PostAsync("/lobbies/ROOM1", JsonContent("{}"));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await client.PostAsync("/lobbies/ROOM2", JsonContent("{}"));
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }
}
