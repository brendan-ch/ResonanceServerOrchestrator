using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

public static class LobbyEndpoints
{
    public static IEndpointRouteBuilder MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/lobbies/{lobbyCode}", HandlePostLobby);
        app.MapGet("/lobbies/{lobbyCode}", HandleGetLobby);
        return app;
    }

    private static async Task<IResult> HandlePostLobby(
        string lobbyCode,
        HttpRequest request,
        ILobbyStore store,
        IProcessLauncher launcher,
        IOptions<OrchestratorOptions> options)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
            return Results.BadRequest("lobbyCode must not be empty.");

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();

        if (!store.TrySet(lobbyCode, body))
            return Results.StatusCode(403);

        var opts = options.Value;
        var args = $"{opts.UnityServerBaseArgs} -lobbyCode {lobbyCode} -orchestratorUrl {opts.OrchestratorUrl}";
        launcher.Launch(opts.UnityServerPath, args.Trim());

        return Results.Accepted();
    }

    private static IResult HandleGetLobby(string lobbyCode, ILobbyStore store)
    {
        var body = store.Get(lobbyCode);
        return body is not null
            ? Results.Content(body, "application/json")
            : Results.NotFound();
    }
}
