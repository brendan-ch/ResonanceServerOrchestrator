using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services;

public sealed class SteamWebApiTicketValidatorTests : IDisposable
{
    private const uint ConfiguredAppId = 3167030;
    private const string ConfiguredPublisherKey = "publisher-web-api-key";
    private const string TicketHex = "14000000AABBCCDD";
    private const string SteamId = "76561197960287930";

    private readonly List<HttpClient> _httpClients = [];

    public void Dispose()
    {
        foreach (var httpClient in _httpClients)
            httpClient.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_AuthenticatedTicket_ReturnsTheSteamReportedIdentity()
    {
        var validator = CreateValidator(RespondWith(AuthenticatedPayload()));

        var result = await validator.ValidateAsync(TicketHex, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(SteamId, result.SteamId);
        Assert.False(result.IsBanned);
        Assert.Null(result.FailureDetail);
    }

    [Fact]
    public async Task ValidateAsync_SendsThePublisherKeyAppIdAndTicket()
    {
        var handler = RespondWith(AuthenticatedPayload());
        var validator = CreateValidator(handler);

        await validator.ValidateAsync(TicketHex, CancellationToken.None);

        var requestUri = Assert.IsType<Uri>(handler.LastRequestUri);
        Assert.Contains("ISteamUserAuth/AuthenticateUserTicket", requestUri.AbsolutePath);
        Assert.Contains($"key={ConfiguredPublisherKey}", requestUri.Query);
        Assert.Contains($"appid={ConfiguredAppId}", requestUri.Query);
        Assert.Contains($"ticket={TicketHex}", requestUri.Query);
    }

    [Fact]
    public async Task ValidateAsync_ResponseAppIdDiffersFromTheConfiguredAppId_IsRejected()
    {
        var validator = CreateValidator(RespondWith(AuthenticatedPayload(appId: ConfiguredAppId + 1)));

        var result = await validator.ValidateAsync(TicketHex, CancellationToken.None);

        AssertRejected(result);
    }

    [Fact]
    public async Task ValidateAsync_ResponseAppIdMatchesTheConfiguredAppId_IsAccepted()
    {
        var validator = CreateValidator(RespondWith(AuthenticatedPayload(appId: ConfiguredAppId)));

        var result = await validator.ValidateAsync(TicketHex, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_VacBannedAccount_IsRejectedAsBanned()
    {
        var validator = CreateValidator(RespondWith(AuthenticatedPayload(vacBanned: true)));

        var result = await validator.ValidateAsync(TicketHex, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.True(result.IsBanned);
        Assert.Equal(SteamId, result.SteamId);
        Assert.NotNull(result.FailureDetail);
    }

    [Fact]
    public async Task ValidateAsync_PublisherBannedAccount_IsRejectedAsBanned()
    {
        var validator = CreateValidator(RespondWith(AuthenticatedPayload(publisherBanned: true)));

        var result = await validator.ValidateAsync(TicketHex, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.True(result.IsBanned);
        Assert.Equal(SteamId, result.SteamId);
    }

    [Fact]
    public async Task ValidateAsync_SteamReturnsAnErrorEnvelope_IsRejected()
    {
        const string payload = """
            {"response":{"error":{"errorcode":101,"errordesc":"Invalid ticket"}}}
            """;

        var result = await CreateValidator(RespondWith(payload))
            .ValidateAsync(TicketHex, CancellationToken.None);

        AssertRejected(result);
        Assert.Contains("Invalid ticket", result.FailureDetail);
    }

    [Theory]
    [InlineData("""{"response":{"params":{"result":"Failure","steamid":"76561197960287930"}}}""")]
    [InlineData("""{"response":{"params":{"result":"OK"}}}""")]
    [InlineData("""{"response":{"params":{"result":"OK","steamid":""}}}""")]
    [InlineData("""{"response":{"params":{"steamid":"76561197960287930"}}}""")]
    [InlineData("""{"response":{}}""")]
    [InlineData("""{"response":[]}""")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task ValidateAsync_UnusableResponseBody_FailsClosed(string payload)
    {
        var result = await CreateValidator(RespondWith(payload))
            .ValidateAsync(TicketHex, CancellationToken.None);

        AssertRejected(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ValidateAsync_SteamReturnsANonSuccessStatus_FailsClosed(HttpStatusCode statusCode)
    {
        var result = await CreateValidator(RespondWith(AuthenticatedPayload(), statusCode))
            .ValidateAsync(TicketHex, CancellationToken.None);

        AssertRejected(result);
    }

    [Fact]
    public async Task ValidateAsync_SteamIsUnreachable_FailsClosedWithoutThrowing()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("No such host is known."));

        var result = await CreateValidator(handler).ValidateAsync(TicketHex, CancellationToken.None);

        AssertRejected(result);
    }

    [Fact]
    public async Task ValidateAsync_SteamDoesNotAnswerWithinTheTimeout_FailsClosedWithoutThrowing()
    {
        var timeProvider = new FakeTimeProvider();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            requestStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var validation = CreateValidator(handler, timeProvider)
            .ValidateAsync(TicketHex, CancellationToken.None);

        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        AssertRejected(await validation.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_BlankTicket_IsRejectedWithoutCallingSteam(string ticketHex)
    {
        var handler = RespondWith(AuthenticatedPayload());

        var result = await CreateValidator(handler).ValidateAsync(ticketHex, CancellationToken.None);

        AssertRejected(result);
        Assert.Null(handler.LastRequestUri);
    }

    private static void AssertRejected(SteamTicketValidationResult result)
    {
        Assert.False(result.IsValid);
        Assert.NotNull(result.FailureDetail);
    }

    private static string AuthenticatedPayload(
        uint? appId = null, bool vacBanned = false, bool publisherBanned = false)
    {
        var appIdEntry = appId is null ? string.Empty : $"\"appid\":{appId},";

        return $$"""
            {
              "response": {
                "params": {
                  "result": "OK",
                  "steamid": "{{SteamId}}",
                  "ownersteamid": "{{SteamId}}",
                  {{appIdEntry}}
                  "vacbanned": {{(vacBanned ? "true" : "false")}},
                  "publisherbanned": {{(publisherBanned ? "true" : "false")}}
                }
              }
            }
            """;
    }

    private static StubHttpMessageHandler RespondWith(
        string payload, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload),
        }));

    private SteamWebApiTicketValidator CreateValidator(
        HttpMessageHandler handler, TimeProvider? timeProvider = null)
    {
        var httpClient = new HttpClient(handler);
        _httpClients.Add(httpClient);

        return new SteamWebApiTicketValidator(
            httpClient,
            Options.Create(new OrchestratorOptions
            {
                SteamAppId = ConfiguredAppId,
                SteamPublisherWebApiKey = ConfiguredPublisherKey,
            }),
            timeProvider ?? TimeProvider.System,
            NullLogger<SteamWebApiTicketValidator>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return respond(request, cancellationToken);
        }
    }
}
