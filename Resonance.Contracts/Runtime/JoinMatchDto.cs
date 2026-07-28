#nullable enable

namespace Resonance.Contracts
{
    public sealed class JoinMatchDto
    {
        public JoinMatchDto(
            PlatformUserInformationDto platformUserInformation,
            ExpectedLobbyPlayerDto[] expectedLobbyPlayers)
        {
            PlatformUserInformation = platformUserInformation;
            ExpectedLobbyPlayers = expectedLobbyPlayers;
        }

        public PlatformUserInformationDto PlatformUserInformation { get; }

        public ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers { get; }
    }
}
