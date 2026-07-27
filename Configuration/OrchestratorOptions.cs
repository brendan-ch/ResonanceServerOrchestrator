using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Configuration;

public sealed record OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    public string UnityServerPath { get; init; } = string.Empty;
    public string UnityServerBaseArgs { get; init; } = string.Empty;
    public string OrchestratorUrl { get; init; } = string.Empty;
    public LauncherType LauncherType { get; init; } = LauncherType.LocalProcess;

    public int MaxMatches { get; init; } = 1;
    public double MatchTimeoutMinutes { get; init; } = 30;

    public string GameServerHost { get; init; } = "localhost";
    public int GameServerPort { get; init; } = 7777;

    public double RosterAssemblyTimeoutSeconds { get; init; } = 45;
    public double ServerReadyTimeoutSeconds { get; init; } = 30;
    public double TombstoneRetentionMinutes { get; init; } = 10;
    public double CleanupIntervalSeconds { get; init; } = 60;

    public int MaxExpectedLobbyPlayers { get; init; } = 16;
    public int MaxPlatformIdentifierLength { get; init; } = 64;
    public int MaxUsernameLength { get; init; } = 64;
    public int MaxAuthenticationTicketHexLength { get; init; } = 2048;

    public bool SteamCredentialCheckDisabled { get; init; }
    public string SteamPublisherWebApiKey { get; init; } = string.Empty;
    public uint SteamAppId { get; init; }
}
