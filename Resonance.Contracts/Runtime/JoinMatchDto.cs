namespace Resonance.Contracts
{
    public sealed class JoinMatchDto
    {
        public JoinMatchDto(PlatformUserInformationDto platformUserInformation,
            ExpectedLobbyPlayerDto[] expectedLobbyPlayers, string nextSceneName)
        {
            PlatformUserInformation = platformUserInformation;
            ExpectedLobbyPlayers = expectedLobbyPlayers;
            NextSceneName = nextSceneName;
        }

        public PlatformUserInformationDto PlatformUserInformation { get; }

        public ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers { get; }

        public string NextSceneName { get; }
    }
}