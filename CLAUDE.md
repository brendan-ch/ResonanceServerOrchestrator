# ResonanceServerOrchestrator

ASP.NET Core 9 HTTP service that assembles matches from Steam lobbies and launches Unity game
server instances on demand.

## The match flow

A client that is in a ready Steam lobby posts a `JoinMatchDto` — its platform user information plus
the expected lobby roster. The orchestrator validates the Steam auth ticket, places the client into
a match keyed by the platform lobby id, and **holds the request open** until every expected player
has joined and the launched game server has reported itself ready. It then returns the server's
host and port plus a per-player `ServerAuthToken`.

The game server receives its match id, match key and the orchestrator URL as environment variables,
calls back to announce readiness, and pulls the member roster (with tokens) to authenticate
connecting players.

The lobby surface (`/lobbies`) it replaced has been deleted.

## API

All routes are versioned via URL segment (`Asp.Versioning.Http`).

| Method | Path | Caller | Auth |
|--------|------|--------|------|
| POST | `/v1/matches/join` | Game client | Steam ticket (unless disabled) |
| POST | `/v1/matches/leave` | Game client | Steam ticket (unless disabled) |
| POST | `/v1/server/matches/{matchId}/ready` | Game server | `X-Match-Key` header |
| GET | `/v1/server/matches/{matchId}/members` | Game server | `X-Match-Key` header |

The platform lobby id travels in the request body (`PlatformUserInformation.PlatformLobbyId`), not
the path. Every request and response type comes from `Resonance.Contracts`, which the Unity game
consumes as a package — see "The shared contracts package" below before changing any of them.

### Client contract obligations

These are not enforceable server-side and will silently break the flow if ignored:

- **The client's HTTP timeout must exceed `RosterAssemblyTimeoutSeconds + ServerReadyTimeoutSeconds`**
  (75 s by default). A shorter timeout aborts the join before the orchestrator can return its
  `409` with a machine-readable `JoinFailureReason`, so the client cannot tell why the match failed.
- **A fresh Steam auth ticket is required per join attempt.** Tickets are session-scoped and
  resubmitting one after a failure may be rejected by Steam.
- **`SupersededByReconnect` is not a terminal failure.** It means this client's own earlier, still-parked
  request was replaced by its retry; the retry will return normally.

### Game server contract obligations

- **On `410 Gone` from `/ready`, the server must terminate itself.** The match was torn down while it
  was booting, and the orchestrator no longer holds a handle to the process — nothing else will stop
  it, and it would hold the configured UDP port indefinitely.
- **Treat `/members` as authoritative and re-query per connecting player.** There is no push-based
  invalidation, so a cached roster keeps a departed player's token valid forever.

## Build & Run

```bash
dotnet build
dotnet test
dotnet run          # http://0.0.0.0:9000
PORT=8080 dotnet run  # override listen port
```

`Properties/launchSettings.json` sets `ASPNETCORE_ENVIRONMENT=Development`, which selects
`appsettings.Development.json` — `LauncherType: None` and `SteamCredentialCheckDisabled: true`, so
the orchestrator runs without a game binary or Steam credentials. Without that file, `dotnet run`
resolves to Production and fails startup validation.

## Configuration

Settings live under the `Orchestrator` key. Override any value with a double-underscore environment
variable (`Orchestrator__LauncherType=None`). Required values missing at startup throw immediately
rather than failing per-request.

| Key | Purpose |
|-----|---------|
| `LauncherType` | `LocalProcess` or `None` |
| `UnityServerPath`, `UnityServerBaseArgs` | The game server binary and its base arguments |
| `OrchestratorUrl` | Injected into the game server so it can call back |
| `GameServerHost`, `GameServerPort` | Advertised to clients; the port is also the server's bind port |
| `MaxMatches` | Must be 1 under `LocalProcess` — one configured port, one match |
| `MatchTimeoutMinutes` | How long a started match lives after `/ready` |
| `RosterAssemblyTimeoutSeconds` | Budget for every expected player to join |
| `ServerReadyTimeoutSeconds` | Budget for the launched server to call `/ready` |
| `TombstoneRetentionMinutes` | How long a destroyed match id answers `410` instead of `404` |
| `SteamCredentialCheckDisabled` | Disables ticket validation entirely, and is the only setting under which `Platform.Dummy` is accepted |
| `SteamPublisherWebApiKey`, `SteamAppId` | Required unless the check is disabled |

### LauncherType

| Value | Behavior |
|-------|----------|
| `LocalProcess` | Spawns the Unity server binary as a child process with the match environment injected |
| `None` | No-op launcher that reports readiness immediately — use for local testing without a game binary |

Edgegap is the intended production backend and is not implemented; `IGameServerLauncher` is the seam
it will drop into.

## Concurrency

The local process backend hosts **exactly one match at a time** by design. It binds a single
configured `GameServerPort`, so a second concurrent match could not bind. `MaxMatches` is validated
to 1 under `LocalProcess`, and a join that would create a second match is refused with
`503 Service Unavailable`. Running many matches concurrently is Edgegap's job.

## Project Structure

```
Resonance.Contracts/  Wire types shared with the Unity game — see below
Configuration/        OrchestratorOptions + per-rule startup validation
Serialization/        Orchestrator-side JSON conventions (JsonStringEnumConverter)
Endpoints/            MatchEndpoints (client) and ServerEndpoints (game server)
Stores/               IMatchStore + InMemoryMatchStore: matches, members, waiters, tombstones
Services/             Launcher abstraction, Steam ticket validation, match cleanup
```

State is entirely in-memory. An orchestrator restart drops in-flight joins and bricks running
matches (their `/members` calls start returning `404`).

## The shared contracts package

`Resonance.Contracts/` is one folder serving two consumers: a `netstandard2.1` project this
orchestrator references, and a Unity UPM package the game pulls over a git URL with `?path=`.
Both compile the same source under `Runtime/`. See its `README.md` for the Unity side.

Editing anything under `Resonance.Contracts/Runtime/` means editing the game's code too:

- **Unity compiles it from source at C# 9.** No records, no `init`, no file-scoped namespaces,
  no `required`, no implicit usings. `LangVersion 9.0` is pinned so violations fail
  `dotnet build` here rather than in the game repo.
- **No serializer attributes, no dependencies.** Each type has one public constructor whose
  parameter names match its property names — that alone is what lets `System.Text.Json` here
  and Newtonsoft in Unity both bind them. Renaming a constructor parameter without renaming its
  property breaks deserialization with no compile error; `UnitySerializerCompatibilityTests`
  exercises every type through both serializers because Newtonsoft fails this silently.
- **Absent fields are rejected by `RespectRequiredConstructorParameters`**, set in
  `Serialization/OrchestratorJsonOptions.cs`. The contracts cannot use `required` (C# 11), so
  without it an omitted `platform` would bind to `Platform.Steam` — a real member — and succeed
  under an assumed platform. Newtonsoft has no equivalent, so Unity is the lenient side.
- **Enum ordinals are a wire contract.** `Platform` and `JoinFailureReason` pin every value
  explicitly because clients may send the numeric form. Append, never insert — commit `3dc6137`
  inserted `Dummy` ahead of `Steam` and silently renumbered it.
- **Types are immutable.** Get-only properties; construct a new instance rather than mutating.
- **Every file needs a committed `.meta`.** `Library/PackageCache` is an immutable folder: Unity
  does not generate `.meta` files there and ignores any asset lacking one, so a `.cs` added
  without its `.meta` is silently dropped from the assembly and an `.asmdef` without one means
  the assembly does not exist. Add the `.meta` in the same commit as the file.

The orchestrator keeps its own serializer configuration in `Serialization/`; the package stays
serializer-agnostic so Unity can use Newtonsoft, which is all it ships.
