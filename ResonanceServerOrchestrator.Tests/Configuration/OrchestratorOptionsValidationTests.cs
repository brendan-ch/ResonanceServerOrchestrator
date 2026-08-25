using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Tests.TestHelpers;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Configuration;

public sealed class OrchestratorOptionsValidationTests
{
    private static string Key(string propertyName) =>
        $"{OrchestratorOptions.SectionName}:{propertyName}";

    private static string StartupFailureMessage(Dictionary<string, string?> overrides)
    {
        using var factory = new OrchestratorWebApplicationFactory(overrides);

        var failure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        return failure.Message;
    }

    [Fact]
    public void SteamCheckEnabledWithoutAPublisherKey_RefusesToStart()
    {
        var message = StartupFailureMessage(new()
        {
            [Key(nameof(OrchestratorOptions.SteamCredentialCheckDisabled))] = "false",
            [Key(nameof(OrchestratorOptions.SteamPublisherWebApiKey))] = "",
            [Key(nameof(OrchestratorOptions.SteamAppId))] = "480",
        });

        Assert.Contains(nameof(OrchestratorOptions.SteamPublisherWebApiKey), message);
    }

    [Fact]
    public void SteamCheckEnabledWithoutAnAppId_RefusesToStart()
    {
        var message = StartupFailureMessage(new()
        {
            [Key(nameof(OrchestratorOptions.SteamCredentialCheckDisabled))] = "false",
            [Key(nameof(OrchestratorOptions.SteamPublisherWebApiKey))] = "a-key",
            [Key(nameof(OrchestratorOptions.SteamAppId))] = "0",
        });

        Assert.Contains(nameof(OrchestratorOptions.SteamAppId), message);
    }

    [Fact]
    public void LocalProcessWithAMissingServerBinary_RefusesToStart()
    {
        var message = StartupFailureMessage(new()
        {
            [Key(nameof(OrchestratorOptions.LauncherType))] = nameof(LauncherType.LocalProcess),
            [Key(nameof(OrchestratorOptions.UnityServerPath))] = "/nonexistent/game-server",
        });

        Assert.Contains(nameof(OrchestratorOptions.UnityServerPath), message);
    }

    [Fact]
    public void LocalProcessHostingMoreThanOneMatch_RefusesToStart()
    {
        var message = StartupFailureMessage(new()
        {
            [Key(nameof(OrchestratorOptions.LauncherType))] = nameof(LauncherType.LocalProcess),
            [Key(nameof(OrchestratorOptions.MaxMatches))] = "2",
        });

        Assert.Contains(nameof(OrchestratorOptions.MaxMatches), message);
    }

    [Theory]
    [InlineData(nameof(OrchestratorOptions.MaxMatches), "0")]
    [InlineData(nameof(OrchestratorOptions.RosterAssemblyTimeoutSeconds), "0")]
    [InlineData(nameof(OrchestratorOptions.ServerReadyTimeoutSeconds), "-1")]
    [InlineData(nameof(OrchestratorOptions.CleanupIntervalSeconds), "0")]
    [InlineData(nameof(OrchestratorOptions.LocalGameServerInternalAndExternalPort), "0")]
    [InlineData(nameof(OrchestratorOptions.LocalGameServerInternalAndExternalPort), "70000")]
    [InlineData(nameof(OrchestratorOptions.LocalGameServerHost), "")]
    [InlineData(nameof(OrchestratorOptions.MaxExpectedLobbyPlayers), "0")]
    public void NonsensicalValues_RefuseToStart(string propertyName, string value)
    {
        var message = StartupFailureMessage(new() { [Key(propertyName)] = value });

        Assert.Contains(propertyName, message);
    }

    [Fact]
    public void TombstonesThatExpireBeforeTheReadyDeadline_RefuseToStart()
    {
        var message = StartupFailureMessage(new()
        {
            [Key(nameof(OrchestratorOptions.ServerReadyTimeoutSeconds))] = "600",
            [Key(nameof(OrchestratorOptions.TombstoneRetentionMinutes))] = "1",
        });

        Assert.Contains(nameof(OrchestratorOptions.TombstoneRetentionMinutes), message);
    }

    [Fact]
    public void AValidConfigurationStarts()
    {
        using var factory = new OrchestratorWebApplicationFactory();

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
