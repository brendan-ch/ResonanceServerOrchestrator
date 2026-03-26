# ResonanceServerOrchestrator

ASP.NET Core 9 HTTP service that creates game lobbies and launches Unity game server instances on demand.

## Build & Run

```bash
dotnet build
dotnet run          # listens on http://0.0.0.0:9000 by default
PORT=8080 dotnet run  # override listen port
```

## Tests

```bash
dotnet test
```

## Configuration

Settings live in `appsettings.json` / `appsettings.Development.json` under the `Orchestrator` key.
Override any value with an environment variable using double-underscore notation:

```
Orchestrator__LauncherType=None
Orchestrator__UnityServerPath=/path/to/server
```

### LauncherType

| Value | Behavior |
|-------|----------|
| `Process` | Launches the Unity server binary directly as a child process |
| `Docker` | Builds a Docker image then runs a container per lobby |
| `None` | No-op — accepts requests without launching anything (use for local testing) |

`appsettings.Development.json` defaults to `None` so the orchestrator can be tested without a game binary present.

## Project Structure

```
Configuration/          OrchestratorOptions.cs — typed config record
Endpoints/              LobbyEndpoints.cs — POST/GET /lobbies/{code}
Services/               IProcessLauncher, LauncherType enum, and three implementations
Stores/                 ILobbyStore + InMemoryLobbyStore (concurrent dictionary)
Program.cs              DI wiring; selects launcher implementation based on LauncherType
appsettings.json        Production defaults (LauncherType: Process)
appsettings.Development.json  Dev defaults (LauncherType: None)
```

## API

| Method | Path | Description |
|--------|------|-------------|
| POST | `/lobbies/{lobbyCode}` | Create lobby + launch server; body is arbitrary JSON stored for the server to fetch |
| GET | `/lobbies/{lobbyCode}` | Retrieve stored lobby JSON |
