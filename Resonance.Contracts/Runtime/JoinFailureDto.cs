#nullable enable

namespace Resonance.Contracts
{
    public sealed class JoinFailureDto
    {
        public JoinFailureDto(JoinFailureReason reason, int joinedCount, int expectedCount)
        {
            Reason = reason;
            JoinedCount = joinedCount;
            ExpectedCount = expectedCount;
        }

        public JoinFailureReason Reason { get; }

        public int JoinedCount { get; }

        public int ExpectedCount { get; }
    }
}
