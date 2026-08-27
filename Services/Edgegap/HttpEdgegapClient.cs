using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResonanceServerOrchestrator.Services.Edgegap;

/// <summary>
/// A thin client for the Edgegap API.
/// </summary>
/// <param name="httpClient"></param>
/// <param name="token"></param>
public sealed class HttpEdgegapClient(HttpClient httpClient, string token)
    : IEdgegapClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<EdgegapDeploymentResponse> DeployAsync(EdgegapDeploymentRequest request, CancellationToken ct)
    {
        return httpClient.BaseAddress == null
            ? throw new EdgegapClientException("Base URL not configured")
            : SendAsync<EdgegapDeploymentResponse>(HttpMethod.Post, "v2/deployments", request, ct);
    }

    public Task<EdgegapStopResponse> StopAsync(EdgegapStopRequest request, CancellationToken ct)
    {
        return httpClient.BaseAddress == null
            ? throw new EdgegapClientException("Base URL not configured")
            : SendAsync<EdgegapStopResponse>(HttpMethod.Delete, $"v1/stop/{request.DeploymentId}", null, ct);
    }

    public Task<EdgegapGetResponse> GetAsync(EdgegapGetRequest request, CancellationToken ct)
    {
        return httpClient.BaseAddress == null
            ? throw new EdgegapClientException("Base URL not configured")
            : SendAsync<EdgegapGetResponse>(HttpMethod.Get, $"v1/status/{request.DeploymentId}", null, ct);
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, object? body,
        CancellationToken ct)
    {
        var requestMessage = new HttpRequestMessage(method, path)
        {
            Headers =
            {
                { "Authorization", $"token {token}" },
                { "Accept", "application/json" }
            }
        };

        if (body != null)
        {
            var requestBody = JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), JsonSerializerOptions);
            requestMessage.Content = new ByteArrayContent(requestBody);
            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        using var response = await httpClient.SendAsync(requestMessage, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new EdgegapClientException(
                $"HTTP {(int)response.StatusCode}: {errorBody}",
                (int)response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonSerializerOptions, ct);
        return result ?? throw new EdgegapClientException("Empty response body");
    }
}

public class EdgegapClientException : Exception
{
    public int StatusCode { get; }

    public EdgegapClientException(string message) : base(message)
    {
    }

    public EdgegapClientException(string message, int statusCode) : base(message) => StatusCode = statusCode;
}