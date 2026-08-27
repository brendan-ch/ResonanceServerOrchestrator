namespace ResonanceServerOrchestrator.Services.Edgegap;

public sealed record EdgegapDeploymentPort(
    string Name,
    string Link,
    int Internal,
    int External,
    string Protocol,
    bool TlsUpgrade = false);