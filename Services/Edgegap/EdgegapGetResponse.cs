namespace ResonanceServerOrchestrator.Services.Edgegap;

// Response to GET https://api.edgegap.com/v1/status/{deployment_id}
// 200: deployment status; 400/401/404/500 carry Message only.
public sealed record EdgegapGetResponse(
    string RequestId,
    string Fqdn,
    string PublicIp,
    string AppName,
    string AppVersion,
    string CurrentStatus,
    bool Running,
    string? StartTime = null,
    int? ElapsedTime = null,
    int? MaxDuration = null,
    string? RemovalTime = null,
    string? LastStatus = null,
    bool Error = false,
    string? ErrorDetail = null,
    IReadOnlyDictionary<string, EdgegapDeploymentPort>? Ports = null,
    EdgegapLocation? Location = null,
    IReadOnlyList<string>? Tags = null,
    string? Command = null,
    string? Arguments = null,
    string? Message = null)
{
    public const string StatusSeeking = "Status.SEEKING";
    public const string StatusDeploying = "Status.DEPLOYING";
    public const string StatusReady = "Status.READY";
    public const string StatusError = "Status.ERROR";
}

public sealed record EdgegapLocation(
    string City,
    string Country,
    string Continent,
    string AdministrativeDivision,
    string Timezone,
    double Latitude,
    double Longitude);