namespace ResonanceServerOrchestrator.Services.Edgegap;

// Response to DELETE https://api.edgegap.com/v1/stop/{deployment_id}
// 200/202: Message required, DeploymentSummary only present on 200.
// 400/401/403/404/410/500: Message only.
public sealed record EdgegapStopResponse(string Message, EdgegapDeploymentStatus? DeploymentSummary = null);

public sealed record EdgegapDeploymentStatus(
    string AppName,
    string AppVersion,
    string CurrentStatus,
    double ElapsedTime,
    string Error,
    string Fqdn,
    double MaxDuration,
    string PublicIp,
    string RequestId,
    bool Running,
    string StartTime,
    string? RemovalTime = null,
    string? LastStatus = null,
    string? ErrorDetail = null,
    string? Command = null,
    IReadOnlyList<string>? Arguments = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, EdgegapDeploymentPort>? Ports = null,
    EdgegapLocationData? Location = null);

public sealed record EdgegapDeploymentPort(
    string Name,
    string Link,
    int Internal,
    int External,
    string Protocol,
    bool TlsUpgrade = false);

public sealed record EdgegapLocationData(
    string City,
    string Country,
    string Continent,
    string AdministrativeDivision,
    string Timezone,
    double Latitude,
    double Longitude);
