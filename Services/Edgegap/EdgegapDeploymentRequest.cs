namespace ResonanceServerOrchestrator.Services.Edgegap;

// POST https://api.edgegap.com/v2/deployments
public sealed record EdgegapDeploymentRequest(
    string Application,
    string Version,
    IReadOnlyList<EdgegapUser> Users,
    EdgegapResources? Resources = null,
    bool RequireCachedLocations = false,
    IReadOnlyList<EdgegapEnvironmentVariable>? EnvironmentVariables = null,
    IReadOnlyList<string>? Tags = null,
    EdgegapWebhook? WebhookOnReady = null,
    EdgegapWebhook? WebhookOnError = null,
    EdgegapWebhook? WebhookOnTerminated = null);

public sealed record EdgegapResources(int CpuUnits, int MemoryMib);

public sealed record EdgegapUser(string UserType, IReadOnlyDictionary<string, object> UserData);

public sealed record EdgegapEnvironmentVariable(string Key, string Value, bool IsHidden);

public sealed record EdgegapWebhook(string Url);
