using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Tests.TestHelpers;

internal sealed class OrchestratorWebApplicationFactory : WebApplicationFactory<Program>
{
    public IProcessLauncher LauncherSubstitute { get; } = Substitute.For<IProcessLauncher>();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.AddSingleton(LauncherSubstitute));
}
