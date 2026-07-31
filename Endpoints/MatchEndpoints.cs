using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

public static class MatchEndpoints
{
    public const string ClientRateLimiterPolicy = "game-client";

    public static IEndpointRouteBuilder MapMatchEndpoints(
        this IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var matches = app.MapGroup("/v{version:apiVersion}/matches")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0));

        matches.MapPost("/join", HandleJoinMatch).DisableRequestTimeout();
        matches.MapPost("/leave", HandleLeaveMatch);
        matches.RequireRateLimiting(ClientRateLimiterPolicy);

        return app;
    }

    private static async Task<IResult> HandleJoinMatch(
        JoinMatchDto? request,
        IMatchStore store,
        PlayerTicketAuthenticator authenticator,
        MatchLaunchCoordinator launchCoordinator,
        IOptions<OrchestratorOptions> options,
        CancellationToken cancellationToken)
    {
        if (!JoinMatchRequestValidator.TryValidate(
                request, options.Value, out var joiningPlayer, out var problem))
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status400BadRequest);

        var user = request.PlatformUserInformation;
        var lobby = new LobbyKey(user.Platform, user.PlatformLobbyId);

        var authenticationFailure =
            await authenticator.DescribeAuthenticationFailureAsync(user, cancellationToken);

        if (authenticationFailure is not null)
        {
            store.TryTearDownForFailedAuth(lobby, user.GetIdentity());
            return Results.Json(authenticationFailure,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var outcome = store.TryJoin(
            lobby,
            user.GetIdentity(),
            joiningPlayer.Username,
            request.ExpectedLobbyPlayers.Select(player => player.GetIdentity()).ToList(),
            request.NextSceneName
        );

        return outcome switch
        {
            Rejected rejected => JoinMatchResponses.ForFailure(
                rejected.Reason, rejected.JoinedCount, rejected.ExpectedCount),

            RosterComplete rosterComplete => await LaunchThenAwaitCompletionAsync(
                rosterComplete, store, launchCoordinator, user.GetIdentity(), cancellationToken),

            MemberAdded memberAdded => await AwaitCompletionAsync(
                memberAdded.MatchId, memberAdded.MemberGeneration, memberAdded.Completion,
                store, user.GetIdentity(), cancellationToken),

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
        LeaveMatchDto? request,
        IMatchStore store,
        PlayerTicketAuthenticator authenticator,
        IOptions<OrchestratorOptions> options,
        CancellationToken cancellationToken)
    {
        if (request?.PlatformUserInformation is not { } user)
            return Results.Problem(
                detail: "platformUserInformation is required.",
                statusCode: StatusCodes.Status400BadRequest);

        var problem = PlatformUserValidator.DescribeFirstProblem(user, options.Value);
        if (problem is not null)
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status400BadRequest);

        var authenticationFailure =
            await authenticator.DescribeAuthenticationFailureAsync(user, cancellationToken);

        if (authenticationFailure is not null)
            return Results.Json(authenticationFailure,
                statusCode: StatusCodes.Status401Unauthorized);

        return store.TryLeave(user.GetIdentity()) ? Results.NoContent() : Results.NotFound();
    }
}