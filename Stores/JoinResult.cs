using Resonance.Contracts;

namespace ResonanceServerOrchestrator.Stores;

internal abstract record JoinResult;

internal sealed record JoinSucceeded(
    Guid MatchId,
    string DedicatedServerHost,
    int DedicatedServerPort,
    string ServerAuthToken) : JoinResult;

internal sealed record JoinFailed(
    JoinFailureReason Reason,
    int JoinedCount,
    int ExpectedCount) : JoinResult;
