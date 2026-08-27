using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Serialization;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ResonanceServerOrchestrator.Tests.Serialization;

/// <summary>
/// The Unity game consumes Resonance.Contracts as source and deserializes with Newtonsoft,
/// which is all Unity ships — there is no System.Text.Json in the player.
/// </summary>
/// <remarks>
/// The contracts carry no serializer attributes; binding rests entirely on each type having one
/// public constructor whose parameter names match its property names. System.Text.Json throws
/// when that drifts. Newtonsoft does not — it leaves the property null and carries on. These
/// tests are the only thing that would catch such a drift before it reached the game.
/// </remarks>
public sealed class UnitySerializerCompatibilityTests
{
    private const string SampleNextSceneName = "TestScene";
    private const string SampleGameMode = "Arena";
    private const string SampleIntendedServerVersion = "test-server-version";

    private static readonly JsonSerializerOptions OrchestratorOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ApplyOrchestratorConventions();

    /// <summary>Mirrors the settings documented in Resonance.Contracts/README.md.</summary>
    private static JsonSerializerSettings UnitySettings()
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
        };

        settings.Converters.Add(new StringEnumConverter());
        return settings;
    }

    private static string OrchestratorWrites<T>(T value) =>
        JsonSerializer.Serialize(value, OrchestratorOptions);

    private static T UnityReads<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json, UnitySettings())
        ?? throw new InvalidOperationException($"{typeof(T).Name} bound to null under Newtonsoft.");

    private static string UnityWrites<T>(T value) =>
        JsonConvert.SerializeObject(value, UnitySettings());

    [Fact]
    public void JoinMatchResult_WrittenByTheOrchestrator_IsReadableByUnity()
    {
        var matchId = Guid.NewGuid();
        var written = OrchestratorWrites(
            new JoinMatchResultDto(matchId, "game.example", 7777, "token-abc"));

        var read = UnityReads<JoinMatchResultDto>(written);

        Assert.Equal(matchId, read.MatchId);
        Assert.Equal("game.example", read.DedicatedServerHost);
        Assert.Equal(7777, read.DedicatedServerPort);
        Assert.Equal("token-abc", read.ServerAuthToken);
    }

    [Fact]
    public void MatchMember_WrittenByTheOrchestrator_IsReadableByUnity()
    {
        var written = OrchestratorWrites(
            new MatchMemberDto(Platform.Steam, "76561198000000001", "ana", "token-abc", "203.0.113.10"));

        var read = UnityReads<MatchMemberDto>(written);

        Assert.Equal(Platform.Steam, read.Platform);
        Assert.Equal("76561198000000001", read.PlatformUserId);
        Assert.Equal("ana", read.Username);
        Assert.Equal("token-abc", read.ServerAuthToken);
        Assert.Equal("203.0.113.10", read.IpAddress);
    }

    [Theory]
    [InlineData(JoinFailureReason.RosterAssemblyTimedOut)]
    [InlineData(JoinFailureReason.SupersededByReconnect)]
    [InlineData(JoinFailureReason.CapacityReached)]
    public void JoinFailure_WrittenByTheOrchestrator_IsReadableByUnity(JoinFailureReason reason)
    {
        var written = OrchestratorWrites(new JoinFailureDto(reason, 1, 2));

        var read = UnityReads<JoinFailureDto>(written);

        Assert.Equal(reason, read.Reason);
        Assert.Equal(1, read.JoinedCount);
        Assert.Equal(2, read.ExpectedCount);
    }

    [Fact]
    public void JoinRequest_WrittenByUnity_IsReadableByTheOrchestrator()
    {
        var written = UnityWrites(new JoinMatchDto(
            new PlatformUserInformationDto(
                Platform.Steam, "76561198000000001", "lobby-1", "14000000AABB"),
            [
                new ExpectedLobbyPlayerDto("ana", Platform.Steam, "76561198000000001"),
                new ExpectedLobbyPlayerDto("bo", Platform.Steam, "76561198000000002"),
            ], SampleNextSceneName, SampleGameMode));

        var read = JsonSerializer.Deserialize<JoinMatchDto>(written, OrchestratorOptions)
                   ?? throw new InvalidOperationException("The join request bound to null.");

        Assert.Equal(Platform.Steam, read.PlatformUserInformation.Platform);
        Assert.Equal("76561198000000001", read.PlatformUserInformation.PlatformUserId);
        Assert.Equal("lobby-1", read.PlatformUserInformation.PlatformLobbyId);
        Assert.Equal("14000000AABB", read.PlatformUserInformation.AuthenticationTicketHex);
        Assert.Equal(["ana", "bo"], read.ExpectedLobbyPlayers.Select(player => player.Username));
    }

    [Fact]
    public void JoinRequestWithServerVersion_WrittenByUnity_IsReadableByTheOrchestrator()
    {
        var written = UnityWrites(new JoinMatchDto(
            new PlatformUserInformationDto(
                Platform.Steam, "76561198000000001", "lobby-1", "14000000AABB"),
            [
                new ExpectedLobbyPlayerDto("ana", Platform.Steam, "76561198000000001"),
                new ExpectedLobbyPlayerDto("bo", Platform.Steam, "76561198000000002"),
            ], SampleNextSceneName, SampleGameMode, SampleIntendedServerVersion));

        var read = JsonSerializer.Deserialize<JoinMatchDto>(written, OrchestratorOptions)
                   ?? throw new InvalidOperationException("The join request bound to null.");

        Assert.Equal(Platform.Steam, read.PlatformUserInformation.Platform);
        Assert.Equal("76561198000000001", read.PlatformUserInformation.PlatformUserId);
        Assert.Equal("lobby-1", read.PlatformUserInformation.PlatformLobbyId);
        Assert.Equal("14000000AABB", read.PlatformUserInformation.AuthenticationTicketHex);
        Assert.Equal(["ana", "bo"], read.ExpectedLobbyPlayers.Select(player => player.Username));
        Assert.Equal(SampleIntendedServerVersion, read.IntendedServerVersion);
    }

    [Fact]
    public void LeaveRequest_WrittenByUnity_IsReadableByTheOrchestrator()
    {
        var written = UnityWrites(new LeaveMatchDto(
            new PlatformUserInformationDto(Platform.Dummy, "dummy-1", "lobby-1")));

        var read = JsonSerializer.Deserialize<LeaveMatchDto>(written, OrchestratorOptions)
                   ?? throw new InvalidOperationException("The leave request bound to null.");

        Assert.Equal(
            new PlayerIdentity(Platform.Dummy, "dummy-1"),
            read.PlatformUserInformation.GetIdentity());
    }

    /// <remarks>
    /// Byte equality, not just mutual readability: if the two serializers ever disagree on
    /// casing or on the enum form, this is what says so.
    /// </remarks>
    [Fact]
    public void BothSerializers_ProduceTheSameJoinRequestPayload()
    {
        var request = new JoinMatchDto(
            new PlatformUserInformationDto(
                Platform.Dummy, "76561198000000001", "lobby-1", "14000000AABB"),
            [new ExpectedLobbyPlayerDto("ana", Platform.Steam, "76561198000000001")], SampleNextSceneName,
            SampleGameMode);

        Assert.Equal(OrchestratorWrites(request), UnityWrites(request));
    }


    [Fact]
    public void UnityWritesEnumsAsNames_NotOrdinals()
    {
        var written = UnityWrites(
            new PlatformUserInformationDto(Platform.Dummy, "1", "lobby-1"));

        Assert.Contains("\"platform\":\"Dummy\"", written);
    }

    /// <remarks>
    /// The orchestrator's own strictness comes from RespectRequiredConstructorParameters, which
    /// has no Newtonsoft equivalent in the documented settings. Pinned so the asymmetry is a
    /// known property rather than a discovery made in the game.
    /// </remarks>
    [Fact]
    public void Unity_ToleratesAnOmittedPlatform_WhereTheOrchestratorRejectsIt()
    {
        const string json = """{"platformUserId":"1","platformLobbyId":"lobby-1"}""";

        Assert.Throws<System.Text.Json.JsonException>(() =>
            JsonSerializer.Deserialize<PlatformUserInformationDto>(json, OrchestratorOptions));

        Assert.Equal(Platform.Steam, UnityReads<PlatformUserInformationDto>(json).Platform);
    }
}