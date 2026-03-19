using System.Net;
using Microsoft.Extensions.DependencyInjection;
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

    private void SeedStore(string lobbyCode, string body)
    {
        var store = _factory.Services.GetRequiredService<ILobbyStore>();
        store.TrySet(lobbyCode, body);
    }

    [Fact]
    public async Task Get_ExistingCode_Returns200()
    {
        SeedStore("GET200", """{"name":"Test"}""");

        var response = await _client.GetAsync("/lobbies/GET200");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/lobbies/NOTFOUND");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsBodyVerbatim()
    {
        const string body = """{"whatever":"you","want":true}""";
        SeedStore("VERBATIM", body);

        var response = await _client.GetAsync("/lobbies/VERBATIM");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(body, content);
    }
}
