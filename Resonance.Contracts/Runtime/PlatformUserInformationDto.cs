#nullable enable

namespace Resonance.Contracts
{
    /// <remarks>
    /// One public constructor with parameter names matching the property names, so
    /// System.Text.Json and Newtonsoft both bind it without any serializer attribute.
    /// </remarks>
    public sealed class PlatformUserInformationDto
    {
        public PlatformUserInformationDto(
            Platform platform,
            string platformUserId,
            string platformLobbyId,
            string? authenticationTicketHex = null)
        {
            Platform = platform;
            PlatformUserId = platformUserId;
            PlatformLobbyId = platformLobbyId;
            AuthenticationTicketHex = authenticationTicketHex;
        }

        public Platform Platform { get; }

        public string PlatformUserId { get; }

        public string PlatformLobbyId { get; }

        public string? AuthenticationTicketHex { get; }

        /// <remarks>
        /// Deliberately a method rather than a property: a public getter would be serialized
        /// onto the wire as a redundant copy of the two fields it derives from.
        /// </remarks>
        public PlayerIdentity GetIdentity() => new PlayerIdentity(Platform, PlatformUserId);
    }
}
