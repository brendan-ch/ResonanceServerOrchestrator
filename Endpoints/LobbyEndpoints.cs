using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Models;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

public static class LobbyEndpoints
{
    public static IEndpointRouteBuilder MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/lobbies", HandlePostLobby);
        app.MapGet("/lobbies/{lobbyCode}", HandleGetLobby);
        return app;
    }

    private static IResult HandlePostLobby(
        [FromBody] Lobby? lobby,
        ILobbyStore store,
        IProcessLauncher launcher,
        IOptions<OrchestratorOptions> options)
    {
        if (lobby is null)
            return Results.BadRequest("Lobby payload is required.");

        if (string.IsNullOrWhiteSpace(lobby.LobbyCode))
            return Results.BadRequest("LobbyCode must not be empty.");

        store.Set(lobby.LobbyCode, lobby);

        var opts = options.Value;
        var args = $"{opts.UnityServerBaseArgs} --lobbyCode {lobby.LobbyCode} --orchestratorUrl {opts.OrchestratorUrl}";
        launcher.Launch(opts.UnityServerPath, args.Trim());

        return Results.Accepted();
    }

    private static IResult HandleGetLobby(string lobbyCode, ILobbyStore store)
    {
        var lobby = store.Get(lobbyCode);
        return lobby is not null ? Results.Ok(lobby) : Results.NotFound();
    }
}
