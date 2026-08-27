namespace ResonanceServerOrchestrator.Services.Edgegap;

// DELETE https://api.edgegap.com/v1/stop/{deployment_id}
public sealed record EdgegapStopRequest(string DeploymentId, string? ContainerLogStorage = null);
