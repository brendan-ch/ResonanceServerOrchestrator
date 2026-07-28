using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResonanceServerOrchestrator.Serialization;

public static class OrchestratorJsonOptions
{
    public static JsonSerializerOptions ApplyOrchestratorConventions(this JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());

        // The contracts cannot use `required` — Unity compiles them at C# 9 — so absence would
        // otherwise bind to the parameter's default. For an enum that default is a real member
        // (Platform.Steam), which would let a payload omitting `platform` succeed under an
        // assumed platform. This restores the strictness `required` used to provide: every
        // constructor parameter without a default value must be present in the payload.
        options.RespectRequiredConstructorParameters = true;

        return options;
    }
}
