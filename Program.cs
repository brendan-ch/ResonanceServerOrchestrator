using System.Runtime.CompilerServices;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Endpoints;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

[assembly: InternalsVisibleTo("ResonanceServerOrchestrator.Tests")]

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<OrchestratorOptions>(
    builder.Configuration.GetSection(OrchestratorOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ILobbyStore, InMemoryLobbyStore>();

var launcherType = builder.Configuration
    .GetSection(OrchestratorOptions.SectionName)
    .GetValue<LauncherType>(nameof(OrchestratorOptions.LauncherType));

if (launcherType == LauncherType.Docker)
    builder.Services.AddSingleton<IProcessLauncher, DockerProcessLauncher>();
else if (launcherType == LauncherType.None)
    builder.Services.AddSingleton<IProcessLauncher, NullProcessLauncher>();
else
    builder.Services.AddSingleton<IProcessLauncher, UnityProcessLauncher>();

builder.Services.AddHostedService<LobbyCleanupService>();

var app = builder.Build();

app.MapLobbyEndpoints();

app.Run();

public partial class Program { }
