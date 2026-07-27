using System.Text.Json;
using System.Text.Json.Serialization;
using ResonanceServerOrchestrator.Contracts;

namespace ResonanceServerOrchestrator.Serialization;

public sealed class PlatformUserInformationJsonConverter : JsonConverter<IPlatformUserInformationDto>
{
    private const string PlatformDiscriminatorName = "platform";

    public override bool HandleNull => true;

    public override IPlatformUserInformationDto Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        if (element.ValueKind is not JsonValueKind.Object)
            throw new JsonException(
                $"Platform user information must be a JSON object, but was {element.ValueKind}.");

        return ReadPlatformDiscriminator(element) switch
        {
            Platform.Steam => DeserializePlatformShape<SteamPlatformUserInformationDto>(element, options),
            var platform => throw new JsonException(
                $"Platform user information for '{platform}' has no known JSON shape."),
        };
    }

    public override void Write(
        Utf8JsonWriter writer, IPlatformUserInformationDto value, JsonSerializerOptions options)
    {
        if (value is not SteamPlatformUserInformationDto steam)
            throw new JsonException(
                $"Platform user information of type '{value?.GetType().Name ?? "null"}' " +
                "has no known JSON shape.");

        JsonSerializer.Serialize(writer, steam, options);
    }

    private static Platform ReadPlatformDiscriminator(JsonElement element)
    {
        if (!TryFindPlatformDiscriminator(element, out var discriminator))
            throw new JsonException(
                $"Platform user information is missing the '{PlatformDiscriminatorName}' discriminator.");

        if (!TryReadDefinedPlatform(discriminator, out var platform))
            throw new JsonException(
                $"Platform user information carries a '{PlatformDiscriminatorName}' discriminator " +
                $"that names no known platform (as {discriminator.ValueKind}).");

        return platform;
    }

    private static bool TryFindPlatformDiscriminator(JsonElement element, out JsonElement discriminator)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, PlatformDiscriminatorName, StringComparison.OrdinalIgnoreCase))
                continue;

            discriminator = property.Value;
            return true;
        }

        discriminator = default;
        return false;
    }

    private static bool TryReadDefinedPlatform(JsonElement discriminator, out Platform platform)
    {
        switch (discriminator.ValueKind)
        {
            case JsonValueKind.Number
                when discriminator.TryGetInt32(out var ordinal) && Enum.IsDefined((Platform)ordinal):
                platform = (Platform)ordinal;
                return true;

            case JsonValueKind.String
                when Enum.TryParse(discriminator.GetString(), ignoreCase: true, out platform)
                     && Enum.IsDefined(platform):
                return true;

            default:
                platform = default;
                return false;
        }
    }

    private static IPlatformUserInformationDto DeserializePlatformShape<TPlatformUserInformation>(
        JsonElement element, JsonSerializerOptions options)
        where TPlatformUserInformation : class, IPlatformUserInformationDto =>
        element.Deserialize<TPlatformUserInformation>(options)
        ?? throw new JsonException("Platform user information bound to nothing.");
}
