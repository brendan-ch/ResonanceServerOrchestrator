namespace Resonance.Contracts
{
    public sealed class JoinMatchDto
    {
        public JoinMatchDto(PlatformUserInformationDto platformUserInformation,
            ExpectedLobbyPlayerDto[] expectedLobbyPlayers,
            string nextSceneName,
            string gameMode,
            string? intendedServerVersion = null
        )
        {
            PlatformUserInformation = platformUserInformation;
            ExpectedLobbyPlayers = expectedLobbyPlayers;
            NextSceneName = nextSceneName;
            GameMode = gameMode;
            IntendedServerVersion = intendedServerVersion;
        }

        public PlatformUserInformationDto PlatformUserInformation { get; }
        public ExpectedLobbyPlayerDto[] ExpectedLobbyPlayers { get; }
        public string NextSceneName { get; }
        public string GameMode { get; }

        /// <summary>
        /// An optional string indicating which server version to use.
        /// It must match with the version injected into the server build,
        /// including if there is no server version.
        ///
        /// The Edgegap launcher backend also uses this to look up the server version.
        /// </summary>
        public string? IntendedServerVersion { get; }
    }
}