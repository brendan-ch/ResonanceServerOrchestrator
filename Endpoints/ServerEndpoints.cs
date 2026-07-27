using Asp.Versioning;
using Asp.Versioning.Builder;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

public static class ServerEndpoints
{
    public const string MatchKeyHeader = "X-Match-Key";
    public const string RateLimiterPolicy = "game-server";

    public static IEndpointRouteBuilder MapServerEndpoints(
        this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var server = app.MapGroup("/v{version:apiVersion}/server/matches")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .RequireRateLimiting(RateLimiterPolicy);

        server.MapPost("/{matchId:guid}/ready", HandleReady);
        server.MapGet("/{matchId:guid}/members", HandleMembers);

        return app;
    }

    private static IResult HandleReady(Guid matchId, HttpRequest request, IMatchStore store)
    {
        var presentedMatchKey = ReadMatchKey(request);
        if (presentedMatchKey is null)
            return Results.Unauthorized();

        return store.MarkReady(matchId, presentedMatchKey) switch
        {
            MarkReadyOutcome.MatchStarted or MarkReadyOutcome.MatchWasAlreadyStarted =>
                Results.NoContent(),
            MarkReadyOutcome.RosterNotYetComplete => Results.Conflict(),
            MarkReadyOutcome.MatchAlreadyDestroyed => Results.StatusCode(StatusCodes.Status410Gone),
            MarkReadyOutcome.MatchNotFound => Results.NotFound(),
            _ => Results.Unauthorized(),
        };
    }

    private static IResult HandleMembers(Guid matchId, HttpRequest request, IMatchStore store)
    {
        var presentedMatchKey = ReadMatchKey(request);
        if (presentedMatchKey is null)
            return Results.Unauthorized();

        var lookup = store.LookUpSnapshotForGameServer(matchId, presentedMatchKey);

        return lookup.Outcome switch
        {
            MatchSnapshotLookupOutcome.Granted => Results.Ok(lookup.Snapshot!.Members),
            MatchSnapshotLookupOutcome.MatchAlreadyDestroyed =>
                Results.StatusCode(StatusCodes.Status410Gone),
            MatchSnapshotLookupOutcome.MatchNotFound => Results.NotFound(),
            _ => Results.Unauthorized(),
        };
    }

    private static string? ReadMatchKey(HttpRequest request) =>
        request.Headers.TryGetValue(MatchKeyHeader, out var values) &&
        !string.IsNullOrWhiteSpace(values.ToString())
            ? values.ToString()
            : null;
}
