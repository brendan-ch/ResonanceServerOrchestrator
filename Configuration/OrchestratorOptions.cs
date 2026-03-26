using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Configuration;

public sealed record OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    public string UnityServerPath { get; init; } = string.Empty;
    public string UnityServerBaseArgs { get; init; } = string.Empty;
    public string OrchestratorUrl { get; init; } = string.Empty;
    public string UnityServerDockerContextPath { get; init; } = string.Empty;
    public LauncherType LauncherType { get; init; } = LauncherType.Process;
    public int MaxLobbies { get; init; } = 10;
    public double LobbyTimeoutMinutes { get; init; } = 30;
}
