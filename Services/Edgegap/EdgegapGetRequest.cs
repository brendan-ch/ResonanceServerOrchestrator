namespace ResonanceServerOrchestrator.Services.Edgegap;

// GET https://api.edgegap.com/v1/status/{deployment_id}
public sealed record EdgegapGetRequest(string DeploymentId);