namespace ResonanceServerOrchestrator.Contracts;

public sealed record JoinFailureDto(JoinFailureReason Reason, int JoinedCount, int ExpectedCount);
