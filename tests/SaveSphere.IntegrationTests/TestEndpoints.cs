using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaveSphere.Infrastructure.Endpoints;
using SaveSphere.SharedKernel;

namespace SaveSphere.IntegrationTests;

/// <summary>
/// Test-only slice mapped through the same <see cref="IEndpointModule"/> mechanism as real modules.
/// It deliberately misbehaves (throws, logs secrets) so the platform safeguards can be proven.
/// All values are obviously fake.
/// </summary>
internal sealed partial class TestEndpoints(ILogger<TestEndpoints> logger) : IEndpointModule
{
    public const string FakePassword = "example-pw-0001";
    public const string FakeToken = "example-token-0001";
    public const string FakeConnectionString =
        "Host=db.example.invalid;Port=5432;Database=app_example;Username=app_user;Password=" + FakePassword;

    public static void Register(IServiceCollection services) =>
        services.AddSingleton<IEndpointModule, TestEndpoints>();

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/test");

        group.MapGet("/fault", () =>
        {
            throw new InvalidOperationException("Cannot connect: " + FakeConnectionString);
        });

        group.MapPost("/log-secrets", (LoginAttempt attempt) =>
        {
            LogSecrets(logger, FakePassword, "Bearer " + FakeToken, FakeConnectionString, attempt);
            return Results.Ok();
        });
    }

    internal sealed record LoginAttempt(string UserName, [property: Sensitive] string Passphrase);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Secrets {Password} {Authorization} {Target} {@Attempt}")]
    private static partial void LogSecrets(ILogger logger, string password, string authorization, string target, LoginAttempt attempt);
}
