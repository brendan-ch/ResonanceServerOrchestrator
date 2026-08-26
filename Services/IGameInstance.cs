namespace ResonanceServerOrchestrator.Services;

public interface IGameInstance
{
    bool HasExited { get; }

    /// <exception cref="GameInstanceException">Thrown if the instance fails to stop.</exception>
    Task Stop();

    event EventHandler? Exited;
}

public class GameInstanceException : Exception
{
    public GameInstanceException(string message) : base(message) { }

    public GameInstanceException(string message, Exception innerException)
        : base(message, innerException) { }

}
