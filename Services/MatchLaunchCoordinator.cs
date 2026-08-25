using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Services;

internal sealed class MatchLaunchCoordinator(
    IMatchStore store,
    IGameServerLauncher launcher,
    IOptions<OrchestratorOptions> options,
    ILogger<MatchLaunchCoordinator> logger)
{
    public async Task LaunchGameServerFor(MatchSnapshot snapshot)
    {
        IGameInstance instance;
        try
        {
            instance = await launcher.Launch(BuildLaunchSpec(snapshot));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Launching the game server for match {MatchId} failed.",
                snapshot.MatchId);
            store.OnInstanceExited(snapshot.MatchId);
            return;
        }

        if (!store.TrySetInstance(snapshot.MatchId, instance))
        {
            instance.Stop();
            return;
        }

        if (!launcher.ReportsReadiness)
            store.MarkReady(snapshot.MatchId, snapshot.MatchKey);
    }

    private LocalGameServerLaunchSpec BuildLaunchSpec(MatchSnapshot snapshot)
    {
        var configuration = options.Value;

        return new LocalGameServerLaunchSpec(
            configuration.UnityServerPath,
            configuration.UnityServerBaseArgs,
            new Dictionary<string, string>
            {
                [GameServerLaunchSpec.GameServerPortVariable] =
                    snapshot.GameServerPort.ToString(),
                [GameServerLaunchSpec.MatchIdVariable] = snapshot.MatchId.ToString("D"),
                [GameServerLaunchSpec.MatchKeyVariable] = snapshot.MatchKey,
                [GameServerLaunchSpec.OrchestratorUrlVariable] = configuration.OrchestratorUrl,
                [GameServerLaunchSpec.NextSceneNameVariable] = snapshot.NextSceneName,
                [GameServerLaunchSpec.GameModeVariable] = snapshot.GameMode
            });
    }
}
