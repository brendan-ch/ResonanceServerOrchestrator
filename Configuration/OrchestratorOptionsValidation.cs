using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Configuration;

public static class OrchestratorOptionsValidation
{
    public static OptionsBuilder<OrchestratorOptions> ValidateOrchestratorOptions(
        this OptionsBuilder<OrchestratorOptions> builder, IHostEnvironment environment) =>
        builder
            .Validate(
                options => !options.SteamCredentialCheckDisabled || environment.IsDevelopment(),
                $"{Key(nameof(OrchestratorOptions.SteamCredentialCheckDisabled))} may only be true " +
                "in the Development environment. Outside it, disabling the check makes every " +
                "player identity attacker-controlled.")
            .Validate(options => options.MaxExpectedLobbyPlayers > 0,
                $"{Key(nameof(OrchestratorOptions.MaxExpectedLobbyPlayers))} must be positive.")
            .Validate(SteamCredentialsArePresentWhenTheCheckIsEnabled,
                $"{Key(nameof(OrchestratorOptions.SteamPublisherWebApiKey))} and " +
                $"{Key(nameof(OrchestratorOptions.SteamAppId))} are required unless " +
                $"{Key(nameof(OrchestratorOptions.SteamCredentialCheckDisabled))} is true.")
            .Validate(UnityServerPathExistsWhenLaunchingLocalProcesses,
                $"{Key(nameof(OrchestratorOptions.UnityServerPath))} must point at an existing file " +
                $"when {Key(nameof(OrchestratorOptions.LauncherType))} is " +
                $"{nameof(LauncherType.LocalProcess)}.")
            .Validate(LocalProcessHostsExactlyOneMatch,
                $"{Key(nameof(OrchestratorOptions.MaxMatches))} must be 1 when " +
                $"{Key(nameof(OrchestratorOptions.LauncherType))} is " +
                $"{nameof(LauncherType.LocalProcess)}: the local backend binds a single " +
                $"{Key(nameof(OrchestratorOptions.LocalGameServerInternalAndExternalPort))}.")
            .Validate(options => options.MaxMatches > 0,
                $"{Key(nameof(OrchestratorOptions.MaxMatches))} must be greater than zero.")
            .Validate(options => options.RosterAssemblyTimeoutSeconds > 0,
                $"{Key(nameof(OrchestratorOptions.RosterAssemblyTimeoutSeconds))} must be positive.")
            .Validate(options => options.ServerReadyTimeoutSeconds > 0,
                $"{Key(nameof(OrchestratorOptions.ServerReadyTimeoutSeconds))} must be positive.")
            .Validate(options => options.CleanupIntervalSeconds > 0,
                $"{Key(nameof(OrchestratorOptions.CleanupIntervalSeconds))} must be positive.")
            .Validate(options => options.LocalGameServerInternalAndExternalPort is > 0 and <= 65535,
                $"{Key(nameof(OrchestratorOptions.LocalGameServerInternalAndExternalPort))} must be between 1 and 65535.")
            .Validate(TombstonesOutliveTheServerReadyDeadline,
                $"{Key(nameof(OrchestratorOptions.TombstoneRetentionMinutes))} must exceed " +
                $"{Key(nameof(OrchestratorOptions.ServerReadyTimeoutSeconds))}, or a slow server " +
                "receives 404 instead of 410 and never self-terminates.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.LocalGameServerHost),
                $"{Key(nameof(OrchestratorOptions.LocalGameServerHost))} must not be empty.")
            .ValidateOnStart();

    private static bool SteamCredentialsArePresentWhenTheCheckIsEnabled(OrchestratorOptions options) =>
        options.SteamCredentialCheckDisabled ||
        (!string.IsNullOrWhiteSpace(options.SteamPublisherWebApiKey) && options.SteamAppId != 0);

    private static bool UnityServerPathExistsWhenLaunchingLocalProcesses(OrchestratorOptions options) =>
        options.LauncherType != LauncherType.LocalProcess || File.Exists(options.UnityServerPath);

    private static bool LocalProcessHostsExactlyOneMatch(OrchestratorOptions options) =>
        options.LauncherType != LauncherType.LocalProcess || options.MaxMatches == 1;

    private static bool TombstonesOutliveTheServerReadyDeadline(OrchestratorOptions options) =>
        options.TombstoneRetentionMinutes * 60 > options.ServerReadyTimeoutSeconds;

    private static string Key(string propertyName) =>
        $"{OrchestratorOptions.SectionName}:{propertyName}";
}
