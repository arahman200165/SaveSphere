using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SaveSphere.IntegrationTests;

/// <summary>Hosts the real API in-process against a given database, optionally with extra test-only services.</summary>
public sealed class TestApiFactory(
    string connectionString,
    string environment = "Production",
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    /// <summary>A connection string that can never connect (closed loopback port); credential-free fake values.</summary>
    public const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=example;Username=example_user;Password=example-only;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            }));
        builder.ConfigureTestServices(services => configureServices?.Invoke(services));
    }
}
