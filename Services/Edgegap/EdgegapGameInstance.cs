namespace ResonanceServerOrchestrator.Services.Edgegap;

public class EdgegapGameInstance(IEdgegapClient client, string deploymentId) : IGameInstance
{
    private IEdgegapClient _client = client;
    private string _deploymentId = deploymentId;

    public bool HasExited { get; private set; }
    public async Task Stop()
    {
        try
        {
            _ = await _client.StopAsync(new EdgegapStopRequest(_deploymentId), CancellationToken.None);
        }
        catch (Exception e)
        {
            throw new GameInstanceException("Failed to stop the game instance", e);
        }

        HasExited = true;
    }

    public event EventHandler? Exited;
}