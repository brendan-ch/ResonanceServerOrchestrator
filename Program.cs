using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Endpoints;
using ResonanceServerOrchestrator.Serialization;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Services.Edgegap;
using ResonanceServerOrchestrator.Stores;

[assembly: InternalsVisibleTo("ResonanceServerOrchestrator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddOptions<OrchestratorOptions>()
    .Bind(builder.Configuration.GetSection(OrchestratorOptions.SectionName))
    .ValidateOrchestratorOptions(builder.Environment);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.ApplyOrchestratorConventions());

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ServerEndpoints.RateLimiterPolicy, context =>
        FixedWindowByCaller(context, permitLimit: 60));
    options.AddPolicy(MatchEndpoints.ClientRateLimiterPolicy, context =>
        FixedWindowByCaller(context, permitLimit: 30));
});

static RateLimitPartition<string> FixedWindowByCaller(HttpContext context, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxConcurrentConnections = 512;
    kestrel.Limits.MaxRequestBodySize = 256 * 1024;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMatchStore, InMemoryMatchStore>();
builder.Services.AddSingleton<MatchLaunchCoordinator>();
builder.Services.AddHttpClient("Edgegap", (sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<OrchestratorOptions>>().Value;
    http.BaseAddress = new Uri(options.EdgegapBaseUrl);
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("token", options.EdgegapApiKey);
});
builder.Services.AddSingleton<IEdgegapClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OrchestratorOptions>>().Value;
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Edgegap");
    return new HttpEdgegapClient(
        httpClient,
        options.EdgegapApiKey,
        options.EdgegapPollingDelayMs,
        options.EdgegapMaxPollingAttempts);
});
builder.Services.AddScoped<PlayerTicketAuthenticator>();
builder.Services.AddHostedService<MatchCleanupService>();

var launcherType = builder.Configuration
    .GetSection(OrchestratorOptions.SectionName)
    .GetValue<LauncherType>(nameof(OrchestratorOptions.LauncherType));

switch (launcherType)
{
    case LauncherType.None:
        builder.Services.AddSingleton<IGameServerLauncher, NullGameServerLauncher>();
        break;
    case LauncherType.Edgegap:
        builder.Services.AddSingleton<IGameServerLauncher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OrchestratorOptions>>().Value;
            return new EdgegapGameServerLauncher(
                sp.GetRequiredService<IEdgegapClient>(),
                options.EdgegapPollingDelayMs,
                options.EdgegapMaxPollingAttempts,
                sp.GetRequiredService<ILogger<EdgegapGameServerLauncher>>());
        });
        break;
    case LauncherType.LocalProcess:
    default:
        builder.Services.AddSingleton<IGameServerLauncher, LocalProcessGameServerLauncher>();
        break;
}

var steamCredentialCheckDisabled = builder.Configuration
    .GetSection(OrchestratorOptions.SectionName)
    .GetValue<bool>(nameof(OrchestratorOptions.SteamCredentialCheckDisabled));

if (steamCredentialCheckDisabled)
    builder.Services.AddSingleton<ISteamTicketValidator, DisabledSteamTicketValidator>();
else
    builder.Services
        .AddHttpClient<ISteamTicketValidator, SteamWebApiTicketValidator>()
        .RemoveAllLoggers();

var app = builder.Build();

if (steamCredentialCheckDisabled)
{
    app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("ResonanceServerOrchestrator.Startup")
        .LogWarning(
            "Steam credential checking is DISABLED. Every join is accepted without verifying " +
            "the caller's identity. This must never be set in production.");
}

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.UseRateLimiter();

app.MapMatchEndpoints(versionSet);
app.MapServerEndpoints(versionSet);

app.Run();

public partial class Program { }
