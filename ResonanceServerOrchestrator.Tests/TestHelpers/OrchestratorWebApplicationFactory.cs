using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Tests.TestHelpers;

internal sealed class OrchestratorWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly int _maxLobbies;

    public IProcessLauncher LauncherSubstitute { get; } = Substitute.For<IProcessLauncher>();

    public OrchestratorWebApplicationFactory(int maxLobbies = 0)
    {
        _maxLobbies = maxLobbies;
        LauncherSubstitute.Launch(Arg.Any<string>(), Arg.Any<string>())
            .Returns(NullGameInstance.Instance);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => services.AddSingleton(LauncherSubstitute));

        if (_maxLobbies > 0)
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection([
                    new($"{OrchestratorOptions.SectionName}:{nameof(OrchestratorOptions.MaxLobbies)}",
                        _maxLobbies.ToString())
                ]));
        }
    }
}
