namespace ResonanceServerOrchestrator.Services.Edgegap;

public class EdgegapGameInstance(IEdgegapClient client, string deploymentId) : IGameInstance
{
    private IEdgegapClient _client = client;
    private string _deploymentId = deploymentId;

    public bool HasExited { get; private set; } = false;
    public Task Stop()
    {
        throw new NotImplementedException();
    }

    public event EventHandler? Exited;
}