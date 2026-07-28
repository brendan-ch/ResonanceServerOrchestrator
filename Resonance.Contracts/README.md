# Resonance.Contracts

Wire types shared between the [Resonance server orchestrator](https://github.com/brendan-ch/ResonanceServerOrchestrator)
and the Unity game. One folder, two consumers: a `netstandard2.1` project for the orchestrator
and a UPM package for Unity, both compiling the same source in `Runtime/`.

## Consuming from Unity

Add to `Packages/manifest.json`:

```json
"dev.bchen.resonance.contracts":
  "https://github.com/brendan-ch/ResonanceServerOrchestrator.git?path=/Resonance.Contracts#contracts-v1.0.0"
```

### Reference the assembly by name, not by GUID

`.meta` files are deliberately **not** committed here, so the assembly definition's GUID is
regenerated per machine. Every game assembly definition that references `Resonance.Contracts`
must therefore have **"Use GUIDs" unchecked** in its Inspector:

```json
{ "name": "LobbySystem", "references": ["Resonance.Contracts"] }
```

A GUID-form reference (`"GUID:8f3a..."`) resolves on the machine that created it and fails on a
clean checkout.

This works because no type here derives from `MonoBehaviour` or `ScriptableObject`, so no scene
or prefab can reference one. If that ever changes, `.meta` files have to start being committed.

### Match the serializer conventions

The orchestrator serializes with `System.Text.Json` using web defaults plus
`JsonStringEnumConverter`. To produce and consume the same JSON with Newtonsoft:

```csharp
var settings = new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    NullValueHandling = NullValueHandling.Include,
};
settings.Converters.Add(new StringEnumConverter());
```

- **camelCase property names.** ASP.NET Core applies this by default.
- **Enums as names.** `Platform` and `JoinFailureReason` travel as `"Steam"` and
  `"RosterAssemblyTimedOut"`. Newtonsoft writes integers unless `StringEnumConverter` is
  registered. The orchestrator still *accepts* the numeric form on read.

No type here carries a serializer attribute, and the package has no dependencies. Each type has
exactly one public constructor whose parameter names match its property names, which is what
lets both serializers bind them with no configuration beyond the above.

## Language ceiling

Unity 6.2 compiles [C# 9.0](https://docs.unity3d.com/6000.2/Documentation/Manual/csharp-compiler.html)
and this package is compiled from source, so the source must stay within it. The `.csproj` pins
`LangVersion 9.0` and `netstandard2.1` so violations fail the orchestrator's build rather than
the game's. In practice nothing here needs past C# 7.2 — no records, no `init`, no
`IsExternalInit` shim, no file-scoped namespaces.

## Platform ordinals are a wire contract

`Platform` and `JoinFailureReason` pin every value explicitly. Clients may send the numeric
form, so inserting a member in the middle would silently renumber the ones after it — which is
exactly what happened in commit `3dc6137`. Append, never insert.
