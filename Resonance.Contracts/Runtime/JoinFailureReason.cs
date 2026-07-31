namespace Resonance.Contracts
{
    public enum JoinFailureReason
    {
        RosterAssemblyTimedOut = 0,
        ServerReadyTimedOut = 1,
        RosterMismatch = 2,
        PeerLeft = 3,
        PeerAuthenticationFailed = 4,
        PlayerInMultipleLobbies = 5,
        ServerLaunchFailed = 6,
        SupersededByReconnect = 7,
        MatchAlreadyStarted = 8,
        CapacityReached = 9,
        OtherDataMismatch = 10
    }
}
