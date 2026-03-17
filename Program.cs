using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Endpoints;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<OrchestratorOptions>(
    builder.Configuration.GetSection(OrchestratorOptions.SectionName));

builder.Services.AddSingleton<ILobbyStore, InMemoryLobbyStore>();
builder.Services.AddSingleton<IProcessLauncher, UnityProcessLauncher>();

var app = builder.Build();

app.MapLobbyEndpoints();

app.Run();

public partial class Program { }
