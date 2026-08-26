using System.Text.Json;

namespace ResonanceServerOrchestrator.Services.Edgegap;

/// <summary>
/// A thin client for the Edgegap API.
/// </summary>
/// <param name="httpClient"></param>
/// <param name="token"></param>
/// <param name="pollingDelay"></param>
/// <param name="pollingAttempts"></param>
public sealed class HttpEdgegapClient(HttpClient httpClient, string token, int pollingDelay, int pollingAttempts)
    : IEdgegapClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        // All Edgegap objects use this policy
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _httpClient = httpClient;
    private string _token = token;
    public int PollingDelayMs { get; } = pollingDelay;
    public int MaxPollingAttempts { get; } = pollingAttempts;

    public Task<EdgegapDeploymentResponse> DeployAsync(EdgegapDeploymentRequest request, CancellationToken token)
    {
        if (_httpClient.BaseAddress == null) throw new EdgegapClientException();

        throw new NotImplementedException();
    }

    public Task<EdgegapStopResponse> StopAsync(EdgegapStopRequest request, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<EdgegapGetResponse> GetAsync(EdgegapGetRequest request, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}

public class EdgegapClientException : Exception
{
}