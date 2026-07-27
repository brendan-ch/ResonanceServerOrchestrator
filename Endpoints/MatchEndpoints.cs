using Asp.Versioning;
using Asp.Versioning.Builder;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(
        this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var matches = app.MapGroup("/v{version:apiVersion}/matches")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0));

        matches.MapPost("/join", HandleJoinMatch).DisableRequestTimeout();
        matches.MapPost("/leave", HandleLeaveMatch);

        return app;
    }

    private static async Task<IResult> HandleJoinMatch(
        JoinMatchDto request,
        IMatchStore store,
        PlayerTicketAuthenticator authenticator,
        MatchLaunchCoordinator launchCoordinator,
        CancellationToken cancellationToken)
    {
        var problem = JoinMatchRequestValidator.DescribeFirstProblem(request);
        if (problem is not null)
            return Results.BadRequest(problem);

        var user = request.PlatformUserInformation;
        var lobby = new LobbyKey(user.Platform, user.PlatformLobbyId);

        var authenticationFailure =
            await authenticator.DescribeAuthenticationFailureAsync(user, cancellationToken);

        if (authenticationFailure is not null)
        {
            store.TryTearDownForFailedAuth(lobby, user.Identity);
            return Results.Json(authenticationFailure,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var username = request.ExpectedLobbyPlayers
            .First(player => player.Identity == user.Identity).Username;

        var outcome = store.TryJoin(
            lobby,
            user.Identity,
            username,
            request.ExpectedLobbyPlayers.Select(player => player.Identity).ToList());

        return outcome switch
        {
            Rejected rejected => JoinMatchResponses.ForFailure(
                rejected.Reason, rejected.JoinedCount, rejected.ExpectedCount),

            RosterComplete rosterComplete => await LaunchThenAwaitCompletionAsync(
                rosterComplete, store, launchCoordinator, user.Identity, cancellationToken),

            MemberAdded memberAdded => await AwaitCompletionAsync(
                memberAdded.MatchId, memberAdded.MemberGeneration, memberAdded.Completion,
                store, user.Identity, cancellationToken),

            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> LaunchThenAwaitCompletionAsync(
        RosterComplete rosterComplete,
        IMatchStore store,
        MatchLaunchCoordinator launchCoordinator,
        PlayerIdentity identity,
        CancellationToken cancellationToken)
    {
        launchCoordinator.LaunchGameServerFor(rosterComplete.Snapshot);

        return await AwaitCompletionAsync(
            rosterComplete.MatchId, rosterComplete.MemberGeneration, rosterComplete.Completion,
            store, identity, cancellationToken);
    }

    private static async Task<IResult> AwaitCompletionAsync(
        Guid matchId,
        long memberGeneration,
        Task<JoinResult> completion,
        IMatchStore store,
        PlayerIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var abortRegistration = cancellationToken.Register(() =>
            store.DeregisterAbortedMember(matchId, identity, memberGeneration));

        return JoinMatchResponses.From(await completion);
    }

    private static async Task<IResult> HandleLeaveMatch(
        LeaveMatchDto request,
        IMatchStore store,
        PlayerTicketAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var user = request.PlatformUserInformation;

        if (user is null || string.IsNullOrWhiteSpace(user.PlatformUserId))
            return Results.BadRequest("platformUserInformation.platformUserId must not be empty.");

        var authenticationFailure =
            await authenticator.DescribeAuthenticationFailureAsync(user, cancellationToken);

        if (authenticationFailure is not null)
            return Results.Json(authenticationFailure,
                statusCode: StatusCodes.Status401Unauthorized);

        return store.TryLeave(user.Identity) ? Results.NoContent() : Results.NotFound();
    }
}
