using System.Text.Json;

namespace ResonanceServerOrchestrator.Services.Edgegap;

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