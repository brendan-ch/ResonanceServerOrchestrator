namespace ResonanceServerOrchestrator.Services.Edgegap;

public sealed class EdgegapGameServerLauncher(
    IEdgegapClient client,
    int pollingDelayMs,
    int maxPollingAttempts,
    ILogger<EdgegapGameServerLauncher> logger) : IGameServerLauncher
{
    public bool ReportsReadiness => true;
    private int _pollingDelayMs = pollingDelayMs;
    private int _maxPollingAttempts = maxPollingAttempts;

    public async Task<IGameInstance> Launch(GameServerLaunchSpec spec, CancellationToken token = default)
    {
        if (spec is not EdgegapGameServerLaunchSpec edgegapSpec)
        {
            throw new GameServerLaunchException("Invalid launch spec");
        }

        var deploymentRequest = new EdgegapDeploymentRequest(
            "Resonance",
            edgegapSpec.ServerVersion,
            new List<EdgegapUser>(),
            EnvironmentVariables: new List<EdgegapEnvironmentVariable>()
        );

        foreach (var ip in edgegapSpec.UserIpAddresses)
        {
            _ = deploymentRequest.Users.Append(new EdgegapUser(
                UserType: "ip_address",
                UserData: new Dictionary<string, object>()
                {
                    { "ip_address", ip }
                }
            ));
        }

        foreach (var envVar in edgegapSpec.Environment)
        {
            _ = deploymentRequest.EnvironmentVariables?.Append(new EdgegapEnvironmentVariable(
                Key: envVar.Key,
                Value: envVar.Value,
                IsHidden: false
            ));
        }

        try
        {
            var deploymentResponse = await client.DeployAsync(deploymentRequest, token);
            var requestId = deploymentResponse.RequestId;
            if (requestId == null)
            {
                throw new GameServerLaunchException("Failed to deploy game server");
            }

            EdgegapGetResponse? readyResponse = null;
            var numPollingAttempts = 0;
            while (readyResponse == null && numPollingAttempts < _maxPollingAttempts)
            {
                var getResponse = await client.GetAsync(new EdgegapGetRequest(requestId), token);
                await Task.Delay(_pollingDelayMs, token);
                if (getResponse.CurrentStatus == EdgegapGetResponse.StatusReady)
                {
                    readyResponse = getResponse;
                }

                numPollingAttempts++;
            }

            if (numPollingAttempts >= _maxPollingAttempts)
            {
                throw new TimeoutException("Unable to obtain ready status in specified number of polling attempts");
            }

            var instance = new EdgegapGameInstance(client, requestId);

            return instance;
        }
        catch (Exception e)
        {
            // don't expose errors to calling client, do log for visibility
            logger.LogError(e, "Failed to deploy game server");
            throw new GameServerLaunchException("Failed to deploy game server");
        }
    }
}