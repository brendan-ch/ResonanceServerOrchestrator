using System.Text.Json;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Serialization;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Serialization;

public sealed class PlatformUserInformationJsonConverterTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).AddPlatformUserInformationConverter();

    [Fact]
    public void Read_PlatformDiscriminatorFirst_BindsTheSteamShape()
    {
        const string json = """
            {
              "platform": 0,
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694",
              "authenticationTicketHex": "14000000"
            }
            """;

        var bound = Deserialize(json);

        var steam = Assert.IsType<SteamPlatformUserInformationDto>(bound);
        Assert.Equal(Platform.Steam, steam.Platform);
        Assert.Equal("76561197960287930", steam.PlatformUserId);
        Assert.Equal("109775241004308694", steam.PlatformLobbyId);
        Assert.Equal("14000000", steam.AuthenticationTicketHex);
    }

    [Fact]
    public void Read_PlatformDiscriminatorLast_BindsTheSteamShape()
    {
        const string json = """
            {
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694",
              "authenticationTicketHex": "14000000",
              "platform": 0
            }
            """;

        var steam = Assert.IsType<SteamPlatformUserInformationDto>(Deserialize(json));

        Assert.Equal("76561197960287930", steam.PlatformUserId);
    }

    [Fact]
    public void Read_PlatformDiscriminatorWrittenAsItsEnumName_BindsTheSteamShape()
    {
        const string json = """
            {
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694",
              "platform": "Steam"
            }
            """;

        Assert.IsType<SteamPlatformUserInformationDto>(Deserialize(json));
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

        var steam = Assert.IsType<SteamPlatformUserInformationDto>(Deserialize(json));

        Assert.Null(steam.AuthenticationTicketHex);
    }

    [Theory]
    [InlineData("""{"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":99,"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":-1,"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":"Xbox","platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":null,"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":{},"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":[0],"platformUserId":"765","platformLobbyId":"109"}""")]
    [InlineData("""{"platform":0,"platformLobbyId":"109"}""")]
    [InlineData("""{"platform":0,"platformUserId":"765"}""")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""["steam"]""")]
    [InlineData("\"steam\"")]
    [InlineData("5")]
    [InlineData("true")]
    public void Read_UnbindablePayload_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => Deserialize(json));
    }

    [Fact]
    public void Read_ExplicitNullInsideAJoinRequest_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => DeserializeJoinRequest(JoinRequestWithPlatformUser("null")));
    }

    [Fact]
    public void Read_NonObjectInsideAJoinRequest_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => DeserializeJoinRequest(JoinRequestWithPlatformUser("[]")));
    }

    [Fact]
    public void Read_PlatformUserInformationOmittedFromAJoinRequest_ThrowsJsonException()
    {
        const string json = """
            {
              "expectedLobbyPlayers": [
                { "username": "ana", "platform": 0, "platformUserId": "765" }
              ]
            }
            """;

        Assert.Throws<JsonException>(() => DeserializeJoinRequest(json));
    }

    [Fact]
    public void Read_WellFormedJoinRequest_BindsThePlatformUserInformation()
    {
        const string platformUser = """
            {
              "platformUserId": "76561197960287930",
              "platformLobbyId": "109775241004308694",
              "platform": 0
            }
            """;

        var request = DeserializeJoinRequest(JoinRequestWithPlatformUser(platformUser));

        Assert.Equal(
            new PlayerIdentity(Platform.Steam, "76561197960287930"),
            request.PlatformUserInformation.Identity);
    }

    [Fact]
    public void WriteThenRead_PreservesEveryField()
    {
        IPlatformUserInformationDto original = new SteamPlatformUserInformationDto
        {
            PlatformUserId = "76561197960287930",
            PlatformLobbyId = "109775241004308694",
            AuthenticationTicketHex = "14000000AABB",
        };

        var written = JsonSerializer.Serialize(original, SerializerOptions);

        Assert.Equal(original, Deserialize(written));
    }

    [Fact]
    public void Write_EmitsThePlatformDiscriminator()
    {
        IPlatformUserInformationDto original = new SteamPlatformUserInformationDto
        {
            PlatformUserId = "76561197960287930",
            PlatformLobbyId = "109775241004308694",
        };

        var written = JsonSerializer.Serialize(original, SerializerOptions);

        using var document = JsonDocument.Parse(written);
        Assert.Equal(
            (int)Platform.Steam,
            document.RootElement.GetProperty("platform").GetInt32());
    }

    private static IPlatformUserInformationDto? Deserialize(string json) =>
        JsonSerializer.Deserialize<IPlatformUserInformationDto>(json, SerializerOptions);

    private static JoinMatchDto DeserializeJoinRequest(string json) =>
        JsonSerializer.Deserialize<JoinMatchDto>(json, SerializerOptions)
        ?? throw new InvalidOperationException("The join request bound to null.");

    private static string JoinRequestWithPlatformUser(string platformUserJson) =>
        $$"""
          {
            "platformUserInformation": {{platformUserJson}},
            "expectedLobbyPlayers": [
              { "username": "ana", "platform": 0, "platformUserId": "76561197960287930" }
            ]
          }
          """;
}
