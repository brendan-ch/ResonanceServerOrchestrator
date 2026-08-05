namespace Resonance.Contracts
{
    public sealed class JoinMatchDto
    {
        public JoinMatchDto(PlatformUserInformationDto platformUserInformation,
            ExpectedLobbyPlayerDto[] expectedLobbyPlayers, string nextSceneName, string gameMode)
        {
            PlatformUserInformation = platformUserInformation;
            ExpectedLobbyPlayers = expectedLobbyPlayers;
            NextSceneName = nextSceneName;
            GameMode = gameMode;
        }

        public PlatformUserInformationDto PlatformUserInformation { get; }

        public ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers { get; }

        public string NextSceneName { get; }
        public string GameMode { get; }
    }
}