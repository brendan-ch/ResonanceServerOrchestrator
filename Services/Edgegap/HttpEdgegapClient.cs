using System.Text.Json;

namespace ResonanceServerOrchestrator.Services.Edgegap;

public sealed class HttpEdgegapClient(HttpClient httpClient, string token) : IEdgegapClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        // All Edgegap objects use this policy
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    private readonly HttpClient _httpClient = httpClient;
    private string _token = token;
    public Task<EdgegapDeploymentResponse> DeployAsync(EdgegapDeploymentRequest request, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<EdgegapStopResponse> StopAsync(EdgegapStopRequest request, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}