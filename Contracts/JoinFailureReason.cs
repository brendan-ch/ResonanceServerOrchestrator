namespace ResonanceServerOrchestrator.Contracts;

public enum JoinFailureReason
{
    RosterAssemblyTimedOut,
    ServerReadyTimedOut,
    RosterMismatch,
    PeerLeft,
    PeerAuthenticationFailed,
    PlayerInMultipleLobbies,
    ServerLaunchFailed,
    SupersededByReconnect,
    MatchAlreadyStarted,
    CapacityReached
}
