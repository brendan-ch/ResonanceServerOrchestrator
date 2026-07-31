using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Stores;

internal interface IMatchStore
{
    JoinOutcome TryJoin(
        LobbyKey lobby,
        PlayerIdentity identity,
        string username,
        IReadOnlyList<PlayerIdentity> expectedRoster,
        string expectedNextSceneName);

    bool TrySetInstance(Guid matchId, IGameInstance instance);

    MarkReadyOutcome MarkReady(Guid matchId, string presentedMatchKey);

    MatchSnapshotLookup LookUpSnapshotForGameServer(Guid matchId, string presentedMatchKey);

    bool TryTearDownForFailedAuth(LobbyKey lobby, PlayerIdentity claimedIdentity);

    bool TryLeave(PlayerIdentity identity);

    void DeregisterAbortedMember(Guid matchId, PlayerIdentity identity, long memberGeneration);

    void OnInstanceExited(Guid matchId);

    void ReapExpired();
}

internal enum MarkReadyOutcome
{
    MatchStarted,
    MatchWasAlreadyStarted,
    RosterNotYetComplete,
    MatchKeyRejected,
    MatchAlreadyDestroyed,
    MatchNotFound
}

internal enum MatchSnapshotLookupOutcome
{
    Granted,
    MatchKeyRejected,
    MatchAlreadyDestroyed,
    MatchNotFound
}

internal sealed record MatchSnapshotLookup(MatchSnapshotLookupOutcome Outcome, MatchSnapshot? Snapshot);