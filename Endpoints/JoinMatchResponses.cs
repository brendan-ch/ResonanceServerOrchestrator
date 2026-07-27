using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Endpoints;

internal static class JoinMatchResponses
{
    private const int RetryAfterSecondsWhenAtCapacity = 5;

    public static IResult From(JoinResult result) => result switch
    {
        JoinSucceeded success => Results.Ok(new JoinMatchResultDto(
            success.MatchId,
            success.DedicatedServerHost,
            success.DedicatedServerPort,
            success.ServerAuthToken)),

        JoinFailed failure => ForFailure(
            failure.Reason, failure.JoinedCount, failure.ExpectedCount),

        _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
    };

    public static IResult ForFailure(JoinFailureReason reason, int joinedCount, int expectedCount)
    {
        var body = new JoinFailureDto(reason, joinedCount, expectedCount);

        return reason == JoinFailureReason.CapacityReached
            ? new AtCapacityResult(body)
            : Results.Json(body, statusCode: StatusCodes.Status409Conflict);
    }

    private sealed class AtCapacityResult(JoinFailureDto body) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter =
                RetryAfterSecondsWhenAtCapacity.ToString();

            return Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable)
                .ExecuteAsync(httpContext);
        }
    }
}
