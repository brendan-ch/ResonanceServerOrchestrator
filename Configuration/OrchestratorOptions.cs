namespace ResonanceServerOrchestrator.Configuration;

public sealed record OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    public string UnityServerPath { get; init; } = string.Empty;
    public string UnityServerBaseArgs { get; init; } = string.Empty;
    public string OrchestratorUrl { get; init; } = string.Empty;
}
