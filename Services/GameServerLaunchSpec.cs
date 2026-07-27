namespace ResonanceServerOrchestrator.Services;

public sealed record GameServerLaunchSpec(
    string ExecutablePath,
    string Arguments,
    IReadOnlyDictionary<string, string> Environment
)
{
    public const string GameServerPortVariable = "ARBITRIUM_PORT_GAMEPORT_INTERNAL";
    public const string MatchIdVariable = "RESONANCE_MATCH_ID";
    public const string MatchKeyVariable = "RESONANCE_MATCH_KEY";
    public const string OrchestratorUrlVariable = "RESONANCE_ORCHESTRATOR_URL";
}
