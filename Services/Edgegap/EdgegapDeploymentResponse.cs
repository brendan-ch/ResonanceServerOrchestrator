namespace ResonanceServerOrchestrator.Services.Edgegap;

// Response to POST https://api.edgegap.com/v2/deployments
// 202: deployment request accepted; 400/401/422/500 carry Message (and 422 also RequestId).
public sealed record EdgegapDeploymentResponse(
    string? RequestId = null,
    string? Message = null,
    IReadOnlyDictionary<string, object>? Details = null);
