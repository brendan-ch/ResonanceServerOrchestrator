#nullable enable

namespace Resonance.Contracts
{
    public sealed class ExpectedLobbyPlayerDto
    {
        public ExpectedLobbyPlayerDto(string username, Platform platform, string platformUserId)
        {
            Username = username;
            Platform = platform;
            PlatformUserId = platformUserId;
        }

        public string Username { get; }

        public Platform Platform { get; }

        public string PlatformUserId { get; }

        /// <remarks>
        /// Deliberately a method rather than a property: a public getter would be serialized
        /// onto the wire as a redundant copy of the two fields it derives from.
        /// </remarks>
        public PlayerIdentity GetIdentity() => new PlayerIdentity(Platform, PlatformUserId);
    }
}
