namespace ResonanceServerOrchestrator.Services;

public sealed class GameServerLaunchException : Exception
{
    public GameServerLaunchException(string message) : base(message) { }

    public GameServerLaunchException(string message, Exception innerException)
        : base(message, innerException) { }
}
