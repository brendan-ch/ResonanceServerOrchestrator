#nullable enable
using System;

namespace Resonance.Contracts
{
    public sealed class JoinMatchResultDto
    {
        public JoinMatchResultDto(
            Guid matchId,
            string dedicatedServerHost,
            int dedicatedServerPort,
            string serverAuthToken)
        {
            MatchId = matchId;
            DedicatedServerHost = dedicatedServerHost;
            DedicatedServerPort = dedicatedServerPort;
            ServerAuthToken = serverAuthToken;
        }

        public Guid MatchId { get; }

        public string DedicatedServerHost { get; }

        public int DedicatedServerPort { get; }

        public string ServerAuthToken { get; }
    }
}
