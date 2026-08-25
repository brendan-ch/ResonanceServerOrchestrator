namespace ResonanceServerOrchestrator.Services.Edgegap;

public interface IEdgegapClient
{
    public int PollingDelayMs { get; }

    /// <summary>
    /// The maximum number of attempts to get the deployment status from Edgegap.
    /// If set to zero, the client will poll indefinitely.
    /// </summary>
    public int MaxPollingAttempts { get; }
    // TODO: encode the rate limits here at some point

    public Task<EdgegapDeploymentResponse> DeployAsync(EdgegapDeploymentRequest request, CancellationToken token);
    public Task<EdgegapStopResponse> StopAsync(EdgegapStopRequest request, CancellationToken token);
    public Task<EdgegapGetResponse> GetAsync(EdgegapGetRequest request, CancellationToken token);
}