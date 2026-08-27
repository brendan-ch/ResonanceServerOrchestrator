using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;
using ResonanceServerOrchestrator.Stores;

namespace ResonanceServerOrchestrator.Tests.TestHelpers;

internal sealed class OrchestratorWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configurationOverrides;

    public FakeTimeProvider Clock { get; } = new();

    public IGameServerLauncher LauncherSubstitute { get; } = Substitute.For<IGameServerLauncher>();

    public ISteamTicketValidator TicketValidatorSubstitute { get; } =
        Substitute.For<ISteamTicketValidator>();

    private readonly TaskCompletionSource<LocalGameServerLaunchSpec> _launchObserved =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public OrchestratorWebApplicationFactory(
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        _configurationOverrides = configurationOverrides is null
            ? []
            : new Dictionary<string, string?>(configurationOverrides);

        LauncherSubstitute.ReportsReadiness.Returns(true);
        LauncherSubstitute.Launch(Arg.Any<LocalGameServerLaunchSpec>())
            .Returns(call =>
            {
                _launchObserved.TrySetResult(call.Arg<LocalGameServerLaunchSpec>());
                return new NullGameInstance();
            });
    }

    internal InMemoryMatchStore Store =>
        (InMemoryMatchStore)Services.GetRequiredService<IMatchStore>();

    internal Task<LocalGameServerLaunchSpec> LaunchObserved => _launchObserved.Task;

    internal bool HasLaunched => _launchObserved.Task.IsCompleted;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = DefaultConfiguration();
            foreach (var (key, value) in _configurationOverrides)
                settings[key] = value;

            configuration.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton(LauncherSubstitute);
            services.RemoveAll<ISteamTicketValidator>();
            services.AddSingleton(TicketValidatorSubstitute);
            services.AddTransient<IStartupFilter, FakeRemoteIpAddressStartupFilter>();
        });
    }

    private sealed class FakeRemoteIpAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                return nextMiddleware();
            });
            next(app);
        };
    }

    private static Dictionary<string, string?> DefaultConfiguration() => new()
    {
        [Key(nameof(OrchestratorOptions.LauncherType))] = nameof(LauncherType.LocalProcess),
        [Key(nameof(OrchestratorOptions.UnityServerPath))] = ExistingFilePath(),
        [Key(nameof(OrchestratorOptions.SteamCredentialCheckDisabled))] = "true",
        [Key(nameof(OrchestratorOptions.MaxMatches))] = "1",
        [Key(nameof(OrchestratorOptions.LocalGameServerHost))] = "test-host",
        [Key(nameof(OrchestratorOptions.LocalGameServerInternalAndExternalPort))] = "7777",
        [Key(nameof(OrchestratorOptions.OrchestratorUrl))] = "http://orchestrator.test",
        [Key(nameof(OrchestratorOptions.RosterAssemblyTimeoutSeconds))] = "45",
        [Key(nameof(OrchestratorOptions.ServerReadyTimeoutSeconds))] = "30",
    };

    private static string ExistingFilePath() =>
        typeof(OrchestratorWebApplicationFactory).Assembly.Location;

    private static string Key(string propertyName) =>
        $"{OrchestratorOptions.SectionName}:{propertyName}";
}
