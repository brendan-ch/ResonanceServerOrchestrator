using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResonanceServerOrchestrator.Serialization;

public static class OrchestratorJsonOptions
{
    public static JsonSerializerOptions ApplyOrchestratorConventions(this JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new PlatformUserInformationJsonConverter());
        return options;
    }
}
