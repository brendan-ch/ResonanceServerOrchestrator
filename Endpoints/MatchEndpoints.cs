using ResonanceServerOrchestrator.Contracts;

namespace ResonanceServerOrchestrator.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var matches = app.MapGroup("/matches");
        matches.MapPost("/{lobbyCode}/join", HandleJoinMatch);
        matches.MapPost("/{lobbyCode}/leave", HandleLeaveMatch);
        return app;
    }

    private static IResult HandleJoinMatch(string lobbyCode, JoinMatchDto request)
    {
        throw new NotImplementedException();
    }

    private static IResult HandleLeaveMatch(string lobbyCode, LeaveMatchDto request)
    {
        throw new NotImplementedException();
    }
}
