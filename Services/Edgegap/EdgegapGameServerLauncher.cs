namespace ResonanceServerOrchestrator.Services.Edgegap;

public sealed class EdgegapGameServerLauncher(
    IEdgegapClient client,
    int pollingDelayMs,
    int maxPollingAttempts,
    ILogger<EdgegapGameServerLauncher> logger) : IGameServerLauncher
{
    public bool ReportsReadiness => true;

    public async Task<IGameInstance> Launch(GameServerLaunchSpec spec, CancellationToken token = default)
    {
        if (spec is not EdgegapGameServerLaunchSpec edgegapSpec)
        {
            throw new GameServerLaunchException("Invalid launch spec");
        }

        var users = edgegapSpec.UserIpAddresses.Select(ip => new EdgegapUser(
            UserType: "ip_address",
            UserData: new Dictionary<string, object>()
            {
                { "ip_address", ip }
            }
        )).ToList();

        var environmentVariables = edgegapSpec.Environment.Select(envVar => new EdgegapEnvironmentVariable(
            Key: envVar.Key,
            Value: envVar.Value,
            IsHidden: false
        )).ToList();

        var deploymentRequest = new EdgegapDeploymentRequest(
            "Resonance",
            edgegapSpec.ServerVersion,
            users,
            EnvironmentVariables: environmentVariables
        );

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
            while (readyResponse == null && numPollingAttempts < maxPollingAttempts)
            {
                var getResponse = await client.GetAsync(new EdgegapGetRequest(requestId), token);
                await Task.Delay(pollingDelayMs, token);
                if (getResponse.CurrentStatus == EdgegapGetResponse.StatusReady)
                {
                    readyResponse = getResponse;
                }

                numPollingAttempts++;
            }

            if (numPollingAttempts >= maxPollingAttempts)
            {
                throw new TimeoutException("Unable to obtain ready status in specified number of polling attempts");
            }

            var instance = new EdgegapGameInstance(client, requestId);

            return instance;
        }
        catch (Exception e)
        {
            throw new GameServerLaunchException("Failed to deploy game server", e);
        }
    }
}