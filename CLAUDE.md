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
the path.

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
| `SteamCredentialCheckDisabled` | Disables ticket validation entirely |
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
Configuration/   OrchestratorOptions + per-rule startup validation
Contracts/       Wire types: join/leave DTOs, PlayerIdentity, failure reasons
Serialization/   Custom converter for the polymorphic platform user information
Endpoints/       MatchEndpoints (client) and ServerEndpoints (game server)
Stores/          IMatchStore + InMemoryMatchStore: matches, members, waiters, tombstones
Services/        Launcher abstraction, Steam ticket validation, match cleanup
```

State is entirely in-memory. An orchestrator restart drops in-flight joins and bricks running
matches (their `/members` calls start returning `404`).
