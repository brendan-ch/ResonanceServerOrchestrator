using System.Net;
using System.Text;
using ResonanceServerOrchestrator.Services.Edgegap;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Services.Edgegap;

public sealed class HttpEdgegapClientTests
{
    private const string Token = "test-token";
    private const string BaseUrl = "https://api.edgegap.com/";

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastRequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return Response;
        }
    }

    private static (HttpEdgegapClient client, StubHttpMessageHandler handler) CreateClient()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        var client = new HttpEdgegapClient(httpClient, Token);
        return (client, handler);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task DeployAsync_SendsPostWithAuthHeaderAndJsonBody()
    {
        var (client, handler) = CreateClient();
        handler.Response = JsonResponse(HttpStatusCode.Accepted,
            """{"request_id":"req-1","message":"accepted"}""");

        var request = new EdgegapDeploymentRequest(
            "Resonance",
            "1.0.0",
            new List<EdgegapUser>());

        var response = await client.DeployAsync(request, CancellationToken.None);

        Assert.Equal("req-1", response.RequestId);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v2/deployments", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal($"token {Token}", handler.LastRequest.Headers.Authorization?.ToString());
        Assert.Contains("\"version\":\"1.0.0\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task StopAsync_SendsDeleteToDeploymentIdPathWithNoBody()
    {
        var (client, handler) = CreateClient();
        handler.Response = JsonResponse(HttpStatusCode.OK, """{"message":"stopped"}""");

        var request = new EdgegapStopRequest("deploy-123");

        var response = await client.StopAsync(request, CancellationToken.None);

        Assert.Equal("stopped", response.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("/v1/stop/deploy-123", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Null(handler.LastRequest.Content);
    }

    [Fact]
    public async Task GetAsync_SendsGetToDeploymentIdPathWithNoBody()
    {
        var (client, handler) = CreateClient();
        handler.Response = JsonResponse(HttpStatusCode.OK, """
                                                           {
                                                               "request_id": "req-1",
                                                               "fqdn": "example.edgegap.net",
                                                               "public_ip": "1.2.3.4",
                                                               "app_name": "Resonance",
                                                               "app_version": "1.0.0",
                                                               "current_status": "Status.READY",
                                                               "running": true,
                                                               "start_time": "2026-01-01 00:00:00",
                                                               "elapsed_time": 1,
                                                               "max_duration": 1440
                                                           }
                                                           """);

        var response = await client.GetAsync(new EdgegapGetRequest("deploy-123"), CancellationToken.None);

        Assert.Equal(EdgegapGetResponse.StatusReady, response.CurrentStatus);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/v1/status/deploy-123", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Null(handler.LastRequest.Content);
    }

    [Fact]
    public async Task SendAsync_ThrowsEdgegapClientExceptionWithStatusCodeOnFailure()
    {
        var (client, handler) = CreateClient();
        handler.Response = JsonResponse(HttpStatusCode.Unauthorized, """{"message":"bad token"}""");

        var ex = await Assert.ThrowsAsync<EdgegapClientException>(() =>
            client.GetAsync(new EdgegapGetRequest("deploy-123"), CancellationToken.None));

        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("bad token", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ThrowsEdgegapClientExceptionOnEmptyResponseBody()
    {
        var (client, handler) = CreateClient();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<EdgegapClientException>(() =>
            client.GetAsync(new EdgegapGetRequest("deploy-123"), CancellationToken.None));
    }

    [Fact]
    public async Task DeployAsync_ThrowsWhenBaseAddressNotConfigured()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new HttpEdgegapClient(httpClient, Token);

        await Assert.ThrowsAsync<EdgegapClientException>(() => client.DeployAsync(
            new EdgegapDeploymentRequest("Resonance", "1.0.0", new List<EdgegapUser>()),
            CancellationToken.None));
    }
}