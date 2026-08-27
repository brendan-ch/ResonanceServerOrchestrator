using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Configuration;

public sealed record OrchestratorOptions
{
    public const string SectionName = "Orchestrator";

    #region Launcher type
    public LauncherType LauncherType { get; init; } = LauncherType.LocalProcess;
    #endregion

    #region Local process launcher
    public string UnityServerPath { get; init; } = string.Empty;
    public string UnityServerBaseArgs { get; init; } = string.Empty;
    public string OrchestratorUrl { get; init; } = string.Empty;
    public string LocalGameServerHost { get; init; } = "localhost";
    public int LocalGameServerInternalAndExternalPort { get; init; } = 7777;
    #endregion

    #region Match configuration
    public int MaxMatches { get; init; } = 1;
    public double MatchTimeoutMinutes { get; init; } = 30;


    public double RosterAssemblyTimeoutSeconds { get; init; } = 45;
    public double ServerReadyTimeoutSeconds { get; init; } = 30;
    public double TombstoneRetentionMinutes { get; init; } = 10;
    public double CleanupIntervalSeconds { get; init; } = 60;

    public int MaxExpectedLobbyPlayers { get; init; } = 16;
    public int MaxPlatformIdentifierLength { get; init; } = 64;
    public int MaxUsernameLength { get; init; } = 64;
    public int MaxAuthenticationTicketHexLength { get; init; } = 2048;
    #endregion

    #region Steam configuration
    public bool SteamCredentialCheckDisabled { get; init; }
    public string SteamPublisherWebApiKey { get; init; } = string.Empty;
    public uint SteamAppId { get; init; }
    #endregion

    #region Edgegap configuration

    public string EdgegapApplicationName { get; init; } = "Resonance";
    public string EdgegapApiKey { get; init; } = string.Empty;
    public string EdgegapBaseUrl { get; init; } = "https://api.edgegap.com";
    public int EdgegapPollingDelayMs { get; init; } = 2000;
    public int EdgegapMaxPollingAttempts { get; init; } = 30;
    #endregion
}
