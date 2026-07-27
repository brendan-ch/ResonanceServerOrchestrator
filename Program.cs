using System.Runtime.CompilerServices;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Endpoints;

[assembly: InternalsVisibleTo("ResonanceServerOrchestrator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddOptions<OrchestratorOptions>()
    .Bind(builder.Configuration.GetSection(OrchestratorOptions.SectionName))
    .ValidateOrchestratorOptions();

builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.MapMatchEndpoints();

app.Run();

public partial class Program { }
