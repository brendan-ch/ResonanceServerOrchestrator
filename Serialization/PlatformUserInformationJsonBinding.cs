using System.Text.Json;

namespace ResonanceServerOrchestrator.Serialization;

public static class PlatformUserInformationJsonBinding
{
    public static JsonSerializerOptions AddPlatformUserInformationConverter(this JsonSerializerOptions options)
    {
        options.Converters.Add(new PlatformUserInformationJsonConverter());
        return options;
    }
}
