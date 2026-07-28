using Resonance.Contracts;

namespace ResonanceServerOrchestrator.Stores;

internal abstract record JoinOutcome;

internal sealed record MemberAdded(
    Guid MatchId,
    long MemberGeneration,
    Task<JoinResult> Completion) : JoinOutcome;

internal sealed record RosterComplete(
    Guid MatchId,
    long MemberGeneration,
    MatchSnapshot Snapshot,
    Task<JoinResult> Completion) : JoinOutcome;

internal sealed record Rejected(
    JoinFailureReason Reason,
    int JoinedCount,
    int ExpectedCount) : JoinOutcome;
