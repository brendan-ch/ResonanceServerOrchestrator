#nullable enable

namespace Resonance.Contracts
{
    public sealed class MatchMemberDto
    {
        public MatchMemberDto(
            Platform platform,
            string platformUserId,
            string username,
            string serverAuthToken,
            string ipAddress)
        {
            Platform = platform;
            PlatformUserId = platformUserId;
            Username = username;
            ServerAuthToken = serverAuthToken;
            IpAddress = ipAddress;
        }

        public Platform Platform { get; }

        public string PlatformUserId { get; }

        public string Username { get; }

        public string ServerAuthToken { get; }

        public string IpAddress { get; }
    }
}
