namespace ResonanceServerOrchestrator.Services.Edgegap;

public interface IEdgegapClient
{
    public Task<EdgegapDeploymentResponse> DeployAsync(EdgegapDeploymentRequest request, CancellationToken token);
    public Task<EdgegapStopResponse> StopAsync(EdgegapStopRequest request, CancellationToken token);
    public Task<EdgegapGetResponse> GetAsync(EdgegapGetRequest request, CancellationToken token);
}