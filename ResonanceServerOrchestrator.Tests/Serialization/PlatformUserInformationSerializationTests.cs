using System.Text.Json;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Serialization;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Serialization;

public sealed class PlatformUserInformationSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ApplyOrchestratorConventions();

    /// <remarks>
    /// The numeric form travels on the wire, so a platform added in the middle of the enum
    /// would silently renumber the ones after it. Commit 3dc6137 did exactly that.
    /// </remarks>
    [Fact]
    public void PlatformOrdinals_ArePinnedToTheirWireValues()
    {
        Assert.Equal(0, (int)Platform.Steam);
        Assert.Equal(1, (int)Platform.Dummy);
    }

    [Theory]
    [InlineData("0", Platform.Steam)]
    [InlineData("1", Platform.Dummy)]
    [InlineData("\"Steam\"", Platform.Steam)]
    [InlineData("\"steam\"", Platform.Steam)]
    [InlineData("\"Dummy\"", Platform.Dummy)]
    public void Read_PlatformInEitherWireForm_Binds(string platformJson, Platform expected)
    {
        var json = $$"""
            {
              "platform": {{platformJson}},
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694"
            }
            """;

        Assert.Equal(expected, Deserialize(json).Platform);
    }

    [Fact]
    public void Read_PlatformDiscriminatorLast_StillBinds()
    {
        const string json = """
            {
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694",
              "authenticationTicketHex": "14000000",
              "platform": 0
            }
            """;

        var user = Deserialize(json);

        Assert.Equal(Platform.Steam, user.Platform);
        Assert.Equal("76561197960287930", user.PlatformUserId);
        Assert.Equal("109775241004308694", user.PlatformLobbyId);
        Assert.Equal("14000000", user.AuthenticationTicketHex);
    }

    [Fact]
    public void Read_AuthenticationTicketOmitted_BindsWithoutATicket()
    {
        const string json = """
            {
              "platform": 0,
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694"
            }
            """;

        Assert.Null(Deserialize(json).AuthenticationTicketHex);
    }

    [Theory]
    [InlineData("""{"platform":"Xbox","platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":null,"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":{},"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":[0],"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("[]")]
    [InlineData("\"steam\"")]
    [InlineData("5")]
    [InlineData("true")]
    public void Read_UnbindablePayload_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => Deserialize(json));
    }

    [Theory]
    [InlineData(Platform.Steam, "14000000AABB")]
    [InlineData(Platform.Steam, null)]
    [InlineData(Platform.Dummy, null)]
    public void WriteThenRead_PreservesEveryField(Platform platform, string? ticket)
    {
        var original = new PlatformUserInformationDto(
            platform, "76561197960287930", "109775241004308694", ticket);

        var written = JsonSerializer.Serialize(original, SerializerOptions);

        // Compared field by field rather than with Assert.Equal: the contracts are plain
        // classes, so equality is by reference.
        var read = Deserialize(written);

        Assert.Equal(original.Platform, read.Platform);
        Assert.Equal(original.PlatformUserId, read.PlatformUserId);
        Assert.Equal(original.PlatformLobbyId, read.PlatformLobbyId);
        Assert.Equal(original.AuthenticationTicketHex, read.AuthenticationTicketHex);
    }

    [Fact]
    public void Write_EmitsThePlatformAsItsName()
    {
        var written = JsonSerializer.Serialize(
            new PlatformUserInformationDto(Platform.Steam, "765", "109"), SerializerOptions);

        using var document = JsonDocument.Parse(written);

        Assert.Equal(
            nameof(Platform.Steam),
            document.RootElement.GetProperty("platform").GetString());
    }

    /// <remarks>
    /// The identity is derived from two fields already on the wire. Keeping it a method rather
    /// than a property is what keeps the redundant copy out of the payload.
    /// </remarks>
    [Fact]
    public void Write_DoesNotEmitTheDerivedIdentity()
    {
        var request = new JoinMatchDto(
            new PlatformUserInformationDto(Platform.Steam, "765", "109"),
            [new ExpectedLobbyPlayerDto("ana", Platform.Steam, "765")]);

        var written = JsonSerializer.Serialize(request, SerializerOptions);

        Assert.DoesNotContain("identity", written, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetIdentity_PairsThePlatformWithTheUserId()
    {
        var user = new PlatformUserInformationDto(Platform.Dummy, "765", "109");

        Assert.Equal(new PlayerIdentity(Platform.Dummy, "765"), user.GetIdentity());
    }

    [Fact]
    public void Read_WellFormedJoinRequest_BindsThePlatformUserInformation()
    {
        const string json = """
            {
              "platformUserInformation": {
                "platformUserId": "76561197960287930",
                "platformLobbyId": "109775241004308694",
                "platform": 0
              },
              "expectedLobbyPlayers": [
                { "username": "ana", "platform": 0, "platformUserId": "76561197960287930" }
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<JoinMatchDto>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The join request bound to null.");

        Assert.Equal(
            new PlayerIdentity(Platform.Steam, "76561197960287930"),
            request.PlatformUserInformation.GetIdentity());
    }

    private static PlatformUserInformationDto Deserialize(string json) =>
        JsonSerializer.Deserialize<PlatformUserInformationDto>(json, SerializerOptions)
        ?? throw new InvalidOperationException("The platform user information bound to null.");
}
