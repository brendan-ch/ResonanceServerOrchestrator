namespace ResonanceServerOrchestrator.Services.Edgegap;

public class EdgegapGameInstance(HttpEdgegapClient client) : IGameInstance
{
    private HttpEdgegapClient _client = client;

    public bool HasExited { get; private set; } = false;
    public Task Stop()
    {
        throw new NotImplementedException();
    }

    public event EventHandler? Exited;
}